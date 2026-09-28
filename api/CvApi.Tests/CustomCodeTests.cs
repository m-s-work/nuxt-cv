using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace CvApi.Tests;

public sealed class CustomCodeTests : IDisposable
{
    private readonly ApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private HttpClient Admin()
    {
        var admin = _factory.ClientFor(ApiFactory.SharedHost);
        admin.DefaultRequestHeaders.Add("X-Admin-Key", ApiFactory.AdminKey);
        return admin;
    }

    [Fact]
    public async Task Custom_code_can_be_redeemed_and_is_returned_in_the_link()
    {
        var response = await Admin().PostAsJsonAsync("/api/admin/tenants/alice/invites", new { profile = "full", code = "demo" });
        var body = await response.Content.ReadFromJsonAsync<JsonObject>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("demo", body!["code"]!.GetValue<string>());
        Assert.EndsWith("/?c=demo", body["link"]!.GetValue<string>());

        var visitor = _factory.ClientFor(ApiFactory.SharedHost);
        Assert.Equal(HttpStatusCode.NoContent, (await visitor.PostAsJsonAsync("/api/access/redeem", new { code = "demo" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await visitor.GetAsync("/api/cv")).StatusCode);
    }

    [Theory]
    [InlineData("abc")]                       // too short
    [InlineData("has space")]
    [InlineData("umlaut-ä")]
    [InlineData("slash/code")]
    public async Task Invalid_custom_codes_are_rejected(string code)
    {
        var response = await Admin().PostAsJsonAsync("/api/admin/tenants/alice/invites", new { profile = "full", code });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Active_custom_code_cannot_be_reused_but_a_revoked_one_is_released()
    {
        var admin = Admin();
        var first = await (await admin.PostAsJsonAsync("/api/admin/tenants/alice/invites", new { profile = "full", code = "demo" }))
            .Content.ReadFromJsonAsync<JsonObject>();
        Assert.Equal(HttpStatusCode.Conflict,
            (await admin.PostAsJsonAsync("/api/admin/tenants/bob/invites", new { profile = "full", code = "demo" })).StatusCode);

        await admin.DeleteAsync($"/api/admin/tenants/alice/invites/{first!["invite"]!["id"]}");
        var second = await admin.PostAsJsonAsync("/api/admin/tenants/bob/invites", new { profile = "full", code = "demo" });
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        // "demo" now opens bob's CV.
        var visitor = _factory.ClientFor(ApiFactory.SharedHost);
        await visitor.PostAsJsonAsync("/api/access/redeem", new { code = "demo" });
        var cv = await (await visitor.GetAsync("/api/cv")).Content.ReadFromJsonAsync<JsonObject>();
        Assert.Equal("bob", cv!["access"]!["tenant"]!.GetValue<string>());
    }
}
