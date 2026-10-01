using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CvApi.Tenants;
using Microsoft.EntityFrameworkCore;

namespace CvApi.Tracking;

/// <summary>Owner-only reports on the tracking data (docs/VISITOR_SESSION_TRACKING.md §7, §8.2).</summary>
public sealed partial class AnalyticsService(TrackingDbContext db, TenantStore tenants)
{
    /// <summary>Events that show the reader wanted more detail (R7.1 "detail seeking").</summary>
    public static readonly string[] DetailEvents = ["expand", "lightbox", "link_out", "timeline"];

    /// <summary>Events that show intent to get in touch or keep the CV (R7.2).</summary>
    public static readonly string[] IntentEvents = ["contact", "pdf", "print"];

    private const double WordsPerMinute = 230;

    public sealed record ScoreParts(double Time, double Coverage, double Returns, double Detail, double Intent, double Spread)
    {
        public int Total => (int)Math.Round(Time + Coverage + Returns + Detail + Intent + Spread);
    }

    public sealed record GroupSummary(string GroupKey, int Visitors, int Persons, int Sessions, int Visits, long ActiveMs,
        long VisibleMs, DateTimeOffset? FirstVisit, DateTimeOffset? LastVisit, ConsentCounts Consent, int Score,
        int SectionsSeen, int SectionsKnown);

    public sealed record ConsentCounts(int Accept, int Decline, int Withdraw, Dictionary<string, int> DeclineBySource);

    /// <summary>Optional time range of reports: sessions started (consents given) in [From, To).</summary>
    public readonly record struct Period(DateTimeOffset? From, DateTimeOffset? To);

    /// <summary>Most recent sessions listed in a group detail by default; aggregates always cover all sessions.</summary>
    public const int DefaultSessionLimit = 200;
    public const int MaxSessionLimit = 1000;

    /// <summary>Events read for the technology intent of a group (most recent first); other counts are aggregated in SQL.</summary>
    public const int MaxTechEvents = 5000;

    private IQueryable<TrackSession> Sessions(string tenantId, Period period)
    {
        var q = db.Sessions.Where(s => s.TenantId == tenantId);
        if (period.From is { } from) q = q.Where(s => s.StartedAt >= from);
        if (period.To is { } to) q = q.Where(s => s.StartedAt < to);
        return q;
    }

    private IQueryable<ConsentRecord> Consents(string tenantId, Period period)
    {
        var q = db.Consents.Where(c => c.TenantId == tenantId);
        if (period.From is { } from) q = q.Where(c => c.CreatedAt >= from);
        if (period.To is { } to) q = q.Where(c => c.CreatedAt < to);
        return q;
    }

    /// <summary>Sessions without the large fingerprint / network columns, for aggregates.</summary>
    private static IQueryable<TrackSession> Slim(IQueryable<TrackSession> q) => q.Select(s => new TrackSession
    {
        Id = s.Id, TenantId = s.TenantId, GroupKey = s.GroupKey, VisitorId = s.VisitorId, VisitId = s.VisitId,
        StartedAt = s.StartedAt, LastSeenAt = s.LastSeenAt, EndedAt = s.EndedAt, ActiveMs = s.ActiveMs, VisibleMs = s.VisibleMs,
        Breakpoint = s.Breakpoint, AppSha = s.AppSha, CvSourceSha = s.CvSourceSha, CvVersion = s.CvVersion,
        IpCountry = s.IpCountry, IpCity = s.IpCity, AsOrg = s.AsOrg,
    });

    /// <summary>Per visitor group (invite / public profile): reach, time, consent, coverage and interest score.</summary>
    public async Task<List<GroupSummary>> GroupsAsync(string tenantId, CancellationToken ct) => await GroupsAsync(tenantId, default, ct);

