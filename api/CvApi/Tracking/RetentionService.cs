using CvApi.Tenants;
using Microsoft.EntityFrameworkCore;

namespace CvApi.Tracking;

/// <summary>
/// Applies the sliding retention (R9.2) once a day: periods count from the last visit of the probable person, so a
/// returning visitor keeps their history. IPs are truncated and fingerprints removed first, then raw events, then
/// everything about the person; heat cells expire by their last contribution.
/// </summary>
public sealed class RetentionService(IServiceScopeFactory scopes, TimeProvider time, IConfiguration configuration,
    ILogger<RetentionService> logger) : BackgroundService
{
    /// <summary>Consent records are proof of consent; kept at most this long.</summary>
    public const int ConsentMonths = Retention.MaxMonths + 1;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromHours(configuration.GetValue("Tracking:RetentionIntervalHours", 24.0));
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(1), time, stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    await RunAsync(scope.ServiceProvider.GetRequiredService<TrackingDbContext>(),
                        scope.ServiceProvider.GetRequiredService<TenantStore>(), time.GetUtcNow(), stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Tracking retention run failed");
                }
                await Task.Delay(interval, time, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
    }

    public static async Task RunAsync(TrackingDbContext db, TenantStore tenants, DateTimeOffset now, CancellationToken ct)
    {
        var tenantIds = await db.Persons.Select(p => p.TenantId).Distinct().ToListAsync(ct);
        tenantIds = tenantIds.Union(await db.HeatCells.Select(c => c.TenantId).Distinct().ToListAsync(ct)).ToList();

        foreach (var tenantId in tenantIds)
        {
            var retention = Retention.From(tenants.Get(tenantId)?.Config.Tracking?.Retention);

            // Visitors of persons last seen before a limit → their sessions.
            IQueryable<string> SessionsOfPersonsBefore(DateTimeOffset limit)
            {
                var persons = db.Persons.Where(p => p.TenantId == tenantId && p.LastSeen < limit).Select(p => p.Id);
                var visitors = db.Visitors.Where(v => persons.Contains(v.PersonId)).Select(v => v.Id);
                return db.Sessions.Where(s => visitors.Contains(s.VisitorId)).Select(s => s.Id);
            }

            // 1. Identifiers: truncate IPs, drop fingerprints.
            var identifiersLimit = now.AddMonths(-retention.IdentifiersMonths);
            var expiredIdentifiers = SessionsOfPersonsBefore(identifiersLimit);
            var toTruncate = await db.Sessions.Where(s => expiredIdentifiers.Contains(s.Id) && !s.IpTruncated).ToListAsync(ct);
            foreach (var s in toTruncate)
            {
                s.Ip = TrackingService.Truncate(s.Ip);
                s.IpTruncated = true;
                s.Fp = null;
                s.FpPartsJson = null;
                s.FpServer = null;
            }
            await db.SaveChangesAsync(ct);
            await db.SessionIps.Where(i => expiredIdentifiers.Contains(i.SessionId)).ExecuteDeleteAsync(ct);

            // 2. Raw events (session timelines).
            var expiredEvents = SessionsOfPersonsBefore(now.AddMonths(-retention.EventsMonths));
            await db.Events.Where(e => expiredEvents.Contains(e.SessionId)).ExecuteDeleteAsync(ct);

            // 3. Everything about the person.
            var summaryLimit = now.AddMonths(-retention.SummaryMonths);
            var expiredSessions = SessionsOfPersonsBefore(summaryLimit);
            await db.Events.Where(e => expiredSessions.Contains(e.SessionId)).ExecuteDeleteAsync(ct);
            await db.SectionStats.Where(s => expiredSessions.Contains(s.SessionId)).ExecuteDeleteAsync(ct);
            await db.SessionIps.Where(i => expiredSessions.Contains(i.SessionId)).ExecuteDeleteAsync(ct);
            await db.Sessions.Where(s => expiredSessions.Contains(s.Id)).ExecuteDeleteAsync(ct);
            var expiredPersons = db.Persons.Where(p => p.TenantId == tenantId && p.LastSeen < summaryLimit).Select(p => p.Id);
            await db.Visitors.Where(v => expiredPersons.Contains(v.PersonId)).ExecuteDeleteAsync(ct);
            await db.Persons.Where(p => p.TenantId == tenantId && p.LastSeen < summaryLimit).ExecuteDeleteAsync(ct);

            // 4. Heat cells (no person reference) by their last contribution.
            var heatLimit = now.AddMonths(-retention.HeatMonths);
            await db.HeatCells.Where(c => c.TenantId == tenantId && c.LastAt < heatLimit).ExecuteDeleteAsync(ct);
        }

        // CV snapshots no session or heat cell refers to any more.
        var usedBySessions = db.Sessions.Select(s => s.TenantId + "|" + s.CvVersion);
        var usedByCells = db.HeatCells.Select(c => c.TenantId + "|" + c.CvVersion);
        await db.CvSnapshots
            .Where(s => !usedBySessions.Contains(s.TenantId + "|" + s.CvVersion) && !usedByCells.Contains(s.TenantId + "|" + s.CvVersion))
            .ExecuteDeleteAsync(ct);

        var consentLimit = now.AddMonths(-ConsentMonths);
        await db.Consents.Where(c => c.CreatedAt < consentLimit).ExecuteDeleteAsync(ct);
    }
}
