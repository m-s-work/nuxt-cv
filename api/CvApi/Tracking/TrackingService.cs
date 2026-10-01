using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CvApi.Access;
using CvApi.Tenants;
using CvApi.Versioning;
using Microsoft.EntityFrameworkCore;

namespace CvApi.Tracking;

public sealed record SessionStart(
    string? Locale, int? ViewportW, int? ViewportH, string? ColorScheme, string? Referrer, int? LocalHour, int? TzOffset,
    string? Language, string? AppSha, string? CvSourceSha, string? CvVersion, string? Fp, List<string>? FpParts);

/// <summary>A batch of events of one session (POST /api/events).</summary>
public sealed record EventBatch(string SessionId, int Seq, string? TabId, string? PreviousSessionId, string? Bp,
    SessionStart? Start, List<JsonElement>? Events);

public enum IngestResult { Stored, Ignored }

/// <summary>The server's own view of the CV of a new session: version, redacted JSON and locale (R6.10, R6.12).</summary>
public sealed record ServerCv(string Version, string Json, string Locale);

/// <summary>Consent handling and event ingest (docs/VISITOR_SESSION_TRACKING.md §3–§6, §8.1, §9.1).</summary>
public sealed partial class TrackingService(
    TrackingDbContext db,
    ConsentCookies cookies,
    GeoLookup geo,
    TimeProvider time)
{
    public const int MaxEventsPerBatch = 500;
    private const int MaxPayloadChars = 1024;

    private static readonly HashSet<string> RawEventTypes =
    [
        "visibility", "click", "tech_filter", "timeline", "lightbox", "expand", "link_out", "contact", "copy", "select",
        "pdf", "print", "locale_switch", "theme_switch", "rage_click", "dead_click", "session_end", "resume",
    ];

    private static readonly string[] Breakpoints = ["sm", "md", "lg", "xl"];

    // Striped locks: batches of one session are processed one after another.
    private static readonly SemaphoreSlim[] SessionLocks = Enumerable.Range(0, 64).Select(_ => new SemaphoreSlim(1, 1)).ToArray();

    [GeneratedRegex("^[A-Za-z0-9_-]{16,64}$")]
    public static partial Regex IdRegex();

    [GeneratedRegex("^[a-z][a-zA-Z]{0,31}:[^<>\"'\\u0000-\\u001f]{1,80}$")]
    public static partial Regex AnchorRegex();

    [GeneratedRegex("^[0-9a-f]{64}$")]
    private static partial Regex HashRegex();

    [GeneratedRegex("^[A-Za-z0-9._-]{1,64}$")]
    private static partial Regex ShortValueRegex();

    // ---------------------------------------------------------------------------------------------
    // Consent
    // ---------------------------------------------------------------------------------------------

    /// <summary>Records a choice (R9.10, R9.14) and sets the cookies. Declines are never linked to a visitor.</summary>
    public async Task RecordConsentAsync(HttpContext context, AccessGrant grant, TrackingDecision decision, string choice,
        string source, CancellationToken ct)
    {
        Guid? visitorId = null;
        if (choice == "accept")
        {
            var key = cookies.EnsureVisitorKey(context);
            visitorId = ConsentCookies.VisitorId(key, grant.Tenant.Id);
        }
        else
        {
            // Withdrawal also forgets the browser id (R9.19): later sessions start as a new visitor.
            if (choice == "withdraw" && cookies.ReadVisitorKey(context) is { } key)
                visitorId = ConsentCookies.VisitorId(key, grant.Tenant.Id);
            ConsentCookies.ClearVisitorKey(context);
        }
        cookies.Write(context, choice == "accept" ? "accept" : "decline", decision.PolicyVersion);

        db.Consents.Add(new ConsentRecord
        {
            TenantId = grant.Tenant.Id,
            InviteId = grant.Invite?.Id,
            GroupKey = TrackingPolicy.GroupKey(grant),
            VisitorId = choice == "decline" ? null : visitorId,
            Choice = choice,
            Source = source,
            Signals = Signals(context.Request),
            PolicyVersion = decision.PolicyVersion,
            CreatedAt = time.GetUtcNow(),
        });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Browser DNT / GPC signals counted as a decline (only when the tenant honours them, R9.8). Logged at most once per
    /// group and day, without anything about the visitor.
    /// </summary>
    public async Task RecordSignalDeclineAsync(HttpContext context, AccessGrant grant, TrackingDecision decision, CancellationToken ct)
    {
        var signals = Signals(context.Request);
        if (signals is null) return;
        var group = TrackingPolicy.GroupKey(grant);
        var since = time.GetUtcNow().AddDays(-1);
        var source = signals.Contains("gpc") ? "gpc" : "dnt";
        if (await db.Consents.AnyAsync(c => c.TenantId == grant.Tenant.Id && c.GroupKey == group && c.Source == source && c.CreatedAt > since, ct))
            return;
        db.Consents.Add(new ConsentRecord
        {
            TenantId = grant.Tenant.Id, InviteId = grant.Invite?.Id, GroupKey = group, Choice = "decline", Source = source,
            Signals = signals, PolicyVersion = decision.PolicyVersion, CreatedAt = time.GetUtcNow(),
        });
        await db.SaveChangesAsync(ct);
    }

    public static string? Signals(HttpRequest request)
    {
        var list = new List<string>();
        if (request.Headers["DNT"].ToString() == "1") list.Add("dnt");
        if (request.Headers["Sec-GPC"].ToString() == "1") list.Add("gpc");
        return list.Count == 0 ? null : string.Join(',', list);
    }

    // ---------------------------------------------------------------------------------------------
    // Ingest
    // ---------------------------------------------------------------------------------------------

    /// <summary>Stores a batch. Requires accepted consent for the current policy and a valid cv_vid (R8.1, R8.2).</summary>
    public async Task<IngestResult> IngestAsync(HttpContext context, AccessGrant grant, TrackingDecision decision, EventBatch batch,
        ServerCv? serverCv, string cvSourceSha, CancellationToken ct)
    {
        if (!decision.Enabled || cookies.State(context, decision.PolicyVersion) != "accept") return IngestResult.Ignored;
        if (cookies.ReadVisitorKey(context) is not { } browserKey) return IngestResult.Ignored;
        if (!IdRegex().IsMatch(batch.SessionId ?? "")) return IngestResult.Ignored;

        var tenantId = grant.Tenant.Id;
        var visitorId = ConsentCookies.VisitorId(browserKey, tenantId);
        var gate = SessionLocks[(batch.SessionId!.GetHashCode() & 0x7fffffff) % SessionLocks.Length];
        await gate.WaitAsync(ct);
        try
        {
            var now = time.GetUtcNow();
            var session = await db.Sessions.SingleOrDefaultAsync(s => s.Id == batch.SessionId, ct);
            if (session is null)
            {
                if (batch.Start is null) return IngestResult.Ignored;
                session = await CreateSessionAsync(context, grant, decision, batch, visitorId, serverCv, cvSourceSha, now, ct);
            }
            // A session belongs to one visitor and tenant; ids of other visitors cannot be written to.
            else if (session.VisitorId != visitorId || session.TenantId != tenantId)
            {
                return IngestResult.Ignored;
            }
            // Retry of a stored batch; batches arriving out of order (slow fetch after a beacon) are still applied (R8.3).
            if (!AcceptSeq(session, batch.Seq)) return IngestResult.Ignored;
            session.LastSeenAt = now;
            TrackIp(session, context.Connection.RemoteIpAddress, now);

            var visitor = db.Visitors.Local.FirstOrDefault(v => v.Id == visitorId) ?? await db.Visitors.SingleAsync(v => v.Id == visitorId, ct);
            visitor.LastSeen = now;
            var person = db.Persons.Local.FirstOrDefault(p => p.Id == visitor.PersonId)
                ?? await db.Persons.SingleOrDefaultAsync(p => p.Id == visitor.PersonId, ct);
            if (person is not null) person.LastSeen = now;

            await ApplyEventsAsync(session, batch, now, ct);
            await db.SaveChangesAsync(ct);
            return IngestResult.Stored;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<TrackSession> CreateSessionAsync(HttpContext context, AccessGrant grant, TrackingDecision decision, EventBatch batch,
        Guid visitorId, ServerCv? serverCv, string cvSourceSha, DateTimeOffset now, CancellationToken ct)
    {
        var start = batch.Start!;
        var tenantId = grant.Tenant.Id;
        var ip = context.Connection.RemoteIpAddress;
        var ipNet = Network(ip);
        var fp = start.Fp is { } f && HashRegex().IsMatch(f) ? f : null;
        var fpParts = start.FpParts?.Where(p => HashRegex().IsMatch(p)).Take(32).ToList();

        var visitor = await db.Visitors.SingleOrDefaultAsync(v => v.Id == visitorId, ct);
        if (visitor is null)
        {
            var ua = UserAgentInfo.Parse(context.Request.Headers.UserAgent.ToString());
            visitor = new Visitor
            {
                Id = visitorId, TenantId = tenantId, FirstSeen = now, LastSeen = now,
                Device = ua.Device, Browser = ua.Browser, Os = ua.Os,
                Language = Short(start.Language), TzOffset = start.TzOffset is { } tz ? Math.Clamp(tz, -900, 900) : null,
            };
            var linked = await FindPersonAsync(tenantId, visitorId, ipNet, fp, fpParts, decision.Retention, now, ct);
            if (linked is not null)
            {
                visitor.PersonId = linked.Value;
                visitor.PersonReason = "fingerprint";
            }
            else
            {
                var person = new Person { TenantId = tenantId, FirstSeen = now, LastSeen = now };
                db.Persons.Add(person);
                visitor.PersonId = person.Id;
            }
            db.Visitors.Add(visitor);
        }

        // Linked sessions (R4.7): the previous session of the same tab and visitor starts the visit.
        string visitId = batch.SessionId;
        if (batch.PreviousSessionId is { } prevId && IdRegex().IsMatch(prevId)
            && await db.Sessions.SingleOrDefaultAsync(s => s.Id == prevId && s.VisitorId == visitorId, ct) is { } previous)
        {
            visitId = previous.VisitId;
            previous.EndedAt ??= now;
        }

        // Heat data must be stored under a version with a CV snapshot (R6.12). When the client's version differs from
        // the server's (e.g. a CV deploy between /api/cv and the first batch), the client's version is kept only if a
        // snapshot of it already exists; otherwise the session is recorded under the server's version, which is
        // snapshotted below, and the client's version is kept for reference (R6.10).
        var clientCv = Short(start.CvVersion);
        var serverVersion = serverCv?.Version;
        var mismatch = clientCv is not null && clientCv != (serverVersion ?? "unknown");
        var cvVersion = !mismatch ? serverVersion ?? clientCv ?? "unknown"
            : serverVersion is null || await db.CvSnapshots.AnyAsync(s => s.TenantId == tenantId && s.CvVersion == clientCv, ct) ? clientCv!
            : serverVersion;
        var g = await geo.LookupAsync(ip, ct);
        var session = new TrackSession
        {
            Id = batch.SessionId,
            VisitorId = visitorId,
            TenantId = tenantId,
            GroupKey = TrackingPolicy.GroupKey(grant),
            InviteId = grant.Invite?.Id,
            Profile = grant.ProfileName,
            TabId = batch.TabId is { } tab && IdRegex().IsMatch(tab) ? tab : null,
            PreviousSessionId = visitId == batch.SessionId ? null : batch.PreviousSessionId,
            VisitId = visitId,
            StartedAt = now,
            LastSeenAt = now,
            Locale = Short(start.Locale),
            Breakpoint = Breakpoints.Contains(batch.Bp) ? batch.Bp : null,
            ViewportW = start.ViewportW is { } w ? Math.Clamp(w, 0, 20000) : null,
            ViewportH = start.ViewportH is { } h ? Math.Clamp(h, 0, 20000) : null,
            Referrer = start.Referrer is "direct" or "qr" or "link" ? start.Referrer : null,
            LocalHour = start.LocalHour is >= 0 and < 24 ? start.LocalHour : null,
            ColorScheme = start.ColorScheme is "dark" or "light" ? start.ColorScheme : null,
            Signals = Signals(context.Request),
            Ip = ip?.ToString(),
            IpNet = ipNet,
            IpCountry = g.Country, IpRegion = g.Region, IpCity = g.City, Asn = g.Asn, AsOrg = g.AsOrg,
            Fp = fp,
            FpPartsJson = fpParts is { Count: > 0 } ? JsonSerializer.Serialize(fpParts) : null,
            FpServer = ServerFingerprint(context.Request),
            AppSha = Short(start.AppSha) ?? "unknown",
            ApiSha = BuildInfo.Current.Commit,
            CvSourceSha = cvSourceSha,
            CvVersion = cvVersion,
            ClientCvVersion = clientCv != cvVersion ? clientCv : null,
            VersionMismatch = mismatch,
        };
        db.Sessions.Add(session);
        if (session.Ip is not null)
            db.SessionIps.Add(new SessionIp { SessionId = session.Id, Ip = session.Ip, FirstSeen = now, LastSeen = now });

        await SnapshotAsync(session, serverCv, now, ct);
        return session;
    }

    /// <summary>
    /// Stores the redacted CV of this session's version once (R6.12): the JSON the server computed the version from, so
    /// the snapshot always matches its key.
    /// </summary>
    private async Task SnapshotAsync(TrackSession session, ServerCv? serverCv, DateTimeOffset now, CancellationToken ct)
    {
        if (session.CvVersion is null) return;
        var existing = db.CvSnapshots.Local.FirstOrDefault(s => s.TenantId == session.TenantId && s.CvVersion == session.CvVersion)
            ?? await db.CvSnapshots.SingleOrDefaultAsync(s => s.TenantId == session.TenantId && s.CvVersion == session.CvVersion, ct);
        if (existing is not null)
        {
            existing.LastUsed = now;
            return;
        }
        if (serverCv is null || serverCv.Version != session.CvVersion) return;
        db.CvSnapshots.Add(new CvSnapshot
        {
            TenantId = session.TenantId, CvVersion = session.CvVersion, Locale = serverCv.Locale, CvSourceSha = session.CvSourceSha,
            Json = serverCv.Json, FirstSeen = now, LastUsed = now,
        });
    }

    /// <summary>Width of the window of batch numbers below the highest one that are still accepted.</summary>
    public const int SeqWindowSize = 64;

    /// <summary>
    /// Marks batch <paramref name="seq"/> as applied; false when it was applied before (retry) or is too old to tell.
    /// Batches may arrive out of order (a slow fetch after the page-hide beacon, R8.3), so every seq not applied yet is
    /// accepted within <see cref="SeqWindowSize"/> of the highest.
    /// </summary>
    public static bool AcceptSeq(TrackSession session, int seq)
    {
        if (seq < 0) return false;
        // Sessions stored before the window existed: everything up to LastSeq counts as applied.
        var window = session.SeqWindow is { } w ? unchecked((ulong)w) : session.LastSeq >= 0 ? ulong.MaxValue : 0UL;
        if (seq > session.LastSeq)
        {
            var shift = (long)seq - session.LastSeq;
            window = (shift >= SeqWindowSize ? 0UL : window << (int)shift) | 1UL;
            session.LastSeq = seq;
        }
        else
        {
            var back = session.LastSeq - seq;
            if (back >= SeqWindowSize) return false;
            var bit = 1UL << back;
            if ((window & bit) != 0) return false;
            window |= bit;
        }
        session.SeqWindow = unchecked((long)window);
        return true;
    }

    /// <summary>
    /// Probable person of a new visitor (R3.10): a session of another visitor in the same network whose fingerprint is
    /// equal or ≥ 90 % similar, still within the identifier retention.
    /// </summary>
    private async Task<Guid?> FindPersonAsync(string tenantId, Guid visitorId, string? ipNet, string? fp, List<string>? fpParts,
        Retention retention, DateTimeOffset now, CancellationToken ct)
    {
        if (ipNet is null || (fp is null && fpParts is not { Count: > 0 })) return null;
        var since = now.AddMonths(-retention.IdentifiersMonths);
        var candidates = await db.Sessions
            .Where(s => s.TenantId == tenantId && s.IpNet == ipNet && s.VisitorId != visitorId && s.Fp != null && s.LastSeenAt > since)
            .OrderByDescending(s => s.LastSeenAt).Take(50)
            .Select(s => new { s.VisitorId, s.Fp, s.FpPartsJson })
            .ToListAsync(ct);
        foreach (var c in candidates)
        {
            var match = (fp is not null && c.Fp == fp)
                || (fpParts is { Count: > 0 } && c.FpPartsJson is not null
                    && Similarity(fpParts, JsonSerializer.Deserialize<List<string>>(c.FpPartsJson) ?? []) >= 0.9);
            if (!match) continue;
            var personId = await db.Visitors.Where(v => v.Id == c.VisitorId).Select(v => (Guid?)v.PersonId).SingleOrDefaultAsync(ct);
            if (personId is not null) return personId;
        }
        return null;
    }

    /// <summary>Share of equal fingerprint components (same position = same trait).</summary>
    public static double Similarity(IReadOnlyList<string> a, IReadOnlyList<string> b)
    {
        var n = Math.Max(a.Count, b.Count);
        if (n == 0) return 0;
        var same = 0;
        for (var i = 0; i < Math.Min(a.Count, b.Count); i++)
            if (a[i] == b[i]) same++;
        return (double)same / n;
    }

    private void TrackIp(TrackSession session, IPAddress? ip, DateTimeOffset now)
    {
        var value = ip?.ToString();
        if (value is null || session.IpTruncated) return;
        var known = db.SessionIps.Local.FirstOrDefault(i => i.SessionId == session.Id && i.Ip == value)
            ?? db.SessionIps.FirstOrDefault(i => i.SessionId == session.Id && i.Ip == value);
        if (known is not null) known.LastSeen = now;
        else db.SessionIps.Add(new SessionIp { SessionId = session.Id, Ip = value, FirstSeen = now, LastSeen = now });
    }

    private async Task ApplyEventsAsync(TrackSession session, EventBatch batch, DateTimeOffset now, CancellationToken ct)
    {
        var events = batch.Events ?? [];
        if (events.Count > MaxEventsPerBatch) events = events.Take(MaxEventsPerBatch).ToList();
        var stats = new Dictionary<string, SectionStat>(StringComparer.Ordinal);
        var cells = new Dictionary<(string Type, string Anchor, int X, int Y), long>();

        async Task<SectionStat> Stat(string anchor)
        {
            if (stats.TryGetValue(anchor, out var s)) return s;
            s = await db.SectionStats.SingleOrDefaultAsync(x => x.SessionId == session.Id && x.Anchor == anchor, ct)
                ?? db.SectionStats.Add(new SectionStat { SessionId = session.Id, Anchor = anchor }).Entity;
            return stats[anchor] = s;
        }

        void Cell(string type, string anchor, double x, double y, long weight)
        {
            var key = (type, anchor, Math.Clamp((int)Math.Round(x), 0, 100), Math.Clamp((int)Math.Round(y), 0, 100));
            cells[key] = cells.GetValueOrDefault(key) + weight;
        }

        foreach (var e in events)
        {
            if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty("e", out var typeProp) || typeProp.ValueKind != JsonValueKind.String)
                continue;
            var type = typeProp.GetString()!;
            var t = Long(e, "t") ?? 0;
            var anchor = e.TryGetProperty("a", out var a) && a.ValueKind == JsonValueKind.String && AnchorRegex().IsMatch(a.GetString()!)
                ? a.GetString()
                : null;

            switch (type)
            {
                case "heartbeat":
                    session.VisibleMs += Math.Clamp(Long(e, "vis") ?? 0, 0, 60_000);
                    session.ActiveMs += Math.Clamp(Long(e, "act") ?? 0, 0, 60_000);
                    continue;
                case "scroll":
                    session.MaxScroll = Math.Max(session.MaxScroll, (int)Math.Clamp(Long(e, "d") ?? 0, 0, 100));
                    continue;
                case "section_view" when anchor is not null:
                {
                    var stat = await Stat(anchor);
                    stat.VisibleMs += Math.Clamp(Long(e, "ms") ?? 0, 0, 600_000);
                    stat.Views++;
                    continue;
                }
                case "hover" when anchor is not null:
                    (await Stat(anchor)).HoverMs += Math.Clamp(Long(e, "ms") ?? 0, 0, 600_000);
                    continue;
                case "pointer":
                    if (e.TryGetProperty("s", out var samples) && samples.ValueKind == JsonValueKind.Array)
                        foreach (var sample in samples.EnumerateArray().Take(2000))
                        {
                            if (sample.ValueKind != JsonValueKind.Array || sample.GetArrayLength() < 4) continue;
                            var sa = sample[0].ValueKind == JsonValueKind.String ? sample[0].GetString() : null;
                            if (sa is null || !AnchorRegex().IsMatch(sa) || !sample[1].TryGetDouble(out var sx)
                                || !sample[2].TryGetDouble(out var sy) || !sample[3].TryGetDouble(out var dt)) continue;
                            Cell("move", sa, sx, sy, (long)Math.Clamp(dt, 0, 2000));
                        }
                    continue;
                case "click" when anchor is not null:
                    (await Stat(anchor)).Clicks++;
                    if ((Double(e, "x"), Double(e, "y")) is ({ } cx, { } cy)) Cell("click", anchor, cx, cy, 1);
                    break;
                case "session_end":
                    session.EndedAt = now;
                    session.EndReason = e.TryGetProperty("reason", out var r) && r.ValueKind == JsonValueKind.String
                        && r.GetString() is "pagehide" or "timeout" or "resume_timeout" or "version_change" or "withdraw"
                        ? r.GetString()
                        : "pagehide";
                    break;
            }

            if (!RawEventTypes.Contains(type)) continue;
            db.Events.Add(new TrackEvent
            {
                SessionId = session.Id, Seq = batch.Seq, T = Math.Max(0, t), Type = type, Anchor = anchor, PayloadJson = Payload(e),
            });
        }

        // Heat cells use the layout of this batch: a resized window or rotated tablet changes it mid-session (R6.5).
        var breakpoint = Breakpoints.Contains(batch.Bp) ? batch.Bp! : session.Breakpoint;
        session.Breakpoint ??= breakpoint;
        if (cells.Count == 0 || breakpoint is null) return;
        var appSha = session.AppSha ?? "unknown";
        var cvVersion = session.CvVersion ?? "unknown";
        var anchors = cells.Keys.Select(k => k.Anchor).Distinct().ToList();
        var existing = await db.HeatCells
            .Where(c => c.TenantId == session.TenantId && c.GroupKey == session.GroupKey && c.Breakpoint == breakpoint
                        && c.AppSha == appSha && c.CvVersion == cvVersion && anchors.Contains(c.Anchor))
            .ToListAsync(ct);
        var byKey = existing.ToDictionary(c => (c.Type, c.Anchor, c.Cx, c.Cy));
        foreach (var (key, weight) in cells)
        {
            if (byKey.TryGetValue(key, out var cell))
            {
                cell.Weight += weight;
                cell.LastAt = now;
            }
            else
            {
                db.HeatCells.Add(new HeatCell
                {
                    TenantId = session.TenantId, GroupKey = session.GroupKey, Breakpoint = breakpoint, AppSha = appSha,
                    CvVersion = cvVersion, Type = key.Type, Anchor = key.Anchor, Cx = key.X, Cy = key.Y, Weight = weight, LastAt = now,
                });
            }
        }
    }

    /// <summary>Event fields except type/time/anchor, only simple values, capped in size. Never contains CV text (R5.1).</summary>
    private static string? Payload(JsonElement e)
    {
        var obj = new JsonObject();
        foreach (var p in e.EnumerateObject())
        {
            if (p.Name is "e" or "t" or "a" || p.Name.Length > 32) continue;
            JsonNode? value = p.Value.ValueKind switch
            {
                JsonValueKind.Number => JsonValue.Create(p.Value.GetDouble()),
                JsonValueKind.True or JsonValueKind.False => JsonValue.Create(p.Value.GetBoolean()),
                JsonValueKind.String when p.Value.GetString()!.Length <= 80 => JsonValue.Create(p.Value.GetString()),
                _ => null,
            };
            if (value is not null) obj[p.Name] = value;
        }
        var json = obj.ToJsonString();
        return obj.Count == 0 || json.Length > MaxPayloadChars ? null : json;
    }

    private static long? Long(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var d) && double.IsFinite(d)
            ? (long)Math.Clamp(d, long.MinValue / 2, long.MaxValue / 2)
            : null;

    private static double? Double(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var d) && double.IsFinite(d) ? d : null;

    private static string? Short(string? value) => value is not null && ShortValueRegex().IsMatch(value) ? value : null;

    /// <summary>Hash of request headers that describe the browser, incl. DNT / GPC (R3.9, R3.12).</summary>
    public static string ServerFingerprint(HttpRequest request)
    {
        var h = request.Headers;
        return Sha256.OfText(string.Join('\n', h.UserAgent.ToString(), h.AcceptLanguage.ToString(), h["Sec-CH-UA"].ToString(),
            h["Sec-CH-UA-Platform"].ToString(), h["Sec-CH-UA-Mobile"].ToString(), h["DNT"].ToString(), h["Sec-GPC"].ToString()));
    }

    /// <summary>/24 (IPv4) or /48 (IPv6) network of an address, e.g. "203.0.113.0/24".</summary>
    public static string? Network(IPAddress? ip)
    {
        if (ip is null) return null;
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        var bytes = ip.GetAddressBytes();
        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            bytes[3] = 0;
            return $"{new IPAddress(bytes)}/24";
        }
        for (var i = 6; i < bytes.Length; i++) bytes[i] = 0;
        return $"{new IPAddress(bytes)}/48";
    }

    /// <summary>Truncated form of a stored IP for the retention job (R9.2).</summary>
    public static string? Truncate(string? ip) =>
        IPAddress.TryParse(ip, out var address) ? Network(address) : null;
}