    public async Task<List<GroupSummary>> GroupsAsync(string tenantId, Period period, CancellationToken ct)
    {
        var sessions = await Slim(Sessions(tenantId, period)).ToListAsync(ct);
        var consents = await Consents(tenantId, period).ToListAsync(ct);
        var visitorPersons = await db.Visitors.Where(v => v.TenantId == tenantId).ToDictionaryAsync(v => v.Id, v => v.PersonId, ct);
        var sectionCount = await KnownSectionCountAsync(tenantId, ct);

        var groups = sessions.Select(s => s.GroupKey).Concat(consents.Select(c => c.GroupKey)).Distinct();
        var result = new List<GroupSummary>();
        foreach (var group in groups)
        {
            var gs = sessions.Where(s => s.GroupKey == group).ToList();
            var score = gs.Count == 0 ? EmptyScore : await ScoreDetailAsync(gs, sectionCount, includeSpread: true, ct);
            result.Add(new GroupSummary(
                group,
                gs.Select(s => s.VisitorId).Distinct().Count(),
                gs.Select(s => visitorPersons.GetValueOrDefault(s.VisitorId)).Distinct().Count(),
                gs.Count,
                gs.Select(s => s.VisitId).Distinct().Count(),
                gs.Sum(s => s.ActiveMs),
                gs.Sum(s => s.VisibleMs),
                gs.Count == 0 ? null : gs.Min(s => s.StartedAt),
                gs.Count == 0 ? null : gs.Max(s => s.EndOrLast),
                Count(consents.Where(c => c.GroupKey == group)),
                score.Parts.Total,
                score.SectionsSeen,
                sectionCount));
        }
        return result.OrderByDescending(r => r.LastVisit).ToList();
    }

    public static ConsentCounts Count(IEnumerable<ConsentRecord> records)
    {
        var list = records.ToList();
        return new ConsentCounts(
            list.Count(c => c.Choice == "accept"),
            list.Count(c => c.Choice == "decline"),
            list.Count(c => c.Choice == "withdraw"),
            list.Where(c => c.Choice == "decline").GroupBy(c => c.Source).ToDictionary(g => g.Key, g => g.Count()));
    }

    /// <summary>
    /// Detail of one visitor group: visitors, sessions, attention per anchor, intent signals, networks, versions.
    /// Aggregates cover every session of the period (events are counted in SQL); the session list holds the most
    /// recent <paramref name="limit"/> sessions.
    /// </summary>
    public async Task<object> GroupAsync(Tenant tenant, string groupKey, CancellationToken ct) =>
        await GroupAsync(tenant, groupKey, default, DefaultSessionLimit, ct);

    public async Task<object> GroupAsync(Tenant tenant, string groupKey, Period period, int limit, CancellationToken ct)
    {
        limit = Math.Clamp(limit, 1, MaxSessionLimit);
        var query = Sessions(tenant.Id, period).Where(s => s.GroupKey == groupKey);
        var sessions = await Slim(query).ToListAsync(ct);
        var listed = await query.OrderByDescending(s => s.StartedAt).Take(limit).ToListAsync(ct);
        var sessionIds = sessions.Select(s => s.Id).ToList();
        var visitorIds = sessions.Select(s => s.VisitorId).Distinct().ToList();
        var visitors = await db.Visitors.Where(v => visitorIds.Contains(v.Id)).ToListAsync(ct);
        var stats = await db.SectionStats.Where(s => sessionIds.Contains(s.SessionId))
            .GroupBy(s => s.Anchor)
            .Select(g => new
            {
                Anchor = g.Key, VisibleMs = g.Sum(s => s.VisibleMs), HoverMs = g.Sum(s => s.HoverMs), Clicks = g.Sum(s => s.Clicks),
                Views = g.Sum(s => s.Views), Sessions = g.Count(),
            })
            .ToListAsync(ct);
        var events = db.Events.Where(e => sessionIds.Contains(e.SessionId));
        var actions = await events.Where(e => e.Type != "visibility" && e.Type != "click" && e.Type != "session_end")
            .GroupBy(e => e.Type).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count, ct);
        var techEvents = await events.Where(e => e.Type == "tech_filter" || (e.Type == "click" && e.Anchor != null && e.Anchor.StartsWith("tech:")))
            .OrderByDescending(e => e.Id).Take(MaxTechEvents).ToListAsync(ct);
        var consents = await Consents(tenant.Id, period).Where(c => c.GroupKey == groupKey).ToListAsync(ct);
        var sectionCount = await KnownSectionCountAsync(tenant.Id, ct);
        var labels = AnchorLabels(tenant);
        var words = await WordCountsAsync(tenant.Id, sessions, ct);

