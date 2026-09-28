using System.Text.Json;
using System.Text.RegularExpressions;

namespace CvApi.Tenants;

/// <summary>
/// Resolves which template (and template variables) a visitor gets. Most specific wins:
/// explicit (admin preview) &gt; invite overrides (if the tenant allows them) &gt; profile &gt; tenant.
/// Template names: null means "frontend default"; unknown names fall back to the default in the frontend.
/// Variables are merged per key; the frontend applies only keys and values its template declares.
/// </summary>
public static partial class TemplateResolver
{
    private const int MaxVars = 32;
    private const int MaxListItems = 12;

    public static TemplateSelection Resolve(TenantConfig tenant, AccessPolicy? profile, AccessPolicy? inviteOverrides,
        string? explicitPdf = null, IReadOnlyDictionary<string, JsonElement>? explicitVars = null)
    {
        var invite = tenant.AllowInviteTemplateOverride ? inviteOverrides?.Templates : null;
        return new TemplateSelection
        {
            Pdf = Valid(explicitPdf) ?? Valid(invite?.Pdf) ?? Valid(profile?.Templates?.Pdf) ?? Valid(tenant.Templates?.Pdf),
            Html = Valid(invite?.Html) ?? Valid(profile?.Templates?.Html) ?? Valid(tenant.Templates?.Html),
            PdfVars = MergeVars(tenant.Templates?.PdfVars, profile?.Templates?.PdfVars, invite?.PdfVars, explicitVars),
        };
    }

    public static string? Valid(string? name) =>
        name is not null && NameRegex().IsMatch(name) ? name : null;

    /// <summary>Later levels override earlier ones per key. Invalid keys/values are dropped. Null if empty.</summary>
    public static Dictionary<string, JsonElement>? MergeVars(params IReadOnlyDictionary<string, JsonElement>?[] levels)
    {
        var merged = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var level in levels)
        {
            if (level is null) continue;
            foreach (var (key, value) in level)
            {
                if (!VarKeyRegex().IsMatch(key) || !IsValidValue(value)) continue;
                if (!merged.ContainsKey(key) && merged.Count >= MaxVars) continue;
                merged[key] = value.Clone();
            }
        }
        return merged.Count == 0 ? null : merged;
    }

    /// <summary>Strings (colours, enum values), booleans, numbers and short string lists (palettes).</summary>
    private static bool IsValidValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.True or JsonValueKind.False or JsonValueKind.Number => true,
        JsonValueKind.String => IsValidString(value.GetString()),
        JsonValueKind.Array => value.GetArrayLength() <= MaxListItems &&
                               value.EnumerateArray().All(v => v.ValueKind == JsonValueKind.String && IsValidString(v.GetString())),
        _ => false,
    };

    private static bool IsValidString(string? s) => s is not null && s.Length <= 64 && VarStringRegex().IsMatch(s);

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,31}$")]
    private static partial Regex NameRegex();

    [GeneratedRegex("^[a-zA-Z][a-zA-Z0-9]{0,31}$")]
    private static partial Regex VarKeyRegex();

    [GeneratedRegex("^[#A-Za-z0-9 ._-]*$")]
    private static partial Regex VarStringRegex();
}
