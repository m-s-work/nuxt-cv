using System.Text.Json.Nodes;
using CvApi.Access;
using CvApi.Redaction;
using CvApi.Tenants;
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

        app.MapGet("/cv", async (string? locale, HttpContext ctx, AccessService access, TenantStore tenants, CancellationToken ct) =>
        {
            NoStore(ctx);
            var grant = await access.ResolveAsync(ctx, ct);
            if (grant is null) return NoAccess(ctx, tenants);

            var loaded = tenants.LoadCv(grant.Tenant, locale);
            if (loaded is null) return NoAccess(ctx, tenants);

            var (master, resolvedLocale) = loaded.Value;
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
                cv = CvRedactor.Redact(master, grant.Policy),
            });
        });

        app.MapGet("/assets/{file}", async (string file, HttpContext ctx, AccessService access, TenantStore tenants, CancellationToken ct) =>
        {
            NoStore(ctx);
            var grant = await access.ResolveAsync(ctx, ct);
            if (grant is null) return Results.NotFound();

            var path = tenants.AssetPath(grant.Tenant, file);
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
        var locales = Directory.EnumerateFiles(grant.Tenant.Directory, "cv.*.json")
            .Select(f => Path.GetFileName(f)["cv.".Length..^".json".Length]);
        foreach (var locale in locales)
        {
            if (tenants.LoadCv(grant.Tenant, locale) is not { } loaded) continue;
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
