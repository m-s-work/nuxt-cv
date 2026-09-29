using CvApi.Tenants;

namespace CvApi.Versioning;

/// <summary>
/// SHA-256 of a tenant's raw files (tenant.json, cv.&lt;locale&gt;.json, assets/*), comparable with
/// `sha256sum` on the files in Git, plus one combined hash over all of them.
/// </summary>
public static class TenantHashes
{
    public sealed record Result(IReadOnlyDictionary<string, string> Files, string Combined);

    public static Result Compute(Tenant tenant)
    {
        var files = new SortedDictionary<string, string>(DataFiles(tenant.Directory), StringComparer.Ordinal);
        var tenantJson = Path.Combine(tenant.Directory, "tenant.json");
        if (File.Exists(tenantJson)) files["tenant.json"] = Sha256.OfFile(tenantJson);

        // Same line format as `sha256sum`, so the combined hash can be reproduced with shell tools.
        var combined = Sha256.OfText(string.Concat(files.Select(f => $"{f.Value}  {f.Key}\n")));
        return new Result(files, combined);
    }

    /// <summary>
    /// SHA-256 of the CV content of a directory: cv.&lt;locale&gt;.json and assets/* (no tenant.json).
    /// Used for the tenant folder and for stored CV revisions (§14), so both can be compared file by file.
    /// </summary>
    public static SortedDictionary<string, string> DataFiles(string directory)
    {
        var files = new SortedDictionary<string, string>(StringComparer.Ordinal);
        if (!Directory.Exists(directory)) return files;
        foreach (var file in Directory.EnumerateFiles(directory, "cv.*.json"))
            files[Path.GetFileName(file)] = Sha256.OfFile(file);
        var assets = Path.Combine(directory, "assets");
        if (Directory.Exists(assets))
            foreach (var file in Directory.EnumerateFiles(assets))
                files["assets/" + Path.GetFileName(file)] = Sha256.OfFile(file);
        return files;
    }

    /// <summary>Files that differ between <paramref name="from"/> and <paramref name="to"/>: added, removed or modified.</summary>
    public static List<FileChange> Diff(IReadOnlyDictionary<string, string> from, IReadOnlyDictionary<string, string> to) =>
        from.Keys.Union(to.Keys).Order(StringComparer.Ordinal)
            .Select(path => (from.TryGetValue(path, out var a), to.TryGetValue(path, out var b), a, b, path))
            .Where(x => x.a != x.b)
            .Select(x => new FileChange(x.path, !x.Item1 ? "added" : !x.Item2 ? "removed" : "modified"))
            .ToList();

    public sealed record FileChange(string Path, string Change);
}
