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

    /// <summary>Leave out the "Created with …" credit in PDFs (only effective on Pro or managed tenants, SaaS §4).</summary>
    public bool HideCredit { get; set; }

    /// <summary>Browser tab icon (symbol and colours). Null = default.</summary>
    public FaviconConfig? Favicon { get; set; }

    /// <summary>Controller of the visitor tracking (shown in the consent modal). Without it, nothing is tracked.</summary>
    public PrivacySettings? Privacy { get; set; }

    /// <summary>Visitor tracking defaults of this tenant (see docs/VISITOR_SESSION_TRACKING.md).</summary>
    public TrackingSettings? Tracking { get; set; }
}

public sealed class PrivacySettings
{
    /// <summary>Name of the controller, e.g. "Bob Builder".</summary>
    public string? Controller { get; set; }

    /// <summary>Contact for data protection requests (e-mail or address).</summary>
    public string? Contact { get; set; }
}

/// <summary>
/// Tracking switch. On the tenant it is the default; profiles and invites (overrides) can set <see cref="Enabled"/>
/// again – most specific wins: invite &gt; profile &gt; tenant. Disabled = no consent modal and no tracking.
/// </summary>
public sealed class TrackingSettings
{
    public bool? Enabled { get; set; }

    /// <summary>
    /// How consent is obtained (inherited like <see cref="Enabled"/>): "modal" (default, ask first), "notice" (no modal,
    /// a non-blocking notice with opt-out – only for visitors outside the EU/EEA/UK/CH) or "prior" (consent was given
    /// elsewhere, e.g. on another platform; no modal, opt-out stays available).
    /// </summary>
    public string? Consent { get; set; }

    /// <summary>Where / when the prior consent was obtained (for "prior"; shown in the admin, kept as proof).</summary>
    public string? ConsentNote { get; set; }

    /// <summary>Treat DNT / GPC as a decline without showing the modal (default: ask anyway). Tenant level only.</summary>
    public bool? HonorBrowserSignals { get; set; }

    /// <summary>Sliding retention periods in months, counted from a person's last visit. Tenant level only.</summary>
    public RetentionSettings? Retention { get; set; }
}

public sealed class RetentionSettings
{
    public int? IdentifiersMonths { get; set; }
    public int? EventsMonths { get; set; }
    public int? SummaryMonths { get; set; }
    public int? HeatMonths { get; set; }
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

    /// <summary>
    /// Git SHA (or unique prefix) of a registered CV revision to show instead of the current CV.
    /// In invite overrides it replaces the profile's pin; "" there means "current CV".
    /// </summary>
    public string? Revision { get; set; }

    /// <summary>Template choice for this profile / invite (overrides the tenant default).</summary>
    public TemplateSelection? Templates { get; set; }

    /// <summary>Tracking switch for this profile / invite (only <see cref="TrackingSettings.Enabled"/> is used here).</summary>
    public TrackingSettings? Tracking { get; set; }
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

public sealed record Tenant(string Id, TenantConfig Config, string Directory)
{
    /// <summary>Unset redaction flags of the public profile default to "hide".</summary>
    public bool IsPublicProfile(string profile) =>
        Config.PublicProfile is { } p && string.Equals(p, profile, StringComparison.OrdinalIgnoreCase);

    /// <summary>Effective policy of one of this tenant's profiles (with optional invite overrides).</summary>
    public Redaction.EffectivePolicy PolicyFor(string profile, AccessPolicy definition, AccessPolicy? overrides = null) =>
        Redaction.EffectivePolicy.From(definition, overrides, hideByDefault: IsPublicProfile(profile));
}
