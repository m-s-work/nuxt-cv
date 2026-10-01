using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

namespace CvApi.Accounts;

/// <summary>Sign-in endpoints (docs/REQUIREMENTS_SAAS.md §2).</summary>
public static class AuthEndpoints
{
    public const string MagicLinkRateLimitPolicy = "magic-link";
    private static readonly TimeSpan MagicLinkLifetime = TimeSpan.FromMinutes(15);

    public sealed record MagicLinkRequest(string? Email, string? ReturnUrl);

    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/auth");

        auth.MapGet("/providers", (IConfiguration config, IEmailSender email) => Results.Ok(new
        {
            providers = AuthSetup.Providers.Where(p => AuthSetup.IsConfigured(config, p.Value)).Select(p => p.Key),
            magicLink = email.Enabled,
        }));

        auth.MapGet("/login/{provider}", (string provider, string? returnUrl, HttpContext ctx, IConfiguration config) =>
        {
            if (!AuthSetup.Providers.TryGetValue(provider, out var scheme) || !AuthSetup.IsConfigured(config, scheme))
                return Results.NotFound();
            var complete = $"{ctx.Request.PathBase}/auth/complete?provider={provider}&returnUrl={Uri.EscapeDataString(AuthSetup.SafeReturnUrl(returnUrl))}";
            return Results.Challenge(new AuthenticationProperties { RedirectUri = complete }, [scheme]);
        });

