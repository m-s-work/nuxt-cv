using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CvApi.Tenants;

/// <summary>A registered CV version: a snapshot of the tenant's cv.&lt;locale&gt;.json files and assets at a git commit.</summary>
public sealed record CvRevision(
    string Sha,
    string? Message,
    DateTimeOffset? CommittedAt,
    DateTimeOffset RegisteredAt,
    string ContentHash);

public sealed record RevisionIndex(string? Current, List<CvRevision> Revisions);

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

    /// <summary>Full SHA of a registered revision from a full SHA or a unique prefix (≥ 7 chars), else null.</summary>
    public static string? Resolve(Tenant tenant, string shaOrPrefix)
    {
        var s = shaOrPrefix.Trim().ToLowerInvariant();
        if (!ShaRegex().IsMatch(s)) return null;
        var matches = Read(tenant).Revisions.Where(r => r.Sha.StartsWith(s, StringComparison.Ordinal)).ToList();
        return matches.Count == 1 ? matches[0].Sha : null;
    }

    /// <summary>Snapshots the tenant's current CV files as <paramref name="sha"/> and marks it as current.</summary>
    public static CvRevision Register(Tenant tenant, string sha, string? message, DateTimeOffset? committedAt, DateTimeOffset now)
    {
        lock (WriteLock)
        {
            var target = Path.Combine(Directory(tenant), sha);
            var staging = target + ".tmp";
            if (System.IO.Directory.Exists(staging)) System.IO.Directory.Delete(staging, recursive: true);
            System.IO.Directory.CreateDirectory(staging);
            foreach (var file in CvFiles(tenant.Directory))
                File.Copy(file, Path.Combine(staging, Path.GetFileName(file)));
            var assets = Path.Combine(tenant.Directory, "assets");
            if (System.IO.Directory.Exists(assets))
            {
                System.IO.Directory.CreateDirectory(Path.Combine(staging, "assets"));
                foreach (var file in System.IO.Directory.GetFiles(assets))
                    File.Copy(file, Path.Combine(staging, "assets", Path.GetFileName(file)));
            }
            if (System.IO.Directory.Exists(target)) System.IO.Directory.Delete(target, recursive: true);
            System.IO.Directory.Move(staging, target);

            var revision = new CvRevision(sha, message, committedAt, now, ContentHash(target));
            var index = Read(tenant);
            var revisions = index.Revisions.Where(r => r.Sha != sha).Prepend(revision).ToList();
            Write(tenant, new RevisionIndex(sha, revisions));
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
            var used = pins.Select(p => p.Trim().ToLowerInvariant()).Where(p => p.Length > 0).ToList();
            bool Keep(string sha) => sha == index.Current || used.Any(p => sha.StartsWith(p, StringComparison.Ordinal));

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
                Write(tenant, index with { Revisions = index.Revisions.Where(r => !removed.Contains(r.Sha)).ToList() });
            return removed;
        }
    }

    private static void Write(Tenant tenant, RevisionIndex index) =>
        File.WriteAllText(Path.Combine(Directory(tenant), "index.json"), JsonSerializer.Serialize(index, JsonOptions));

    /// <summary>Hash over the cv.&lt;locale&gt;.json files and assets in a directory (names and bytes).</summary>
    public static string ContentHash(string directory)
    {
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var assets = Path.Combine(directory, "assets");
        var files = CvFiles(directory).Concat(System.IO.Directory.Exists(assets)
            ? System.IO.Directory.GetFiles(assets).Order(StringComparer.Ordinal)
            : []);
        foreach (var file in files)
        {
            sha.AppendData(System.Text.Encoding.UTF8.GetBytes(Path.GetRelativePath(directory, file).Replace('\\', '/') + "\n"));
            sha.AppendData(File.ReadAllBytes(file));
            sha.AppendData("\n"u8);
        }
        return Convert.ToHexStringLower(sha.GetHashAndReset());
    }

    private static IEnumerable<string> CvFiles(string directory) =>
        System.IO.Directory.Exists(directory)
            ? System.IO.Directory.GetFiles(directory, "cv.*.json").Order(StringComparer.Ordinal)
            : [];
}
