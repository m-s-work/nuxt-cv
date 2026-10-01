using System.Text.Json.Nodes;
using CvApi.Access;
using CvApi.Links;
using CvApi.Pdf;
using CvApi.Redaction;
using CvApi.Tenants;
using CvApi.Versioning;
using Microsoft.AspNetCore.StaticFiles;

namespace CvApi.Endpoints;

public static partial class PublicEndpoints
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
            IConfiguration config, Tracking.ConsentCookies consentCookies, Tracking.TrackingService tracking,
            Tracking.CvSourceVersion sourceVersion, Accounts.TenantOwners owners, CancellationToken ct) =>
        {
            NoStore(ctx);
            var grant = await access.ResolveAsync(ctx, ct);
            if (grant is null) return NoAccess(ctx, tenants);

            var loaded = tenants.LoadCv(grant.Tenant, locale, grant.Policy.Revision);
            if (loaded is null) return NoAccess(ctx, tenants);

            var (master, resolvedLocale) = loaded.Value;
            var redacted = CvRedactor.Redact(master, grant.Policy);
            // Hashes and versions are of the redacted CV itself (as tracking and cv-sync compute them); only then are
            // website links replaced by /api/go paths (§7.2).
            var redactedJson = redacted.ToJsonString();
            // The PDF renderer also gets the original links (printed as text / clear links, §7.2).
            ExternalLinks.Rewrite(redacted, keepTarget: grant.ViaRenderTicket);
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
                // "shared": the frontend shows the CV at /cv and keeps "/" for the showcase (§11). Never names a tenant.
                host = HostKind(ctx, tenants),
                locale = resolvedLocale,
                features = new { pdf = pdf.Enabled },
                templates = new { pdf = grant.Templates.Pdf, html = grant.Templates.Html, pdfVars = grant.Templates.PdfVars },
                // Platform site for the "Created with …" credit (shared base URL, if configured).
                links = new { platform = Accounts.Branding.PlatformLink(grant.Tenant, config, owners) },
                // SHA-256 of the redacted CV (the "cv" value below before website links are replaced) – for tests and deployment checks.
                cvHash = Sha256.OfText(redactedJson),
                // Versions of this view for the visitor tracking (§6.4): short hash of the redacted CV and the CV's git SHA.
                cvVersion = TrackingEndpoints.CvVersionOf(redactedJson),
                cvSourceSha = sourceVersion.For(grant.Tenant, grant.Policy.Revision),
                // Consent modal (docs/VISITOR_SESSION_TRACKING.md §9.1); required = false: no modal, no tracking.
                consent = await TrackingEndpoints.ConsentInfoAsync(ctx, grant, consentCookies, tracking, ct),
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

        // Favicon of the visitor's tenant (or the tenant of the host); the default icon otherwise.
        // Only symbol and colours are configurable, so it reveals nothing about the CV.
        app.MapGet("/favicon.svg", async (HttpContext ctx, AccessService access, TenantStore tenants, CancellationToken ct) =>
        {
            var tenant = (await access.ResolveAsync(ctx, ct))?.Tenant ?? tenants.FindByHost(ctx.Request.Host.Host);
            // Revalidate on every page load: the icon changes after redeeming an invite or editing tenant.json.
            ctx.Response.Headers.CacheControl = "private, no-cache";
            return Results.Text(Favicon.Svg(tenant?.Config.Favicon), "image/svg+xml");
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


        // Link to a live website in the visitor's CV: redirects there (§7.2). On the web page the frontend records the
        // click as link_out; links printed into a PDF carry the PDF's QR code (c) and are counted here per PDF.
        app.MapGet("/go/{key}", async (string key, string? c, HttpContext ctx, AccessService access, TenantStore tenants,
            CancellationToken ct) =>
        {
            NoStore(ctx);
            ctx.Response.Headers["Referrer-Policy"] = "no-referrer";
            if (!string.IsNullOrEmpty(c))
            {
                // No redeem and no cookie: following a printed link is not a visit of the CV.
                if (await access.FindActiveByCodeAsync(c, ct) is not { } invite || access.GrantFor(invite) is not { } printed
                    || FindLink(printed, tenants, key) is not { } printedUrl) return Results.NotFound();
                await access.CountLinkClickAsync(invite, key, printedUrl, ct);
                return Results.Redirect(printedUrl);
            }
            var grant = await access.ResolveAsync(ctx, ct);
            if (grant is null || FindLink(grant, tenants, key) is not { } url) return Results.NotFound();
            return Results.Redirect(url);
        });
    }

    /// <summary>Website link of a key if the visitor's redacted CV (in any locale) contains it.</summary>
    private static string? FindLink(AccessGrant grant, TenantStore tenants, string key)
    {
        if (!KeyRegex().IsMatch(key)) return null;
        foreach (var locale in TenantStore.Locales(grant.Tenant, grant.Policy.Revision))
        {
            if (tenants.LoadCv(grant.Tenant, locale, grant.Policy.Revision) is not { } loaded) continue;
            if (ExternalLinks.Find(CvRedactor.Redact(loaded.Cv, grant.Policy), key) is { } url) return url;
        }
        return null;
    }

    [System.Text.RegularExpressions.GeneratedRegex("^[0-9a-f]{32}$")]
    private static partial System.Text.RegularExpressions.Regex KeyRegex();

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
            host = HostKind(ctx, tenants),
        }, statusCode: StatusCodes.Status403Forbidden);

    private static string HostKind(HttpContext ctx, TenantStore tenants) =>
        tenants.FindByHost(ctx.Request.Host.Host) is null ? "shared" : "tenant";

    private static void NoStore(HttpContext ctx)
    {
        ctx.Response.Headers.CacheControl = "private, no-store";
        ctx.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
    }
}
