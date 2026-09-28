using System.Text.Json;
using System.Text.RegularExpressions;

namespace CvApi.Tenants;

/// <summary>A registered CV version: a snapshot of the tenant's cv.&lt;locale&gt;.json files and assets at a git commit.</summary>
public sealed record CvRevision(
    string Sha,
    string? Message,
    DateTimeOffset? CommittedAt,
    DateTimeOffset RegisteredAt,
    SortedDictionary<string, string> Files)
{
    /// <summary>SHA-256 per file (cv.&lt;locale&gt;.json, assets/*), same format as GET /admin/tenants/{id}/hash.</summary>
    public SortedDictionary<string, string> Files { get; init; } = Files ?? new(StringComparer.Ordinal);
}

/// <summary>Where the tenant's CV lives in git (reported by tools/cv-sync.sh), used to fetch pruned revisions again.</summary>
public sealed record RevisionSource(string Repo, string Path);

/// <param name="Refs">Tags/branches used as pins, resolved to the commit they pointed to when fetched.</param>
public sealed record RevisionIndex(string? Current, List<CvRevision> Revisions, RevisionSource? Source = null,
    Dictionary<string, string>? Refs = null);

/// <summary>
/// Snapshots of a tenant's master CV and assets per git commit, so invites and profiles can be pinned
/// to the version that was sent (see docs/REQUIREMENTS_ACCESS_AND_TENANCY.md §14). Stored in
/// {tenant}/revisions/{sha}/(cv.&lt;locale&gt;.json|assets/) plus {tenant}/revisions/index.json.
/// Only revisions still in use are kept (<see cref="Prune"/>).
/// </summary>
public static partial class RevisionStore
{
    private static readonly Lock WriteLock = new();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [GeneratedRegex("^[0-9a-f]{7,40}$")]
    public static partial Regex ShaRegex();

    public static string Directory(Tenant tenant) => Path.Combine(tenant.Directory, "revisions");

    public static RevisionIndex Read(Tenant tenant)
    {
        var file = Path.Combine(Directory(tenant), "index.json");
        if (!File.Exists(file)) return new RevisionIndex(null, []);
        return JsonSerializer.Deserialize<RevisionIndex>(File.ReadAllText(file), JsonOptions) ?? new RevisionIndex(null, []);
    }

    /// <summary>
    /// Full SHA of a stored revision from a full SHA, a unique prefix (≥ 7 chars) or a fetched tag/branch name, else null.
    /// </summary>
    public static string? Resolve(Tenant tenant, string pin) => Resolve(Read(tenant), pin);

    private static string? Resolve(RevisionIndex index, string pin)
    {
        var trimmed = pin.Trim();
        if (index.Refs?.TryGetValue(trimmed, out var bySha) == true && index.Revisions.Any(r => r.Sha == bySha)) return bySha;
        var s = trimmed.ToLowerInvariant();
        if (!ShaRegex().IsMatch(s)) return null;
        var matches = index.Revisions.Where(r => r.Sha.StartsWith(s, StringComparison.Ordinal)).ToList();
        return matches.Count == 1 ? matches[0].Sha : null;
    }

    /// <summary>Remembers which commit a tag/branch pointed to (no-op for SHAs).</summary>
    public static void AddRef(Tenant tenant, string reference, string sha)
    {
        if (ShaRegex().IsMatch(reference.ToLowerInvariant()) && sha.StartsWith(reference.ToLowerInvariant(), StringComparison.Ordinal)) return;
        lock (WriteLock)
        {
            var index = Read(tenant);
            var refs = new Dictionary<string, string>(index.Refs ?? []) { [reference] = sha };
            Write(tenant, index with { Refs = refs });
        }
    }

    /// <summary>Snapshots the tenant's current CV files as <paramref name="sha"/> and marks it as current.</summary>
    public static CvRevision Register(Tenant tenant, string sha, string? message, DateTimeOffset? committedAt,
        DateTimeOffset now, RevisionSource? source = null) =>
        Store(tenant, sha, tenant.Directory, message, committedAt, now, makeCurrent: true, source);