        // The provider handler signed in to the external cookie; turn it into an account session.
        auth.MapGet("/complete", async (string? provider, string? returnUrl, HttpContext ctx, AccountService accounts, CancellationToken ct) =>
        {
            var external = await ctx.AuthenticateAsync(AuthSetup.ExternalScheme);
            await ctx.SignOutAsync(AuthSetup.ExternalScheme);
            // The provider is taken from the authenticated identity, not from the query: a login of one provider must
            // never be stored under another provider's name.
            var scheme = external.Succeeded ? external.Principal?.Identity?.AuthenticationType : null;
            provider = AuthSetup.Providers.FirstOrDefault(x => x.Value == scheme).Key;
            if (provider is null) return Results.Redirect("/login?error=provider_failed");

            var p = external.Principal!;
            var subject = p.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(subject)) return Results.Redirect("/login?error=provider_failed");
            var verified = provider == "github" || string.Equals(p.FindFirstValue(AuthSetup.EmailVerifiedClaim), "true", StringComparison.OrdinalIgnoreCase);
            var (user, error) = await accounts.SignInAsync(provider, subject, p.FindFirstValue(ClaimTypes.Email),
                p.FindFirstValue(ClaimTypes.Name), p.FindFirstValue(AuthSetup.AvatarClaim), verified, ct);
            if (user is null) return Results.Redirect("/login?error=" + ErrorCode(error));

            await ctx.SignInAsync(AuthSetup.SessionScheme, AuthSetup.SessionPrincipal(user));
            return Results.Redirect(AuthSetup.SafeReturnUrl(returnUrl));
        });

        // Always 204, so the response does not tell whether an account exists (S2.2).
        auth.MapPost("/magic-link", async (MagicLinkRequest body, HttpContext ctx, AccountsDbContext db, IEmailSender email,
            TimeProvider time, ILoggerFactory loggers, IConfiguration config, CancellationToken ct) =>
        {
            if (!email.Enabled) return Results.NotFound();
            if (!AccountService.IsValidEmail(body.Email)) return Results.BadRequest(new { error = "invalid_email" });
            var address = AccountService.NormalizeEmail(body.Email!);
            var now = time.GetUtcNow();

            // At most 3 links per address and 15 minutes.
            var recent = await db.MagicLinks.CountAsync(m => m.Email == address && m.CreatedAt > now - MagicLinkLifetime, ct);
            if (recent >= 3) return Results.NoContent();

            var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            db.MagicLinks.Add(new MagicLinkToken
            {
                TokenHash = Hash(token),
                Email = address,
                ReturnUrl = AuthSetup.SafeReturnUrl(body.ReturnUrl),
                CreatedAt = now,
                ExpiresAt = now + MagicLinkLifetime,
            });
            // Clean up old tokens on the way.
            await db.MagicLinks.Where(m => m.ExpiresAt < now - TimeSpan.FromDays(1)).ExecuteDeleteAsync(ct);
            await db.SaveChangesAsync(ct);

            // Never built from the Host header: a forged Host would send the victim a valid token for another site.
            var link = $"{MagicLinkBase(ctx, config)}/auth/magic?token={token}";
            var sent = await email.SendAsync(address, "Your sign-in link",
                $"Sign in with this link (valid for 15 minutes, can be used once):\n\n{link}\n\nIf you did not ask for it, ignore this e-mail.",
                $"<p>Sign in with this link (valid for 15 minutes, can be used once):</p><p><a href=\"{link}\">Sign in</a></p>" +
                "<p>If you did not ask for it, ignore this e-mail.</p>", ct);
            if (!sent) loggers.CreateLogger("Auth").LogWarning("Magic link could not be sent");
            return Results.NoContent();
        }).RequireRateLimiting(MagicLinkRateLimitPolicy);

        auth.MapGet("/magic", async (string? token, HttpContext ctx, AccountsDbContext db, AccountService accounts,
            TimeProvider time, CancellationToken ct) =>
        {
            if (string.IsNullOrEmpty(token) || token.Length > 128) return Results.Redirect("/login?error=link_invalid");
            var hash = Hash(token);
            var now = time.GetUtcNow();
            // Single use: mark as used atomically, so two clicks cannot both sign in.
            var used = await db.MagicLinks.Where(m => m.TokenHash == hash && m.UsedAt == null && m.ExpiresAt > now)
                .ExecuteUpdateAsync(u => u.SetProperty(m => m.UsedAt, now), ct);
            if (used == 0) return Results.Redirect("/login?error=link_invalid");
            var link = await db.MagicLinks.AsNoTracking().SingleAsync(m => m.TokenHash == hash, ct);

            var (user, error) = await accounts.SignInAsync("email", link.Email, link.Email, null, null, emailVerified: true, ct);
            if (user is null) return Results.Redirect("/login?error=" + ErrorCode(error));
            await ctx.SignInAsync(AuthSetup.SessionScheme, AuthSetup.SessionPrincipal(user));
            return Results.Redirect(AuthSetup.SafeReturnUrl(link.ReturnUrl));
        });

        auth.MapGet("/me", async (HttpContext ctx, AccountsDbContext db, TenantOwners owners, TimeProvider time, CancellationToken ct) =>
        {
            ctx.Response.Headers.CacheControl = "private, no-store";
            if (await AuthSetup.CurrentUserAsync(ctx) is not { } user) return Results.Unauthorized();
            var logins = await db.Logins.AsNoTracking().Where(l => l.UserId == user.Id).Select(l => l.Provider).ToListAsync(ct);
            return Results.Ok(AccountEndpoints.Describe(user, logins, owners, time.GetUtcNow()));
        });

        auth.MapPost("/logout", async (HttpContext ctx) =>
        {
            await ctx.SignOutAsync(AuthSetup.SessionScheme);
            return Results.NoContent();
        });
    }

    private static string ErrorCode(SignInError error) => error switch
    {
        SignInError.AccountExists => "account_exists",
        SignInError.Blocked => "blocked",
        SignInError.NoEmail => "no_email",
        _ => "provider_failed",
    };

    /// <summary>API base for e-mailed links: the shared base URL if configured (production), else the request (development).</summary>
    private static string MagicLinkBase(HttpContext ctx, IConfiguration config) =>
        config["Cv:SharedBaseUrl"] is { Length: > 0 } shared
            ? $"{shared.TrimEnd('/')}/api"
            : $"{ctx.Request.Scheme}://{ctx.Request.Host}{ctx.Request.PathBase}";

    private static string Hash(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
