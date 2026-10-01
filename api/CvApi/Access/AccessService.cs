using System.Text.Json;
using CvApi.Redaction;
using CvApi.Tenants;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace CvApi.Access;

/// <summary>What the current visitor may see: a tenant, a profile and the merged redaction policy.</summary>
/// <param name="ViaRenderTicket">Opened by the PDF renderer (never tracked).</param>
public sealed record AccessGrant(Tenant Tenant, string ProfileName, EffectivePolicy Policy, Invite? Invite, TemplateSelection Templates,
    bool ViaRenderTicket = false);

public enum RedeemResult { Ok, Invalid }

/// <summary>Short-lived, signed permission for the PDF renderer to open exactly one grant's view.</summary>
/// <param name="Revision">CV version of a profile grant ("" = current CV, null = the profile's own pin); invites carry their own.</param>
public sealed record RenderTicket(string TenantId, string Profile, Guid? InviteId, string? PdfTemplate = null,
    Dictionary<string, JsonElement>? PdfVars = null, string? Revision = null);

/// <summary>
/// Resolves tenant + profile from hostname and access cookie (see docs/REQUIREMENTS_ACCESS_AND_TENANCY.md §2–§4).
/// </summary>
public sealed class AccessService(
    TenantStore tenants,
    AppDbContext db,
    IDataProtectionProvider dataProtection,
    TimeProvider time,
    Accounts.TenantOwners owners)
{
    public const string CookieName = "cv_access";
    public const string RenderCookieName = "cv_render";
    private static readonly TimeSpan RenderTicketLifetime = TimeSpan.FromMinutes(2);

    private readonly IDataProtector _protector = dataProtection.CreateProtector("CvApi.AccessCookie.v1");
    // Purpose name predates storing codes of all invites; kept so existing codes stay readable.
    private readonly IDataProtector _codeProtector = dataProtection.CreateProtector("CvApi.DerivedInviteCode.v1");
    private readonly ITimeLimitedDataProtector _renderProtector =
        dataProtection.CreateProtector("CvApi.RenderTicket.v1").ToTimeLimitedDataProtector();

    /// <summary>Resolves access for the request, re-validating the invite on every call. Null = no access.</summary>
    public async Task<AccessGrant?> ResolveAsync(HttpContext context, CancellationToken ct)
    {
        // The PDF renderer calls from inside the container network (host "web"), with a render ticket.
        if (await ReadRenderTicketAsync(context, ct) is { } rendered) return rendered with { ViaRenderTicket = true };

        var hostTenant = tenants.FindByHost(context.Request.Host.Host);
        var now = time.GetUtcNow();

        var invite = await ReadCookieInviteAsync(context, ct);
        if (invite is not null && await IsActiveAsync(invite, now, ct) && (hostTenant is null || hostTenant.Id == invite.TenantId)
            && GrantFor(invite) is { } inviteGrant)
            return inviteGrant;

        // Public access only on the tenant's own hosts and only if explicitly configured.
        if (hostTenant?.Config.PublicProfile is { } publicName &&
            hostTenant.Config.Profiles.TryGetValue(publicName, out var publicProfile))
            return new AccessGrant(hostTenant, publicName, hostTenant.PolicyFor(publicName, publicProfile), null,
                TemplateResolver.Resolve(hostTenant.Config, publicProfile, null));

        return null;
    }

    public async Task<RedeemResult> RedeemAsync(HttpContext context, string? code, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > 128) return RedeemResult.Invalid;

        var hash = InviteCodes.Hash(code);
        var invite = await db.Invites.SingleOrDefaultAsync(i => i.CodeHash == hash, ct);
        var now = time.GetUtcNow();
        if (invite is null) return RedeemResult.Invalid;

        // A used view-once code is gone, except for the browser that redeemed it (e.g. opening the link again
        // within the grace window). The cookie stays as it is.
        if (invite.IsViewOnce && invite.UseCount > 0)
            return await ReadCookieInviteAsync(context, ct) is { } own && own.Id == invite.Id && await IsActiveAsync(invite, now, ct)
                ? RedeemResult.Ok
                : RedeemResult.Invalid;

        if (!invite.CanRedeem(now) || !await IsActiveAsync(invite, now, ct)) return RedeemResult.Invalid;

        var tenant = tenants.Get(invite.TenantId);
        if (tenant is null || !tenant.Config.Profiles.ContainsKey(invite.Profile) || owners.IsBlocked(tenant.Id)) return RedeemResult.Invalid;

        // On a tenant host, only that tenant's invites are accepted.
        var hostTenant = tenants.FindByHost(context.Request.Host.Host);
        if (hostTenant is not null && hostTenant.Id != invite.TenantId) return RedeemResult.Invalid;

        string? token = null;
        if (invite.ViewOnceMinutes is { } graceMinutes)
        {
            // Burn atomically: of two devices redeeming at the same moment, only one gets in. The token binds
            // the access to this browser's cookie, so a rearmed invite does not let earlier browsers back in.
            var until = now.AddMinutes(graceMinutes);
            token = InviteCodes.Generate();
            var burned = await db.Invites.Where(i => i.Id == invite.Id && i.UseCount == 0).ExecuteUpdateAsync(u => u
                .SetProperty(i => i.UseCount, 1)
                .SetProperty(i => i.LastUsedAt, now)
                .SetProperty(i => i.ViewOnceUntil, until)
                .SetProperty(i => i.ViewOnceToken, token), ct);
            if (burned == 0) return RedeemResult.Invalid;
            invite.UseCount = 1;
            invite.LastUsedAt = now;
            invite.ViewOnceUntil = until;
            invite.ViewOnceToken = token;
        }
        else
        {
            invite.UseCount++;
            invite.LastUsedAt = now;
            await db.SaveChangesAsync(ct);
        }

        // Expiry and view-once window are enforced on every request, not by the cookie lifetime, so the
        // admin can extend them later (§4).
        var payload = invite.Id.ToString("N") + (token is null ? "" : "." + token);
        context.Response.Cookies.Append(CookieName, _protector.Protect(payload), new CookieOptions
        {
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            MaxAge = TimeSpan.FromDays(365),
        });
        return RedeemResult.Ok;
    }

    /// <summary>An invite is active if it and (for derived invites) its parent are neither expired nor revoked.</summary>
    public async Task<bool> IsActiveAsync(Invite invite, DateTimeOffset now, CancellationToken ct)
    {
        if (!invite.IsActive(now)) return false;
        if (invite.ParentId is not { } parentId) return true;
        var parent = await db.Invites.SingleOrDefaultAsync(i => i.Id == parentId, ct);
        return parent is not null && parent.IsActive(now);
    }

    /// <summary>
    /// Code for the QR code printed into an invite's PDF: a linked invite with the same view, expiry and
    /// revocation as its parent, marked <see cref="InviteSources.PdfQr"/> so the owner can tell scans of
    /// the printed PDF apart. Created on first use and reused for every re-render.
    /// </summary>
    public async Task<string> GetOrCreateQrCodeAsync(Invite invite, CancellationToken ct)
    {
        if (invite.Source == InviteSources.PdfQr && invite.CodeProtected is { } own)
            return _codeProtector.Unprotect(own);

        var child = await db.Invites.FirstOrDefaultAsync(i => i.ParentId == invite.Id && i.Source == InviteSources.PdfQr && i.RevokedAt == null, ct);
        if (child?.CodeProtected is { } existing)
        {
            child.ExpiresAt = invite.ExpiresAt;       // follow the parent if it was changed
            child.Label = invite.Label;
            await db.SaveChangesAsync(ct);
            return _codeProtector.Unprotect(existing);
        }

        var code = InviteCodes.Generate();
        db.Invites.Add(new Invite
        {
            TenantId = invite.TenantId,
            Profile = invite.Profile,
            CodeHash = InviteCodes.Hash(code),
            CodeProtected = _codeProtector.Protect(code),
            Label = invite.Label,
            OverridesJson = invite.OverridesJson,
            CreatedAt = time.GetUtcNow(),
            ExpiresAt = invite.ExpiresAt,
            ParentId = invite.Id,
            Source = InviteSources.PdfQr,
        });
        await db.SaveChangesAsync(ct);
        return code;
    }

    /// <summary>Encrypts a plain invite code for storage (<see cref="Invite.CodeProtected"/>).</summary>
    public string ProtectCode(string code) => _codeProtector.Protect(code);

    /// <summary>Plain code of an invite for the admin, or null if it was not stored (or the keys changed).</summary>
    public string? RevealCode(Invite invite)
    {
        if (invite.CodeProtected is not { } protectedCode) return null;
        try { return _codeProtector.Unprotect(protectedCode); }
        catch (System.Security.Cryptography.CryptographicException) { return null; }
    }

    /// <summary>Grant of an invite (ignores expiry/revocation; callers check <see cref="IsActiveAsync"/>).</summary>
    public AccessGrant? GrantFor(Invite invite)
    {
        var tenant = tenants.Get(invite.TenantId);
        // CVs of blocked users are not shown to anyone (SaaS §6 S6.3).
        if (tenant is null || !tenant.Config.Profiles.TryGetValue(invite.Profile, out var profile) || owners.IsBlocked(tenant.Id)) return null;
        var overrides = ParseOverrides(invite.OverridesJson);
        return new AccessGrant(tenant, invite.Profile, tenant.PolicyFor(invite.Profile, profile, overrides), invite,
            TemplateResolver.Resolve(tenant.Config, profile, overrides));
    }

    /// <summary>Grant of a profile without invite (public profile, admin preview).</summary>
    /// <param name="revision">CV version: SHA/tag, "" = current CV, null = the profile's own pin (§14).</param>
    public static AccessGrant? GrantForProfile(Tenant tenant, string profileName, string? pdfTemplate = null,
        IReadOnlyDictionary<string, JsonElement>? pdfVars = null, string? revision = null) =>
        tenant.Config.Profiles.TryGetValue(profileName, out var profile)
            ? new AccessGrant(tenant, profileName,
                tenant.PolicyFor(profileName, profile, revision is null ? null : new AccessPolicy { Revision = revision }), null,
                TemplateResolver.Resolve(tenant.Config, profile, null, pdfTemplate, pdfVars))
            : null;

    public string CreateRenderTicket(AccessGrant grant) => _renderProtector.Protect(
        JsonSerializer.Serialize(new RenderTicket(grant.Tenant.Id, grant.ProfileName, grant.Invite?.Id, grant.Templates.Pdf, grant.Templates.PdfVars,
            grant.Invite is null ? grant.Policy.Revision ?? "" : null)),
        RenderTicketLifetime);

    private async Task<AccessGrant?> ReadRenderTicketAsync(HttpContext context, CancellationToken ct)
    {
        if (!context.Request.Cookies.TryGetValue(RenderCookieName, out var raw) || string.IsNullOrEmpty(raw)) return null;
        RenderTicket? ticket;
        try
        {
            ticket = JsonSerializer.Deserialize<RenderTicket>(_renderProtector.Unprotect(raw));
        }
        catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or JsonException)
        {
            return null;
        }
        if (ticket is null) return null;

        if (ticket.InviteId is { } inviteId)
        {
            var invite = await db.Invites.SingleOrDefaultAsync(i => i.Id == inviteId, ct);
            if (invite is null || !await IsActiveAsync(invite, time.GetUtcNow(), ct) || GrantFor(invite) is not { } g) return null;
            return g with { Templates = new TemplateSelection { Pdf = ticket.PdfTemplate ?? g.Templates.Pdf, Html = g.Templates.Html, PdfVars = ticket.PdfVars ?? g.Templates.PdfVars } };
        }

        var tenant = tenants.Get(ticket.TenantId);
        return tenant is null ? null : GrantForProfile(tenant, ticket.Profile, ticket.PdfTemplate, ticket.PdfVars, ticket.Revision);
    }

    public static void ClearCookie(HttpContext context) =>
        context.Response.Cookies.Delete(CookieName, new CookieOptions { Path = "/" });

    public static AccessPolicy? ParseOverrides(string? json) =>
        string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<AccessPolicy>(json, TenantStore.FileJsonOptions);

    /// <summary>
    /// Invite of the access cookie ("&lt;id&gt;" or "&lt;id&gt;.&lt;view-once token&gt;"). A view-once invite is only
    /// returned for the cookie of its current redemption.
    /// </summary>
    private async Task<Invite?> ReadCookieInviteAsync(HttpContext context, CancellationToken ct)
    {
        if (!context.Request.Cookies.TryGetValue(CookieName, out var raw) || string.IsNullOrEmpty(raw)) return null;
        try
        {
            var parts = _protector.Unprotect(raw).Split('.', 2);
            var id = Guid.ParseExact(parts[0], "N");
            var invite = await db.Invites.SingleOrDefaultAsync(i => i.Id == id, ct);
            if (invite is { IsViewOnce: true } && (invite.ViewOnceToken is null || parts.ElementAtOrDefault(1) != invite.ViewOnceToken))
                return null;
            return invite;
        }
        catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or FormatException)
        {
            return null;
        }
    }
}
