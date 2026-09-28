using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CvApi.Tenants;

/// <summary>A registered CV version: a snapshot of the tenant's cv.&lt;locale&gt;.json files at a git commit.</summary>
public sealed record CvRevision(
    string Sha,
    string? Message,
    DateTimeOffset? CommittedAt,
    DateTimeOffset RegisteredAt,
    string ContentHash);

public sealed record RevisionIndex(string? Current, List<CvRevision> Revisions);

/// <summary>
/// Snapshots of a tenant's master CV per git commit, so invites and profiles can be pinned to the
/// version that was sent (see docs/REQUIREMENTS_ACCESS_AND_TENANCY.md §14). Stored in
/// {tenant}/revisions/{sha}/cv.&lt;locale&gt;.json plus {tenant}/revisions/index.json.
/// Assets are not versioned.
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
            if (System.IO.Directory.Exists(target)) System.IO.Directory.Delete(target, recursive: true);
            System.IO.Directory.Move(staging, target);

            var revision = new CvRevision(sha, message, committedAt, now, ContentHash(target));
            var index = Read(tenant);
            var revisions = index.Revisions.Where(r => r.Sha != sha).Prepend(revision).ToList();
            File.WriteAllText(Path.Combine(Directory(tenant), "index.json"),
                JsonSerializer.Serialize(new RevisionIndex(sha, revisions), JsonOptions));
            return revision;
        }
    }

    /// <summary>Hash over the cv.&lt;locale&gt;.json files in a directory (names and bytes).</summary>
    public static string ContentHash(string directory)
    {
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var file in CvFiles(directory))
        {
            sha.AppendData(System.Text.Encoding.UTF8.GetBytes(Path.GetFileName(file) + "\n"));
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
