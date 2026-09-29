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
        long VisibleMs, DateTimeOffset? FirstVisit, DateTimeOffset? LastVisit, ConsentCounts Consent, int Score);

    public sealed record ConsentCounts(int Accept, int Decline, int Withdraw, Dictionary<string, int> DeclineBySource);

    /// <summary>Per visitor group (invite / public profile): reach, time, consent and interest score.</summary>
    public async Task<List<GroupSummary>> GroupsAsync(string tenantId, CancellationToken ct)
    {
        var sessions = await db.Sessions.Where(s => s.TenantId == tenantId).ToListAsync(ct);
        var consents = await db.Consents.Where(c => c.TenantId == tenantId).ToListAsync(ct);
        var visitorPersons = await db.Visitors.Where(v => v.TenantId == tenantId).ToDictionaryAsync(v => v.Id, v => v.PersonId, ct);
        var sectionCount = await KnownSectionCountAsync(tenantId, ct);

        var groups = sessions.Select(s => s.GroupKey).Concat(consents.Select(c => c.GroupKey)).Distinct();
        var result = new List<GroupSummary>();
        foreach (var group in groups)
        {
            var gs = sessions.Where(s => s.GroupKey == group).ToList();
            var score = gs.Count == 0 ? new ScoreParts(0, 0, 0, 0, 0, 0) : await ScoreAsync(gs, sectionCount, includeSpread: true, ct);
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
                score.Total));
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

    /// <summary>Detail of one visitor group: visitors, sessions, attention per anchor, intent signals, networks, versions.</summary>
    public async Task<object> GroupAsync(Tenant tenant, string groupKey, CancellationToken ct)
    {
        var sessions = await db.Sessions.Where(s => s.TenantId == tenant.Id && s.GroupKey == groupKey)
            .OrderByDescending(s => s.StartedAt).ToListAsync(ct);
        var sessionIds = sessions.Select(s => s.Id).ToList();
        var visitorIds = sessions.Select(s => s.VisitorId).Distinct().ToList();
        var visitors = await db.Visitors.Where(v => visitorIds.Contains(v.Id)).ToListAsync(ct);
        var stats = await db.SectionStats.Where(s => sessionIds.Contains(s.SessionId)).ToListAsync(ct);
        var events = await db.Events.Where(e => sessionIds.Contains(e.SessionId)).ToListAsync(ct);
        var consents = await db.Consents.Where(c => c.TenantId == tenant.Id && c.GroupKey == groupKey).ToListAsync(ct);
        var sectionCount = await KnownSectionCountAsync(tenant.Id, ct);
        var labels = AnchorLabels(tenant);
        var words = await WordCountsAsync(tenant.Id, sessions, ct);

        var anchors = stats.GroupBy(s => s.Anchor).Select(g =>
        {
            var visible = g.Sum(s => s.VisibleMs);
            double? ratio = words.TryGetValue(g.Key, out var w) && w > 0 ? Math.Round(visible / (w / WordsPerMinute * 60_000.0), 2) : null;
            return new
            {
                anchor = g.Key,
                label = labels.GetValueOrDefault(g.Key),
                visibleMs = visible,
                hoverMs = g.Sum(s => s.HoverMs),
                clicks = g.Sum(s => s.Clicks),
                views = g.Sum(s => s.Views),
                sessions = g.Select(s => s.SessionId).Distinct().Count(),
                // Reading ratio (R7.1): < 0.2 skimmed, 0.2–0.8 scanned, > 0.8 read.
                readingRatio = ratio,
                reading = ratio is null ? null : ratio < 0.2 ? "skimmed" : ratio <= 0.8 ? "scanned" : "read",
            };
        }).OrderByDescending(a => a.visibleMs).ToList();

        // Technology intent (R7.2): what was filtered for and which tech badges were clicked.
        var techIntent = events.Where(e => e.Type == "tech_filter" && Payload(e)?["on"]?.GetValue<bool>() != false)
            .Select(e => Payload(e)?["tech"]?.GetValue<string>() ?? e.Anchor?.Split(':', 2)[1])
            .Concat(events.Where(e => e.Type == "click" && e.Anchor?.StartsWith("tech:") == true).Select(e => e.Anchor![5..]))
            .OfType<string>()
            .GroupBy(t => t, StringComparer.OrdinalIgnoreCase)
            .Select(g => new { tech = g.Key, count = g.Count() })
            .OrderByDescending(t => t.count).ToList();

        var score = sessions.Count == 0 ? new ScoreParts(0, 0, 0, 0, 0, 0) : await ScoreAsync(sessions, sectionCount, includeSpread: true, ct);
        return new
        {
            groupKey,
            score = new { total = score.Total, parts = score },
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
                };
            }).OrderByDescending(v => v.LastSeen),
            sessions = sessions.Select(SessionSummary),
            anchors,
            techIntent,
            actions = events.Where(e => e.Type is not ("visibility" or "click" or "session_end"))
                .GroupBy(e => e.Type).ToDictionary(g => g.Key, g => g.Count()),
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
        s.Fp, s.FpServer, s.AppSha, s.ApiSha, s.CvSourceSha, s.CvVersion, s.VersionMismatch,
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

    /// <summary>
    /// Heatmap cells for rendering (R6.8): move / click cells summed per anchor and 1 % cell, or attention per anchor.
    /// Also returns which breakpoints and versions have data, for the filters.
    /// </summary>
    public async Task<object> HeatmapAsync(string tenantId, string? group, string? bp, string? appSha, string? cvVersion, string type,
        CancellationToken ct)
    {
        var cells = db.HeatCells.Where(c => c.TenantId == tenantId);
        if (!string.IsNullOrEmpty(group)) cells = cells.Where(c => c.GroupKey == group);
        var facets = await cells.GroupBy(c => new { c.Breakpoint, c.AppSha, c.CvVersion })
            .Select(g => new { g.Key.Breakpoint, g.Key.AppSha, g.Key.CvVersion, weight = g.Sum(c => c.Weight) })
            .ToListAsync(ct);

        if (!string.IsNullOrEmpty(bp)) cells = cells.Where(c => c.Breakpoint == bp);
        if (!string.IsNullOrEmpty(appSha)) cells = cells.Where(c => c.AppSha == appSha);
        if (!string.IsNullOrEmpty(cvVersion)) cells = cells.Where(c => c.CvVersion == cvVersion);

        if (type == "attention")
        {
            var sessions = db.Sessions.Where(s => s.TenantId == tenantId);
            if (!string.IsNullOrEmpty(group)) sessions = sessions.Where(s => s.GroupKey == group);
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

    /// <summary>
    /// Interest score 0–100 (R7.5): active time, coverage, returns, detail seeking, contact/keep intent and (for groups)
    /// spread over several visitors. Always reported with its parts.
    /// </summary>
    public async Task<ScoreParts> ScoreAsync(IReadOnlyList<TrackSession> sessions, int knownSections, bool includeSpread, CancellationToken ct)
    {
        var ids = sessions.Select(s => s.Id).ToList();
        var seenSections = await db.SectionStats.Where(s => ids.Contains(s.SessionId) && s.Anchor.StartsWith("section:") && s.VisibleMs > 0)
            .Select(s => s.Anchor).Distinct().CountAsync(ct);
        var types = await db.Events.Where(e => ids.Contains(e.SessionId)).Select(e => e.Type).ToListAsync(ct);

        var activeMinutes = sessions.Sum(s => s.ActiveMs) / 60_000.0;
        var visits = sessions.Select(s => s.VisitId).Distinct().Count();
        var visitors = sessions.Select(s => s.VisitorId).Distinct().Count();
        var detail = types.Count(t => DetailEvents.Contains(t));
        return new ScoreParts(
            Time: Math.Round(25 * Math.Min(activeMinutes / 5, 1), 1),
            Coverage: Math.Round(20 * (knownSections == 0 ? 0 : Math.Min((double)seenSections / knownSections, 1)), 1),
            Returns: Math.Round(15 * Math.Min(visits - 1, 3) / 3.0, 1),
            Detail: Math.Round(15 * Math.Min(detail / 5.0, 1), 1),
            Intent: types.Any(t => IntentEvents.Contains(t)) ? 15 : 0,
            Spread: includeSpread ? Math.Round(10 * Math.Min(visitors - 1, 3) / 3.0, 1) : 0);
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
