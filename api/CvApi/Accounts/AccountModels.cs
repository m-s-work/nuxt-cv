using Microsoft.EntityFrameworkCore;

namespace CvApi.Accounts;

/// <summary>A CV owner with an account (docs/REQUIREMENTS_SAAS.md §1). Owns at most one tenant.</summary>
public sealed class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Email { get; set; }
    public string Name { get; set; } = "";
    public string? AvatarUrl { get; set; }

    /// <summary>Handle = id of the user's tenant; null until onboarding created it.</summary>
    public string? TenantId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
    public DateTimeOffset? BlockedAt { get; set; }

    /// <summary>Changed on block, "sign out everywhere" and deletion; sessions carrying another stamp are rejected.</summary>
    public string SecurityStamp { get; set; } = NewStamp();

    /// <summary>End of the paid Pro time (passes add days to it).</summary>
    public DateTimeOffset? ProUntil { get; set; }

    /// <summary>Pro without end, set by the super-admin.</summary>
    public bool ProForever { get; set; }

    /// <summary>Super-admin note on the plan (e.g. "paid via invoice 2026-17").</summary>
    public string? PlanNote { get; set; }

    /// <summary>Own domain (Pro, §5.4); only resolves while the plan is Pro.</summary>
    public string? CustomDomain { get; set; }

    public List<ExternalLogin> Logins { get; set; } = [];

    public bool IsPro(DateTimeOffset now) => ProForever || ProUntil > now;

    public static string NewStamp() => Convert.ToHexStringLower(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16));
}

/// <summary>Sign-in method of a user: provider ("google", "microsoft", "github", "linkedin", "email") + subject.</summary>
public sealed class ExternalLogin
{
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public required string Provider { get; set; }
    public required string Subject { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>One-time e-mail sign-in link (stored hashed).</summary>
public sealed class MagicLinkToken
{
    public long Id { get; set; }
    public required string TokenHash { get; set; }
    public required string Email { get; set; }
    public string? ReturnUrl { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
}

/// <summary>A payment (Paddle transaction or a manual plan change by the super-admin).</summary>
public sealed class Payment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>"paddle" or "manual".</summary>
    public required string Provider { get; set; }

    /// <summary>Transaction / adjustment id of the provider; unique per provider (idempotency).</summary>
    public string? ExternalId { get; set; }

    /// <summary>Null when the webhook named no known user, or after the user was deleted (anonymised).</summary>
    public Guid? UserId { get; set; }

    /// <summary>E-mail at the time of payment (removed on account deletion).</summary>
    public string? Email { get; set; }

    /// <summary>Pass id (e.g. "month") or "manual".</summary>
    public string? Pass { get; set; }

    public int Days { get; set; }

    /// <summary>Amount in minor units (cents), as reported by the provider.</summary>
    public long Amount { get; set; }

    public string? Currency { get; set; }

    /// <summary>"completed", "unmatched", "refunded", "chargeback", "manual".</summary>
    public required string Status { get; set; }

    public string? Note { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public static class PaymentStatus
{
    public const string Completed = "completed";
    public const string Unmatched = "unmatched";
    public const string Refunded = "refunded";
    public const string Chargeback = "chargeback";
    public const string Manual = "manual";
}

/// <summary>Accounts live in their own database file (/data/accounts.db), like tracking.db.</summary>
public sealed class AccountsDbContext(DbContextOptions<AccountsDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<ExternalLogin> Logins => Set<ExternalLogin>();
    public DbSet<MagicLinkToken> MagicLinks => Set<MagicLinkToken>();
    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var user = modelBuilder.Entity<User>();
        user.HasIndex(u => u.Email).IsUnique();
        user.HasIndex(u => u.TenantId).IsUnique();
        user.Property(u => u.Email).HasMaxLength(254);
        user.Property(u => u.TenantId).HasMaxLength(64);
        user.HasMany(u => u.Logins).WithOne().HasForeignKey(l => l.UserId).OnDelete(DeleteBehavior.Cascade);

        var login = modelBuilder.Entity<ExternalLogin>();
        login.HasIndex(l => new { l.Provider, l.Subject }).IsUnique();

        modelBuilder.Entity<MagicLinkToken>().HasIndex(t => t.TokenHash).IsUnique();

        var payment = modelBuilder.Entity<Payment>();
        payment.HasIndex(p => new { p.Provider, p.ExternalId }).IsUnique();
        payment.HasIndex(p => p.UserId);

        // SQLite cannot order/compare DateTimeOffset natively; store as UTC ticks (like app.db).
        var converter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.DateTimeOffsetToBinaryConverter();
        foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(t => t.GetProperties())
                     .Where(p => p.ClrType == typeof(DateTimeOffset) || p.ClrType == typeof(DateTimeOffset?)))
            property.SetValueConverter(converter);
    }
}