        var anchors = stats.Select(g =>
        {
            double? ratio = words.TryGetValue(g.Anchor, out var w) && w > 0 ? Math.Round(g.VisibleMs / (w / WordsPerMinute * 60_000.0), 2) : null;
            return new
            {
                anchor = g.Anchor,
                label = labels.GetValueOrDefault(g.Anchor),
                visibleMs = g.VisibleMs,
                hoverMs = g.HoverMs,
                clicks = g.Clicks,
                views = g.Views,
                sessions = g.Sessions,
                // Reading ratio (R7.1): < 0.2 skimmed, 0.2–0.8 scanned, > 0.8 read.
                readingRatio = ratio,
                reading = ratio is null ? null : ratio < 0.2 ? "skimmed" : ratio <= 0.8 ? "scanned" : "read",
            };
        }).OrderByDescending(a => a.visibleMs).ToList();

        // Technology intent (R7.2): what was filtered for and which tech badges were clicked.
        var techIntent = techEvents.Where(e => e.Type == "tech_filter" && Payload(e)?["on"]?.GetValue<bool>() != false)
            .Select(e => Payload(e)?["tech"]?.GetValue<string>() ?? e.Anchor?.Split(':', 2)[1])
            .Concat(techEvents.Where(e => e.Type == "click" && e.Anchor?.StartsWith("tech:") == true).Select(e => e.Anchor![5..]))
            .OfType<string>()
            .GroupBy(t => t, StringComparer.OrdinalIgnoreCase)
            .Select(g => new { tech = g.Key, count = g.Count() })
            .OrderByDescending(t => t.count).ToList();

