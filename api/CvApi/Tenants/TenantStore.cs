using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace CvApi.Tenants;

/// <summary>
/// Reads tenants from {DataPath}/tenants/{id}/. Files are re-read after a short cache period,
/// so CV edits on the volume need no redeploy.
/// </summary>
public sealed partial class TenantStore(IConfiguration configuration, ILogger<TenantStore> logger, TimeProvider time)
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

    /// <summary>Locales with a master CV file (cv.&lt;locale&gt;.json), sorted.</summary>
    public static IReadOnlyList<string> Locales(Tenant tenant) =>
        System.IO.Directory.Exists(tenant.Directory)
            ? System.IO.Directory.GetFiles(tenant.Directory, "cv.*.json")
                .Select(f => Path.GetFileName(f)[3..^5])
                .Where(l => LocaleRegex().IsMatch(l))
                .Order(StringComparer.Ordinal)
                .ToList()
            : [];

    public Tenant? FindByHost(string host) => Current().ByHost.GetValueOrDefault(NormalizeHost(host));

    public static string NormalizeHost(string host)
    {
        var h = host.Trim().ToLowerInvariant();
        // Strip port, but keep IPv6 literals like [::1] intact.
        var colon = h.LastIndexOf(':');
        if (colon > 0 && !h.EndsWith(']')) h = h[..colon];
        return h.TrimEnd('.');
    }

    /// <summary>Loads the master CV for a locale, falling back to the tenant's default locale.</summary>
    public (JsonObject Cv, string Locale)? LoadCv(Tenant tenant, string? locale)
    {
        foreach (var candidate in new[] { locale, tenant.Config.DefaultLocale })
        {
            if (candidate is null || !LocaleRegex().IsMatch(candidate)) continue;
            var file = Path.Combine(tenant.Directory, $"cv.{candidate}.json");
            if (!File.Exists(file)) continue;
            var node = JsonNode.Parse(File.ReadAllText(file), documentOptions: FileDocumentOptions);
            if (node is JsonObject obj) return (obj, candidate);
            logger.LogWarning("CV file {File} is not a JSON object", file);
        }
        return null;
    }

    /// <summary>Resolves an asset file name inside the tenant's asset folder, or null if it is invalid/missing.</summary>
    public string? AssetPath(Tenant tenant, string fileName)
    {
        if (!AssetNameRegex().IsMatch(fileName)) return null;
        var path = Path.Combine(tenant.Directory, "assets", fileName);
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
            }
            catch (JsonException ex)
            {
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

    private sealed record Snapshot(DateTimeOffset LoadedAt, Dictionary<string, Tenant> ById, Dictionary<string, Tenant> ByHost);

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,62}$")]
    private static partial Regex TenantIdRegex();

    [GeneratedRegex("^[a-z]{2}(-[A-Z]{2})?$")]
    private static partial Regex LocaleRegex();

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$")]
    private static partial Regex AssetNameRegex();
}
