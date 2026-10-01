using Microsoft.EntityFrameworkCore;

namespace CvApi.Accounts;

/// <summary>A Pro pass that can be bought (docs/REQUIREMENTS_SAAS.md §4.1).</summary>
public sealed class PassOption
{
    public string Id { get; set; } = "";
    public int Days { get; set; }

    /// <summary>Price in minor units (cents).</summary>
    public long Amount { get; set; }

    public string Currency { get; set; } = "EUR";

    /// <summary>Paddle price id (pri_…); passes without one cannot be bought online.</summary>
    public string? PaddlePriceId { get; set; }
}

/// <summary>Plan limits and passes (config section "Saas" / "Billing").</summary>
public sealed class PlanOptions
{
    public int FreeMaxActiveInvites { get; set; } = 3;
    public int QuotaFreeMb { get; set; } = 20;
    public int QuotaProMb { get; set; } = 200;

    public List<PassOption> Passes { get; set; } = DefaultPasses();

    public static List<PassOption> DefaultPasses() =>
    [
        new() { Id = "week", Days = 7, Amount = 500 },
        new() { Id = "month", Days = 30, Amount = 1700 },
        new() { Id = "halfyear", Days = 182, Amount = 7800 },
        new() { Id = "year", Days = 365, Amount = 10400 },
    ];

    public static PlanOptions From(IConfiguration config)
    {
        var options = new PlanOptions
        {
            FreeMaxActiveInvites = config.GetValue("Saas:FreeMaxActiveInvites", 3),
            QuotaFreeMb = config.GetValue("Saas:QuotaFreeMb", 20),
            QuotaProMb = config.GetValue("Saas:QuotaProMb", 200),
        };
        // Billing:Passes:<n>:{Id,Days,Amount,Currency,PaddlePriceId}; configured entries replace the defaults by id.
        var configured = config.GetSection("Billing:Passes").Get<List<PassOption>>() ?? [];
        foreach (var pass in configured.Where(p => !string.IsNullOrWhiteSpace(p.Id)))
        {
            var existing = options.Passes.FindIndex(p => p.Id == pass.Id);
            if (existing >= 0)
            {
                var d = options.Passes[existing];
                options.Passes[existing] = new PassOption
                {
                    Id = pass.Id,
                    Days = pass.Days > 0 ? pass.Days : d.Days,
                    Amount = pass.Amount > 0 ? pass.Amount : d.Amount,
                    Currency = string.IsNullOrWhiteSpace(pass.Currency) ? d.Currency : pass.Currency,
                    PaddlePriceId = string.IsNullOrWhiteSpace(pass.PaddlePriceId) ? null : pass.PaddlePriceId.Trim(),
                };
            }
            else if (pass.Days > 0)
            {
                options.Passes.Add(pass);
            }
        }
        return options;
    }
}

/// <summary>What a tenant may use (§4). Managed tenants (no owner) are unlimited.</summary>
public sealed record Entitlements(bool Pro, bool Managed, int? MaxActiveInvites, long QuotaBytes)
{
    public bool CanHideCredit => Pro;
    public bool Heatmaps => Pro;
    public bool CustomDomain => Pro;
    public string Plan => Managed ? "managed" : Pro ? "pro" : "free";
}

/// <summary>Owner state of a tenant, cached for the request pipeline (tenant resolution, PDFs, limits).</summary>
public sealed record TenantOwner(Guid UserId, string TenantId, bool Blocked, bool ProForever, DateTimeOffset? ProUntil, string? CustomDomain)
{
    public bool IsPro(DateTimeOffset now) => ProForever || ProUntil > now;
}

/// <summary>
/// Cached map tenant → owner from accounts.db. Used where a scoped database is not at hand or would be queried
/// on every visitor request (tenant resolution). Invalidated on account changes; otherwise refreshed every few seconds.
/// </summary>
public sealed class TenantOwners(IServiceScopeFactory scopes, TimeProvider time, IConfiguration config)
{
    private readonly Lock _lock = new();
    private (DateTimeOffset LoadedAt, Dictionary<string, TenantOwner> ByTenant)? _snapshot;
    private PlanOptions? _plans;

    private TimeSpan CacheDuration => TimeSpan.FromSeconds(config.GetValue("Cv:ConfigCacheSeconds", 10));

    public PlanOptions Plans => _plans ??= PlanOptions.From(config);

    public void Invalidate()
    {
        lock (_lock) _snapshot = null;
    }

    public TenantOwner? Get(string tenantId) => Current().GetValueOrDefault(tenantId);

    /// <summary>Tenant of a blocked user: its CV is not shown to anyone (§6 S6.3).</summary>
    public bool IsBlocked(string tenantId) => Get(tenantId)?.Blocked == true;

    /// <summary>A user's own domain that may not be used now (plan not Pro, §4 S4.2).</summary>
    public bool IsSuspendedHost(string tenantId, string host)
    {
        var owner = Get(tenantId);
        return owner?.CustomDomain is { } domain
               && string.Equals(domain, host, StringComparison.OrdinalIgnoreCase)
               && !owner.IsPro(time.GetUtcNow());
    }

    public Entitlements For(string tenantId)
    {
        var owner = Get(tenantId);
        var plans = Plans;
        if (owner is null) return new Entitlements(Pro: true, Managed: true, MaxActiveInvites: null, QuotaBytes: (long)plans.QuotaProMb * 1024 * 1024);
        var pro = owner.IsPro(time.GetUtcNow());
        return new Entitlements(pro, Managed: false, pro ? null : plans.FreeMaxActiveInvites,
            (long)(pro ? plans.QuotaProMb : plans.QuotaFreeMb) * 1024 * 1024);
    }

    private Dictionary<string, TenantOwner> Current()
    {
        lock (_lock)
        {
            var now = time.GetUtcNow();
            if (_snapshot is { } s && now - s.LoadedAt < CacheDuration) return s.ByTenant;
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AccountsDbContext>();
            var byTenant = db.Users.AsNoTracking().Where(u => u.TenantId != null).AsEnumerable()
                .ToDictionary(u => u.TenantId!, u => new TenantOwner(u.Id, u.TenantId!, u.BlockedAt is not null, u.ProForever, u.ProUntil, u.CustomDomain),
                    StringComparer.OrdinalIgnoreCase);
            _snapshot = (now, byTenant);
            return byTenant;
        }
    }
}

public static class Branding
{
    /// <summary>
    /// Platform URL for the "Created with …" credit in PDFs (Cv:SharedBaseUrl), or null when it is left out:
    /// no shared URL configured, or the tenant hides it and its plan allows that (§4).
    /// </summary>
    public static string? PlatformLink(Tenants.Tenant tenant, IConfiguration config, TenantOwners owners) =>
        string.IsNullOrEmpty(config["Cv:SharedBaseUrl"]) || (tenant.Config.HideCredit && owners.For(tenant.Id).CanHideCredit)
            ? null
            : config["Cv:SharedBaseUrl"]!.TrimEnd('/');
}
