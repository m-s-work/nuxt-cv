using System.Security.Cryptography;
using System.Text;

namespace CvApi.Versioning;

/// <summary>
/// Build identity baked into the image during `docker build`: files SOURCE_COMMIT (Git SHA, from Coolify's
/// build variable) and BUILD_TIME next to the app. The SOURCE_COMMIT environment variable is only a
/// fallback (e.g. local runs) – at runtime Coolify may set it to something else. Otherwise "unknown".
/// </summary>
public sealed record BuildInfo(string Commit, string? BuiltAt)
{
    public static BuildInfo Current { get; } = Load();

    private static BuildInfo Load()
    {
        var commit = Known(ReadFile("SOURCE_COMMIT")) ?? Known(Environment.GetEnvironmentVariable("SOURCE_COMMIT")) ?? "unknown";
        return new BuildInfo(commit, ReadFile("BUILD_TIME"));
    }

    private static string? Known(string? value) =>
        string.IsNullOrWhiteSpace(value) || value.Trim() == "unknown" ? null : value.Trim();

    private static string? ReadFile(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, name);
        return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
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
