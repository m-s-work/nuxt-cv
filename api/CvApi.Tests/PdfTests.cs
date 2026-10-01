using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using CvApi.Pdf;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace CvApi.Tests;

/// <summary>Records render calls; can fail on demand.</summary>
public sealed class FakeRenderer : IPdfRenderer
{
    public List<(Uri Url, IReadOnlyDictionary<string, string> Cookies)> Calls { get; } = [];
    public bool Fail { get; set; }

    public Task<JsonObject?> VersionAsync(CancellationToken ct) =>
        Task.FromResult<JsonObject?>(new JsonObject { ["commit"] = "renderer-sha", ["builtAt"] = "2026-01-01T00:00:00Z" });

    public Task<byte[]> RenderAsync(Uri url, IReadOnlyDictionary<string, string> cookies, CancellationToken ct)
    {
        Calls.Add((url, cookies));
        if (Fail) throw new PdfRenderException("boom");
        return Task.FromResult(Encoding.ASCII.GetBytes($"%PDF-1.7 {url}"));
    }
}

public sealed class PdfApiFactory : ApiFactory
{
    public FakeRenderer Renderer { get; } = new();

    public PdfApiFactory() => WriteCv("alice", "de", """{ "profile": { "name": "Alice DE" }, "experiences": [] }""");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Pdf:RendererUrl", "http://renderer.test");
        builder.ConfigureTestServices(services => services.AddSingleton<IPdfRenderer>(Renderer));
    }
}

public sealed class PdfTests : IDisposable
{
    private readonly PdfApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private async Task<JsonObject> CreateInvite(object body)
    {
        var admin = _factory.ClientFor(ApiFactory.SharedHost);
        admin.DefaultRequestHeaders.Add("X-Admin-Key", ApiFactory.AdminKey);
        var response = await admin.PostAsJsonAsync("/api/admin/tenants/alice/invites", body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonObject>())!;
    }

    [Fact]
    public async Task Invite_creation_renders_every_locale_and_reports_success()
    {
        var created = await CreateInvite(new { profile = "full" });

        var outcomes = created["pdf"]!.AsArray();
        Assert.Equal(["de", "en"], outcomes.Select(o => o!["locale"]!.GetValue<string>()));
        Assert.All(outcomes, o => Assert.True(o!["ok"]!.GetValue<bool>()));
        // alice has an own host: the PDF's QR code points there (with a QR invite code), not to the internal render URL.
        var qrUrls = _factory.Renderer.Calls.Select(QrUrl).ToList();
        Assert.StartsWith("https://alice-cv.example.org/de?c=", qrUrls[0]);
        Assert.StartsWith("https://alice-cv.example.org/?c=", qrUrls[1]);
        Assert.Equal(["http://web/de/cv", "http://web/cv"], _factory.Renderer.Calls.Select(c => c.Url.GetLeftPart(UriPartial.Path)));
    }

    [Fact]
    public async Task Qr_code_of_a_tenant_without_own_host_points_to_cv_on_the_shared_host()
    {
        _factory.Settings["Cv:SharedBaseUrl"] = "https://cv.example.org/";
        _factory.SetHosts("alice");
        await CreateInvite(new { profile = "full" });

        // "/" is the showcase on the shared host, so the QR code opens /cv.
        var qrUrls = _factory.Renderer.Calls.Select(QrUrl).ToList();
        Assert.StartsWith("https://cv.example.org/de/cv?c=", qrUrls[0]);
        Assert.StartsWith("https://cv.example.org/cv?c=", qrUrls[1]);
    }

    [Fact]
    public async Task Invite_creation_reports_render_failures_immediately()
    {
        _factory.Renderer.Fail = true;
        var created = await CreateInvite(new { profile = "full" });

        Assert.NotNull(created["code"]);                                  // invite still exists
        Assert.All(created["pdf"]!.AsArray(), o =>
        {
            Assert.False(o!["ok"]!.GetValue<bool>());
            Assert.Equal("boom", o["error"]!.GetValue<string>());
        });
    }

