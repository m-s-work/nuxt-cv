using System.Security.Cryptography;
using System.Text;

namespace CvApi.Versioning;

/// <summary>
/// Build identity baked into the image: SOURCE_COMMIT (Git SHA, from Coolify's build variable) and the
/// build time (file written during `docker build`). Local builds report "unknown".
/// </summary>
public sealed record BuildInfo(string Commit, string? BuiltAt)
{
    public static BuildInfo Current { get; } = Load();

    private static BuildInfo Load()
    {
        var commit = Environment.GetEnvironmentVariable("SOURCE_COMMIT");
        var buildTimeFile = Path.Combine(AppContext.BaseDirectory, "BUILD_TIME");
        var builtAt = File.Exists(buildTimeFile) ? File.ReadAllText(buildTimeFile).Trim() : null;
        return new BuildInfo(string.IsNullOrWhiteSpace(commit) ? "unknown" : commit.Trim(), builtAt);
    }
}

/// <summary>SHA-256 of raw file bytes, lower-case hex – identical to `sha256sum`.</summary>
public static class Sha256
{
    public static string OfFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    public static string OfText(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
