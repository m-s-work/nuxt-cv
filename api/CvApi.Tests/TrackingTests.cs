using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using CvApi.Tenants;
using CvApi.Tracking;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CvApi.Tests;

public sealed class TrackingTests : IDisposable
{
    private readonly ApiFactory _factory = new();

    public TrackingTests()
    {
        _factory.UpdateTenant("alice", t => t["privacy"] = new JsonObject { ["controller"] = "Alice", ["contact"] = "privacy@alice.test" });
    }

    public void Dispose() => _factory.Dispose();

    private static readonly string Fp = new('a', 64);

    private async Task<HttpClient> InvitedClient(object? overrides = null, string profile = "full", string ip = "203.0.113.7")
    {
        var code = await _factory.CreateInviteAsync("alice", new { profile, label = "ACME", overrides });
        var client = _factory.ClientFor(ApiFactory.SharedHost);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", ip);
        (await client.PostAsJsonAsync("/api/access/redeem", new { code })).EnsureSuccessStatusCode();
        return client;
    }

    private static async Task<JsonObject> Cv(HttpClient client) =>
        (await client.GetFromJsonAsync<JsonObject>("/api/cv"))!;

    private static async Task Accept(HttpClient client)
    {
        var consent = (await Cv(client))["consent"]!;
        var response = await client.PostAsJsonAsync("/api/consent",
            new { choice = "accept", policyVersion = consent["policyVersion"]!.GetValue<string>() });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private static object Start(string cvVersion, string? fp = null, List<string>? parts = null) => new
    {
        locale = "en", viewportW = 1280, viewportH = 800, colorScheme = "dark", referrer = "direct", localHour = 10,
        language = "de-AT", appSha = "abc1234", cvVersion, fp = fp ?? Fp, fpParts = parts ?? [Fp, Fp],
    };

    private static async Task Send(HttpClient client, object batch) =>
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/events", batch)).StatusCode);

    private T Db<T>(Func<TrackingDbContext, T> query)
    {
        using var scope = _factory.Services.CreateScope();
        return query(scope.ServiceProvider.GetRequiredService<TrackingDbContext>());
    }

    [Fact]
    public async Task No_modal_without_privacy_controller()
    {
        _factory.UpdateTenant("alice", t => t.Remove("privacy"));
        var cv = await Cv(await InvitedClient());
        Assert.False(cv["consent"]!["required"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Modal_is_required_and_shows_controller_and_versions()
    {
        var cv = await Cv(await InvitedClient());
        var consent = cv["consent"]!;
        Assert.True(consent["required"]!.GetValue<bool>());
        Assert.Null(consent["state"]);
        Assert.Equal("Alice", consent["controller"]!.GetValue<string>());
        Assert.Equal(13, consent["retention"]!["identifiersMonths"]!.GetValue<int>());
        Assert.Equal(16, cv["cvVersion"]!.GetValue<string>().Length);
        Assert.Equal("unversioned", cv["cvSourceSha"]!.GetValue<string>());
    }

    [Theory]
    // invite > profile > tenant; most specific wins.
    [InlineData(null, null, null, true)]
    [InlineData(false, null, null, false)]
    [InlineData(false, true, null, true)]
    [InlineData(null, false, null, false)]
    [InlineData(null, false, true, true)]
    [InlineData(true, true, false, false)]
    public async Task Tracking_switch_is_inherited(bool? tenant, bool? profile, bool? invite, bool expected)
    {
        _factory.UpdateTenant("alice", t =>
        {
            if (tenant is { } te) t["tracking"] = new JsonObject { ["enabled"] = te };
            if (profile is { } pe) t["profiles"]!["full"]!["tracking"] = new JsonObject { ["enabled"] = pe };
        });
        var client = await InvitedClient(invite is { } ie ? new { tracking = new { enabled = ie } } : null);
        Assert.Equal(expected, (await Cv(client))["consent"]!["required"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Accept_sets_visitor_cookie_and_is_logged()
    {
        var client = await InvitedClient();
        await Accept(client);
        Assert.Equal("accept", (await Cv(client))["consent"]!["state"]!.GetValue<string>());
        var record = Db(db => db.Consents.Single());
        Assert.Equal("accept", record.Choice);
        Assert.NotNull(record.VisitorId);
    }

    [Fact]
    public async Task Decline_is_logged_without_visitor_and_nothing_is_tracked()
    {
        var client = await InvitedClient();
        var cv = await Cv(client);
        await client.PostAsJsonAsync("/api/consent", new { choice = "decline", policyVersion = cv["consent"]!["policyVersion"]!.GetValue<string>() });
        Assert.Equal("decline", (await Cv(client))["consent"]!["state"]!.GetValue<string>());

        await Send(client, new { sessionId = "s-decline-000000001", seq = 0, bp = "lg", start = Start(cv["cvVersion"]!.GetValue<string>()), events = Array.Empty<object>() });

        var record = Db(db => db.Consents.Single());
        Assert.Equal("decline", record.Choice);
        Assert.Null(record.VisitorId);
        Assert.Equal(0, Db(db => db.Sessions.Count()));
        Assert.Equal(0, Db(db => db.Visitors.Count()));
    }

    [Fact]
    public async Task Outdated_policy_version_is_rejected()
    {
        var client = await InvitedClient();
        var response = await client.PostAsJsonAsync("/api/consent", new { choice = "accept", policyVersion = "outdated" });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Events_are_stored_after_accept()
    {
        var client = await InvitedClient();
        await Accept(client);
        var cvVersion = (await Cv(client))["cvVersion"]!.GetValue<string>();

        await Send(client, new
        {
            sessionId = "s-main-00000000001", seq = 0, tabId = "tab-0000000000000001", bp = "lg", start = Start(cvVersion),
            events = new object[]
            {
                new { e = "heartbeat", t = 15000, vis = 15000, act = 12000 },
                new { e = "section_view", t = 15000, a = "section:experiences", ms = 9000 },
                new { e = "section_view", t = 15000, a = "experience:1", ms = 4000 },
                new { e = "click", t = 16000, a = "experience:1", x = 40.2, y = 10, k = "text" },
                new { e = "pointer", t = 16000, s = new object[] { new object[] { "experience:1", 42, 17, 100 }, new object[] { "experience:1", 42, 17, 100 } } },
                new { e = "tech_filter", t = 17000, a = "tech:C#", tech = "C#", on = true },
                new { e = "pdf", t = 18000, locale = "en" },
                new { e = "scroll", t = 18000, d = 64 },
            },
        });
        // Retry of the same batch is ignored.
        await Send(client, new { sessionId = "s-main-00000000001", seq = 0, bp = "lg", events = new object[] { new { e = "heartbeat", vis = 15000, act = 12000 } } });

        var session = Db(db => db.Sessions.Single());
        Assert.Equal("203.0.113.7", session.Ip);
        Assert.Equal("203.0.113.0/24", session.IpNet);
        Assert.Equal(Fp, session.Fp);
        Assert.Equal(cvVersion, session.CvVersion);
        Assert.False(session.VersionMismatch);
        Assert.Equal("abc1234", session.AppSha);
        Assert.Equal(12000, session.ActiveMs);
        Assert.Equal(64, session.MaxScroll);
        Assert.Equal(1, Db(db => db.CvSnapshots.Count()));
        Assert.Equal(200, Db(db => db.HeatCells.Where(c => c.Type == "move").Sum(c => c.Weight)));
        Assert.Equal(1, Db(db => db.HeatCells.Count(c => c.Type == "click")));
        Assert.Equal(["click", "pdf", "tech_filter"], Db(db => db.Events.Select(e => e.Type).OrderBy(t => t).ToList()));

        // Owner reports.
        var admin = _factory.ClientFor(ApiFactory.SharedHost);
        admin.DefaultRequestHeaders.Add("X-Admin-Key", ApiFactory.AdminKey);
        var groups = (await admin.GetFromJsonAsync<JsonArray>("/api/admin/tenants/alice/analytics/groups"))!;
        var group = Assert.Single(groups)!;
        Assert.Equal("ACME", group["label"]!.GetValue<string>());
        Assert.Equal(1, group["sessions"]!.GetValue<int>());
        Assert.Equal(1, group["consent"]!["accept"]!.GetValue<int>());
        Assert.True(group["score"]!.GetValue<int>() > 0);

        var detail = (await admin.GetFromJsonAsync<JsonObject>($"/api/admin/tenants/alice/analytics/groups/{group["groupKey"]}"))!;
        Assert.Equal("C#", detail["techIntent"]![0]!["tech"]!.GetValue<string>());
        Assert.Equal("ACME", detail["anchors"]!.AsArray().First(a => a!["anchor"]!.GetValue<string>() == "experience:1")!["label"]!.GetValue<string>());

        var heat = (await admin.GetFromJsonAsync<JsonObject>($"/api/admin/tenants/alice/analytics/heatmap?group={group["groupKey"]}&bp=lg"))!;
        Assert.Equal(200, heat["cells"]![0]!["w"]!.GetValue<int>());
        var snapshot = (await admin.GetFromJsonAsync<JsonObject>($"/api/admin/tenants/alice/analytics/cv-snapshots/{cvVersion}"))!;
        Assert.Equal("ACME", snapshot["cv"]!["experiences"]![0]!["company"]!.GetValue<string>());

        var sessionDetail = (await admin.GetFromJsonAsync<JsonObject>("/api/admin/tenants/alice/analytics/sessions/s-main-00000000001"))!;
        Assert.Equal(3, sessionDetail["events"]!.AsArray().Count);
    }

    [Fact]
    public async Task Sessions_of_other_visitors_cannot_be_written()
    {
        var alice = await InvitedClient();
        await Accept(alice);
        var cvVersion = (await Cv(alice))["cvVersion"]!.GetValue<string>();
        await Send(alice, new { sessionId = "s-shared-0000000001", seq = 0, bp = "lg", start = Start(cvVersion), events = Array.Empty<object>() });

        var other = await InvitedClient();
        await Accept(other);
        await Send(other, new { sessionId = "s-shared-0000000001", seq = 5, bp = "lg", events = new object[] { new { e = "heartbeat", vis = 1000, act = 1000 } } });

        Assert.Equal(0, Db(db => db.Sessions.Single().ActiveMs));
    }

    [Fact]
    public async Task Split_sessions_are_linked_into_one_visit()
    {
        var client = await InvitedClient();
        await Accept(client);
        var cvVersion = (await Cv(client))["cvVersion"]!.GetValue<string>();
        await Send(client, new { sessionId = "s-first-00000000001", seq = 0, bp = "lg", start = Start(cvVersion), events = Array.Empty<object>() });
        await Send(client, new
        {
            sessionId = "s-second-0000000001", seq = 0, bp = "lg", previousSessionId = "s-first-00000000001",
            start = Start(cvVersion), events = Array.Empty<object>(),
        });

        var sessions = Db(db => db.Sessions.OrderBy(s => s.StartedAt).ToList());
        Assert.Equal(2, sessions.Count);
        Assert.Equal("s-first-00000000001", sessions[1].VisitId);
        Assert.Equal(sessions[0].VisitId, sessions[1].VisitId);
    }

    [Fact]
    public async Task Same_fingerprint_on_same_network_links_to_one_person()
    {
        var first = await InvitedClient(ip: "198.51.100.10");
        await Accept(first);
        var cvVersion = (await Cv(first))["cvVersion"]!.GetValue<string>();
        await Send(first, new { sessionId = "s-person-0000000001", seq = 0, bp = "lg", start = Start(cvVersion), events = Array.Empty<object>() });

        // Cookie lost (new browser profile), same device and network.
        var second = await InvitedClient(ip: "198.51.100.77");
        await Accept(second);
        await Send(second, new { sessionId = "s-person-0000000002", seq = 0, bp = "lg", start = Start(cvVersion), events = Array.Empty<object>() });

        // Other network: not linked.
        var third = await InvitedClient(ip: "192.0.2.1");
        await Accept(third);
        await Send(third, new { sessionId = "s-person-0000000003", seq = 0, bp = "lg", start = Start(cvVersion), events = Array.Empty<object>() });

        var visitors = Db(db => db.Visitors.ToList());
        Assert.Equal(3, visitors.Count);
        Assert.Equal(2, visitors.Select(v => v.PersonId).Distinct().Count());
        Assert.Single(visitors, v => v.PersonReason == "fingerprint");
    }

    [Fact]
    public async Task Retention_truncates_identifiers_then_deletes()
    {
        var client = await InvitedClient();
        await Accept(client);
        var cvVersion = (await Cv(client))["cvVersion"]!.GetValue<string>();
        await Send(client, new { sessionId = "s-retain-0000000001", seq = 0, bp = "lg", start = Start(cvVersion),
            events = new object[] { new { e = "pdf", t = 1 } } });

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TrackingDbContext>();
            var tenants = scope.ServiceProvider.GetRequiredService<TenantStore>();
            await RetentionService.RunAsync(db, tenants, DateTimeOffset.UtcNow.AddMonths(14), default);
        }
        var session = Db(db => db.Sessions.Single());
        Assert.Equal("203.0.113.0/24", session.Ip);
        Assert.True(session.IpTruncated);
        Assert.Null(session.Fp);
        Assert.Equal(0, Db(db => db.Events.Count()));

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TrackingDbContext>();
            var tenants = scope.ServiceProvider.GetRequiredService<TenantStore>();
            await RetentionService.RunAsync(db, tenants, DateTimeOffset.UtcNow.AddMonths(26), default);
        }
        Assert.Equal(0, Db(db => db.Sessions.Count()));
        Assert.Equal(0, Db(db => db.Visitors.Count()));
        Assert.Equal(0, Db(db => db.CvSnapshots.Count()));
    }

    [Fact]
    public async Task Browser_signals_do_not_block_the_modal_unless_honoured()
    {
        var client = await InvitedClient();
        client.DefaultRequestHeaders.Add("Sec-GPC", "1");
        var consent = (await Cv(client))["consent"]!;
        Assert.Null(consent["state"]);
        Assert.Equal("gpc", consent["signals"]!.GetValue<string>());

        _factory.UpdateTenant("alice", t => t["tracking"] = new JsonObject { ["honorBrowserSignals"] = true });
        Assert.Equal("decline", (await Cv(client))["consent"]!["state"]!.GetValue<string>());
        await Cv(client);
        Assert.Equal("gpc", Db(db => db.Consents.Single()).Source); // logged once per day
    }

    [Fact]
    public async Task Withdraw_stops_tracking_and_erase_deletes_the_visitor()
    {
        var client = await InvitedClient();
        await Accept(client);
        var cvVersion = (await Cv(client))["cvVersion"]!.GetValue<string>();
        await Send(client, new { sessionId = "s-erase-00000000001", seq = 0, bp = "lg", start = Start(cvVersion), events = Array.Empty<object>() });

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/consent")).StatusCode);
        Assert.Equal("decline", (await Cv(client))["consent"]!["state"]!.GetValue<string>());

        var admin = _factory.ClientFor(ApiFactory.SharedHost);
        admin.DefaultRequestHeaders.Add("X-Admin-Key", ApiFactory.AdminKey);
        var visitorId = Db(db => db.Visitors.Single().Id);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/admin/tenants/alice/analytics/visitors/{visitorId}")).StatusCode);
        Assert.Equal(0, Db(db => db.Sessions.Count()));
        Assert.All(Db(db => db.Consents.ToList()), c => Assert.Null(c.VisitorId));
    }

    [Theory]
    [InlineData("notice")]
    [InlineData("prior")]
    public async Task Implied_consent_tracks_without_modal_and_can_be_withdrawn(string mode)
    {
        var client = await InvitedClient(new { tracking = new { consent = mode, consentNote = "Agreed on LinkedIn, 2026-09-01" } });
        var cv = await Cv(client);
        var consent = cv["consent"]!;
        Assert.Equal(mode, consent["mode"]!.GetValue<string>());
        Assert.Equal("accept", consent["state"]!.GetValue<string>());
        Assert.True(consent["impliedNow"]!.GetValue<bool>());
        Assert.DoesNotContain("LinkedIn", cv.ToJsonString());           // the note is owner-only

        // Second visit: remembered, logged only once.
        Assert.False((await Cv(client))["consent"]!["impliedNow"]!.GetValue<bool>());
        var record = Db(db => db.Consents.Single());
        Assert.Equal(mode, record.Source);
        Assert.NotNull(record.VisitorId);

        await Send(client, new { sessionId = "s-implied-000000001", seq = 0, bp = "lg", start = Start(cv["cvVersion"]!.GetValue<string>()), events = Array.Empty<object>() });
        Assert.Equal(1, Db(db => db.Sessions.Count()));

        await client.DeleteAsync("/api/consent");
        Assert.Equal("decline", (await Cv(client))["consent"]!["state"]!.GetValue<string>());
    }

    [Fact]
    public async Task Notice_mode_honours_browser_signals_prior_mode_does_not()
    {
        var notice = await InvitedClient(new { tracking = new { consent = "notice" } });
        notice.DefaultRequestHeaders.Add("Sec-GPC", "1");
        Assert.Equal("decline", (await Cv(notice))["consent"]!["state"]!.GetValue<string>());

        var prior = await InvitedClient(new { tracking = new { consent = "prior" } });
        prior.DefaultRequestHeaders.Add("Sec-GPC", "1");
        Assert.Equal("accept", (await Cv(prior))["consent"]!["state"]!.GetValue<string>());
    }

    [Fact]
    public async Task Consent_mode_is_inherited_and_an_earlier_decline_is_kept()
    {
        _factory.UpdateTenant("alice", t => t["tracking"] = new JsonObject { ["consent"] = "notice" });
        var client = await InvitedClient(new { tracking = new { consent = "modal" } });
        var consent = (await Cv(client))["consent"]!;
        Assert.Equal("modal", consent["mode"]!.GetValue<string>());
        Assert.Null(consent["state"]);
        await client.PostAsJsonAsync("/api/consent", new { choice = "decline", policyVersion = consent["policyVersion"]!.GetValue<string>() });

        var profileLevel = await InvitedClient();                        // tenant default: notice
        Assert.Equal("notice", (await Cv(profileLevel))["consent"]!["mode"]!.GetValue<string>());

        // Same browser, profile switched to "prior" meanwhile: the earlier decline stays.
        _factory.UpdateTenant("alice", t => t["profiles"]!["full"]!["tracking"] = new JsonObject { ["consent"] = "prior" });
        Assert.Equal("decline", (await Cv(client))["consent"]!["state"]!.GetValue<string>());
    }

    [Fact]
    public void Fingerprint_similarity_and_networks()
    {
        Assert.Equal(0.5, TrackingService.Similarity(["a", "b"], ["a", "c"]));
        Assert.Equal("2001:db8:1::/48", TrackingService.Network(IPAddress.Parse("2001:db8:1:2::5")));
        Assert.Equal("10.1.2.0/24", TrackingService.Truncate("10.1.2.3"));
        Assert.Equal(new UserAgentInfo("mobile", "Safari", "iOS"),
            UserAgentInfo.Parse("Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1"));
    }
}
