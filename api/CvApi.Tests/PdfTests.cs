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
        // alice has an own host: the PDF's QR code points there, not to the internal render URL.
        Assert.Equal(
            ["http://web/de?print=1&qr=https%3A%2F%2Falice-cv.example.org%2Fde", "http://web/?print=1&qr=https%3A%2F%2Falice-cv.example.org%2F"],
            _factory.Renderer.Calls.Select(c => c.Url.OriginalString));
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
        Assert.Equal("hit", cached.Headers.GetValues("X-Pdf-Cache").Single());
        Assert.Equal(rendersAfterCreation, _factory.Renderer.Calls.Count);

        _factory.WriteCv("alice", "en", """{ "profile": { "name": "Alice v2" }, "experiences": [] }""");
        var stale = await client.GetAsync("/api/pdf?locale=en");
        Assert.Equal("miss", stale.Headers.GetValues("X-Pdf-Cache").Single());
        Assert.Equal(rendersAfterCreation + 1, _factory.Renderer.Calls.Count);

        Assert.Equal("hit", (await client.GetAsync("/api/pdf?locale=en")).Headers.GetValues("X-Pdf-Cache").Single());
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
    public async Task Cv_response_announces_pdf_feature()
    {
        var created = await CreateInvite(new { profile = "full" });
        var client = _factory.ClientFor(ApiFactory.SharedHost);
        await client.PostAsJsonAsync("/api/access/redeem", new { code = created["code"]!.GetValue<string>() });
        var body = await (await client.GetAsync("/api/cv")).Content.ReadFromJsonAsync<JsonObject>();
        Assert.True(body!["features"]!["pdf"]!.GetValue<bool>());
    }
}
