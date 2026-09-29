using System.Text.Json.Nodes;
using CvApi.Access;
using CvApi.Pdf;
using CvApi.Redaction;
using CvApi.Tenants;
using CvApi.Versioning;
using Microsoft.AspNetCore.StaticFiles;

namespace CvApi.Endpoints;

public static class PublicEndpoints
{
    public const string AssetPrefix = "/api/assets/";
    public const string RedeemRateLimitPolicy = "redeem";

    public sealed record RedeemRequest(string? Code);

    public static void MapPublicEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

        // Deployed software version: Git commit + build time of the API and the PDF renderer
        // (the web container serves /version.json). Used to check that a deployment is current.
        app.MapGet("/version", async (HttpContext ctx, PdfService pdf, IServiceProvider services) =>
        {
            NoStore(ctx);
            object? renderer = null;
            if (pdf.Enabled)
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                try
                {
                    renderer = await services.GetRequiredService<IPdfRenderer>().VersionAsync(timeout.Token);
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
                {
                    renderer = new { error = "unreachable" };
                }
            }
            return Results.Ok(new { api = BuildInfo.Current, pdf = renderer });
        });

        app.MapPost("/access/redeem", async (RedeemRequest body, HttpContext ctx, AccessService access, CancellationToken ct) =>
        {
            NoStore(ctx);
            return await access.RedeemAsync(ctx, body.Code, ct) == RedeemResult.Ok
                ? Results.NoContent()
                : Results.BadRequest(new { error = "invalid_invite" });
        }).RequireRateLimiting(RedeemRateLimitPolicy);

        app.MapPost("/access/logout", (HttpContext ctx) =>
        {
            AccessService.ClearCookie(ctx);
            return Results.NoContent();
        });

        app.MapGet("/cv", async (string? locale, HttpContext ctx, AccessService access, TenantStore tenants, PdfService pdf,
            IConfiguration config, CancellationToken ct) =>
        {
            NoStore(ctx);
            var grant = await access.ResolveAsync(ctx, ct);
            if (grant is null) return NoAccess(ctx, tenants);

            var loaded = tenants.LoadCv(grant.Tenant, locale, grant.Policy.Revision);
            if (loaded is null) return NoAccess(ctx, tenants);

            var (master, resolvedLocale) = loaded.Value;
            var redacted = CvRedactor.Redact(master, grant.Policy);
            return Results.Ok(new
            {
                access = new
                {
                    tenant = grant.Tenant.Id,
                    profile = grant.ProfileName,
                    viaInvite = grant.Invite is not null,
                    label = grant.Invite?.Label,
                    expiresAt = grant.Invite?.ExpiresAt,
                },
                locale = resolvedLocale,
                features = new { pdf = pdf.Enabled },
                templates = new { pdf = grant.Templates.Pdf, html = grant.Templates.Html, pdfVars = grant.Templates.PdfVars },
                // Platform site for the "Created with …" credit (shared base URL, if configured).
                links = new { platform = string.IsNullOrEmpty(config["Cv:SharedBaseUrl"]) ? null : config["Cv:SharedBaseUrl"]!.TrimEnd('/') },
                // SHA-256 of exactly this redacted CV (the "cv" value below) – for tests and deployment checks.
                cvHash = Sha256.OfText(redacted.ToJsonString()),
                cv = redacted,
            });
        });

        // PDF of exactly the visitor's view. Served from cache; re-rendered when the CV changed
        // (can take several seconds – the frontend shows a loading message meanwhile).
        app.MapGet("/pdf", async (string? locale, HttpContext ctx, AccessService access, PdfService pdf,
            ILoggerFactory loggers, CancellationToken ct) =>
        {
            NoStore(ctx);
            var grant = await access.ResolveAsync(ctx, ct);
            if (grant is null) return Results.Json(new { error = "no_access" }, statusCode: StatusCodes.Status403Forbidden);
            if (!pdf.Enabled) return Results.NotFound(new { error = "pdf_disabled" });

            try
            {
                var result = await pdf.GetOrRenderAsync(grant, locale, ct);
                if (result is null) return Results.NotFound();
                ctx.Response.Headers["X-Pdf-Cache"] = result.FromCache ? "hit" : "miss";
                return Results.File(result.Content, "application/pdf", result.FileName);
            }
            catch (Exception ex) when (ex is PdfRenderException or HttpRequestException or TaskCanceledException)
            {
                loggers.CreateLogger("Pdf").LogError(ex, "PDF rendering failed for tenant {Tenant}", grant.Tenant.Id);
                return Results.Json(new { error = "pdf_failed" }, statusCode: StatusCodes.Status502BadGateway);
            }
        });

        app.MapGet("/assets/{file}", async (string file, HttpContext ctx, AccessService access, TenantStore tenants, CancellationToken ct) =>
        {
            NoStore(ctx);
            var grant = await access.ResolveAsync(ctx, ct);
            if (grant is null) return Results.NotFound();

            var path = tenants.AssetPath(grant.Tenant, file, grant.Policy.Revision);
            if (path is null || !IsReferenced(grant, tenants, file)) return Results.NotFound();

            var contentType = new FileExtensionContentTypeProvider().TryGetContentType(file, out var type)
                ? type
                : "application/octet-stream";
            return Results.File(path, contentType);
        });
    }

    /// <summary>An asset is only served if the visitor's redacted CV (in any locale) references it.</summary>
    private static bool IsReferenced(AccessGrant grant, TenantStore tenants, string file)
    {
        var url = AssetPrefix + file;
        foreach (var locale in TenantStore.Locales(grant.Tenant, grant.Policy.Revision))
        {
            if (tenants.LoadCv(grant.Tenant, locale, grant.Policy.Revision) is not { } loaded) continue;
            if (CvRedactor.AllStrings(CvRedactor.Redact(loaded.Cv, grant.Policy)).Contains(url)) return true;
        }
        return false;
    }

    /// <summary>
    /// 403 with the kind of host: "shared" (frontend shows the showcase) or "tenant"
    /// (neutral invitation page). Never names the tenant.
    /// </summary>
    private static IResult NoAccess(HttpContext ctx, TenantStore tenants) =>
        Results.Json(new
        {
            error = "no_access",
            host = tenants.FindByHost(ctx.Request.Host.Host) is null ? "shared" : "tenant",
        }, statusCode: StatusCodes.Status403Forbidden);

    private static void NoStore(HttpContext ctx)
    {
        ctx.Response.Headers.CacheControl = "private, no-store";
        ctx.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
    }
}
