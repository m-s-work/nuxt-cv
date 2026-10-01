using System.Net;
using System.Text.Json.Nodes;
using CvApi.Access;
using CvApi.Import;
using CvApi.Tenants;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

namespace CvApi.Accounts;

/// <summary>The signed-in user's own account (docs/REQUIREMENTS_SAAS.md §3, §5.4, §7, §8).</summary>
public static class AccountEndpoints
{
    private const long MaxImportBytes = 20 * 1024 * 1024;

    public sealed record CreateTenantRequest(string? Handle, string? Locale, string? Name);
    public sealed record UpdateAccountRequest(string? Name, bool? HideCredit, bool? NotifyOnOpen);
    public sealed record DomainRequest(string? Domain);
    public sealed record DeleteRequest(string? Confirm);

    public static void MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var account = app.MapGroup("/account").AddEndpointFilter(RequireUser);

        account.MapGet("/", async (HttpContext ctx, AppDbContext appDb, AccountsDbContext db, TenantStore tenants, TenantOwners owners,
            TimeProvider time, CancellationToken ct) =>
        {
            var user = CurrentUser(ctx);
            var now = time.GetUtcNow();
            var logins = await db.Logins.AsNoTracking().Where(l => l.UserId == user.Id).Select(l => l.Provider).ToListAsync(ct);
            var tenant = user.TenantId is null ? null : tenants.Get(user.TenantId);
            object? usage = tenant is null ? null : new
            {
                activeInvites = await AccountService.ActiveInviteCountAsync(appDb, tenant.Id, now, ct),
                assetBytes = AccountService.AssetBytes(tenant),
                hideCredit = tenant.Config.HideCredit,
                hosts = tenant.Config.Hosts,
            };
            return Results.Ok(new { user = Describe(user, logins, owners, now), usage });
        });

        account.MapGet("/handle/{handle}", (string handle, AccountService accounts) =>
            Results.Ok(new { handle, error = HandleErrorCode(accounts.CheckHandle(handle.Trim().ToLowerInvariant())) }));

        account.MapPost("/tenant", async (CreateTenantRequest body, HttpContext ctx, AccountService accounts, AccountsDbContext db, CancellationToken ct) =>
        {
            var user = await db.Users.SingleAsync(u => u.Id == CurrentUser(ctx).Id, ct);
            var error = await accounts.CreateTenantAsync(user, body.Handle ?? "", body.Locale, body.Name, ct);
            return error == AccountService.HandleError.None
                ? Results.Ok(new { tenantId = user.TenantId })
                : Results.BadRequest(new { error = HandleErrorCode(error) });
        });

        account.MapPatch("/", async (UpdateAccountRequest body, HttpContext ctx, AccountsDbContext db, TenantStore tenants,
            TenantOwners owners, CancellationToken ct) =>
        {
            var user = await db.Users.SingleAsync(u => u.Id == CurrentUser(ctx).Id, ct);
            if (body.Name is { } name) user.Name = name.Trim()[..Math.Min(name.Trim().Length, 120)];
            if (body.NotifyOnOpen is { } notify) user.NotifyOnOpen = notify;
            await db.SaveChangesAsync(ct);
            if (body.HideCredit is { } hide && user.TenantId is { } tenantId && tenants.Get(tenantId) is { } tenant)
            {
                if (hide && !owners.For(tenantId).CanHideCredit)
                    return PlanLimit("hideCredit", null);
                await TenantFiles.PatchTenantJsonAsync(tenant, json => json["hideCredit"] = hide, ct);
                tenants.Invalidate();
            }
            return Results.NoContent();
        });