    [Fact]
    public async Task Render_ticket_opens_exactly_the_invites_view_on_the_internal_host()
    {
        var created = await CreateInvite(new { profile = "full", overrides = new { flags = new { hideCompanies = true } } });
        var ticket = _factory.Renderer.Calls[0].Cookies["cv_render"];

        var renderer = _factory.ClientFor("web");
        renderer.DefaultRequestHeaders.Add("Cookie", $"cv_render={ticket}");
        var body = await (await renderer.GetAsync("/api/cv?locale=en")).Content.ReadFromJsonAsync<JsonObject>();

        Assert.Equal("full", body!["access"]!["profile"]!.GetValue<string>());
        Assert.Equal("Big corp", body["cv"]!["experiences"]![0]!["company"]!.GetValue<string>());

        // Revoking the invite also invalidates outstanding render tickets.
        var admin = _factory.ClientFor(ApiFactory.SharedHost);
        admin.DefaultRequestHeaders.Add("X-Admin-Key", ApiFactory.AdminKey);
        await admin.DeleteAsync($"/api/admin/tenants/alice/invites/{created["invite"]!["id"]}");
        Assert.Equal(HttpStatusCode.Forbidden, (await renderer.GetAsync("/api/cv")).StatusCode);
    }

    [Fact]
    public async Task Pdf_is_served_from_cache_and_rerendered_when_the_cv_changes()
    {
        var created = await CreateInvite(new { profile = "full" });
        var client = _factory.ClientFor(ApiFactory.SharedHost);
        await client.PostAsJsonAsync("/api/access/redeem", new { code = created["code"]!.GetValue<string>() });
        var rendersAfterCreation = _factory.Renderer.Calls.Count;

        var cached = await client.GetAsync("/api/pdf?locale=en");
        Assert.Equal(HttpStatusCode.OK, cached.StatusCode);
        Assert.Equal("application/pdf", cached.Content.Headers.ContentType?.MediaType);
        Assert.Equal("cv-alice-en.pdf", cached.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        Assert.Equal("hit", cached.Headers.GetValues("X-Pdf-Cache").Single());
        Assert.Equal(rendersAfterCreation, _factory.Renderer.Calls.Count);

        _factory.WriteCv("alice", "en", """{ "profile": { "name": "Alice v2" }, "experiences": [] }""");
        var stale = await client.GetAsync("/api/pdf?locale=en");
        Assert.Equal("miss", stale.Headers.GetValues("X-Pdf-Cache").Single());
        Assert.Equal(rendersAfterCreation + 1, _factory.Renderer.Calls.Count);

        Assert.Equal("hit", (await client.GetAsync("/api/pdf?locale=en")).Headers.GetValues("X-Pdf-Cache").Single());
    }

    [Fact]
    public async Task Cached_pdf_is_rerendered_when_the_qr_target_changes()
    {
        var created = await CreateInvite(new { profile = "full" });
        var client = _factory.ClientFor(ApiFactory.SharedHost);
        await client.PostAsJsonAsync("/api/access/redeem", new { code = created["code"]!.GetValue<string>() });
        Assert.Equal("hit", (await client.GetAsync("/api/pdf?locale=en")).Headers.GetValues("X-Pdf-Cache").Single());

        // Same CV, but the tenant moved to another host: the QR code in the cached PDF would be wrong.
        _factory.SetHosts("alice", "new-alice.example.org");
        _factory.Renderer.Calls.Clear();

        var response = await client.GetAsync("/api/pdf?locale=en");
        Assert.Equal("miss", response.Headers.GetValues("X-Pdf-Cache").Single());
        Assert.StartsWith("https://new-alice.example.org/?c=", QrUrl(_factory.Renderer.Calls.Single()));
    }

    [Fact]
    public async Task Pdf_requires_access_and_reports_failures()
    {
        Assert.Equal(HttpStatusCode.Forbidden, (await _factory.ClientFor(ApiFactory.SharedHost).GetAsync("/api/pdf")).StatusCode);

        var created = await CreateInvite(new { profile = "full" });
        var client = _factory.ClientFor(ApiFactory.SharedHost);
        await client.PostAsJsonAsync("/api/access/redeem", new { code = created["code"]!.GetValue<string>() });
        _factory.WriteCv("alice", "en", """{ "profile": { "name": "changed" } }""");
        _factory.Renderer.Fail = true;

        Assert.Equal(HttpStatusCode.BadGateway, (await client.GetAsync("/api/pdf?locale=en")).StatusCode);
    }

    [Fact]
    public async Task Website_links_in_the_pdf_are_tracked_per_pdf_without_redeeming_the_code()
    {
        const string site = "https://shop.example.com/demo";
        _factory.WriteCv("alice", "en", $$"""
            { "profile": { "name": "Alice" }, "experiences": [],
              "projects": [ { "id": 1, "name": "Shop", "startDate": "2021", "endDate": null, "url": "{{site}}" } ] }
            """);
        var created = await CreateInvite(new { profile = "full" });
        var call = _factory.Renderer.Calls.Last();
        var code = QrCode(call);
        var key = CvApi.Links.ExternalLinks.Key(site);

        // The renderer gets the tracked link base (public host, the PDF's QR code) …
        var go = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(call.Url.Query)["go"].ToString();
        Assert.Equal($"https://alice-cv.example.org/api/go/{{key}}?c={code}", go);
        // … and the original link to print as text.
        var renderer = _factory.ClientFor("web");
        renderer.DefaultRequestHeaders.Add("Cookie", $"cv_render={call.Cookies["cv_render"]}");
        var project = (await renderer.GetFromJsonAsync<JsonObject>("/api/cv?locale=en"))!["cv"]!["projects"]![0]!;
        Assert.Equal(site, project["urlTarget"]!.GetValue<string>());
        Assert.Equal($"/api/go/{key}", project["url"]!.GetValue<string>());

        // Visitors of the web page never get the original link.
        var visitor = _factory.ClientFor(ApiFactory.SharedHost);
        await visitor.PostAsJsonAsync("/api/access/redeem", new { code = created["code"]!.GetValue<string>() });
        Assert.Null((await visitor.GetFromJsonAsync<JsonObject>("/api/cv?locale=en"))!["cv"]!["projects"]![0]!["urlTarget"]);

        // A click from the printed PDF: no cookie, redirect, counted for that PDF, the code is not redeemed.
        var reader = _factory.CreateClient(new() { BaseAddress = new Uri("http://alice-cv.example.org"), AllowAutoRedirect = false, HandleCookies = false });
        for (var i = 0; i < 2; i++)
        {
            var response = await reader.GetAsync($"/api/go/{key}?c={code}");
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal(site, response.Headers.Location!.ToString());
            Assert.False(response.Headers.Contains("Set-Cookie"));
        }
        Assert.Equal(HttpStatusCode.NotFound, (await reader.GetAsync($"/api/go/{CvApi.Links.ExternalLinks.Key("https://elsewhere.example.com/")}?c={code}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await reader.GetAsync($"/api/go/{key}?c=wrong-code")).StatusCode);

        var admin = _factory.ClientFor(ApiFactory.SharedHost);
        admin.DefaultRequestHeaders.Add("X-Admin-Key", ApiFactory.AdminKey);
        var qrInvite = (await admin.GetFromJsonAsync<JsonArray>("/api/admin/tenants/alice/invites"))!
            .Single(i => i!["source"]?.GetValue<string>() == "pdf-qr")!;
        Assert.Equal(0, qrInvite["useCount"]!.GetValue<int>());
        var clicks = Assert.Single(qrInvite["linkClicks"]!.AsArray())!;
        Assert.Equal(site, clicks["url"]!.GetValue<string>());
        Assert.Equal(2, clicks["count"]!.GetValue<int>());

        // Revoking the invite also ends its printed links.
        await admin.DeleteAsync($"/api/admin/tenants/alice/invites/{created["invite"]!["id"]}");
        Assert.Equal(HttpStatusCode.NotFound, (await reader.GetAsync($"/api/go/{key}?c={code}")).StatusCode);
    }

    [Fact]
    public async Task Pdf_of_a_view_once_invite_has_no_tracked_links()
    {
        await CreateInvite(new { profile = "full", viewOnce = true });
        Assert.All(_factory.Renderer.Calls, call => Assert.DoesNotContain("go=", call.Url.Query));
    }

    private static string QrUrl((Uri Url, IReadOnlyDictionary<string, string> Cookies) call) =>
        Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(call.Url.Query)["qr"].ToString();

    private static string QrCode((Uri Url, IReadOnlyDictionary<string, string> Cookies) call) =>
        QrUrl(call).Split("?c=")[1];

    [Fact]
    public async Task Pdf_of_a_view_once_invite_has_no_reusable_qr_code()
    {
        await CreateInvite(new { profile = "full", viewOnce = true });

        Assert.All(_factory.Renderer.Calls, call => Assert.DoesNotContain("?c=", QrUrl(call)));
        var admin = _factory.ClientFor(ApiFactory.SharedHost);
        admin.DefaultRequestHeaders.Add("X-Admin-Key", ApiFactory.AdminKey);
        var invites = (await admin.GetFromJsonAsync<JsonArray>("/api/admin/tenants/alice/invites"))!;
        Assert.Equal(30, Assert.Single(invites)!["viewOnceMinutes"]!.GetValue<int>());
    }

    [Fact]
    public async Task Turning_view_once_on_later_revokes_the_qr_invite()
    {
        var created = await CreateInvite(new { profile = "full" });
        var qrCode = QrCode(_factory.Renderer.Calls[0]);
        var admin = _factory.ClientFor(ApiFactory.SharedHost);
        admin.DefaultRequestHeaders.Add("X-Admin-Key", ApiFactory.AdminKey);

        var response = await admin.PutAsJsonAsync($"/api/admin/tenants/alice/invites/{created["invite"]!["id"]}/settings", new { viewOnceMinutes = 30 });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await _factory.ClientFor(ApiFactory.SharedHost).PostAsJsonAsync("/api/access/redeem", new { code = qrCode })).StatusCode);
    }

    [Fact]
    public async Task Qr_code_in_pdf_is_a_linked_invite_with_the_same_view()
    {
        var created = await CreateInvite(new { profile = "full", label = "ACME", overrides = new { flags = new { hideCompanies = true } } });
        var parentCode = created["code"]!.GetValue<string>();
        var qrCode = QrCode(_factory.Renderer.Calls[0]);

        Assert.NotEqual(parentCode, qrCode);
        Assert.Equal(qrCode, QrCode(_factory.Renderer.Calls[1]));        // same code for every locale / re-render

        // Scanning the QR code opens the same view as the parent invite.
        var scanner = _factory.ClientFor(ApiFactory.AliceHost);
        Assert.Equal(HttpStatusCode.NoContent, (await scanner.PostAsJsonAsync("/api/access/redeem", new { code = qrCode })).StatusCode);
        var body = await (await scanner.GetAsync("/api/cv")).Content.ReadFromJsonAsync<JsonObject>();
        Assert.Equal("full", body!["access"]!["profile"]!.GetValue<string>());
        Assert.Equal("Big corp", body["cv"]!["experiences"]![0]!["company"]!.GetValue<string>());
        Assert.DoesNotContain("pdf-qr", body.ToJsonString());             // origin marker is owner-only

        // The owner sees the linked invite and its usage.
        var admin = _factory.ClientFor(ApiFactory.SharedHost);
        admin.DefaultRequestHeaders.Add("X-Admin-Key", ApiFactory.AdminKey);
        var invites = (await admin.GetFromJsonAsync<JsonArray>("/api/admin/tenants/alice/invites"))!;
        var qrInvite = invites.Single(i => i!["source"]?.GetValue<string>() == "pdf-qr")!;
        Assert.Equal(created["invite"]!["id"]!.GetValue<string>(), qrInvite["parentId"]!.GetValue<string>());
        Assert.Equal(1, qrInvite["useCount"]!.GetValue<int>());

        // Revoking the parent revokes the QR invite as well.
        await admin.DeleteAsync($"/api/admin/tenants/alice/invites/{created["invite"]!["id"]}");
        Assert.Equal(HttpStatusCode.Forbidden, (await scanner.GetAsync("/api/cv")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await _factory.ClientFor(ApiFactory.SharedHost).PostAsJsonAsync("/api/access/redeem", new { code = qrCode })).StatusCode);
    }

    [Fact]
    public async Task Pdf_of_the_qr_invite_reuses_its_own_code()
    {
        await CreateInvite(new { profile = "full" });
        var qrCode = QrCode(_factory.Renderer.Calls[0]);
        var scanner = _factory.ClientFor(ApiFactory.SharedHost);
        await scanner.PostAsJsonAsync("/api/access/redeem", new { code = qrCode });
        _factory.Renderer.Calls.Clear();

        (await scanner.GetAsync("/api/pdf?locale=en")).EnsureSuccessStatusCode();
        Assert.Equal(qrCode, QrCode(_factory.Renderer.Calls.Single()));   // no QR-of-QR chain
    }

    [Fact]
    public async Task Cv_response_announces_pdf_feature()
    {
        var created = await CreateInvite(new { profile = "full" });
        var client = _factory.ClientFor(ApiFactory.SharedHost);
        await client.PostAsJsonAsync("/api/access/redeem", new { code = created["code"]!.GetValue<string>() });
        var body = await (await client.GetAsync("/api/cv")).Content.ReadFromJsonAsync<JsonObject>();
        Assert.True(body!["features"]!["pdf"]!.GetValue<bool>());
    }
}
