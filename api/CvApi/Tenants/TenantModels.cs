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
}

/// <summary>A redaction policy. Used for profiles and (as partial override) for invites.</summary>
public sealed class AccessPolicy
{
    public List<string>? Grants { get; set; }
    public RedactionFlags? Flags { get; set; }
    public List<string>? HiddenFields { get; set; }
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
