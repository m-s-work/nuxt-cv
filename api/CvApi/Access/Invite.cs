using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace CvApi.Access;

public sealed class Invite
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string TenantId { get; set; }
    public required string Profile { get; set; }
    public required string CodeHash { get; set; }
    public string Label { get; set; } = "";

    /// <summary>Serialized <see cref="Tenants.AccessPolicy"/> applied on top of the profile.</summary>
    public string? OverridesJson { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public int? MaxUses { get; set; }
    public int UseCount { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }

    /// <summary>Set for invites derived from another invite, e.g. the one printed as QR code into its PDF.</summary>
    public Guid? ParentId { get; set; }

    /// <summary>Owner-only origin marker (e.g. <see cref="InviteSources.PdfQr"/>); never shown to invitees.</summary>
    public string? Source { get; set; }

    /// <summary>
    /// Plain code, encrypted with data protection, so the admin can show it again and re-rendered
    /// PDFs can embed QR codes. Null for invites created before codes were stored. Lookup uses <see cref="CodeHash"/>.
    /// </summary>
    public string? CodeProtected { get; set; }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && (ExpiresAt is null || ExpiresAt > now);

    public bool CanRedeem(DateTimeOffset now) => IsActive(now) && (MaxUses is null || UseCount < MaxUses);
}

public static class InviteSources
{
    /// <summary>Invite printed as QR code into the PDF of its parent invite.</summary>
    public const string PdfQr = "pdf-qr";
}

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Invite> Invites => Set<Invite>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var invite = modelBuilder.Entity<Invite>();
        invite.HasIndex(i => i.CodeHash).IsUnique();
        invite.HasIndex(i => i.TenantId);
        invite.HasIndex(i => i.ParentId);
        invite.Property(i => i.Source).HasMaxLength(32);
        invite.Property(i => i.TenantId).HasMaxLength(64);
        invite.Property(i => i.Profile).HasMaxLength(64);
        invite.Property(i => i.CodeHash).HasMaxLength(64);

        // SQLite cannot order/compare DateTimeOffset natively; store as UTC ticks.
        var converter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.DateTimeOffsetToBinaryConverter();
        foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(t => t.GetProperties())
                     .Where(p => p.ClrType == typeof(DateTimeOffset) || p.ClrType == typeof(DateTimeOffset?)))
            property.SetValueConverter(converter);
    }
}

public static class InviteCodes
{
    /// <summary>128 bit random, base64url (22 chars).</summary>
    public static string Generate() => Base64Url(RandomNumberGenerator.GetBytes(16));

    public static string Hash(string code) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(code.Trim())));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
