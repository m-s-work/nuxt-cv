using CvApi.Endpoints;
using Microsoft.EntityFrameworkCore;

namespace CvApi.Accounts;

/// <summary>Super-admin user management (docs/REQUIREMENTS_SAAS.md §6). Admin key only.</summary>
public static class UserAdminEndpoints
{
    /// <param name="ProUntil">End of Pro (null = none).</param>
    /// <param name="AddDays">Alternative to <paramref name="ProUntil"/>: extend the current Pro time by this many days.</param>
    public sealed record SetPlanRequest(DateTimeOffset? ProUntil, int? AddDays, bool ProForever, string? Note);

    public static void MapUserAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var users = app.MapGroup("/admin").AddEndpointFilter(AdminEndpoints.RequireAdminKey).AddEndpointFilter(SuperAdminFilter);

        users.MapGet("/users", async (string? q, AccountsDbContext db, TimeProvider time, CancellationToken ct) =>
        {
            var query = db.Users.AsNoTracking().Include(u => u.Logins).AsQueryable();
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim().ToLowerInvariant();
                query = query.Where(u => u.Email.Contains(term) || u.Name.ToLower().Contains(term) || (u.TenantId != null && u.TenantId.Contains(term)));
            }
            var list = await query.ToListAsync(ct);
            var now = time.GetUtcNow();
            return Results.Ok(list.OrderByDescending(u => u.CreatedAt).Take(500).Select(u => Dto(u, now)));
        });

        users.MapGet("/users/{id:guid}", async (Guid id, AccountsDbContext db, TimeProvider time, CancellationToken ct) =>
        {
            var user = await db.Users.AsNoTracking().Include(u => u.Logins).SingleOrDefaultAsync(u => u.Id == id, ct);
            if (user is null) return Results.NotFound();
            var payments = await db.Payments.AsNoTracking().Where(p => p.UserId == id).ToListAsync(ct);
            return Results.Ok(new { user = Dto(user, time.GetUtcNow()), payments = payments.OrderByDescending(p => p.CreatedAt) });
        });

        // Manual plan, e.g. paid via another platform (S6.2). Recorded as a "manual" payment row for the history.
        users.MapPut("/users/{id:guid}/plan", async (Guid id, SetPlanRequest body, AccountsDbContext db, TenantOwners owners,
            TimeProvider time, CancellationToken ct) =>
        {
            var user = await db.Users.SingleOrDefaultAsync(u => u.Id == id, ct);
            if (user is null) return Results.NotFound();
            if (body.AddDays is < -3650 or > 3650) return Results.BadRequest(new { error = "invalid_days" });
            var now = time.GetUtcNow();
            var before = user.ProUntil;
            if (body.AddDays is { } days)
                user.ProUntil = (user.ProUntil > now ? user.ProUntil.Value : now).AddDays(days);
            else
                user.ProUntil = body.ProUntil;
            user.ProForever = body.ProForever;
            user.PlanNote = string.IsNullOrWhiteSpace(body.Note) ? user.PlanNote : body.Note.Trim();
            db.Payments.Add(new Payment
            {
                Provider = "manual",
                ExternalId = Guid.NewGuid().ToString("N"),
                UserId = user.Id,
                Email = user.Email,
                Pass = "manual",
                Days = body.AddDays ?? (int)Math.Round(((user.ProUntil ?? now) - (before > now ? before.Value : now)).TotalDays),
                Status = PaymentStatus.Manual,
                Note = $"{(body.ProForever ? "Pro forever" : $"Pro until {user.ProUntil:yyyy-MM-dd}")}{(string.IsNullOrWhiteSpace(body.Note) ? "" : " – " + body.Note.Trim())}",
                CreatedAt = now,
            });
            await db.SaveChangesAsync(ct);
            owners.Invalidate();
            return Results.Ok(Dto(await db.Users.AsNoTracking().Include(u => u.Logins).SingleAsync(u => u.Id == id, ct), now));
        });

        users.MapPost("/users/{id:guid}/block", (Guid id, AccountsDbContext db, TenantOwners owners, TimeProvider time, CancellationToken ct) =>
            SetBlockedAsync(id, true, db, owners, time, ct));
        users.MapPost("/users/{id:guid}/unblock", (Guid id, AccountsDbContext db, TenantOwners owners, TimeProvider time, CancellationToken ct) =>
            SetBlockedAsync(id, false, db, owners, time, ct));

        users.MapDelete("/users/{id:guid}", async (Guid id, AccountsDbContext db, AccountService accounts, CancellationToken ct) =>
        {
            var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id, ct);
            if (user is null) return Results.NotFound();
            await accounts.DeleteAsync(user, ct);
            return Results.NoContent();
        });

        users.MapGet("/payments", async (AccountsDbContext db, CancellationToken ct) =>
        {
            var payments = await db.Payments.AsNoTracking().ToListAsync(ct);
            return Results.Ok(payments.OrderByDescending(p => p.CreatedAt).Take(1000));
        });
    }

    private static async Task<IResult> SetBlockedAsync(Guid id, bool blocked, AccountsDbContext db, TenantOwners owners, TimeProvider time,
        CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return Results.NotFound();
        user.BlockedAt = blocked ? user.BlockedAt ?? time.GetUtcNow() : null;
        // Blocking ends every session right away (S2.4).
        if (blocked) user.SecurityStamp = User.NewStamp();
        await db.SaveChangesAsync(ct);
        owners.Invalidate();
        return Results.NoContent();
    }

    private static object Dto(User u, DateTimeOffset now) => new
    {
        id = u.Id,
        email = u.Email,
        name = u.Name,
        tenantId = u.TenantId,
        providers = u.Logins.Select(l => l.Provider).Distinct(),
        createdAt = u.CreatedAt,
        lastLoginAt = u.LastLoginAt,
        blockedAt = u.BlockedAt,
        plan = u.IsPro(now) ? "pro" : "free",
        proUntil = u.ProUntil,
        proForever = u.ProForever,
        planNote = u.PlanNote,
        customDomain = u.CustomDomain,
    };

    private static async ValueTask<object?> SuperAdminFilter(EndpointFilterInvocationContext context, EndpointFilterDelegate next) =>
        AdminCaller.Of(context.HttpContext).IsSuperAdmin ? await next(context) : Results.NotFound();
}
