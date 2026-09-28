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
        var body = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.Equal("shared", body!["host"]!.GetValue<string>());
    }

    [Fact]
    public async Task Tenant_host_without_public_profile_has_no_access()
    {
        var response = await _factory.ClientFor(ApiFactory.AliceHost).GetAsync("/api/cv");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.Equal("tenant", body!["host"]!.GetValue<string>());
        Assert.DoesNotContain("alice", body.ToJsonString());
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
    public async Task Admin_can_list_read_and_delete_tenant_files()
    {
        var admin = _factory.ClientFor(ApiFactory.SharedHost);
        admin.DefaultRequestHeaders.Add("X-Admin-Key", ApiFactory.AdminKey);

        var files = await admin.GetFromJsonAsync<JsonArray>("/api/admin/tenants/alice/files");
        var paths = files!.Select(f => f!["path"]!.GetValue<string>()).ToList();
        Assert.Equal(["assets/alice.jpg", "assets/unlisted.jpg", "cv.en.json", "tenant.json"], paths);

        var tenantJson = await admin.GetStringAsync("/api/admin/tenants/alice/files/tenant.json");
        Assert.Contains("\"Alice\"", tenantJson);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/admin/tenants/alice/files/cv.de.json")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/admin/tenants/alice/files/..%2Fbob%2Ftenant.json")).StatusCode);

        var tenants = await admin.GetFromJsonAsync<JsonArray>("/api/admin/tenants");
        var alice = tenants!.Single(t => t!["id"]!.GetValue<string>() == "alice")!;
        Assert.Equal(["en"], alice["locales"]!.AsArray().Select(l => l!.GetValue<string>()));

        var profiles = await admin.GetFromJsonAsync<JsonObject>("/api/admin/tenants/alice/profiles");
        Assert.True(profiles!["public"]!["flags"]!["hideCompanies"]!.GetValue<bool>());

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.DeleteAsync("/api/admin/tenants/alice/files/tenant.json")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync("/api/admin/tenants/alice/files/assets/unlisted.jpg")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/admin/tenants/alice/files/assets/unlisted.jpg")).StatusCode);
    }

    [Fact]
    public async Task Public_profile_hides_unset_flags_by_default()
    {
        _factory.SetPublicProfile("alice", "full");                // "full" sets no flags
        var experience = (await Cv(_factory.ClientFor(ApiFactory.AliceHost)))["cv"]!["experiences"]![0]!;
        Assert.Equal("Big corp", experience["company"]!.GetValue<string>());
        Assert.Equal("2020", experience["startDate"]!.GetValue<string>());

        // The same profile via invite on a tenant that has no public profile shows everything.
        _factory.SetPublicProfile("alice", null);
        var code = await _factory.CreateInviteAsync("alice", new { profile = "full" });
        var client = _factory.ClientFor(ApiFactory.SharedHost);
        await Redeem(client, code);
        var full = (await Cv(client))["cv"]!["experiences"]![0]!;
        Assert.Equal("ACME", full["company"]!.GetValue<string>());
        Assert.Equal("2020-03-15", full["startDate"]!.GetValue<string>());
    }

    [Fact]
    public async Task Admin_invite_list_includes_code_and_link()
    {
        var admin = _factory.ClientFor(ApiFactory.SharedHost);
        admin.DefaultRequestHeaders.Add("X-Admin-Key", ApiFactory.AdminKey);
        var code = await _factory.CreateInviteAsync("alice", new { profile = "full", label = "listed" });

        var invites = await admin.GetFromJsonAsync<JsonArray>("/api/admin/tenants/alice/invites");
        var listed = invites!.Single(i => i!["label"]!.GetValue<string>() == "listed")!;
        Assert.Equal(code, listed["code"]!.GetValue<string>());
        Assert.Equal($"https://alice-cv.example.org/?c={code}", listed["link"]!.GetValue<string>());
    }

    [Fact]
    public async Task Invites_can_be_pinned_to_a_cv_revision()
    {
        var admin = _factory.ClientFor(ApiFactory.SharedHost);
        admin.DefaultRequestHeaders.Add("X-Admin-Key", ApiFactory.AdminKey);
        const string v1 = "1111111111111111111111111111111111111111";
        const string v2 = "2222222222222222222222222222222222222222";

        _factory.WriteCv("bob", "en", """{ "profile": { "name": "Bob v1" } }""");
        (await admin.PostAsJsonAsync("/api/admin/tenants/bob/revisions", new { sha = v1, message = "first" })).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/admin/tenants/bob/invites",
            new { profile = "full", overrides = new { revision = "deadbeef" } })).StatusCode);
        var pinnedCode = await _factory.CreateInviteAsync("bob", new { profile = "full", label = "pinned", overrides = new { revision = "1111111" } });
        var liveCode = await _factory.CreateInviteAsync("bob", new { profile = "full", label = "live" });

        _factory.WriteCv("bob", "en", """{ "profile": { "name": "Bob v2" } }""");

        async Task<string> NameFor(string code)
        {
            var client = _factory.ClientFor(ApiFactory.SharedHost);
            await Redeem(client, code);
            return (await Cv(client))["cv"]!["profile"]!["name"]!.GetValue<string>();
        }
        Assert.Equal("Bob v1", await NameFor(pinnedCode));
        Assert.Equal("Bob v2", await NameFor(liveCode));

        // Admin sees the pin, and that the CV changed since (manual edit, not yet registered).
        var revisions = await admin.GetFromJsonAsync<JsonObject>("/api/admin/tenants/bob/revisions");
        Assert.True(revisions!["modified"]!.GetValue<bool>());
        Assert.True(revisions["revisions"]![0]!["outdated"]!.GetValue<bool>());
        var invites = await admin.GetFromJsonAsync<JsonArray>("/api/admin/tenants/bob/invites");
        var pinned = invites!.Single(i => i!["label"]!.GetValue<string>() == "pinned")!;
        Assert.Equal(v1, pinned["revision"]!.GetValue<string>());
        Assert.Equal("invite", pinned["pinnedBy"]!.GetValue<string>());

        // Registering v2 and re-pinning brings the invite up to date.
        (await admin.PostAsJsonAsync("/api/admin/tenants/bob/revisions", new { sha = v2 })).EnsureSuccessStatusCode();
        revisions = await admin.GetFromJsonAsync<JsonObject>("/api/admin/tenants/bob/revisions");
        Assert.Equal(v2, revisions!["current"]!.GetValue<string>());
        Assert.False(revisions["modified"]!.GetValue<bool>());
        Assert.Equal(2, revisions["revisions"]!.AsArray().Count);    // v1 kept: still pinned
        var preview = await admin.GetFromJsonAsync<JsonObject>($"/api/admin/tenants/bob/preview?profile=full&revision={v1}");
        Assert.Equal("Bob v1", preview!["cv"]!["profile"]!["name"]!.GetValue<string>());

        (await admin.PutAsJsonAsync($"/api/admin/tenants/bob/invites/{pinned["id"]}/revision", new { revision = v2 })).EnsureSuccessStatusCode();
        Assert.Equal("Bob v2", await NameFor(pinnedCode));

        // v1 is no longer used by anything and has been removed.
        revisions = await admin.GetFromJsonAsync<JsonObject>("/api/admin/tenants/bob/revisions");
        Assert.Equal([v2], revisions!["revisions"]!.AsArray().Select(r => r!["sha"]!.GetValue<string>()));
        Assert.False(Directory.Exists(Path.Combine(_factory.DataPath, "tenants", "bob", "revisions", v1)));
        _factory.WriteCv("bob", "en", """{ "profile": { "name": "Bob" }, "experiences": [] }""");
    }

    [Fact]
    public async Task Pinned_invites_get_the_assets_of_their_revision_until_revoked()
    {
        var admin = _factory.ClientFor(ApiFactory.SharedHost);
        admin.DefaultRequestHeaders.Add("X-Admin-Key", ApiFactory.AdminKey);
        const string v1 = "3333333333333333333333333333333333333333";
        const string v2 = "4444444444444444444444444444444444444444";
        var photo = Path.Combine(_factory.DataPath, "tenants", "bob", "assets", "bob.jpg");
        _factory.WriteCv("bob", "en", """{ "profile": { "name": "Bob", "photoUrl": "/api/assets/bob.jpg" } }""");

        File.WriteAllText(photo, "old photo");
        (await admin.PostAsJsonAsync("/api/admin/tenants/bob/revisions", new { sha = v1 })).EnsureSuccessStatusCode();
        var pinned = await _factory.CreateInviteAsync("bob", new { profile = "full", overrides = new { revision = v1 } });

        File.WriteAllText(photo, "new photo");
        (await admin.PostAsJsonAsync("/api/admin/tenants/bob/revisions", new { sha = v2 })).EnsureSuccessStatusCode();
        var revisions = await admin.GetFromJsonAsync<JsonObject>("/api/admin/tenants/bob/revisions");
        var old = revisions!["revisions"]!.AsArray().Single(r => r!["sha"]!.GetValue<string>() == v1)!;
        Assert.True(old["outdated"]!.GetValue<bool>());           // only the photo changed

        var client = _factory.ClientFor(ApiFactory.SharedHost);
        await Redeem(client, pinned);
        Assert.Equal("old photo", await client.GetStringAsync("/api/assets/bob.jpg"));

        var invites = await admin.GetFromJsonAsync<JsonArray>("/api/admin/tenants/bob/invites");
        var id = invites!.Single(i => i!["revision"]?.GetValue<string>() == v1)!["id"]!.GetValue<string>();
        (await admin.DeleteAsync($"/api/admin/tenants/bob/invites/{id}")).EnsureSuccessStatusCode();
        Assert.False(Directory.Exists(Path.Combine(_factory.DataPath, "tenants", "bob", "revisions", v1)));

        File.Delete(photo);
        _factory.WriteCv("bob", "en", """{ "profile": { "name": "Bob" }, "experiences": [] }""");
    }

    [Fact]
    public async Task Pruned_revisions_are_fetched_from_git_again()
    {
        var admin = _factory.ClientFor(ApiFactory.SharedHost);
        admin.DefaultRequestHeaders.Add("X-Admin-Key", ApiFactory.AdminKey);
        var repo = Path.Combine(_factory.DataPath, "cv-repo");
        var folder = Path.Combine(repo, "tenants", "bob");
        Directory.CreateDirectory(Path.Combine(folder, "assets"));
        Git(repo, "init", "-q");

        async Task<string> Deploy(string name, string message)
        {
            var cv = $$"""{ "profile": { "name": "{{name}}" } }""";
            File.WriteAllText(Path.Combine(folder, "cv.en.json"), cv);
            File.WriteAllText(Path.Combine(folder, "assets", "logo.svg"), name);
            Git(repo, "add", "-A");
            Git(repo, "-c", "user.email=t@t", "-c", "user.name=t", "commit", "-qm", message);
            var sha = Git(repo, "rev-parse", "HEAD");
            _factory.WriteCv("bob", "en", cv);                          // what cv-sync.sh uploads
            (await admin.PostAsJsonAsync("/api/admin/tenants/bob/revisions",
                new { sha, message, repo, path = "tenants/bob" })).EnsureSuccessStatusCode();
            return sha;
        }

        var sent = await Deploy("Bob as sent", "Application ACME");
        Git(repo, "tag", "sent-acme");
        await Deploy("Bob later", "Later changes");
        var revisions = await admin.GetFromJsonAsync<JsonObject>("/api/admin/tenants/bob/revisions");
        Assert.DoesNotContain(revisions!["revisions"]!.AsArray(), r => r!["sha"]!.GetValue<string>() == sent);   // pruned

        // Pinning to the pruned commit (by tag) fetches it from git again.
        var code = await _factory.CreateInviteAsync("bob", new { profile = "full", overrides = new { revision = "sent-acme" } });
        var client = _factory.ClientFor(ApiFactory.SharedHost);
        await Redeem(client, code);
        Assert.Equal("Bob as sent", (await Cv(client))["cv"]!["profile"]!["name"]!.GetValue<string>());

        revisions = await admin.GetFromJsonAsync<JsonObject>("/api/admin/tenants/bob/revisions");
        var fetched = revisions!["revisions"]!.AsArray().Single(r => r!["sha"]!.GetValue<string>() == sent)!;
        Assert.Equal("Application ACME", fetched["message"]!.GetValue<string>());
        Assert.Equal(["sent-acme"], fetched["refs"]!.AsArray().Select(r => r!.GetValue<string>()));
        Assert.True(fetched["outdated"]!.GetValue<bool>());
        Assert.Equal("tenants/bob", revisions["source"]!["path"]!.GetValue<string>());

        var unknown = await admin.PostAsJsonAsync("/api/admin/tenants/bob/revisions/fetch", new { @ref = "no-such-tag" });
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/admin/tenants/bob/revisions/fetch", new { @ref = "--upload-pack=x" })).StatusCode);

        _factory.WriteCv("bob", "en", """{ "profile": { "name": "Bob" }, "experiences": [] }""");
    }

    private static string Git(string dir, params string[] args)
    {
        var start = new System.Diagnostics.ProcessStartInfo("git") { WorkingDirectory = dir, RedirectStandardOutput = true };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = System.Diagnostics.Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();
        Assert.Equal(0, process.ExitCode);
        return output;
    }

    [Fact]
    public async Task Pdf_is_disabled_without_renderer()
    {
        _factory.SetPublicProfile("alice", "public");
        var client = _factory.ClientFor(ApiFactory.AliceHost);
        Assert.False((await Cv(client))["features"]!["pdf"]!.GetValue<bool>());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/pdf")).StatusCode);
    }

    [Fact]
    public async Task Works_without_api_prefix_when_proxy_strips_it()
    {
        Assert.Equal(HttpStatusCode.OK, (await _factory.ClientFor(ApiFactory.SharedHost).GetAsync("/health")).StatusCode);
    }
}