    /// <summary>
    /// Stores the cv.&lt;locale&gt;.json files and assets/ of <paramref name="sourceDirectory"/> as snapshot
    /// <paramref name="sha"/>; optionally marks it as current and updates the git source.
    /// </summary>
    public static CvRevision Store(Tenant tenant, string sha, string sourceDirectory, string? message, DateTimeOffset? committedAt,
        DateTimeOffset now, bool makeCurrent, RevisionSource? source = null)
    {
        lock (WriteLock)
        {
            var target = Path.Combine(Directory(tenant), sha);
            var staging = target + ".tmp";
            if (System.IO.Directory.Exists(staging)) System.IO.Directory.Delete(staging, recursive: true);
            System.IO.Directory.CreateDirectory(staging);
            foreach (var file in CvFiles(sourceDirectory))
                File.Copy(file, Path.Combine(staging, Path.GetFileName(file)));
            var assets = Path.Combine(sourceDirectory, "assets");
            if (System.IO.Directory.Exists(assets))
            {
                System.IO.Directory.CreateDirectory(Path.Combine(staging, "assets"));
                foreach (var file in System.IO.Directory.GetFiles(assets))
                    File.Copy(file, Path.Combine(staging, "assets", Path.GetFileName(file)));
            }
            if (System.IO.Directory.Exists(target)) System.IO.Directory.Delete(target, recursive: true);
            System.IO.Directory.Move(staging, target);

            var revision = new CvRevision(sha, message, committedAt, now, Versioning.TenantHashes.DataFiles(target));
            var index = Read(tenant);
            var revisions = index.Revisions.Where(r => r.Sha != sha).Prepend(revision).ToList();
            Write(tenant, new RevisionIndex(makeCurrent ? sha : index.Current, revisions, source ?? index.Source));
            return revision;
        }
    }

    /// <summary>
    /// Deletes every snapshot except the current revision and those matched by <paramref name="pins"/>
    /// (full SHAs or prefixes of pins still in use). Returns the removed SHAs.
    /// </summary>
    public static IReadOnlyList<string> Prune(Tenant tenant, IEnumerable<string> pins)
    {
        lock (WriteLock)
        {
            var index = Read(tenant);
            var used = pins.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => Resolve(index, p)).OfType<string>().ToHashSet();
            bool Keep(string sha) => sha == index.Current || used.Contains(sha);

            var removed = index.Revisions.Where(r => !Keep(r.Sha)).Select(r => r.Sha).ToList();
            foreach (var sha in removed)
            {
                var dir = Path.Combine(Directory(tenant), sha);
                if (System.IO.Directory.Exists(dir)) System.IO.Directory.Delete(dir, recursive: true);
            }
            // Snapshot folders without index entry (e.g. an interrupted registration).
            if (System.IO.Directory.Exists(Directory(tenant)))
                foreach (var dir in System.IO.Directory.GetDirectories(Directory(tenant)))
                    if (!index.Revisions.Any(r => r.Sha == Path.GetFileName(dir)) || removed.Contains(Path.GetFileName(dir)))
                        System.IO.Directory.Delete(dir, recursive: true);

            if (removed.Count > 0)
                Write(tenant, index with
                {
                    Revisions = index.Revisions.Where(r => !removed.Contains(r.Sha)).ToList(),
                    Refs = index.Refs?.Where(r => !removed.Contains(r.Value)).ToDictionary(),
                });
            return removed;
        }
    }

    private static void Write(Tenant tenant, RevisionIndex index)
    {
        System.IO.Directory.CreateDirectory(Directory(tenant));
        File.WriteAllText(Path.Combine(Directory(tenant), "index.json"), JsonSerializer.Serialize(index, JsonOptions));
    }

    private static IEnumerable<string> CvFiles(string directory) =>
        System.IO.Directory.Exists(directory)
            ? System.IO.Directory.GetFiles(directory, "cv.*.json").Order(StringComparer.Ordinal)
            : [];
}
