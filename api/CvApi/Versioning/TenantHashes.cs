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
        var paths = new List<string>();
        if (File.Exists(Path.Combine(tenant.Directory, "tenant.json"))) paths.Add("tenant.json");
        paths.AddRange(Directory.EnumerateFiles(tenant.Directory, "cv.*.json").Select(Path.GetFileName)!);
        var assets = Path.Combine(tenant.Directory, "assets");
        if (Directory.Exists(assets))
            paths.AddRange(Directory.EnumerateFiles(assets).Select(f => "assets/" + Path.GetFileName(f)));

        var files = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var path in paths) files[path] = Sha256.OfFile(Path.Combine(tenant.Directory, path));

        // Same line format as `sha256sum`, so the combined hash can be reproduced with shell tools.
        var combined = Sha256.OfText(string.Concat(files.Select(f => $"{f.Value}  {f.Key}\n")));
        return new Result(files, combined);
    }
}
