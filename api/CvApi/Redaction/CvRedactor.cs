using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CvApi.Tenants;

namespace CvApi.Redaction;

/// <summary>The fully merged policy (profile + invite overrides) applied to a master CV.</summary>
public sealed record EffectivePolicy(IReadOnlySet<string> Grants, EffectiveFlags Flags, IReadOnlyList<string> HiddenFields,
    string? Revision = null)
{
    /// <summary>
    /// Merges a profile with invite overrides: set flags replace, hiddenFields are added,
    /// grants (if set) replace. Flags set nowhere default to <paramref name="hideByDefault"/>
    /// (true for a tenant's public profile, so its view only shows what is explicitly allowed).
    /// </summary>
    public static EffectivePolicy From(AccessPolicy profile, AccessPolicy? overrides = null, bool hideByDefault = false)
    {
        var d = hideByDefault;
        var grants = overrides?.Grants ?? profile.Grants ?? [];
        var hidden = (profile.HiddenFields ?? []).Concat(overrides?.HiddenFields ?? []).Distinct().ToList();
        var p = profile.Flags;
        var o = overrides?.Flags;
        var flags = new EffectiveFlags(
            HideCompanies: o?.HideCompanies ?? p?.HideCompanies ?? d,
            HideTimeframeDays: o?.HideTimeframeDays ?? p?.HideTimeframeDays ?? d,
            HideTimeframeMonths: o?.HideTimeframeMonths ?? p?.HideTimeframeMonths ?? d,
            HidePhoto: o?.HidePhoto ?? p?.HidePhoto ?? d,
            HideContactDetails: o?.HideContactDetails ?? p?.HideContactDetails ?? d,
            HideBirthDate: o?.HideBirthDate ?? p?.HideBirthDate ?? d,
            HideMedia: o?.HideMedia ?? p?.HideMedia ?? d);
        // Pinned CV revision: the override replaces the profile's pin; "" unpins.
        var revision = overrides?.Revision ?? profile.Revision;
        return new EffectivePolicy(new HashSet<string>(grants, StringComparer.OrdinalIgnoreCase), flags, hidden,
            string.IsNullOrWhiteSpace(revision) ? null : revision.Trim());
    }
}

public sealed record EffectiveFlags(
    bool HideCompanies,
    bool HideTimeframeDays,
    bool HideTimeframeMonths,
    bool HidePhoto,
    bool HideContactDetails,
    bool HideBirthDate,
    bool HideMedia);

/// <summary>
/// Produces the redacted CV a visitor may see. Works on a deep copy; removed data is absent from the result.
/// </summary>
public static partial class CvRedactor
{
    private const string Requires = "requires";
    private const string FieldRequires = "fieldRequires";
    private static readonly string[] DateFields = ["startDate", "endDate"];
    private static readonly string[] MediaFields = ["images", "screenshots", "logos"];

    public static JsonObject Redact(JsonObject master, EffectivePolicy policy)
    {
        var cv = master.DeepClone().AsObject();

        ApplyRequires(cv, policy.Grants);

        foreach (var path in policy.HiddenFields)
            RemovePath(cv, path.Split('.', StringSplitOptions.RemoveEmptyEntries));

        ApplyFlags(cv, policy.Flags);
        return cv;
    }

