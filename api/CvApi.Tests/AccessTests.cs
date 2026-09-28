using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace CvApi.Tests;

public sealed class AccessTests : IDisposable
{
    private readonly ApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private static async Task<HttpResponseMessage> Redeem(HttpClient client, string code) =>
        await client.PostAsJsonAsync("/api/access/redeem", new { code });

    private static async Task<JsonObject> Cv(HttpClient client)
    {
        var response = await client.GetAsync("/api/cv");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonObject>())!;
    }

    [Fact]
    public async Task Shared_host_without_invite_has_no_access()
    {
        var response = await _factory.ClientFor(ApiFactory.SharedHost).GetAsync("/api/cv");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.True(response.Headers.CacheControl is { NoStore: true, Private: true });
    }

    [Fact]
    public async Task Tenant_host_without_public_profile_has_no_access()
    {
        var response = await _factory.ClientFor(ApiFactory.AliceHost).GetAsync("/api/cv");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Tenant_host_with_public_profile_gets_redacted_cv()
    {
        _factory.SetPublicProfile("alice", "public");

        var body = await Cv(_factory.ClientFor(ApiFactory.AliceHost));

        Assert.Equal("alice", body["access"]!["tenant"]!.GetValue<string>());
        Assert.False(body["access"]!["viaInvite"]!.GetValue<bool>());
        var experiences = body["cv"]!["experiences"]!.AsArray();
        var exp = Assert.Single(experiences)!.AsObject();       // "requires: private" entry removed
        Assert.Equal("Big corp", exp["company"]!.GetValue<string>());
        Assert.Equal("2020", exp["startDate"]!.GetValue<string>());
        Assert.False(exp.ContainsKey("period"));
        Assert.False(exp.ContainsKey("logos"));
        Assert.False(body["cv"]!["profile"]!.AsObject().ContainsKey("photoUrl"));
    }

    [Fact]
    public async Task Public_profile_is_not_used_on_shared_host()
    {
        _factory.SetPublicProfile("alice", "public");
        var response = await _factory.ClientFor(ApiFactory.SharedHost).GetAsync("/api/cv");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Invite_on_shared_host_selects_tenant_and_profile()
    {
        var code = await _factory.CreateInviteAsync("alice", new { profile = "full", label = "ACME HR" });
        var client = _factory.ClientFor(ApiFactory.SharedHost);

        Assert.Equal(HttpStatusCode.NoContent, (await Redeem(client, code)).StatusCode);
        var body = await Cv(client);

        Assert.Equal("alice", body["access"]!["tenant"]!.GetValue<string>());
        Assert.Equal("full", body["access"]!["profile"]!.GetValue<string>());
        Assert.Equal("ACME HR", body["access"]!["label"]!.GetValue<string>());
        var experiences = body["cv"]!["experiences"]!.AsArray();
        Assert.Equal(2, experiences.Count);
        Assert.Equal("ACME", experiences[0]!["company"]!.GetValue<string>());
        Assert.False(experiences[0]!.AsObject().ContainsKey("companyAlias"));
        Assert.False(experiences[1]!.AsObject().ContainsKey("requires"));
    }

    [Fact]
    public async Task Invite_on_matching_tenant_host_is_accepted_case_and_port_insensitive()
    {
        var code = await _factory.CreateInviteAsync("alice", new { profile = "full" });
        var client = _factory.ClientFor("ALICE-cv.example.org:8443");
        Assert.Equal(HttpStatusCode.NoContent, (await Redeem(client, code)).StatusCode);
        Assert.Equal("full", (await Cv(client))["access"]!["profile"]!.GetValue<string>());
    }

    [Fact]
    public async Task Invite_of_other_tenant_is_rejected_on_tenant_host()
    {
        var code = await _factory.CreateInviteAsync("bob", new { profile = "full" });
        var client = _factory.ClientFor(ApiFactory.AliceHost);
        Assert.Equal(HttpStatusCode.BadRequest, (await Redeem(client, code)).StatusCode);
    }

    [Fact]
    public async Task Invalid_invite_falls_back_to_public_profile_when_enabled()
    {
        _factory.SetPublicProfile("alice", "public");
        var client = _factory.ClientFor(ApiFactory.AliceHost);
        Assert.Equal(HttpStatusCode.BadRequest, (await Redeem(client, "nope")).StatusCode);
        Assert.Equal("public", (await Cv(client))["access"]!["profile"]!.GetValue<string>());
    }

    [Fact]
    public async Task Revoked_invite_loses_access_immediately()
    {
        var admin = _factory.ClientFor(ApiFactory.SharedHost);
        admin.DefaultRequestHeaders.Add("X-Admin-Key", ApiFactory.AdminKey);
        var created = await (await admin.PostAsJsonAsync("/api/admin/tenants/alice/invites", new { profile = "full" }))
            .Content.ReadFromJsonAsync<JsonObject>();
        var code = created!["code"]!.GetValue<string>();
        var id = created["invite"]!["id"]!.GetValue<string>();

        var client = _factory.ClientFor(ApiFactory.SharedHost);
        await Redeem(client, code);
        await Cv(client);

        (await admin.DeleteAsync($"/api/admin/tenants/alice/invites/{id}")).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/cv")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Redeem(client, code)).StatusCode);
    }

    [Fact]
    public async Task Expired_invite_cannot_be_redeemed()
    {
        var code = await _factory.CreateInviteAsync("alice", new { profile = "full", expiresAt = DateTimeOffset.UtcNow.AddMinutes(-1) });
        Assert.Equal(HttpStatusCode.BadRequest, (await Redeem(_factory.ClientFor(ApiFactory.SharedHost), code)).StatusCode);
    }

    [Fact]
    public async Task Max_uses_limits_redemptions()
    {
        var code = await _factory.CreateInviteAsync("alice", new { profile = "full", maxUses = 1 });
        Assert.Equal(HttpStatusCode.NoContent, (await Redeem(_factory.ClientFor(ApiFactory.SharedHost), code)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Redeem(_factory.ClientFor(ApiFactory.SharedHost), code)).StatusCode);
    }

    [Fact]
    public async Task Invite_overrides_are_applied()
    {
        var code = await _factory.CreateInviteAsync("alice", new
        {
            profile = "full",
            overrides = new { flags = new { hideCompanies = true }, hiddenFields = new[] { "experiences.endDate" } },
        });
        var client = _factory.ClientFor(ApiFactory.SharedHost);
        await Redeem(client, code);

        var experiences = (await Cv(client))["cv"]!["experiences"]!.AsArray();
        Assert.Equal("Big corp", experiences[0]!["company"]!.GetValue<string>());
        Assert.Null(experiences[1]!["company"]);                 // no alias → removed
        Assert.False(experiences[1]!.AsObject().ContainsKey("endDate"));
    }

    [Fact]
    public async Task Assets_are_served_only_when_referenced_by_the_visible_cv()
    {
        var code = await _factory.CreateInviteAsync("alice", new { profile = "full" });
        var client = _factory.ClientFor(ApiFactory.SharedHost);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/assets/alice.jpg")).StatusCode);
        await Redeem(client, code);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/assets/alice.jpg")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/assets/unlisted.jpg")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/assets/..%2Ftenant.json")).StatusCode);

        _factory.SetPublicProfile("alice", "public");               // hidePhoto
        var anonymous = _factory.ClientFor(ApiFactory.AliceHost);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync("/api/assets/alice.jpg")).StatusCode);
    }

    [Fact]
    public async Task Admin_requires_key()
    {
        var client = _factory.ClientFor(ApiFactory.SharedHost);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/tenants")).StatusCode);
        client.DefaultRequestHeaders.Add("X-Admin-Key", "wrong");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/tenants")).StatusCode);
    }

    [Fact]
    public async Task Admin_can_upload_tenant_files()
    {
        var admin = _factory.ClientFor(ApiFactory.SharedHost);
        admin.DefaultRequestHeaders.Add("X-Admin-Key", ApiFactory.AdminKey);

        var tenant = new StringContent("""{ "name": "Carol", "hosts": ["carol.example.org"], "publicProfile": "all", "profiles": { "all": {} } }""");
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsync("/api/admin/tenants/carol/files/tenant.json", tenant)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsync("/api/admin/tenants/carol/files/cv.en.json",
            new StringContent("""{ "profile": { "name": "Carol" } }"""))).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsync("/api/admin/tenants/carol/files/cv.en.json",
            new StringContent("{ broken"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsync("/api/admin/tenants/carol/files/assets/..%2F..%2Fapp.db",
            new StringContent("x"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsync("/api/admin/tenants/carol/files/other.txt",
            new StringContent("x"))).StatusCode);

        var body = await Cv(_factory.ClientFor("carol.example.org"));
        Assert.Equal("Carol", body["cv"]!["profile"]!["name"]!.GetValue<string>());
    }

    [Fact]
    public async Task Works_without_api_prefix_when_proxy_strips_it()
    {
        Assert.Equal(HttpStatusCode.OK, (await _factory.ClientFor(ApiFactory.SharedHost).GetAsync("/health")).StatusCode);
    }
}
