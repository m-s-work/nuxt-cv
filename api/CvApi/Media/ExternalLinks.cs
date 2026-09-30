using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace CvApi.Media;

/// <summary>
/// External URLs in a CV (docs/REQUIREMENTS_ACCESS_AND_TENANCY.md §7.2): images on other websites and links to live
/// websites. Visitors never get them directly: images are served through <c>/api/media/{key}</c> (the visitor's
/// browser contacts no third party) and links through <c>/api/go/{key}</c> (a redirect; the click is tracked as
/// <c>link_out</c>). The key is a hash of the URL; a URL is only served if the visitor's redacted CV contains it, so
/// neither endpoint is an open proxy or redirect.
/// </summary>
public static class ExternalLinks
{
    public const string MediaPrefix = "/api/media/";
    public const string GoPrefix = "/api/go/";

    private static readonly string[] MediaLists = ["images", "screenshots", "logos"];

    public enum Kind { Media, Link }

    public static bool IsExternal(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    /// <summary>Stable key of a URL (32 hex chars of its SHA-256).</summary>
    public static string Key(string url) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(url)))[..32];

    /// <summary>Host shown as link text ("www." removed).</summary>
    public static string Host(string url)
    {
        var host = new Uri(url).Host;
        return host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host[4..] : host;
    }

    /// <summary>Replaces external media URLs and website links (in place) by their proxied paths.</summary>
    public static void Rewrite(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (key, value) in obj.ToList())
                {
                    if (IsMediaList(key) && value is JsonArray list)
                    {
                        for (var i = 0; i < list.Count; i++)
                            if (External(list[i]) is { } url) list[i] = MediaPrefix + Key(url);
                    }
                    else if (IsPhoto(key) && External(value) is { } photo)
                    {
                        obj[key] = MediaPrefix + Key(photo);
                    }
                    else if (key == "url" && External(value) is { } link)
                    {
                        obj[key] = GoPrefix + Key(link);
                        if (obj["urlHost"] is null) obj["urlHost"] = Host(link);
                    }
                    else
                    {
                        Rewrite(value);
                    }
                }
                break;
            case JsonArray arr:
                foreach (var item in arr) Rewrite(item);
                break;
        }
    }

    /// <summary>The external URL of a key in a (redacted, not yet rewritten) CV, or null.</summary>
    public static string? Find(JsonNode? cv, string key, Kind kind) =>
        Collect(cv).FirstOrDefault(e => e.Kind == kind && Key(e.Url) == key).Url;

    /// <summary>All external URLs of a CV with their kind.</summary>
    public static IEnumerable<(Kind Kind, string Url)> Collect(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (key, value) in obj)
                {
                    if (IsMediaList(key) && value is JsonArray list)
                    {
                        foreach (var item in list)
                            if (External(item) is { } url) yield return (Kind.Media, url);
                    }
                    else if (IsPhoto(key) && External(value) is { } photo) yield return (Kind.Media, photo);
                    else if (key == "url" && External(value) is { } link) yield return (Kind.Link, link);
                    else
                        foreach (var e in Collect(value)) yield return e;
                }
                break;
            case JsonArray arr:
                foreach (var item in arr)
                foreach (var e in Collect(item)) yield return e;
                break;
        }
    }

    private static bool IsMediaList(string key) => MediaLists.Contains(key);

    // photoUrl, photoUrlLarge, … (same rule as the hidePhoto flag).
    private static bool IsPhoto(string key) => key.StartsWith("photo", StringComparison.OrdinalIgnoreCase);

    private static string? External(JsonNode? node) =>
        node is JsonValue v && v.TryGetValue<string>(out var s) && IsExternal(s) ? s : null;
}