        var score = sessions.Count == 0 ? EmptyScore : await ScoreDetailAsync(sessions, sectionCount, includeSpread: true, ct);
        return new
        {
            groupKey,
            score = new { total = score.Parts.Total, parts = score.Parts },
            coverage = new { seen = score.SectionsSeen, known = sectionCount },
            consent = Count(consents),
            visitors = visitors.Select(v =>
            {
                var vs = sessions.Where(s => s.VisitorId == v.Id).ToList();
                return new
                {
                    id = v.Id, personId = v.PersonId, personReason = v.PersonReason, v.Device, v.Browser, v.Os, v.Language,
                    v.FirstSeen, v.LastSeen,
                    sessions = vs.Count,
                    visits = vs.Select(s => s.VisitId).Distinct().Count(),
                    activeMs = vs.Sum(s => s.ActiveMs),
                    visibleMs = vs.Sum(s => s.VisibleMs),
                };
            }).OrderByDescending(v => v.LastSeen),
            sessionsTotal = sessions.Count,
            sessions = listed.Select(SessionSummary),
            anchors,
            techIntent,
            actions,
            networks = sessions.GroupBy(s => new { s.AsOrg, s.IpCity, s.IpCountry })
                .Select(g => new
                {
                    org = g.Key.AsOrg, city = g.Key.IpCity, country = g.Key.IpCountry,
                    sessions = g.Count(), visitors = g.Select(s => s.VisitorId).Distinct().Count(),
                })
                .OrderByDescending(n => n.sessions),
            versions = sessions.GroupBy(s => new { s.AppSha, s.CvSourceSha, s.CvVersion })
                .Select(g => new
                {
                    g.Key.AppSha, g.Key.CvSourceSha, g.Key.CvVersion, sessions = g.Count(),
                    firstSeen = g.Min(s => s.StartedAt), lastSeen = g.Max(s => s.EndOrLast),
                })
                .OrderByDescending(v => v.lastSeen),
        };
    }

    public static object SessionSummary(TrackSession s) => new
    {
        s.Id, s.VisitorId, s.VisitId, s.PreviousSessionId, s.StartedAt, s.LastSeenAt, s.EndedAt, s.EndReason,
        openMs = (long)(s.EndOrLast - s.StartedAt).TotalMilliseconds, s.VisibleMs, s.ActiveMs, s.MaxScroll,
        s.Locale, s.Breakpoint, s.ViewportW, s.ViewportH, s.Referrer, s.LocalHour, s.ColorScheme, s.Signals,
        s.Ip, s.IpTruncated, s.IpCountry, s.IpRegion, s.IpCity, s.Asn, s.AsOrg,
        s.Fp, s.FpServer, s.AppSha, s.ApiSha, s.CvSourceSha, s.CvVersion, s.ClientCvVersion, s.VersionMismatch,
    };

    /// <summary>A session with its IPs, attention per anchor and event timeline.</summary>
    public async Task<object?> SessionAsync(Tenant tenant, string sessionId, CancellationToken ct)
    {
        var session = await db.Sessions.SingleOrDefaultAsync(s => s.TenantId == tenant.Id && s.Id == sessionId, ct);
        if (session is null) return null;
        var labels = AnchorLabels(tenant);
        return new
        {
            session = SessionSummary(session),
            ips = await db.SessionIps.Where(i => i.SessionId == sessionId).OrderBy(i => i.FirstSeen).ToListAsync(ct),
            linked = await db.Sessions.Where(s => s.VisitId == session.VisitId && s.Id != session.Id)
                .OrderBy(s => s.StartedAt).Select(s => new { s.Id, s.StartedAt, s.EndReason, s.CvVersion, s.AppSha }).ToListAsync(ct),
            anchors = (await db.SectionStats.Where(s => s.SessionId == sessionId).ToListAsync(ct))
                .OrderByDescending(s => s.VisibleMs)
                .Select(s => new { s.Anchor, label = labels.GetValueOrDefault(s.Anchor), s.VisibleMs, s.HoverMs, s.Clicks, s.Views }),
            events = (await db.Events.Where(e => e.SessionId == sessionId).OrderBy(e => e.T).ThenBy(e => e.Id).ToListAsync(ct))
                .Select(e => new { e.T, e.Type, e.Anchor, label = e.Anchor is null ? null : labels.GetValueOrDefault(e.Anchor), payload = Payload(e) }),
        };
    }

    /// <summary>A breakpoint / version combination with heatmap data, for the filters of the heatmap view.</summary>
    public sealed record HeatmapFacet(string Breakpoint, string AppSha, string CvVersion, long Weight, long Move, long Click,
        long AttentionMs, bool Snapshot);

    /// <summary>
    /// Heatmap cells for rendering (R6.8): move / click cells summed per anchor and 1 % cell, or attention per anchor.
    /// Also returns which breakpoints and versions have data – cursor / click cells and attention (section dwell, which
    /// touch-only visitors produce too, R6.9) – and whether a CV snapshot exists to render them on (R6.12).
    /// Without a group: tenant-wide.
    /// </summary>
    public async Task<object> HeatmapAsync(string tenantId, string? group, string? bp, string? appSha, string? cvVersion, string type,
        CancellationToken ct)
    {
        var cells = db.HeatCells.Where(c => c.TenantId == tenantId);
        if (!string.IsNullOrEmpty(group)) cells = cells.Where(c => c.GroupKey == group);
        var sessions = db.Sessions.Where(s => s.TenantId == tenantId);
        if (!string.IsNullOrEmpty(group)) sessions = sessions.Where(s => s.GroupKey == group);
        var facets = await FacetsAsync(tenantId, cells, sessions, ct);

        if (!string.IsNullOrEmpty(bp)) cells = cells.Where(c => c.Breakpoint == bp);
        if (!string.IsNullOrEmpty(appSha)) cells = cells.Where(c => c.AppSha == appSha);
        if (!string.IsNullOrEmpty(cvVersion)) cells = cells.Where(c => c.CvVersion == cvVersion);

        if (type == "attention")
        {
            // Attention is per session; a session counts for the breakpoint it started with.
            if (!string.IsNullOrEmpty(bp)) sessions = sessions.Where(s => s.Breakpoint == bp);
            if (!string.IsNullOrEmpty(appSha)) sessions = sessions.Where(s => s.AppSha == appSha);
            if (!string.IsNullOrEmpty(cvVersion)) sessions = sessions.Where(s => s.CvVersion == cvVersion);
            var ids = sessions.Select(s => s.Id);
            var attention = await db.SectionStats.Where(s => ids.Contains(s.SessionId))
                .GroupBy(s => s.Anchor).Select(g => new { anchor = g.Key, weight = g.Sum(s => s.VisibleMs) })
                .ToListAsync(ct);
            return new { type, facets, anchors = attention };
        }

        var list = await cells.Where(c => c.Type == type)
            .GroupBy(c => new { c.Anchor, c.Cx, c.Cy })
            .Select(g => new { anchor = g.Key.Anchor, x = g.Key.Cx, y = g.Key.Cy, w = g.Sum(c => c.Weight) })
            .ToListAsync(ct);
        return new { type, facets, cells = list };
    }

    private async Task<List<HeatmapFacet>> FacetsAsync(string tenantId, IQueryable<HeatCell> cells, IQueryable<TrackSession> sessions,
        CancellationToken ct)
    {
        var cellFacets = await cells.GroupBy(c => new { c.Breakpoint, c.AppSha, c.CvVersion, c.Type })
            .Select(g => new { g.Key.Breakpoint, g.Key.AppSha, g.Key.CvVersion, g.Key.Type, Weight = g.Sum(c => c.Weight) })
            .ToListAsync(ct);
        var attention = await db.SectionStats
            .Join(sessions.Where(s => s.Breakpoint != null), st => st.SessionId, s => s.Id,
                (st, s) => new { s.Breakpoint, s.AppSha, s.CvVersion, st.VisibleMs })
            .GroupBy(x => new { x.Breakpoint, x.AppSha, x.CvVersion })
            .Select(g => new { g.Key.Breakpoint, g.Key.AppSha, g.Key.CvVersion, Ms = g.Sum(x => x.VisibleMs) })
            .ToListAsync(ct);
        var snapshots = (await db.CvSnapshots.Where(s => s.TenantId == tenantId).Select(s => s.CvVersion).ToListAsync(ct)).ToHashSet();

        var keys = cellFacets.Select(f => (Bp: f.Breakpoint, App: f.AppSha, Cv: f.CvVersion))
            .Concat(attention.Where(a => a.Ms > 0).Select(a => (Bp: a.Breakpoint!, App: a.AppSha ?? "unknown", Cv: a.CvVersion ?? "unknown")))
            .Distinct();
        return keys.Select(k =>
        {
            var c = cellFacets.Where(f => f.Breakpoint == k.Bp && f.AppSha == k.App && f.CvVersion == k.Cv).ToList();
            var ms = attention.Where(a => a.Breakpoint == k.Bp && (a.AppSha ?? "unknown") == k.App && (a.CvVersion ?? "unknown") == k.Cv)
                .Sum(a => a.Ms);
            return new HeatmapFacet(k.Bp, k.App, k.Cv, c.Sum(f => f.Weight), c.Where(f => f.Type == "move").Sum(f => f.Weight),
                c.Where(f => f.Type == "click").Sum(f => f.Weight), ms, snapshots.Contains(k.Cv));
        }).OrderByDescending(f => f.Weight + f.AttentionMs).ToList();
    }

    /// <summary>Consent rate per policy version and per group (R9.15).</summary>
    public async Task<object> ConsentAsync(string tenantId, CancellationToken ct)
    {
        var consents = await db.Consents.Where(c => c.TenantId == tenantId).ToListAsync(ct);
        return new
        {
            total = Count(consents),
            byPolicyVersion = consents.GroupBy(c => c.PolicyVersion).Select(g => new
            {
                policyVersion = g.Key,
                first = g.Min(c => c.CreatedAt),
                last = g.Max(c => c.CreatedAt),
                counts = Count(g),
                acceptRate = Rate(g),
            }).OrderByDescending(v => v.last),
            // How visitors with DNT / GPC decide when asked anyway.
            withSignals = Count(consents.Where(c => c.Signals is not null && c.Source is "modal" or "footer")),
        };
    }

    private static double? Rate(IEnumerable<ConsentRecord> records)
    {
        var list = records.Where(c => c.Choice is "accept" or "decline").ToList();
        return list.Count == 0 ? null : Math.Round((double)list.Count(c => c.Choice == "accept") / list.Count, 3);
    }

    /// <summary>Probable persons (R3.10) with their visitors and how they were linked.</summary>
    public async Task<object> PersonsAsync(string tenantId, CancellationToken ct)
    {
        var persons = await db.Persons.Where(p => p.TenantId == tenantId).OrderByDescending(p => p.LastSeen).ToListAsync(ct);
        var visitors = await db.Visitors.Where(v => v.TenantId == tenantId).ToListAsync(ct);
        var groups = await db.Sessions.Where(s => s.TenantId == tenantId)
            .Select(s => new { s.VisitorId, s.GroupKey }).Distinct().ToListAsync(ct);
        return persons.Select(p => new
        {
            p.Id, p.FirstSeen, p.LastSeen,
            visitors = visitors.Where(v => v.PersonId == p.Id).Select(v => new
            {
                v.Id, v.PersonReason, v.Device, v.Browser, v.Os,
                groups = groups.Where(g => g.VisitorId == v.Id).Select(g => g.GroupKey),
            }),
        });
    }

    /// <summary>Deletes everything recorded about a visitor (erasure request, §8.2). Consent records lose the link.</summary>
    public async Task<bool> EraseVisitorAsync(string tenantId, Guid visitorId, CancellationToken ct)
    {
        var visitor = await db.Visitors.SingleOrDefaultAsync(v => v.TenantId == tenantId && v.Id == visitorId, ct);
        if (visitor is null) return false;
        var sessionIds = db.Sessions.Where(s => s.VisitorId == visitorId).Select(s => s.Id);
        await db.Events.Where(e => sessionIds.Contains(e.SessionId)).ExecuteDeleteAsync(ct);
        await db.SectionStats.Where(s => sessionIds.Contains(s.SessionId)).ExecuteDeleteAsync(ct);
        await db.SessionIps.Where(i => sessionIds.Contains(i.SessionId)).ExecuteDeleteAsync(ct);
        await db.Sessions.Where(s => s.VisitorId == visitorId).ExecuteDeleteAsync(ct);
        await db.Consents.Where(c => c.VisitorId == visitorId).ExecuteUpdateAsync(u => u.SetProperty(c => c.VisitorId, (Guid?)null), ct);
        db.Visitors.Remove(visitor);
        await db.SaveChangesAsync(ct);
        if (!await db.Visitors.AnyAsync(v => v.PersonId == visitor.PersonId, ct))
            await db.Persons.Where(p => p.Id == visitor.PersonId).ExecuteDeleteAsync(ct);
        return true;
    }

    public async Task<CvSnapshot?> SnapshotAsync(string tenantId, string cvVersion, CancellationToken ct) =>
        await db.CvSnapshots.SingleOrDefaultAsync(s => s.TenantId == tenantId && s.CvVersion == cvVersion, ct);

    /// <summary>Interest score with the number of sections the sessions saw (coverage).</summary>
    public sealed record ScoreDetail(ScoreParts Parts, int SectionsSeen);

    private static readonly ScoreDetail EmptyScore = new(new ScoreParts(0, 0, 0, 0, 0, 0), 0);

    /// <summary>
    /// Interest score 0–100 (R7.5): active time, coverage, returns, detail seeking, contact/keep intent and (for groups)
    /// spread over several visitors. Always reported with its parts.
    /// </summary>
    public async Task<ScoreParts> ScoreAsync(IReadOnlyList<TrackSession> sessions, int knownSections, bool includeSpread, CancellationToken ct) =>
        (await ScoreDetailAsync(sessions, knownSections, includeSpread, ct)).Parts;

    public async Task<ScoreDetail> ScoreDetailAsync(IReadOnlyList<TrackSession> sessions, int knownSections, bool includeSpread, CancellationToken ct)
    {
        var ids = sessions.Select(s => s.Id).ToList();
        var seenSections = await db.SectionStats.Where(s => ids.Contains(s.SessionId) && s.Anchor.StartsWith("section:") && s.VisibleMs > 0)
            .Select(s => s.Anchor).Distinct().CountAsync(ct);
        var types = await db.Events.Where(e => ids.Contains(e.SessionId))
            .GroupBy(e => e.Type).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count, ct);

        var activeMinutes = sessions.Sum(s => s.ActiveMs) / 60_000.0;
        var visits = sessions.Select(s => s.VisitId).Distinct().Count();
        var visitors = sessions.Select(s => s.VisitorId).Distinct().Count();
        var detail = DetailEvents.Sum(t => types.GetValueOrDefault(t));
        return new ScoreDetail(new ScoreParts(
            Time: Math.Round(25 * Math.Min(activeMinutes / 5, 1), 1),
            Coverage: Math.Round(20 * (knownSections == 0 ? 0 : Math.Min((double)seenSections / knownSections, 1)), 1),
            Returns: Math.Round(15 * Math.Min(visits - 1, 3) / 3.0, 1),
            Detail: Math.Round(15 * Math.Min(detail / 5.0, 1), 1),
            Intent: IntentEvents.Any(types.ContainsKey) ? 15 : 0,
            Spread: includeSpread ? Math.Round(10 * Math.Min(visitors - 1, 3) / 3.0, 1) : 0), seenSections);
    }

    /// <summary>
    /// Tenant-wide overview of a period: totals, sessions per day (in the owner's time zone; <paramref name="tzOffsetMinutes"/>
    /// as JavaScript's getTimezoneOffset) and breakdowns by device, browser, OS and country.
    /// </summary>
    public async Task<object> OverviewAsync(string tenantId, Period period, int tzOffsetMinutes, DateTimeOffset now, CancellationToken ct)
    {
        var offset = TimeSpan.FromMinutes(-Math.Clamp(tzOffsetMinutes, -840, 840));
        var rows = await Sessions(tenantId, period)
            .Select(s => new { s.StartedAt, s.VisitorId, s.VisitId, s.GroupKey, s.ActiveMs, s.VisibleMs, s.IpCountry })
            .ToListAsync(ct);
        var visitorIds = rows.Select(r => r.VisitorId).Distinct().ToList();
        var visitors = await db.Visitors.Where(v => v.TenantId == tenantId && visitorIds.Contains(v.Id))
            .Select(v => new { v.Id, v.PersonId, v.Device, v.Browser, v.Os })
            .ToDictionaryAsync(v => v.Id, ct);
        var consents = await Consents(tenantId, period).ToListAsync(ct);

        DateOnly Day(DateTimeOffset t) => DateOnly.FromDateTime(t.ToOffset(offset).DateTime);
        var lastDay = Day(period.To is { } to && to < now ? to.AddTicks(-1) : now);
        var firstDay = period.From is { } from ? Day(from) : rows.Count > 0 ? Day(rows.Min(r => r.StartedAt)) : lastDay;
        if (firstDay < lastDay.AddDays(-365)) firstDay = lastDay.AddDays(-365);
        var byDay = rows.GroupBy(r => Day(r.StartedAt)).ToDictionary(g => g.Key, g => g.ToList());
        var perDay = new List<object>();
        for (var d = firstDay; d <= lastDay; d = d.AddDays(1))
        {
            var list = byDay.GetValueOrDefault(d) ?? [];
            perDay.Add(new
            {
                date = d.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), sessions = list.Count,
                visitors = list.Select(r => r.VisitorId).Distinct().Count(), activeMs = list.Sum(r => r.ActiveMs),
            });
        }

        List<object> Breakdown(Func<Guid, string?, string?> key) => rows
            .GroupBy(r => key(r.VisitorId, r.IpCountry) ?? "unknown")
            .Select(g => new { key = g.Key, visitors = g.Select(r => r.VisitorId).Distinct().Count(), sessions = g.Count() })
            .OrderByDescending(b => b.visitors).ThenByDescending(b => b.sessions).ThenBy(b => b.key)
            .Cast<object>().ToList();

        var sessions = rows.Count;
        var activeMs = rows.Sum(r => r.ActiveMs);
        var visibleMs = rows.Sum(r => r.VisibleMs);
        return new
        {
            from = period.From,
            to = period.To,
            totals = new
            {
                visitors = visitorIds.Count,
                persons = visitors.Values.Select(v => v.PersonId).Distinct().Count(),
                sessions,
                visits = rows.Select(r => r.VisitId).Distinct().Count(),
                groups = rows.Select(r => r.GroupKey).Distinct().Count(),
                activeMs,
                visibleMs,
                avgActiveMs = sessions == 0 ? 0 : activeMs / sessions,
                avgVisibleMs = sessions == 0 ? 0 : visibleMs / sessions,
            },
            consent = Count(consents),
            perDay,
            devices = Breakdown((v, _) => visitors.GetValueOrDefault(v)?.Device),
            browsers = Breakdown((v, _) => visitors.GetValueOrDefault(v)?.Browser),
            os = Breakdown((v, _) => visitors.GetValueOrDefault(v)?.Os),
            countries = Breakdown((_, country) => country),
        };
    }

    private async Task<int> KnownSectionCountAsync(string tenantId, CancellationToken ct)
    {
        var ids = db.Sessions.Where(s => s.TenantId == tenantId).Select(s => s.Id);
        return await db.SectionStats.Where(s => ids.Contains(s.SessionId) && s.Anchor.StartsWith("section:"))
            .Select(s => s.Anchor).Distinct().CountAsync(ct);
    }

    private static JsonObject? Payload(TrackEvent e)
    {
        if (e.PayloadJson is null) return null;
        try { return JsonNode.Parse(e.PayloadJson) as JsonObject; }
        catch (JsonException) { return null; }
    }

    /// <summary>Readable labels of entry anchors from the unredacted master CV (owner-only, §8.2).</summary>
    public Dictionary<string, string> AnchorLabels(Tenant tenant)
    {
        var labels = new Dictionary<string, string>(StringComparer.Ordinal);
        if (tenants.LoadCv(tenant, tenant.Config.DefaultLocale) is not { } loaded) return labels;
        var cv = loaded.Cv;
        void Add(string list, string kind, params string[] fields)
        {
            if (cv[list] is not JsonArray items) return;
            foreach (var item in items.OfType<JsonObject>())
            {
                var id = item["id"]?.ToString();
                if (id is null) continue;
                var text = string.Join(" · ", fields.Select(f => item[f]?.ToString()).Where(v => !string.IsNullOrEmpty(v)));
                if (text.Length > 0) labels[$"{kind}:{id}"] = text;
            }
        }
        Add("experiences", "experience", "company", "position");
        Add("studies", "study", "institution", "degree");
        Add("projects", "project", "name", "client");
        Add("otherEntries", "other", "title", "institution");
        return labels;
    }

    /// <summary>Word count per entry anchor from the CV snapshots the sessions saw (for the reading ratio).</summary>
    private async Task<Dictionary<string, int>> WordCountsAsync(string tenantId, IEnumerable<TrackSession> sessions, CancellationToken ct)
    {
        var versions = sessions.Select(s => s.CvVersion).OfType<string>().Distinct().ToList();
        var snapshot = await db.CvSnapshots.Where(s => s.TenantId == tenantId && versions.Contains(s.CvVersion))
            .OrderByDescending(s => s.LastUsed).FirstOrDefaultAsync(ct);
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        if (snapshot is null || JsonNode.Parse(snapshot.Json) is not JsonObject cv) return counts;
        foreach (var (list, kind) in new[] { ("experiences", "experience"), ("studies", "study"), ("projects", "project"), ("otherEntries", "other") })
        {
            if (cv[list] is not JsonArray items) continue;
            foreach (var item in items.OfType<JsonObject>())
                if (item["id"]?.ToString() is { } id)
                    counts[$"{kind}:{id}"] = Words(item);
        }
        return counts;
    }

    private static int Words(JsonNode? node) => node switch
    {
        JsonObject o => o.Sum(p => p.Key is "id" or "startDate" or "endDate" or "icon" ? 0 : Words(p.Value)),
        JsonArray a => a.Sum(Words),
        JsonValue v when v.TryGetValue<string>(out var s) && !s.StartsWith('/') => WordRegex().Matches(s).Count,
        _ => 0,
    };

    [GeneratedRegex(@"\w+")]
    private static partial Regex WordRegex();
}
