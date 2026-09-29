using CvApi.Access;
using CvApi.Tenants;
using CvApi.Versioning;

namespace CvApi.Tracking;

/// <summary>Retention periods in months, counted from a person's last visit (R9.2).</summary>
public sealed record Retention(int IdentifiersMonths, int EventsMonths, int SummaryMonths, int HeatMonths)
{
    public const int MaxMonths = 36;
    public static Retention Default { get; } = new(13, 13, 25, 25);

    public static Retention From(RetentionSettings? s) => new(
        Clamp(s?.IdentifiersMonths, Default.IdentifiersMonths),
        Clamp(s?.EventsMonths, Default.EventsMonths),
        Clamp(s?.SummaryMonths, Default.SummaryMonths),
        Clamp(s?.HeatMonths, Default.HeatMonths));

    private static int Clamp(int? months, int fallback) => months is { } m ? Math.Clamp(m, 1, MaxMonths) : fallback;
}

/// <summary>Effective tracking settings of one visitor's grant.</summary>
/// <param name="Enabled">Tracking (and with it the consent modal) is on for this grant.</param>
/// <param name="DisabledBy">Level that switched it off: "invite", "profile", "tenant", "no-privacy" or "render".</param>
public sealed record TrackingDecision(bool Enabled, string? DisabledBy, string? Controller, string? Contact, bool HonorBrowserSignals,
    Retention Retention, string PolicyVersion);

public static class TrackingPolicy
{
    /// <summary>
    /// Version of the consent texts in the frontend (src/app/components/CvConsentModal.vue, CONSENT_TEXT_VERSION).
    /// Bump both together when the wording changes: every visitor is asked again.
    /// </summary>
    public const string TextVersion = "2026-09-29";

    /// <summary>
    /// Resolves the tracking switch: invite overrides &gt; profile &gt; tenant, default on. Tenants without a
    /// <c>privacy.controller</c> never track (the modal needs a controller). The PDF renderer is never tracked.
    /// </summary>
    public static TrackingDecision Resolve(AccessGrant grant)
    {
        var config = grant.Tenant.Config;
        var tenantTracking = config.Tracking;
        var profile = config.Profiles.GetValueOrDefault(grant.ProfileName);
        var invite = grant.Invite is { } i ? AccessService.ParseOverrides(i.OverridesJson) : null;

        var (enabled, level) = invite?.Tracking?.Enabled is { } fromInvite ? (fromInvite, "invite")
            : profile?.Tracking?.Enabled is { } fromProfile ? (fromProfile, "profile")
            : tenantTracking?.Enabled is { } fromTenant ? (fromTenant, "tenant")
            : (true, "tenant");

        var controller = config.Privacy?.Controller?.Trim();
        var contact = config.Privacy?.Contact?.Trim();
        var retention = Retention.From(tenantTracking?.Retention);
        var honor = tenantTracking?.HonorBrowserSignals ?? false;

        string? disabledBy = grant.ViaRenderTicket ? "render"
            : !enabled ? level
            : string.IsNullOrEmpty(controller) ? "no-privacy"
            : null;
        return new TrackingDecision(disabledBy is null, disabledBy, controller, contact, honor, retention,
            PolicyVersionOf(controller, contact, retention));
    }

    /// <summary>Hash of everything the consent text shows; a change asks every visitor again (R9.12).</summary>
    public static string PolicyVersionOf(string? controller, string? contact, Retention retention) =>
        Sha256.OfText($"{TextVersion}\n{controller}\n{contact}\n{retention.IdentifiersMonths}/{retention.EventsMonths}/{retention.SummaryMonths}/{retention.HeatMonths}")[..12];

    /// <summary>Visitor group of a grant: the invite, or "public:&lt;profile&gt;".</summary>
    public static string GroupKey(AccessGrant grant) =>
        grant.Invite is { } invite ? invite.Id.ToString("N") : $"public:{grant.ProfileName}";
}
