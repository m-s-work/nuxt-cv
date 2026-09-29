using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;

namespace CvApi.Tracking;

public sealed record StoredConsent(string Choice, string PolicyVersion, DateTimeOffset GivenAt);

/// <summary>
/// The two tracking cookies (both HttpOnly, signed with data protection):
/// <c>cv_consent</c> remembers the visitor's choice for this browser (R9.19), <c>cv_vid</c> identifies the
/// browser after it accepted (R3.1). Neither is set before the visitor decided.
/// </summary>
public sealed class ConsentCookies(IDataProtectionProvider dataProtection, TimeProvider time)
{
    public const string ConsentCookie = "cv_consent";
    public const string VisitorCookie = "cv_vid";

    /// <summary>An accepted consent is valid for 13 months from when it was given (R9.13); a decline is remembered for 6.</summary>
    public static readonly TimeSpan AcceptLifetime = TimeSpan.FromDays(396);
    public static readonly TimeSpan DeclineLifetime = TimeSpan.FromDays(183);

    private readonly IDataProtector _consent = dataProtection.CreateProtector("CvApi.Consent.v1");
    private readonly IDataProtector _visitor = dataProtection.CreateProtector("CvApi.Visitor.v1");

    public StoredConsent? Read(HttpContext context)
    {
        if (!context.Request.Cookies.TryGetValue(ConsentCookie, out var raw) || string.IsNullOrEmpty(raw)) return null;
        try
        {
            var parts = _consent.Unprotect(raw).Split('|');
            if (parts.Length != 3 || !long.TryParse(parts[2], out var unix)) return null;
            var consent = new StoredConsent(parts[0], parts[1], DateTimeOffset.FromUnixTimeSeconds(unix));
            var lifetime = consent.Choice == "accept" ? AcceptLifetime : DeclineLifetime;
            return time.GetUtcNow() - consent.GivenAt < lifetime ? consent : null;
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    /// <summary>"accept" / "decline" for the current policy version, or null = ask (again).</summary>
    public string? State(HttpContext context, string policyVersion) =>
        Read(context) is { } c && c.PolicyVersion == policyVersion ? c.Choice : null;

    public void Write(HttpContext context, string choice, string policyVersion)
    {
        var now = time.GetUtcNow();
        var value = _consent.Protect($"{choice}|{policyVersion}|{now.ToUnixTimeSeconds()}");
        context.Response.Cookies.Append(ConsentCookie, value, Options(context, choice == "accept" ? AcceptLifetime : DeclineLifetime));
    }

    /// <summary>Browser id from cv_vid, or null.</summary>
    public Guid? ReadVisitorKey(HttpContext context)
    {
        if (!context.Request.Cookies.TryGetValue(VisitorCookie, out var raw) || string.IsNullOrEmpty(raw)) return null;
        try { return Guid.ParseExact(_visitor.Unprotect(raw), "N"); }
        catch (Exception ex) when (ex is CryptographicException or FormatException) { return null; }
    }

    /// <summary>Sets (or refreshes the lifetime of) cv_vid; only call with accepted consent.</summary>
    public Guid EnsureVisitorKey(HttpContext context)
    {
        var key = ReadVisitorKey(context) ?? Guid.NewGuid();
        context.Response.Cookies.Append(VisitorCookie, _visitor.Protect(key.ToString("N")), Options(context, AcceptLifetime));
        return key;
    }

    public static void ClearVisitorKey(HttpContext context) =>
        context.Response.Cookies.Delete(VisitorCookie, new CookieOptions { Path = "/" });

    /// <summary>Visitor row id: the browser key combined with the tenant, so tenants never share visitor ids.</summary>
    public static Guid VisitorId(Guid browserKey, string tenantId) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes($"{browserKey:N}|{tenantId}")).AsSpan(0, 16));

    private static CookieOptions Options(HttpContext context, TimeSpan maxAge) => new()
    {
        HttpOnly = true,
        Secure = context.Request.IsHttps,
        SameSite = SameSiteMode.Lax,
        Path = "/",
        MaxAge = maxAge,
    };
}
