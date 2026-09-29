using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using CvApi.Tenants;

namespace CvApi.Tests;

public sealed class FaviconRenderTests
{
    [Fact]
    public void Defaults_to_cv_braces_in_blue_on_dark()
    {
        var svg = Favicon.Svg(null);
        Assert.Contains("{cv}", svg);
        Assert.Contains("color=\"#60a5fa\"", svg);
        Assert.Contains("fill=\"#0f172a\"", svg);
    }

    [Fact]
    public void Uses_configured_symbol_and_colours()
    {
        var svg = Favicon.Svg(new FaviconConfig { Symbol = "terminal", Color = "#FFF", Background = "green" });
        Assert.Contains("&gt;_", svg);
        Assert.Contains("color=\"#fff\"", svg);
        Assert.Contains("fill=\"#00c16a\"", svg);
    }

    [Theory]
    [InlineData("red\" onload=\"alert(1)")]
    [InlineData("#12345")]
    [InlineData("url(#x)")]
    public void Invalid_colours_fall_back_to_the_default(string color)
    {
        var svg = Favicon.Svg(new FaviconConfig { Symbol = "nope", Color = color, Background = color });
        Assert.Equal(Favicon.Svg(null), svg);
    }

    [Fact]
    public void Every_symbol_renders_valid_xml()
    {
        foreach (var symbol in Favicon.Symbols.Keys)
            System.Xml.Linq.XDocument.Parse(Favicon.Svg(new FaviconConfig { Symbol = symbol }));
    }
}

public sealed class FaviconApiTests : IDisposable
{
    private readonly ApiFactory _factory = new();

    public FaviconApiTests() =>
        _factory.UpdateTenant("alice", t => t["favicon"] = new JsonObject { ["symbol"] = "lambda", ["color"] = "amber" });

    public void Dispose() => _factory.Dispose();

    private static async Task<string> Get(HttpClient client)
    {
        var response = await client.GetAsync("/api/favicon.svg");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/svg+xml", response.Content.Headers.ContentType?.MediaType);
        return await response.Content.ReadAsStringAsync();
    }

    [Fact]
    public async Task Tenant_host_gets_the_tenant_icon_without_access()
    {
        Assert.Contains("λ", await Get(_factory.ClientFor(ApiFactory.AliceHost)));
    }

    [Fact]
    public async Task Shared_host_gets_the_default_until_an_invite_is_redeemed()
    {
        var visitor = _factory.ClientFor(ApiFactory.SharedHost);
        Assert.Equal(Favicon.Svg(null), await Get(visitor));

        var code = await _factory.CreateInviteAsync("alice", new { profile = "full" });
        await visitor.PostAsJsonAsync("/api/access/redeem", new { code });
        Assert.Contains("λ", await Get(visitor));
    }

    [Fact]
    public async Task Admin_catalogue_draws_every_symbol_in_the_requested_colours()
    {
        var admin = _factory.ClientFor(ApiFactory.SharedHost);
        admin.DefaultRequestHeaders.Add("X-Admin-Key", ApiFactory.AdminKey);
        var catalogue = await admin.GetFromJsonAsync<JsonObject>("/api/admin/favicon?color=violet&background=%23ffffff");

        Assert.Equal("cv-braces", catalogue!["defaults"]!["symbol"]!.GetValue<string>());
        Assert.Equal("#a78bfa", catalogue["colors"]!["violet"]!.GetValue<string>());
        var symbols = catalogue["symbols"]!.AsArray();
        Assert.Equal(Favicon.Symbols.Count, symbols.Count);
        Assert.All(symbols, s => Assert.Contains("color=\"#a78bfa\"", s!["svg"]!.GetValue<string>()));
        Assert.All(symbols, s => Assert.Contains("fill=\"#ffffff\"", s!["svg"]!.GetValue<string>()));
    }

    [Fact]
    public async Task Admin_catalogue_needs_the_admin_key()
    {
        var response = await _factory.ClientFor(ApiFactory.SharedHost).GetAsync("/api/admin/favicon");
        Assert.False(response.IsSuccessStatusCode);
    }
}
