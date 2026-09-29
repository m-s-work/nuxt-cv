using System.Globalization;
using System.Text.RegularExpressions;

namespace CvApi.Tenants;

/// <summary>Favicon of a tenant (tenant.json "favicon"). Null fields use the defaults.</summary>
public sealed class FaviconConfig
{
    /// <summary>One of <see cref="Favicon.Symbols"/>, e.g. "cv-braces".</summary>
    public string? Symbol { get; set; }

    /// <summary>Symbol colour: a name from <see cref="Favicon.Colors"/> or "#rgb" / "#rrggbb".</summary>
    public string? Color { get; set; }

    /// <summary>Background colour of the rounded square (same format as <see cref="Color"/>).</summary>
    public string? Background { get; set; }
}

/// <summary>
/// Renders the tenant's favicon as SVG. Unknown symbols and invalid colours fall back to the defaults,
/// so a typo in tenant.json never breaks the page (same as template names).
/// </summary>
public static partial class Favicon
{
    public const string DefaultSymbol = "cv-braces";
    public const string DefaultColor = "blue";
    public const string DefaultBackground = "dark";

    /// <summary>Named colours; any "#rgb" / "#rrggbb" value works as well.</summary>
    public static readonly IReadOnlyDictionary<string, string> Colors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["green"] = "#00c16a",
        ["blue"] = "#60a5fa",
        ["violet"] = "#a78bfa",
        ["amber"] = "#fbbf24",
        ["white"] = "#ffffff",
        ["dark"] = "#0f172a",
        ["light"] = "#f1f5f9",
    };

    /// <summary>Symbol name → SVG content drawn in a 32×32 box with the symbol colour as "currentColor".</summary>
    public static readonly IReadOnlyDictionary<string, string> Symbols = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["code"] = """<g fill="none" stroke="currentColor" stroke-width="2.6" stroke-linecap="round" stroke-linejoin="round"><path d="M10.5 10 5 16l5.5 6"/><path d="M21.5 10 27 16l-5.5 6"/><path d="M18 8.5 14 23.5"/></g>""",
        ["braces"] = Text("{}", 19),
        ["terminal"] = Text(">_", 19),
        ["cv-braces"] = Text("{cv}", 11.5),
        ["cv-tag"] = Text("<cv/>", 9.5),
        ["lambda"] = Text("λ", 22),
    };

    /// <summary>What each symbol shows (labels in the admin picker), in catalogue order.</summary>
    public static readonly IReadOnlyList<(string Name, string Glyph)> Glyphs =
    [
        ("code", "</>"), ("braces", "{}"), ("terminal", ">_"), ("cv-braces", "{cv}"), ("cv-tag", "<cv/>"), ("lambda", "λ"),
    ];

    public static string Svg(FaviconConfig? config)
    {
        var symbol = config?.Symbol is { } s && Symbols.TryGetValue(s, out var content) ? content : Symbols[DefaultSymbol];
        var color = ColorValue(config?.Color) ?? Colors[DefaultColor];
        var background = ColorValue(config?.Background) ?? Colors[DefaultBackground];
        return $"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32" color="{color}"><rect width="32" height="32" rx="7" fill="{background}"/>{symbol}</svg>""";
    }

    /// <summary>Hex value of a named or hex colour; null if invalid (never passes arbitrary text into the SVG).</summary>
    public static string? ColorValue(string? value) =>
        value is null ? null
        : Colors.TryGetValue(value, out var named) ? named
        : HexColorRegex().IsMatch(value) ? value.ToLowerInvariant()
        : null;

    private static string Text(string text, double size) =>
        $"""<text x="16" y="16" dy="0.35em" text-anchor="middle" fill="currentColor" font-size="{size.ToString(CultureInfo.InvariantCulture)}" font-weight="700" font-family="ui-monospace, 'SF Mono', Menlo, Consolas, 'DejaVu Sans Mono', 'Liberation Mono', monospace">{System.Net.WebUtility.HtmlEncode(text)}</text>""";

    [GeneratedRegex("^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$")]
    private static partial Regex HexColorRegex();
}
