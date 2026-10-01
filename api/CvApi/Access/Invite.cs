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

    /// <summary>
    /// "View once": set to the grace window in minutes. The first redemption burns the code; only the browser
    /// that redeemed it (access cookie) keeps access until <see cref="ViewOnceUntil"/>. Null = normal invite.
    /// </summary>
    public int? ViewOnceMinutes { get; set; }

    /// <summary>End of the grace window of a redeemed view-once invite; null while it is unused.</summary>
    public DateTimeOffset? ViewOnceUntil { get; set; }

    /// <summary>Random token of the current view-once redemption, also in that browser's access cookie.</summary>
    public string? ViewOnceToken { get; set; }

    public bool IsViewOnce => ViewOnceMinutes is not null;

    /// <summary>Makes the code redeemable again: resets the use count and a used view-once state (§4, R4.10).</summary>
    public void Rearm()
    {
        UseCount = 0;
        ViewOnceUntil = null;
        ViewOnceToken = null;
    }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && (ExpiresAt is null || ExpiresAt > now)
        && (ViewOnceUntil is null || ViewOnceUntil > now);

    public bool CanRedeem(DateTimeOffset now) => IsActive(now) && (MaxUses is null || UseCount < MaxUses)
        && !(IsViewOnce && UseCount > 0);
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
        invite.Property(i => i.ViewOnceToken).HasMaxLength(32);

        // SQLite cannot order/compare DateTimeOffset natively; store as UTC ticks.
        var converter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.DateTimeOffsetToBinaryConverter();
        foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(t => t.GetProperties())
                     .Where(p => p.ClrType == typeof(DateTimeOffset) || p.ClrType == typeof(DateTimeOffset?)))
            property.SetValueConverter(converter);
    }

    /// <summary>
    /// EnsureCreated does not touch existing databases: adds nullable columns introduced after a database was
    /// created (e.g. <see cref="Invite.ViewOnceMinutes"/>), so deployments keep their invites.
    /// </summary>
    public void AddMissingColumns() => AddMissingColumns(this);

    /// <summary>Adds nullable columns of the model that are missing in an existing SQLite database.</summary>
    public static void AddMissingColumns(DbContext context)
    {
        var Database = context.Database;
        var Model = context.Model;
        var connection = Database.GetDbConnection();
        var opened = connection.State != System.Data.ConnectionState.Open;
        if (opened) connection.Open();
        try
        {
            foreach (var entity in Model.GetEntityTypes())
            {
                var table = entity.GetTableName();
                if (table is null) continue;
                var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = $"PRAGMA table_info(\"{table}\")";
                    using var reader = command.ExecuteReader();
                    while (reader.Read()) existing.Add(reader.GetString(1));
                }
                if (existing.Count == 0) continue;
                foreach (var property in entity.GetProperties().Where(p => p.IsNullable))
                {
                    var column = property.GetColumnName();
                    if (existing.Contains(column)) continue;
                    using var alter = connection.CreateCommand();
                    alter.CommandText = $"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {property.GetColumnType()} NULL";
                    alter.ExecuteNonQuery();
                }
            }
        }
        finally
        {
            if (opened) connection.Close();
        }
    }
}

public static class InviteCodes
{
    /// <summary>128 bit random, base64url (22 chars).</summary>
    public static string Generate() => Base64Url(RandomNumberGenerator.GetBytes(16));

    /// <summary>
    /// Admin-chosen codes (e.g. "demo"): URL-safe, 4–64 characters. Short codes are guessable,
    /// so they are meant for demo or public content only.
    /// </summary>
    public static bool IsValidCustom(string code) =>
        code.Length is >= 4 and <= 64 && code.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    public static string Hash(string code) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(code.Trim())));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
