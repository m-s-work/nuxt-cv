using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.EntityFrameworkCore;

namespace CvApi.Accounts;

/// <summary>
/// Account sign-in (docs/REQUIREMENTS_SAAS.md §2): a session cookie plus OAuth providers that are only registered
/// when configured. Providers sign in to a short-lived "external" cookie; /auth/complete turns it into a session.
/// </summary>
public static class AuthSetup
{
    public const string SessionScheme = "Session";
    public const string ExternalScheme = "External";
    public const string SessionCookieName = "cv_session";
    public const string UserIdClaim = "uid";
    public const string StampClaim = "stamp";
    public const string AvatarClaim = "avatar";
    public const string EmailVerifiedClaim = "email_verified";

    /// <summary>Provider name → authentication scheme. The provider name is what /auth/login/{provider} takes.</summary>
    public static readonly IReadOnlyDictionary<string, string> Providers = new Dictionary<string, string>
    {
        ["google"] = "Google",
        ["microsoft"] = "Microsoft",
        ["github"] = "GitHub",
        ["linkedin"] = "LinkedIn",
    };

    public static bool IsConfigured(IConfiguration config, string scheme) =>
        !string.IsNullOrWhiteSpace(config[$"Auth:{scheme}:ClientId"]) && !string.IsNullOrWhiteSpace(config[$"Auth:{scheme}:ClientSecret"]);

    public static void AddAccountAuthentication(this WebApplicationBuilder builder)
    {
        var config = builder.Configuration;
        var auth = builder.Services.AddAuthentication(SessionScheme)
            .AddCookie(SessionScheme, o =>
            {
                o.Cookie.Name = SessionCookieName;
                o.Cookie.HttpOnly = true;
                o.Cookie.SameSite = SameSiteMode.Lax;
                o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                o.Cookie.Path = "/";
                o.ExpireTimeSpan = TimeSpan.FromDays(30);
                o.SlidingExpiration = true;
                // API: answer with status codes instead of redirecting to a login page.
                o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
                o.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
                o.Events.OnValidatePrincipal = ValidateSessionAsync;
            })
            .AddCookie(ExternalScheme, o =>
            {
                o.Cookie.Name = "cv_external";
                o.Cookie.HttpOnly = true;
                o.Cookie.SameSite = SameSiteMode.Lax;
                o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                o.ExpireTimeSpan = TimeSpan.FromMinutes(10);
            });

        if (IsConfigured(config, "Google"))
            auth.AddGoogle("Google", o =>
            {
                Common(o, config, "Google", "/signin-google");
                o.ClaimActions.MapJsonKey(AvatarClaim, "picture");
                o.ClaimActions.MapJsonKey(EmailVerifiedClaim, "email_verified");
                o.ClaimActions.MapJsonKey(EmailVerifiedClaim, "verified_email");
            });
        if (IsConfigured(config, "Microsoft"))
            auth.AddMicrosoftAccount("Microsoft", o => Common(o, config, "Microsoft", "/signin-microsoft"));
        if (IsConfigured(config, "GitHub"))
            auth.AddGitHub("GitHub", o =>
            {
                Common(o, config, "GitHub", "/signin-github");
                o.Scope.Add("user:email");
                o.ClaimActions.MapJsonKey(AvatarClaim, "avatar_url");
            });
        if (IsConfigured(config, "LinkedIn"))
            auth.AddLinkedIn("LinkedIn", o =>
            {
                Common(o, config, "LinkedIn", "/signin-linkedin");
                o.ClaimActions.MapJsonKey(AvatarClaim, "picture");
                o.ClaimActions.MapJsonKey(EmailVerifiedClaim, "email_verified");
            });

        builder.Services.AddAuthorization();
    }

    private static void Common(OAuthOptions o, IConfiguration config, string scheme, string callbackPath)
    {
        o.ClientId = config[$"Auth:{scheme}:ClientId"]!;
        o.ClientSecret = config[$"Auth:{scheme}:ClientSecret"]!;
        o.CallbackPath = callbackPath;
        o.SignInScheme = ExternalScheme;
        o.SaveTokens = false;
        o.CorrelationCookie.SameSite = SameSiteMode.Lax;
        o.CorrelationCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        // The provider returned an error (user cancelled etc.): back to the login page with a message.
        o.Events.OnRemoteFailure = ctx =>
        {
            ctx.Response.Redirect("/login?error=" + Uri.EscapeDataString(ctx.Failure?.Message.Contains("access_denied") == true ? "cancelled" : "provider_failed"));
            ctx.HandleResponse();
            return Task.CompletedTask;
        };
    }

    /// <summary>Re-checks the session on every request: user exists, is not blocked, stamp unchanged (§2 S2.4).</summary>
    private static async Task ValidateSessionAsync(CookieValidatePrincipalContext ctx)
    {
        var id = ctx.Principal?.FindFirstValue(UserIdClaim);
        var stamp = ctx.Principal?.FindFirstValue(StampClaim);
        if (!Guid.TryParse(id, out var userId) || stamp is null)
        {
            ctx.RejectPrincipal();
            return;
        }
        var db = ctx.HttpContext.RequestServices.GetRequiredService<AccountsDbContext>();
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId, ctx.HttpContext.RequestAborted);
        if (user is null || user.BlockedAt is not null || user.SecurityStamp != stamp)
        {
            ctx.RejectPrincipal();
            await ctx.HttpContext.SignOutAsync(SessionScheme);
            return;
        }
        ctx.HttpContext.Items[typeof(User)] = user;
    }

    public static ClaimsPrincipal SessionPrincipal(User user) => new(new ClaimsIdentity(
        [new Claim(UserIdClaim, user.Id.ToString()), new Claim(StampClaim, user.SecurityStamp)], SessionScheme));

    /// <summary>The signed-in, validated user of this request (null without session).</summary>
    public static async Task<User?> CurrentUserAsync(HttpContext context)
    {
        if (context.Items.TryGetValue(typeof(User), out var cached)) return cached as User;
        var result = await context.AuthenticateAsync(SessionScheme);
        var user = result.Succeeded && context.Items.TryGetValue(typeof(User), out var validated) ? validated as User : null;
        context.Items[typeof(User)] = user;
        return user;
    }

    /// <summary>Only local paths ("/x", not "//x" or "/\x") are accepted as return URL (§2 S2.6).</summary>
    public static string SafeReturnUrl(string? returnUrl) =>
        returnUrl is { Length: > 0 } r && r[0] == '/' && (r.Length == 1 || (r[1] != '/' && r[1] != '\\')) && !r.Contains("://")
            ? r
            : "/admin";
}
