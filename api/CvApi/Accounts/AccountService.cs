using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CvApi.Access;
using CvApi.Tenants;
using CvApi.Tracking;
using Microsoft.EntityFrameworkCore;

namespace CvApi.Accounts;

public enum SignInError { None, AccountExists, Blocked, NoEmail }

/// <summary>Sign-in linking, onboarding, export and deletion of accounts (docs/REQUIREMENTS_SAAS.md §2, §3, §8).</summary>
public sealed partial class AccountService(
    AccountsDbContext db,
    AppDbContext appDb,
    TrackingDbContext trackingDb,
    TenantStore tenants,
    TenantOwners owners,
    IConfiguration config,
    TimeProvider time,
    ILogger<AccountService> logger)
{
    /// <summary>Providers whose e-mail address is verified, so a new login may join an account with that address (S2.3).</summary>
    private static readonly HashSet<string> VerifiedEmailProviders = ["google", "github", "linkedin", "email"];

    private static readonly HashSet<string> ReservedHandles =
    [
        "admin", "api", "www", "cv", "app", "demo", "mail", "email", "help", "support", "status", "login", "logout",
        "account", "billing", "pricing", "legal", "static", "assets", "cdn", "docs", "blog", "about", "root", "system",
        "test", "dev", "staging", "web", "pdf", "geo", "smtp", "ftp", "ns1", "ns2", "imprint", "privacy", "terms",
    ];

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{2,30}$")]
    private static partial Regex HandleRegex();

    [GeneratedRegex("^[a-z]{2}$")]
    private static partial Regex LocaleRegex();

    private string DataPath => Path.GetFullPath(config["Cv:DataPath"] ?? "/data");

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public static bool IsValidEmail(string? email) =>
        email is { Length: >= 3 and <= 254 } e && e.IndexOf('@') is > 0 and var at && at < e.Length - 1 && !e.Any(char.IsWhiteSpace)
        && e.IndexOf('.', at) > at + 1;

    /// <summary>Finds or creates the user of an external login (S2.3).</summary>
    public async Task<(User? User, SignInError Error)> SignInAsync(string provider, string subject, string? email, string? name,
        string? avatarUrl, bool emailVerified, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var login = await db.Logins.SingleOrDefaultAsync(l => l.Provider == provider && l.Subject == subject, ct);
        User? user = null;
        if (login is not null)
        {
            user = await db.Users.SingleOrDefaultAsync(u => u.Id == login.UserId, ct);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(email) || !IsValidEmail(email)) return (null, SignInError.NoEmail);
            var normalized = NormalizeEmail(email);
            var existing = await db.Users.SingleOrDefaultAsync(u => u.Email == normalized, ct);
            if (existing is not null)
            {
                if (!VerifiedEmailProviders.Contains(provider) || !emailVerified || existing.EmailVerified == false)
                    return (null, SignInError.AccountExists);
                user = existing;
            }
            else
            {
                user = new User
                {
                    Email = normalized, Name = (name ?? "").Trim(), AvatarUrl = avatarUrl, CreatedAt = now,
                    EmailVerified = VerifiedEmailProviders.Contains(provider) && emailVerified ? null : false,
                };
                db.Users.Add(user);
            }
            user.Logins.Add(new ExternalLogin { Provider = provider, Subject = subject, CreatedAt = now });
        }

        if (user is null) return (null, SignInError.NoEmail);
        if (user.BlockedAt is not null) return (null, SignInError.Blocked);
        if (string.IsNullOrWhiteSpace(user.Name) && !string.IsNullOrWhiteSpace(name)) user.Name = name.Trim();
        if (user.AvatarUrl is null && avatarUrl is { Length: <= 2048 } && avatarUrl.StartsWith("https://", StringComparison.Ordinal))
            user.AvatarUrl = avatarUrl;
        user.LastLoginAt = now;
        await db.SaveChangesAsync(ct);
        return (user, SignInError.None);
    }

    public enum HandleError { None, Invalid, Reserved, Taken, HasTenant }

    public HandleError CheckHandle(string handle)
    {
        if (!HandleRegex().IsMatch(handle) || handle.EndsWith('-') || handle.Contains("--")) return HandleError.Invalid;
        if (ReservedHandles.Contains(handle)) return HandleError.Reserved;
        if (tenants.Get(handle) is not null || Directory.Exists(Path.Combine(DataPath, "tenants", handle))) return HandleError.Taken;
        return HandleError.None;
    }

    /// <summary>Creates the user's tenant with starter profiles and CV (§3 S3.1).</summary>
    public async Task<HandleError> CreateTenantAsync(User user, string handle, string? locale, string? name, CancellationToken ct)
    {
        if (user.TenantId is not null) return HandleError.HasTenant;
        handle = handle.Trim().ToLowerInvariant();
        if (CheckHandle(handle) is var error && error != HandleError.None) return error;
        if (await db.Users.AnyAsync(u => u.TenantId == handle, ct)) return HandleError.Taken;
        locale = locale is not null && LocaleRegex().IsMatch(locale) ? locale : "en";
        var displayName = string.IsNullOrWhiteSpace(name) ? (string.IsNullOrWhiteSpace(user.Name) ? handle : user.Name) : name.Trim();

        var dir = Path.Combine(DataPath, "tenants", handle);
        Directory.CreateDirectory(Path.Combine(dir, "assets"));
        var suffix = config["Saas:TenantHostSuffix"]?.Trim().Trim('.');
        var tenantJson = new JsonObject
        {
            ["name"] = displayName,
            ["hosts"] = string.IsNullOrEmpty(suffix) ? new JsonArray() : new JsonArray($"{handle}.{suffix}"),
            ["defaultLocale"] = locale,
            ["privacy"] = new JsonObject { ["controller"] = displayName, ["contact"] = user.Email },
            ["tracking"] = new JsonObject { ["enabled"] = true },
            ["profiles"] = new JsonObject
            {
                ["full"] = new JsonObject { ["grants"] = new JsonArray("contact") },
                ["recruiter"] = new JsonObject
                {
                    ["grants"] = new JsonArray("contact"),
                    ["flags"] = new JsonObject { ["hideBirthDate"] = true, ["hideTimeframeDays"] = true },
                },
                ["public"] = new JsonObject
                {
                    ["flags"] = new JsonObject { ["hideContactDetails"] = true, ["hidePhoto"] = false },
                },
            },
        };
        await File.WriteAllTextAsync(Path.Combine(dir, "tenant.json"), tenantJson.ToJsonString(Indented), ct);
        await File.WriteAllTextAsync(Path.Combine(dir, $"cv.{locale}.json"), StarterCv(displayName, user.Email).ToJsonString(Indented), ct);

        user.TenantId = handle;
        if (string.IsNullOrWhiteSpace(user.Name)) user.Name = displayName;
        await db.SaveChangesAsync(ct);
        tenants.Invalidate();
        owners.Invalidate();
        logger.LogInformation("User {User} created tenant {Tenant}", user.Id, handle);
        return HandleError.None;
    }

    public static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    public static JsonObject StarterCv(string name, string email) => new()
    {
        ["profile"] = new JsonObject { ["name"] = name, ["title"] = "" },
        ["details"] = new JsonObject
        {
            ["email"] = email,
            // Contact details only for profiles granting "contact" (e.g. "full", "recruiter").
            ["fieldRequires"] = new JsonObject { ["email"] = new JsonArray("contact") },
        },
        ["intro"] = new JsonObject { ["text"] = "" },
        ["skills"] = new JsonObject { ["skilled"] = new JsonArray(), ["liked"] = new JsonArray() },
        ["languages"] = new JsonArray(),
        ["experiences"] = new JsonArray(),
        ["studies"] = new JsonArray(),
        ["projects"] = new JsonArray(),
        ["otherEntries"] = new JsonArray(),
    };

    /// <summary>
    /// Invites counted against the plan limit (S4): still giving access (not revoked, not expired, view-once window not
    /// over). Used-up codes count too, because their visitors keep access. QR invites belong to their parent.
    /// </summary>
    public static async Task<int> ActiveInviteCountAsync(AppDbContext appDb, string tenantId, DateTimeOffset now, CancellationToken ct)
    {
        var candidates = await appDb.Invites.AsNoTracking()
            .Where(i => i.TenantId == tenantId && i.RevokedAt == null && i.Source == null).ToListAsync(ct);
        return candidates.Count(i => i.IsActive(now));
    }

    /// <summary>Bytes used by the tenant's assets (S3.6).</summary>
    public static long AssetBytes(Tenant tenant)
    {
        var dir = Path.Combine(tenant.Directory, "assets");
        return Directory.Exists(dir) ? Directory.EnumerateFiles(dir).Sum(f => new FileInfo(f).Length) : 0;
    }

    /// <summary>GDPR export (S8.1): account data, payments and all tenant files as ZIP.</summary>
    public async Task<byte[]> ExportAsync(User user, CancellationToken ct)
    {
        var payments = await db.Payments.AsNoTracking().Where(p => p.UserId == user.Id).OrderBy(p => p.CreatedAt).ToListAsync(ct);
        var logins = await db.Logins.AsNoTracking().Where(l => l.UserId == user.Id).ToListAsync(ct);
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            var account = zip.CreateEntry("account.json");
            await using (var stream = account.Open())
                await JsonSerializer.SerializeAsync(stream, new
                {
                    user.Id, user.Email, user.Name, user.AvatarUrl, user.TenantId, user.CreatedAt, user.LastLoginAt,
                    user.ProUntil, user.ProForever, user.CustomDomain,
                    logins = logins.Select(l => new { l.Provider, l.CreatedAt }),
                    payments = payments.Select(p => new { p.Provider, p.ExternalId, p.Pass, p.Days, p.Amount, p.Currency, p.Status, p.CreatedAt }),
                }, Indented, ct);

            if (user.TenantId is { } tenantId && Path.Combine(DataPath, "tenants", tenantId) is var dir && Directory.Exists(dir))
            {
                foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                {
                    var relative = Path.GetRelativePath(dir, file).Replace('\\', '/');
                    if (relative.StartsWith("revisions/", StringComparison.Ordinal)) continue;
                    zip.CreateEntryFromFile(file, "cv/" + relative);
                }
            }
        }
        return buffer.ToArray();
    }

    /// <summary>Deletes the account and everything of its tenant (S8.2). Payments stay, anonymised.</summary>
    public async Task DeleteAsync(User user, CancellationToken ct)
    {
        if (user.TenantId is { } tenantId)
        {
            await appDb.Invites.Where(i => i.TenantId == tenantId).ExecuteDeleteAsync(ct);
            await DeleteTrackingAsync(tenantId, ct);
            foreach (var dir in new[] { Path.Combine(DataPath, "tenants", tenantId), Path.Combine(DataPath, "pdf", tenantId) })
                if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
        await db.Payments.Where(p => p.UserId == user.Id)
            .ExecuteUpdateAsync(u => u.SetProperty(p => p.UserId, (Guid?)null).SetProperty(p => p.Email, (string?)null), ct);
        await db.MagicLinks.Where(m => m.Email == user.Email).ExecuteDeleteAsync(ct);
        await db.Logins.Where(l => l.UserId == user.Id).ExecuteDeleteAsync(ct);
        await db.Users.Where(u => u.Id == user.Id).ExecuteDeleteAsync(ct);
        tenants.Invalidate();
        owners.Invalidate();
        logger.LogInformation("Deleted user {User} and tenant {Tenant}", user.Id, user.TenantId);
    }

    private async Task DeleteTrackingAsync(string tenantId, CancellationToken ct)
    {
        var sessionIds = trackingDb.Sessions.Where(s => s.TenantId == tenantId).Select(s => s.Id);
        await trackingDb.Events.Where(e => sessionIds.Contains(e.SessionId)).ExecuteDeleteAsync(ct);
        await trackingDb.SectionStats.Where(e => sessionIds.Contains(e.SessionId)).ExecuteDeleteAsync(ct);
        await trackingDb.SessionIps.Where(e => sessionIds.Contains(e.SessionId)).ExecuteDeleteAsync(ct);
        await trackingDb.Sessions.Where(s => s.TenantId == tenantId).ExecuteDeleteAsync(ct);
        await trackingDb.HeatCells.Where(c => c.TenantId == tenantId).ExecuteDeleteAsync(ct);
        await trackingDb.CvSnapshots.Where(c => c.TenantId == tenantId).ExecuteDeleteAsync(ct);
        await trackingDb.Consents.Where(c => c.TenantId == tenantId).ExecuteDeleteAsync(ct);
        await trackingDb.Visitors.Where(v => v.TenantId == tenantId).ExecuteDeleteAsync(ct);
        await trackingDb.Persons.Where(p => p.TenantId == tenantId).ExecuteDeleteAsync(ct);
    }
}
