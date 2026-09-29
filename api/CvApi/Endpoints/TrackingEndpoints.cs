using CvApi.Access;
using CvApi.Redaction;
using CvApi.Tenants;
using CvApi.Tracking;
using CvApi.Versioning;

namespace CvApi.Endpoints;

/// <summary>Consent and event ingest for the visitor tracking (docs/VISITOR_SESSION_TRACKING.md §8.1, §9.1).</summary>
public static class TrackingEndpoints
{
    public const string EventsRateLimitPolicy = "events";
    public const long MaxBatchBytes = 64 * 1024;

    public sealed record ConsentRequest(string? Choice, string? Source, string? PolicyVersion);

    public static void MapTrackingEndpoints(this IEndpointRouteBuilder app)
    {
        // Accept or decline (R9.6). Both keep the CV readable; only accept sets cv_vid.
        app.MapPost("/consent", async (ConsentRequest body, HttpContext ctx, AccessService access, TrackingService tracking,
            CancellationToken ct) =>
        {
            NoStore(ctx);
            var grant = await access.ResolveAsync(ctx, ct);
            if (grant is null) return Results.NoContent();
            var decision = TrackingPolicy.Resolve(grant);
            if (!decision.Enabled) return Results.NoContent();
            if (body.Choice is not ("accept" or "decline")) return Results.BadRequest(new { error = "invalid_choice" });
            // The visitor must have seen the current text; an outdated modal is simply shown again.
            if (body.PolicyVersion is { } seen && seen != decision.PolicyVersion)
                return Results.Conflict(new { error = "policy_changed", policyVersion = decision.PolicyVersion });
            await tracking.RecordConsentAsync(ctx, grant, decision, body.Choice, body.Source == "footer" ? "footer" : "modal", ct);
            return Results.NoContent();
        });

        // Withdraw (footer "Privacy" link): stops tracking and forgets the browser id.
        app.MapDelete("/consent", async (HttpContext ctx, AccessService access, TrackingService tracking, CancellationToken ct) =>
        {
            NoStore(ctx);
            var grant = await access.ResolveAsync(ctx, ct);
            if (grant is null)
            {
                ConsentCookies.ClearVisitorKey(ctx);
                return Results.NoContent();
            }
            var decision = TrackingPolicy.Resolve(grant);
            await tracking.RecordConsentAsync(ctx, grant, decision, "withdraw", "footer", ct);
            return Results.NoContent();
        });

        // Batch of tracking events of one session. Always 204, so the endpoint reveals nothing (R8.1).
        app.MapPost("/events", async (HttpContext ctx, AccessService access, TrackingService tracking, TenantStore tenants,
            CvSourceVersion sourceVersion, CancellationToken ct) =>
        {
            NoStore(ctx);
            if (ctx.Request.ContentLength is > MaxBatchBytes) return Results.NoContent();
            EventBatch? batch;
            try
            {
                using var limited = new MemoryStream();
                await ctx.Request.Body.CopyToAsync(limited, ct);
                if (limited.Length > MaxBatchBytes) return Results.NoContent();
                limited.Position = 0;
                batch = await System.Text.Json.JsonSerializer.DeserializeAsync<EventBatch>(limited,
                    new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web), ct);
            }
            catch (System.Text.Json.JsonException)
            {
                return Results.NoContent();
            }
            if (batch is null) return Results.NoContent();

            var grant = await access.ResolveAsync(ctx, ct);
            if (grant is null) return Results.NoContent();
            var decision = TrackingPolicy.Resolve(grant);
            if (!decision.Enabled) return Results.NoContent();

            // The server's own view of the CV version for this session (R6.10).
            string cvVersion = "unknown";
            if (batch.Start is not null && tenants.LoadCv(grant.Tenant, batch.Start.Locale, grant.Policy.Revision) is { } loaded)
                cvVersion = CvVersionOf(CvRedactor.Redact(loaded.Cv, grant.Policy).ToJsonString());
            await tracking.IngestAsync(ctx, grant, decision, batch, cvVersion, sourceVersion.For(grant.Tenant, grant.Policy.Revision), ct);
            return Results.NoContent();
        }).RequireRateLimiting(EventsRateLimitPolicy);
    }

    /// <summary>Short CV version: first 16 hex chars of the SHA-256 of the redacted CV JSON (§6.4).</summary>
    public static string CvVersionOf(string redactedJson) => Sha256.OfText(redactedJson)[..16];

    /// <summary>
    /// Consent information for /api/cv: whether the modal is needed and what it shows. The visitor's cookie decides the
    /// state; DNT/GPC only count as a decline when the tenant honours them (R9.8).
    /// </summary>
    public static async Task<object> ConsentInfoAsync(HttpContext ctx, AccessGrant grant, ConsentCookies cookies, TrackingService tracking,
        CancellationToken ct)
    {
        var decision = TrackingPolicy.Resolve(grant);
        if (!decision.Enabled) return new { required = false };

        var state = cookies.State(ctx, decision.PolicyVersion);
        if (state is null && decision.HonorBrowserSignals && TrackingService.Signals(ctx.Request) is not null)
        {
            await tracking.RecordSignalDeclineAsync(ctx, grant, decision, ct);
            state = "decline";
        }
        // Every visit with valid consent refreshes the browser id's lifetime (R3.1).
        if (state == "accept") cookies.EnsureVisitorKey(ctx);
        return new
        {
            required = true,
            state,
            policyVersion = decision.PolicyVersion,
            controller = decision.Controller,
            contact = decision.Contact,
            retention = new
            {
                identifiersMonths = decision.Retention.IdentifiersMonths,
                eventsMonths = decision.Retention.EventsMonths,
                summaryMonths = decision.Retention.SummaryMonths,
            },
            signals = TrackingService.Signals(ctx.Request),
        };
    }

    private static void NoStore(HttpContext ctx)
    {
        ctx.Response.Headers.CacheControl = "private, no-store";
        ctx.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
    }
}
