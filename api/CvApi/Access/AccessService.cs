using System.Text.Json;
using CvApi.Redaction;
using CvApi.Tenants;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace CvApi.Access;

/// <summary>What the current visitor may see: a tenant, a profile and the merged redaction policy.</summary>
public sealed record AccessGrant(Tenant Tenant, string ProfileName, EffectivePolicy Policy, Invite? Invite);

public enum RedeemResult { Ok, Invalid }

/// <summary>
/// Resolves tenant + profile from hostname and access cookie (see docs/REQUIREMENTS_ACCESS_AND_TENANCY.md §2–§4).
/// </summary>
public sealed class AccessService(
    TenantStore tenants,
    AppDbContext db,
    IDataProtectionProvider dataProtection,
    TimeProvider time)
{
    public const string CookieName = "cv_access";
    private readonly IDataProtector _protector = dataProtection.CreateProtector("CvApi.AccessCookie.v1");

    /// <summary>Resolves access for the request, re-validating the invite on every call. Null = no access.</summary>
    public async Task<AccessGrant?> ResolveAsync(HttpContext context, CancellationToken ct)
    {
        var hostTenant = tenants.FindByHost(context.Request.Host.Host);
        var now = time.GetUtcNow();

        var invite = await ReadCookieInviteAsync(context, ct);
        if (invite is not null && invite.IsActive(now) && (hostTenant is null || hostTenant.Id == invite.TenantId))
        {
            var tenant = tenants.Get(invite.TenantId);
            if (tenant is not null && tenant.Config.Profiles.TryGetValue(invite.Profile, out var profile))
                return new AccessGrant(tenant, invite.Profile, EffectivePolicy.From(profile, ParseOverrides(invite.OverridesJson)), invite);
        }

        // Public access only on the tenant's own hosts and only if explicitly configured.
        if (hostTenant?.Config.PublicProfile is { } publicName &&
            hostTenant.Config.Profiles.TryGetValue(publicName, out var publicProfile))
            return new AccessGrant(hostTenant, publicName, EffectivePolicy.From(publicProfile), null);

        return null;
    }

    public async Task<RedeemResult> RedeemAsync(HttpContext context, string? code, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > 128) return RedeemResult.Invalid;

        var hash = InviteCodes.Hash(code);
        var invite = await db.Invites.SingleOrDefaultAsync(i => i.CodeHash == hash, ct);
        var now = time.GetUtcNow();
        if (invite is null || !invite.CanRedeem(now)) return RedeemResult.Invalid;

        var tenant = tenants.Get(invite.TenantId);
        if (tenant is null || !tenant.Config.Profiles.ContainsKey(invite.Profile)) return RedeemResult.Invalid;

        // On a tenant host, only that tenant's invites are accepted.
        var hostTenant = tenants.FindByHost(context.Request.Host.Host);
        if (hostTenant is not null && hostTenant.Id != invite.TenantId) return RedeemResult.Invalid;

        invite.UseCount++;
        invite.LastUsedAt = now;
        await db.SaveChangesAsync(ct);

        var maxAge = invite.ExpiresAt is { } exp ? exp - now : TimeSpan.FromDays(365);
        context.Response.Cookies.Append(CookieName, _protector.Protect(invite.Id.ToString("N")), new CookieOptions
        {
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            MaxAge = maxAge,
        });
        return RedeemResult.Ok;
    }

    public static void ClearCookie(HttpContext context) =>
        context.Response.Cookies.Delete(CookieName, new CookieOptions { Path = "/" });

    public static AccessPolicy? ParseOverrides(string? json) =>
        string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<AccessPolicy>(json, TenantStore.FileJsonOptions);

    private async Task<Invite?> ReadCookieInviteAsync(HttpContext context, CancellationToken ct)
    {
        if (!context.Request.Cookies.TryGetValue(CookieName, out var raw) || string.IsNullOrEmpty(raw)) return null;
        try
        {
            var id = Guid.ParseExact(_protector.Unprotect(raw), "N");
            return await db.Invites.SingleOrDefaultAsync(i => i.Id == id, ct);
        }
        catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or FormatException)
        {
            return null;
        }
    }
}
