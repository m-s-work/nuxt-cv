using CvApi.Access;
using CvApi.Tenants;
using CvApi.Tracking;
using Microsoft.EntityFrameworkCore;

namespace CvApi.Endpoints;

/// <summary>Owner-only tracking reports (admin key), docs/VISITOR_SESSION_TRACKING.md §8.2.</summary>
public static class AnalyticsEndpoints
{
    public static void MapAnalyticsEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/admin/tenants/{tenantId}/analytics").AddEndpointFilter(AdminEndpoints.RequireAdminKey);

        // Per visitor group (invite or public profile): reach, time, consent, interest score – with the invite's label.
        admin.MapGet("/groups", async (string tenantId, TenantStore tenants, AnalyticsService analytics, AppDbContext appDb,
            CancellationToken ct) =>
        {
            if (tenants.Get(tenantId) is not { } tenant) return Results.NotFound();
            var groups = await analytics.GroupsAsync(tenant.Id, ct);
            var invites = await appDb.Invites.Where(i => i.TenantId == tenant.Id).ToListAsync(ct);
            return Results.Ok(groups.Select(g =>
            {
                var invite = Guid.TryParseExact(g.GroupKey, "N", out var id) ? invites.FirstOrDefault(i => i.Id == id) : null;
                return new
                {
                    g.GroupKey,
                    inviteId = invite?.Id,
                    label = invite is null ? g.GroupKey : invite.Label,
                    profile = invite?.Profile ?? (g.GroupKey.StartsWith("public:") ? g.GroupKey[7..] : null),
                    source = invite?.Source,
                    parentId = invite?.ParentId,
                    revoked = invite?.RevokedAt is not null,
                    g.Visitors, g.Persons, g.Sessions, g.Visits, g.ActiveMs, g.VisibleMs, g.FirstVisit, g.LastVisit, g.Consent, g.Score,
                };
            }));
        });

        admin.MapGet("/groups/{groupKey}", async (string tenantId, string groupKey, TenantStore tenants, AnalyticsService analytics,
            CancellationToken ct) =>
            tenants.Get(tenantId) is { } tenant ? Results.Ok(await analytics.GroupAsync(tenant, groupKey, ct)) : Results.NotFound());

        admin.MapGet("/sessions/{sessionId}", async (string tenantId, string sessionId, TenantStore tenants, AnalyticsService analytics,
            CancellationToken ct) =>
        {
            if (tenants.Get(tenantId) is not { } tenant) return Results.NotFound();
            return await analytics.SessionAsync(tenant, sessionId, ct) is { } session ? Results.Ok(session) : Results.NotFound();
        });

        // type: move | click | attention. bp / appSha / cvVersion filter; the response lists which combinations have data.
        admin.MapGet("/heatmap", async (string tenantId, string? group, string? bp, string? appSha, string? cvVersion, string? type,
            TenantStore tenants, AnalyticsService analytics, CancellationToken ct) =>
        {
            if (tenants.Get(tenantId) is null) return Results.NotFound();
            var t = type is "click" or "attention" ? type : "move";
            return Results.Ok(await analytics.HeatmapAsync(tenantId, group, bp, appSha, cvVersion, t, ct));
        });

        // The redacted CV exactly as visitors of this version saw it (R6.12), for rendering heatmaps.
        admin.MapGet("/cv-snapshots/{cvVersion}", async (string tenantId, string cvVersion, AnalyticsService analytics, CancellationToken ct) =>
            await analytics.SnapshotAsync(tenantId, cvVersion, ct) is { } s
                ? Results.Ok(new { s.CvVersion, s.Locale, s.CvSourceSha, s.FirstSeen, s.LastUsed, cv = System.Text.Json.Nodes.JsonNode.Parse(s.Json) })
                : Results.NotFound());

        admin.MapGet("/consent", async (string tenantId, AnalyticsService analytics, CancellationToken ct) =>
            Results.Ok(await analytics.ConsentAsync(tenantId, ct)));

        admin.MapGet("/persons", async (string tenantId, AnalyticsService analytics, CancellationToken ct) =>
            Results.Ok(await analytics.PersonsAsync(tenantId, ct)));

        // Erasure request of a visitor (GDPR Art. 17).
        admin.MapDelete("/visitors/{visitorId:guid}", async (string tenantId, Guid visitorId, AnalyticsService analytics, CancellationToken ct) =>
            await analytics.EraseVisitorAsync(tenantId, visitorId, ct) ? Results.NoContent() : Results.NotFound());

        // Effective tracking switch per profile and the tenant settings (for the admin UI).
        admin.MapGet("/settings", (string tenantId, TenantStore tenants, GeoLookup geo) =>
        {
            if (tenants.Get(tenantId) is not { } tenant) return Results.NotFound();
            var c = tenant.Config;
            var retention = Retention.From(c.Tracking?.Retention);
            return Results.Ok(new
            {
                privacy = c.Privacy,
                tenantEnabled = c.Tracking?.Enabled ?? true,
                honorBrowserSignals = c.Tracking?.HonorBrowserSignals ?? false,
                retention,
                geo = geo.Available,
                profiles = c.Profiles.ToDictionary(p => p.Key, p => p.Value.Tracking?.Enabled),
                consentMode = TrackingPolicy.Modes.Contains(c.Tracking?.Consent) ? c.Tracking!.Consent : "modal",
                profileModes = c.Profiles.Where(p => p.Value.Tracking?.Consent is not null)
                    .ToDictionary(p => p.Key, p => p.Value.Tracking!.Consent),
                policyVersion = TrackingPolicy.PolicyVersionOf(c.Privacy?.Controller?.Trim(), c.Privacy?.Contact?.Trim(), retention),
            });
        });
    }
}
