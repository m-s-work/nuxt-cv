using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace CvApi.Links;

/// <summary>
/// Links to live websites in a CV (`url` of an entry, docs/REQUIREMENTS_ACCESS_AND_TENANCY.md §7.2). Visitors get
/// <c>/api/go/{key}</c>, a redirect through this site, so the click is tracked (<c>link_out</c>) and the website
/// gets no referrer. The key is a hash of the URL; it only redirects to URLs in the visitor's redacted CV, so it is
/// no open redirect.
/// </summary>
public static class ExternalLinks
{
    public const string GoPrefix = "/api/go/";
    private const string UrlField = "url";

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

    /// <summary>
    /// Replaces website links (in place) by their /api/go path and adds <c>urlHost</c> for the link text; with
    /// <paramref name="keepTarget"/> (PDF rendering) also <c>urlTarget</c>, the original link.
    /// </summary>
    public static void Rewrite(JsonNode? node, bool keepTarget = false)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (key, value) in obj.ToList())
                {
                    if (key == UrlField && External(value) is { } url)
                    {
                        obj[key] = GoPrefix + Key(url);
                        if (obj["urlHost"] is null) obj["urlHost"] = Host(url);
                        if (keepTarget) obj["urlTarget"] = url;
                    }
                    else Rewrite(value, keepTarget);
                }
                break;
            case JsonArray arr:
                foreach (var item in arr) Rewrite(item, keepTarget);
                break;
        }
    }

    /// <summary>The link with this key in a (redacted, not yet rewritten) CV, or null.</summary>
    public static string? Find(JsonNode? cv, string key) => Collect(cv).FirstOrDefault(url => Key(url) == key);

    /// <summary>All website links of a CV.</summary>
    public static IEnumerable<string> Collect(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (key, value) in obj)
                {
                    if (key == UrlField && External(value) is { } url) yield return url;
                    else
                        foreach (var u in Collect(value)) yield return u;
                }
                break;
            case JsonArray arr:
                foreach (var item in arr)
                foreach (var u in Collect(item)) yield return u;
                break;
        }
    }

    private static string? External(JsonNode? node) =>
        node is JsonValue v && v.TryGetValue<string>(out var s) && IsExternal(s) ? s : null;
}