    /// <summary>Collects every string value in the CV (used to check which assets a visitor may load).</summary>
    public static IEnumerable<string> AllStrings(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (_, value) in obj)
                foreach (var s in AllStrings(value)) yield return s;
                break;
            case JsonArray arr:
                foreach (var item in arr)
                foreach (var s in AllStrings(item)) yield return s;
                break;
            case JsonValue v when v.TryGetValue<string>(out var str):
                yield return str;
                break;
        }
    }

    private static bool IsGranted(JsonNode? requires, IReadOnlySet<string> grants) => requires switch
    {
        null => true,
        JsonArray arr => arr.Any(r => r?.GetValueKind() == System.Text.Json.JsonValueKind.String && grants.Contains(r.GetValue<string>())),
        JsonValue v when v.TryGetValue<string>(out var s) => grants.Contains(s),
        _ => false,
    };

    private static bool IsVisible(JsonNode? node, IReadOnlySet<string> grants) =>
        node is not JsonObject obj || IsGranted(obj[Requires], grants);

    private static void ApplyRequires(JsonNode? node, IReadOnlySet<string> grants)
    {
        switch (node)
        {
            case JsonObject obj:
                if (obj[FieldRequires] is JsonObject fieldRequires)
                {
                    foreach (var (field, requires) in fieldRequires.ToList())
                        if (!IsGranted(requires, grants)) obj.Remove(field);
                }
                obj.Remove(FieldRequires);
                obj.Remove(Requires);

                foreach (var (key, value) in obj.ToList())
                {
                    if (!IsVisible(value, grants)) obj.Remove(key);
                    else ApplyRequires(value, grants);
                }
                break;

            case JsonArray arr:
                for (var i = arr.Count - 1; i >= 0; i--)
                {
                    if (!IsVisible(arr[i], grants)) arr.RemoveAt(i);
                    else ApplyRequires(arr[i], grants);
                }
                break;
        }
    }

    /// <summary>Removes a dot path. A segment applied to an array applies to every element.</summary>
    private static void RemovePath(JsonNode? node, ReadOnlySpan<string> segments)
    {
        if (segments.IsEmpty) return;
        switch (node)
        {
            case JsonArray arr:
                foreach (var item in arr) RemovePath(item, segments);
                break;
            case JsonObject obj when segments.Length == 1:
                obj.Remove(segments[0]);
                break;
            case JsonObject obj:
                RemovePath(obj[segments[0]], segments[1..]);
                break;
        }
    }

    private static void ApplyFlags(JsonObject cv, EffectiveFlags flags)
    {
        // Experiences: company name → alias; company media would reveal the company.
        if (cv["experiences"] is JsonArray experiences)
        {
            foreach (var exp in experiences.OfType<JsonObject>())
            {
                var alias = exp["companyAlias"]?.DeepClone();
                exp.Remove("companyAlias");
                if (!flags.HideCompanies) continue;
                exp.Remove("company");
                if (alias is not null) exp["company"] = alias;
                exp.Remove("logos");
                exp.Remove("images");
            }
        }

        // Projects may name the client the same way.
        if (cv["projects"] is JsonArray projects)
        {
            foreach (var project in projects.OfType<JsonObject>())
            {
                var alias = project["clientAlias"]?.DeepClone();
                project.Remove("clientAlias");
                if (!flags.HideCompanies) continue;
                project.Remove("client");
                if (alias is not null) project["client"] = alias;
            }
        }

        var precision = flags.HideTimeframeMonths ? 4 : flags.HideTimeframeDays ? 7 : 0;
        if (precision > 0) ReduceDates(cv, precision);

        // Covers photoUrl and any size variants (photoUrlLarge, ...).
        if (flags.HidePhoto && cv["profile"] is JsonObject profile)
        {
            foreach (var key in profile.Select(p => p.Key).Where(k => k.StartsWith("photo", StringComparison.OrdinalIgnoreCase)).ToList())
                profile.Remove(key);
        }
        if (flags.HideContactDetails)
        {
            RemovePath(cv, ["details", "email"]);
            RemovePath(cv, ["details", "phone"]);
        }
        if (flags.HideBirthDate) RemovePath(cv, ["details", "birthDate"]);
        if (flags.HideMedia) RemoveEverywhere(cv, MediaFields);
    }

    /// <summary>Truncates start/end dates (anywhere in the CV) and drops hand-written periods that could leak precision.</summary>
    private static void ReduceDates(JsonNode? node, int length)
    {
        switch (node)
        {
            case JsonObject obj:
                obj.Remove("period");
                foreach (var field in DateFields)
                {
                    if (obj[field] is JsonValue v && v.TryGetValue<string>(out var s) && IsoDateRegex().IsMatch(s) && s.Length > length)
                        obj[field] = s[..length];
                }
                foreach (var (_, value) in obj.ToList()) ReduceDates(value, length);
                break;
            case JsonArray arr:
                foreach (var item in arr) ReduceDates(item, length);
                break;
        }
    }

    private static void RemoveEverywhere(JsonNode? node, string[] fields)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var field in fields) obj.Remove(field);
                foreach (var (_, value) in obj.ToList()) RemoveEverywhere(value, fields);
                break;
            case JsonArray arr:
                foreach (var item in arr) RemoveEverywhere(item, fields);
                break;
        }
    }

    [GeneratedRegex(@"^\d{4}(-\d{2}(-\d{2})?)?")]
    private static partial Regex IsoDateRegex();
}
