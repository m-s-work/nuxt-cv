using System.Text.RegularExpressions;

namespace CvApi.Tenants;

/// <summary>
/// Resolves which template a visitor gets. Most specific wins:
/// explicit (admin preview) &gt; invite overrides (if the tenant allows them) &gt; profile &gt; tenant.
/// Null means "frontend default". The API does not know the template registry; unknown names fall back
/// to the default in the frontend.
/// </summary>
public static partial class TemplateResolver
{
    public static TemplateSelection Resolve(TenantConfig tenant, AccessPolicy? profile, AccessPolicy? inviteOverrides, string? explicitPdf = null)
    {
        var invite = tenant.AllowInviteTemplateOverride ? inviteOverrides?.Templates : null;
        return new TemplateSelection
        {
            Pdf = Valid(explicitPdf) ?? Valid(invite?.Pdf) ?? Valid(profile?.Templates?.Pdf) ?? Valid(tenant.Templates?.Pdf),
            Html = Valid(invite?.Html) ?? Valid(profile?.Templates?.Html) ?? Valid(tenant.Templates?.Html),
        };
    }

    public static string? Valid(string? name) =>
        name is not null && NameRegex().IsMatch(name) ? name : null;

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,31}$")]
    private static partial Regex NameRegex();
}
