namespace CvApi.Tenants;

/// <summary>Contents of /data/tenants/{id}/tenant.json.</summary>
public sealed class TenantConfig
{
    public string Name { get; set; } = "";
    public List<string> Hosts { get; set; } = [];
    public string DefaultLocale { get; set; } = "en";

    /// <summary>Profile shown on the tenant's own hosts without a valid invite. Null = no public access.</summary>
    public string? PublicProfile { get; set; }

    public Dictionary<string, AccessPolicy> Profiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Default templates of this person's CV (PDF and, later, web).</summary>
    public TemplateSelection? Templates { get; set; }

    /// <summary>Whether invites may override the template (default: allowed).</summary>
    public bool AllowInviteTemplateOverride { get; set; } = true;
}

/// <summary>Template names per output. Null = not set at this level.</summary>
public sealed class TemplateSelection
{
    public string? Pdf { get; set; }
    public string? Html { get; set; }

    /// <summary>
    /// Variables of the PDF template (colours, toggles, preset …), e.g. { "preset": "graphite", "accent": "#29a8e0" }.
    /// Merged per key across levels; the template defines names, types and defaults.
    /// </summary>
    public Dictionary<string, System.Text.Json.JsonElement>? PdfVars { get; set; }
}

/// <summary>A redaction policy. Used for profiles and (as partial override) for invites.</summary>
public sealed class AccessPolicy
{
    public List<string>? Grants { get; set; }
    public RedactionFlags? Flags { get; set; }
    public List<string>? HiddenFields { get; set; }

    /// <summary>Template choice for this profile / invite (overrides the tenant default).</summary>
    public TemplateSelection? Templates { get; set; }
}

/// <summary>Global redaction flags. Null means "not set" (relevant for invite overrides).</summary>
public sealed class RedactionFlags
{
    public bool? HideCompanies { get; set; }
    public bool? HideTimeframeDays { get; set; }
    public bool? HideTimeframeMonths { get; set; }
    public bool? HidePhoto { get; set; }
    public bool? HideContactDetails { get; set; }
    public bool? HideBirthDate { get; set; }
    public bool? HideMedia { get; set; }
}

public sealed record Tenant(string Id, TenantConfig Config, string Directory);
