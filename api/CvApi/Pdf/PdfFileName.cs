using System.Globalization;
using System.Text;

namespace CvApi.Pdf;

/// <summary>Download names like "cv-max-mustermann-en.pdf" (see frontend utils/cvFileName.ts – keep in sync).</summary>
public static class PdfFileName
{
    private static readonly Dictionary<char, string> Transliterations = new()
    {
        ['ä'] = "ae", ['ö'] = "oe", ['ü'] = "ue", ['ß'] = "ss",
        ['Ä'] = "ae", ['Ö'] = "oe", ['Ü'] = "ue",
    };

    /// <param name="name">Name from the redacted CV; null/empty if the profile hides it.</param>
    public static string For(string? name, string locale)
    {
        var slug = Slug(name);
        return slug.Length == 0 ? $"cv-{locale}.pdf" : $"cv-{slug}-{locale}.pdf";
    }

    public static string Slug(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var sb = new StringBuilder();
        foreach (var c in text.Trim())
            sb.Append(Transliterations.TryGetValue(c, out var t) ? t : c.ToString());

        // Strip remaining diacritics (é → e), then keep a-z0-9 and single dashes.
        var decomposed = sb.ToString().Normalize(NormalizationForm.FormD);
        var result = new StringBuilder();
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            var lower = char.ToLowerInvariant(c);
            if (lower is >= 'a' and <= 'z' or >= '0' and <= '9') result.Append(lower);
            else if (result.Length > 0 && result[^1] != '-') result.Append('-');
        }
        return result.ToString().Trim('-');
    }
}
