using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace CvApi.Tenants;

/// <summary>
/// Reads tenants from {DataPath}/tenants/{id}/. Files are re-read after a short cache period,
/// so CV edits on the volume need no redeploy.
/// </summary>
public sealed partial class TenantStore(IConfiguration configuration, ILogger<TenantStore> logger, TimeProvider time,
    Accounts.TenantOwners owners)
{
    public static readonly JsonSerializerOptions FileJsonOptions = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static readonly JsonDocumentOptions FileDocumentOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly Lock _lock = new();
    private Snapshot? _snapshot;

    private string DataPath => Path.GetFullPath(configuration["Cv:DataPath"] ?? "/data");
    private TimeSpan CacheDuration => TimeSpan.FromSeconds(configuration.GetValue("Cv:ConfigCacheSeconds", 10));

    public IReadOnlyCollection<Tenant> All => Current().ById.Values;

    public Tenant? Get(string id) => Current().ById.GetValueOrDefault(id);

    /// <summary>Drops the cached snapshot so the next read sees files changed via the admin API.</summary>
    public void Invalidate()
    {
        lock (_lock) _snapshot = null;
    }

    /// <summary>Locales with a master CV file (cv.&lt;locale&gt;.json), sorted; of the pinned revision if given.</summary>
    public static IReadOnlyList<string> Locales(Tenant tenant, string? revision = null) =>
        CvDirectory(tenant, revision) is var dir && System.IO.Directory.Exists(dir)
            ? System.IO.Directory.GetFiles(dir, "cv.*.json")
                .Select(f => Path.GetFileName(f)[3..^5])
                .Where(l => LocaleRegex().IsMatch(l))
                .Order(StringComparer.Ordinal)
                .ToList()
            : [];

    /// <summary>
    /// Tenant of a hostname. Tenants of blocked users and own domains of users without Pro do not resolve
    /// (docs/REQUIREMENTS_SAAS.md §4 S4.2, §6 S6.3); such hosts behave like the shared host.
    /// </summary>
    public Tenant? FindByHost(string host)
    {
        var normalized = NormalizeHost(host);
        if (Current().ByHost.GetValueOrDefault(normalized) is not { } tenant) return null;
        return owners.IsBlocked(tenant.Id) || owners.IsSuspendedHost(tenant.Id, normalized) ? null : tenant;
    }

    /// <summary>Tenant a host is configured for, regardless of blocking or plan (for uniqueness checks).</summary>
    public Tenant? ConfiguredOwnerOfHost(string host) => Current().ByHost.GetValueOrDefault(NormalizeHost(host));

    /// <summary>Host for public and invite links: the first configured host that currently resolves to the tenant.</summary>
    public string? PrimaryHost(Tenant tenant) =>
        tenant.Config.Hosts.Select(NormalizeHost).FirstOrDefault(h => !owners.IsSuspendedHost(tenant.Id, h));

    public static string NormalizeHost(string host)
    {
        var h = host.Trim().ToLowerInvariant();
        // Strip port, but keep IPv6 literals like [::1] intact.
        var colon = h.LastIndexOf(':');
        if (colon > 0 && !h.EndsWith(']')) h = h[..colon];
        return h.TrimEnd('.');
    }

    /// <summary>
    /// Directory holding the master CV files: the snapshot of a pinned revision (SHA, prefix or fetched tag), or the
    /// tenant's live files if no revision is pinned or its snapshot is missing (the admin UI warns about that).
    /// </summary>
    public static string CvDirectory(Tenant tenant, string? revision)
    {
        if (revision is not null && RevisionStore.Resolve(tenant, revision) is { } sha)
        {
            var dir = Path.Combine(RevisionStore.Directory(tenant), sha);
            if (System.IO.Directory.Exists(dir)) return dir;
        }
        return tenant.Directory;
    }

    /// <summary>Loads the master CV for a locale, falling back to the tenant's default locale.</summary>
    public (JsonObject Cv, string Locale)? LoadCv(Tenant tenant, string? locale, string? revision = null)
    {
        var dir = CvDirectory(tenant, revision);
        foreach (var candidate in new[] { locale, tenant.Config.DefaultLocale })
        {
            if (candidate is null || !LocaleRegex().IsMatch(candidate)) continue;
            var file = Path.Combine(dir, $"cv.{candidate}.json");
            if (!File.Exists(file)) continue;
            var node = JsonNode.Parse(File.ReadAllText(file), documentOptions: FileDocumentOptions);
            if (node is JsonObject obj) return (obj, candidate);
            logger.LogWarning("CV file {File} is not a JSON object", file);
        }
        return null;
    }

    /// <summary>
    /// Resolves an asset file name inside the tenant's asset folder (of the pinned revision's snapshot, if any),
    /// or null if it is invalid/missing.
    /// </summary>
    public string? AssetPath(Tenant tenant, string fileName, string? revision = null)
    {
        if (!AssetNameRegex().IsMatch(fileName)) return null;
        var dir = CvDirectory(tenant, revision);
        if (!System.IO.Directory.Exists(Path.Combine(dir, "assets"))) dir = tenant.Directory; // snapshot without assets
        var path = Path.Combine(dir, "assets", fileName);
        return File.Exists(path) ? path : null;
    }

    private Snapshot Current()
    {
        lock (_lock)
        {
            var now = time.GetUtcNow();
            if (_snapshot is null || now - _snapshot.LoadedAt >= CacheDuration)
                _snapshot = Load(now);
            return _snapshot;
        }
    }

    private Snapshot Load(DateTimeOffset now)
    {
        var byId = new Dictionary<string, Tenant>(StringComparer.OrdinalIgnoreCase);
        var byHost = new Dictionary<string, Tenant>(StringComparer.OrdinalIgnoreCase);
        var root = Path.Combine(DataPath, "tenants");

        if (!System.IO.Directory.Exists(root))
        {
            logger.LogWarning("Tenant directory {Root} does not exist", root);
            return new Snapshot(now, byId, byHost);
        }

        foreach (var dir in System.IO.Directory.GetDirectories(root).Order(StringComparer.Ordinal))
        {
            var id = Path.GetFileName(dir);
            var file = Path.Combine(dir, "tenant.json");
            if (!TenantIdRegex().IsMatch(id) || !File.Exists(file)) continue;

            TenantConfig? config;
            try
            {
                config = JsonSerializer.Deserialize<TenantConfig>(File.ReadAllText(file), FileJsonOptions);
                if (Validate(config) is { } error)
                {
                    logger.LogError("Invalid tenant config {File}: {Error}", file, error);
                    continue;
                }
            }
            catch (Exception ex) when (ex is JsonException or IOException or NotSupportedException)
            {
                // One broken tenant must not take the others down.
                logger.LogError(ex, "Invalid tenant config {File}", file);
                continue;
            }
            if (config is null) continue;

            // Re-create with a case-insensitive dictionary (deserializer ignores the initializer comparer).
            config.Profiles = new Dictionary<string, AccessPolicy>(config.Profiles, StringComparer.OrdinalIgnoreCase);

            var tenant = new Tenant(id, config, dir);
            byId[id] = tenant;

            foreach (var host in config.Hosts.Select(NormalizeHost))
            {
                if (byHost.TryGetValue(host, out var other))
                {
                    logger.LogError("Host {Host} is configured for tenants {First} and {Second}; ignoring it for the second", host, other.Id, id);
                    continue;
                }
                byHost[host] = tenant;
            }

            if (config.PublicProfile is not null && !config.Profiles.ContainsKey(config.PublicProfile))
                logger.LogError("Tenant {Id}: publicProfile {Profile} does not exist", id, config.PublicProfile);
        }

        return new Snapshot(now, byId, byHost);
    }

    /// <summary>Checks a parsed tenant.json for values the code cannot work with; null = valid.</summary>
    public static string? Validate(TenantConfig? config)
    {
        if (config is null) return "empty";
        if (config.Hosts is null) return "\"hosts\" must be a list (use [] for none)";
        if (config.Profiles is null) return "\"profiles\" must be an object";
        if (config.Hosts.Any(string.IsNullOrWhiteSpace)) return "\"hosts\" must not contain empty entries";
        if (config.Profiles.Any(p => p.Value is null)) return "every profile must be an object";
        if (string.IsNullOrWhiteSpace(config.DefaultLocale) || !LocaleRegex().IsMatch(config.DefaultLocale)) return "\"defaultLocale\" must be like \"en\" or \"de-AT\"";
        return null;
    }

    private sealed record Snapshot(DateTimeOffset LoadedAt, Dictionary<string, Tenant> ById, Dictionary<string, Tenant> ByHost);

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,62}$")]
    private static partial Regex TenantIdRegex();

    [GeneratedRegex("^[a-z]{2}(-[A-Z]{2})?$")]
    private static partial Regex LocaleRegex();

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$")]
    private static partial Regex AssetNameRegex();
}