        // LinkedIn data export (§7): returns the generated CV; ?apply=true writes it as cv.<locale>.json.
        account.MapPost("/import/linkedin", async (HttpContext ctx, string? locale, bool? apply, TenantStore tenants,
            CancellationToken ct) =>
        {
            var user = CurrentUser(ctx);
            if (user.TenantId is null || tenants.Get(user.TenantId) is not { } tenant) return Results.BadRequest(new { error = "no_tenant" });
            if (!ctx.Request.HasFormContentType) return Results.BadRequest(new { error = "multipart_required" });
            if (ctx.Request.ContentLength > MaxImportBytes) return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            var form = await ctx.Request.ReadFormAsync(ct);
            if (form.Files.FirstOrDefault() is not { } file) return Results.BadRequest(new { error = "file_missing" });
            if (file.Length > MaxImportBytes) return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);

            LinkedInImportResult result;
            try
            {
                await using var stream = file.OpenReadStream();
                using var buffer = new MemoryStream();
                await stream.CopyToAsync(buffer, ct);
                buffer.Position = 0;
                result = LinkedInImporter.Import(buffer);
            }
            catch (LinkedInImportException ex)
            {
                return Results.BadRequest(new { error = "import_failed", message = ex.Message });
            }

            var targetLocale = locale is { Length: 2 } l && l.All(char.IsAsciiLetterLower) ? l : tenant.Config.DefaultLocale;
            if (apply == true)
            {
                await File.WriteAllTextAsync(Path.Combine(tenant.Directory, $"cv.{targetLocale}.json"),
                    result.Cv.ToJsonString(AccountService.Indented), ct);
                tenants.Invalidate();
            }
            return Results.Ok(new
            {
                applied = apply == true,
                locale = targetLocale,
                cv = result.Cv,
                warnings = result.Warnings,
                filesRead = result.FilesRead,
                counts = new
                {
                    experiences = result.Experiences, studies = result.Studies, skills = result.Skills,
                    languages = result.Languages, projects = result.Projects, certifications = result.Certifications,
                },
            });
        }).DisableAntiforgery();

        // Own domain (§5.4, Pro only).
        account.MapPut("/domain", async (DomainRequest body, HttpContext ctx, AccountsDbContext db, TenantStore tenants,
            TenantOwners owners, IConfiguration config, CancellationToken ct) =>
        {
            var user = await db.Users.SingleAsync(u => u.Id == CurrentUser(ctx).Id, ct);
            if (user.TenantId is null || tenants.Get(user.TenantId) is not { } tenant) return Results.BadRequest(new { error = "no_tenant" });
            if (!owners.For(tenant.Id).CustomDomain) return PlanLimit("customDomain", null);
            var domain = TenantStore.NormalizeHost(body.Domain ?? "");
            if (!IsValidDomain(domain)) return Results.BadRequest(new { error = "invalid_domain" });

            var shared = SharedHost(config);
            if (domain == shared || tenants.FindByHost(domain) is { } other && other.Id != tenant.Id
                || (config["Saas:TenantHostSuffix"] is { Length: > 0 } suffix && domain.EndsWith("." + suffix.Trim('.'), StringComparison.Ordinal)))
                return Results.BadRequest(new { error = "domain_taken" });

            var target = config["Saas:DomainTarget"] is { Length: > 0 } t ? t : shared;
            if (target is not null && !await PointsToAsync(domain, target, ct))
                return Results.BadRequest(new { error = "dns_mismatch", target });

            var previous = user.CustomDomain;
            user.CustomDomain = domain;
            await db.SaveChangesAsync(ct);
            await TenantFiles.PatchTenantJsonAsync(tenant, json =>
            {
                var hosts = json["hosts"] as JsonArray ?? [];
                foreach (var h in hosts.Where(h => h?.GetValue<string>() is { } v && (v == previous || v == domain)).ToList()) hosts.Remove(h);
                hosts.Add(domain);
                json["hosts"] = hosts;
            }, ct);
            tenants.Invalidate();
            owners.Invalidate();
            return Results.Ok(new { domain });
        });

        account.MapDelete("/domain", async (HttpContext ctx, AccountsDbContext db, TenantStore tenants, TenantOwners owners, CancellationToken ct) =>
        {
            var user = await db.Users.SingleAsync(u => u.Id == CurrentUser(ctx).Id, ct);
            if (user.CustomDomain is not { } domain) return Results.NoContent();
            user.CustomDomain = null;
            await db.SaveChangesAsync(ct);
            if (user.TenantId is { } tenantId && tenants.Get(tenantId) is { } tenant)
                await TenantFiles.PatchTenantJsonAsync(tenant, json =>
                {
                    if (json["hosts"] is JsonArray hosts)
                        foreach (var h in hosts.Where(h => h?.GetValue<string>() == domain).ToList()) hosts.Remove(h);
                }, ct);
            tenants.Invalidate();
            owners.Invalidate();
            return Results.NoContent();
        });

        account.MapGet("/payments", async (HttpContext ctx, AccountsDbContext db, CancellationToken ct) =>
        {
            var userId = CurrentUser(ctx).Id;
            var payments = await db.Payments.AsNoTracking().Where(p => p.UserId == userId).ToListAsync(ct);
            return Results.Ok(payments.OrderByDescending(p => p.CreatedAt).Select(p => new
            {
                p.Id, p.Provider, p.Pass, p.Days, p.Amount, p.Currency, p.Status, p.CreatedAt,
            }));
        });

        account.MapGet("/export", async (HttpContext ctx, AccountService accounts, CancellationToken ct) =>
        {
            var user = CurrentUser(ctx);
            return Results.File(await accounts.ExportAsync(user, ct), "application/zip", $"account-{user.TenantId ?? "export"}.zip");
        });

        // "Sign out everywhere": a new stamp ends all sessions; this one is renewed.
        account.MapPost("/sessions/revoke", async (HttpContext ctx, AccountsDbContext db, CancellationToken ct) =>
        {
            var user = await db.Users.SingleAsync(u => u.Id == CurrentUser(ctx).Id, ct);
            user.SecurityStamp = User.NewStamp();
            await db.SaveChangesAsync(ct);
            await ctx.SignInAsync(AuthSetup.SessionScheme, AuthSetup.SessionPrincipal(user));
            return Results.NoContent();
        });

        account.MapDelete("/", async ([Microsoft.AspNetCore.Mvc.FromBody] DeleteRequest body, HttpContext ctx, AccountService accounts, CancellationToken ct) =>
        {
            var user = CurrentUser(ctx);
            if (!string.Equals(body.Confirm?.Trim(), user.TenantId ?? user.Email, StringComparison.OrdinalIgnoreCase))
                return Results.BadRequest(new { error = "confirmation_mismatch" });
            await accounts.DeleteAsync(user, ct);
            await ctx.SignOutAsync(AuthSetup.SessionScheme);
            return Results.NoContent();
        });
    }

    public static object Describe(User user, IEnumerable<string> logins, TenantOwners owners, DateTimeOffset now)
    {
        var plans = owners.Plans;
        var pro = user.IsPro(now);
        return new
        {
            id = user.Id,
            email = user.Email,
            name = user.Name,
            avatarUrl = user.AvatarUrl,
            tenantId = user.TenantId,
            createdAt = user.CreatedAt,
            logins,
            customDomain = user.CustomDomain,
            notifyOnOpen = user.NotifyOnOpen != false,
            plan = new
            {
                name = pro ? "pro" : "free",
                proUntil = user.ProUntil,
                proForever = user.ProForever,
                limits = new
                {
                    activeInvites = pro ? (int?)null : plans.FreeMaxActiveInvites,
                    assetBytes = (long)(pro ? plans.QuotaProMb : plans.QuotaFreeMb) * 1024 * 1024,
                    hideCredit = pro,
                    heatmaps = pro,
                    customDomain = pro,
                },
            },
        };
    }

    public static IResult PlanLimit(string feature, int? limit) =>
        Results.Json(new { error = "plan_limit", feature, limit }, statusCode: StatusCodes.Status402PaymentRequired);

    private static User CurrentUser(HttpContext ctx) => (User)ctx.Items[typeof(User)]!;

    /// <summary>Signed-in user required; state-changing requests need the CSRF header (§2 S2.5).</summary>
    private static async ValueTask<object?> RequireUser(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        http.Response.Headers.CacheControl = "private, no-store";
        if (await AuthSetup.CurrentUserAsync(http) is null) return Results.Unauthorized();
        if (!Csrf.IsSafe(http)) return Results.BadRequest(new { error = "csrf" });
        return await next(context);
    }

    private static string? HandleErrorCode(AccountService.HandleError error) => error switch
    {
        AccountService.HandleError.None => null,
        AccountService.HandleError.Invalid => "invalid_handle",
        AccountService.HandleError.Reserved => "handle_reserved",
        AccountService.HandleError.Taken => "handle_taken",
        AccountService.HandleError.HasTenant => "has_tenant",
        _ => "invalid_handle",
    };

    internal static bool IsValidDomain(string domain) =>
        domain.Length is >= 4 and <= 253 && domain.Contains('.') && !domain.StartsWith('.') && !domain.EndsWith('.')
        && domain.Split('.').All(label => label.Length is >= 1 and <= 63 && !label.StartsWith('-') && !label.EndsWith('-')
                                          && label.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '-'))
        && !IPAddress.TryParse(domain, out _);

    private static string? SharedHost(IConfiguration config) =>
        Uri.TryCreate(config["Cv:SharedBaseUrl"], UriKind.Absolute, out var uri) ? uri.Host.ToLowerInvariant() : null;

    /// <summary>The domain resolves to (some of) the addresses of the target host.</summary>
    private static async Task<bool> PointsToAsync(string domain, string target, CancellationToken ct)
    {
        try
        {
            var mine = await Dns.GetHostAddressesAsync(domain, ct);
            var expected = await Dns.GetHostAddressesAsync(target, ct);
            return mine.Length > 0 && mine.Any(a => expected.Contains(a));
        }
        catch (System.Net.Sockets.SocketException)
        {
            return false;
        }
    }
}

/// <summary>CSRF guard for cookie-authenticated requests (§2 S2.5).</summary>
public static class Csrf
{
    public const string HeaderName = "X-Requested-With";

    public static bool IsSafe(HttpContext http) =>
        HttpMethods.IsGet(http.Request.Method) || HttpMethods.IsHead(http.Request.Method) || HttpMethods.IsOptions(http.Request.Method)
        || http.Request.Headers.ContainsKey(HeaderName);
}
