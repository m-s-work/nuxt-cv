using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using CvApi.Tenants;

namespace CvApi.Tests;

public sealed class TemplateResolverTests
{
    private static TenantConfig Tenant(string? pdf, bool allowOverride = true) =>
        new() { Templates = new TemplateSelection { Pdf = pdf, Html = "web-default" }, AllowInviteTemplateOverride = allowOverride };

    private static AccessPolicy Policy(string? pdf) => new() { Templates = pdf is null ? null : new TemplateSelection { Pdf = pdf } };

    [Fact]
    public void Most_specific_level_wins()
    {
        Assert.Equal("tenant", TemplateResolver.Resolve(Tenant("tenant"), Policy(null), null).Pdf);
        Assert.Equal("profile", TemplateResolver.Resolve(Tenant("tenant"), Policy("profile"), null).Pdf);
        Assert.Equal("invite", TemplateResolver.Resolve(Tenant("tenant"), Policy("profile"), Policy("invite")).Pdf);
        Assert.Equal("preview", TemplateResolver.Resolve(Tenant("tenant"), Policy("profile"), Policy("invite"), "preview").Pdf);
        Assert.Equal("web-default", TemplateResolver.Resolve(Tenant("tenant"), null, null).Html);
        Assert.Null(TemplateResolver.Resolve(new TenantConfig(), null, null).Pdf);   // frontend default
    }

    [Fact]
    public void Tenant_can_forbid_invite_overrides()
    {
        Assert.Equal("profile", TemplateResolver.Resolve(Tenant("tenant", allowOverride: false), Policy("profile"), Policy("invite")).Pdf);
    }

    [Theory]
    [InlineData("../evil")]
    [InlineData("Upper")]
    [InlineData("with space")]
    public void Invalid_names_are_ignored(string name)
    {
        Assert.Equal("tenant", TemplateResolver.Resolve(Tenant("tenant"), Policy(name), null).Pdf);
    }
}

public sealed class TemplateApiTests : IDisposable
{
    private readonly PdfApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private HttpClient Admin()
    {
        var admin = _factory.ClientFor(ApiFactory.SharedHost);
        admin.DefaultRequestHeaders.Add("X-Admin-Key", ApiFactory.AdminKey);
        return admin;
    }

    private async Task<HttpClient> VisitorWithInvite(object body)
    {
        var created = await (await Admin().PostAsJsonAsync("/api/admin/tenants/alice/invites", body)).Content.ReadFromJsonAsync<JsonObject>();
        var visitor = _factory.ClientFor(ApiFactory.SharedHost);
        await visitor.PostAsJsonAsync("/api/access/redeem", new { code = created!["code"]!.GetValue<string>() });
        return visitor;
    }

    private static async Task<string?> PdfTemplate(HttpClient client) =>
        (await (await client.GetAsync("/api/cv")).Content.ReadFromJsonAsync<JsonObject>())!["templates"]?["pdf"]?.GetValue<string>();

    [Fact]
    public async Task Cv_response_contains_the_resolved_templates()
    {
        _factory.UpdateTenant("alice", t => t["templates"] = new JsonObject { ["pdf"] = "editorial" });

        Assert.Equal("editorial", await PdfTemplate(await VisitorWithInvite(new { profile = "full" })));
        Assert.Equal("classic", await PdfTemplate(await VisitorWithInvite(new { profile = "full", overrides = new { templates = new { pdf = "classic" } } })));
    }

    [Fact]
    public async Task Changing_the_template_rerenders_the_cached_pdf()
    {
        var visitor = await VisitorWithInvite(new { profile = "full" });
        Assert.Equal("hit", (await visitor.GetAsync("/api/pdf?locale=en")).Headers.GetValues("X-Pdf-Cache").Single());

        _factory.UpdateTenant("alice", t => t["templates"] = new JsonObject { ["pdf"] = "classic" });
        Assert.Equal("miss", (await visitor.GetAsync("/api/pdf?locale=en")).Headers.GetValues("X-Pdf-Cache").Single());
    }

    [Fact]
    public async Task Admin_pdf_preview_renders_the_requested_template()
    {
        var response = await Admin().GetAsync("/api/admin/tenants/alice/pdf-preview?profile=full&template=classic&locale=en");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);

        // The renderer's ticket grants exactly that template.
        var ticket = _factory.Renderer.Calls.Last().Cookies["cv_render"];
        var renderer = _factory.ClientFor("web");
        renderer.DefaultRequestHeaders.Add("Cookie", $"cv_render={ticket}");
        Assert.Equal("classic", await PdfTemplate(renderer));

        Assert.Equal(HttpStatusCode.BadRequest,
            (await Admin().GetAsync("/api/admin/tenants/alice/pdf-preview?profile=full&template=../x")).StatusCode);
    }
}
