using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CvApi.Accounts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace CvApi.Tests;

/// <summary>Collects e-mails instead of sending them.</summary>
public sealed class CapturingEmailSender : IEmailSender
{
    public List<(string To, string Subject, string Text)> Sent { get; } = [];
    public bool Enabled => true;

    public Task<bool> SendAsync(string to, string subject, string text, string html, CancellationToken ct)
    {
        lock (Sent) Sent.Add((to, subject, text));
        return Task.FromResult(true);
    }
}

public sealed class AccountApiFactory : ApiFactory
{
    public const string WebhookSecret = "pdl_ntfset_test_secret";
    public CapturingEmailSender Email { get; } = new();

    public AccountApiFactory()
    {
        Settings["Cv:SharedBaseUrl"] = "https://" + SharedHost;
        Settings["Auth:MagicLinkPerMinute"] = "1000";
        Settings["Billing:Paddle:ClientToken"] = "test_client_token";
        Settings["Billing:Paddle:WebhookSecret"] = WebhookSecret;
        Settings["Billing:Passes:1:Id"] = "month";
        Settings["Billing:Passes:1:PaddlePriceId"] = "pri_month";
    }

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services => services.AddSingleton<IEmailSender>(Email));
    }

    /// <summary>A client that keeps cookies and does not follow redirects (sign-in answers with redirects).</summary>
    public HttpClient SessionClient(string host = SharedHost)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri($"http://{host}"), AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(Csrf.HeaderName, "cv");
        return client;
    }

    public HttpClient AdminClient()
    {
        var client = ClientFor(SharedHost);
        client.DefaultRequestHeaders.Add("X-Admin-Key", AdminKey);
        return client;
    }

    /// <summary>Signs in with a magic link and returns the session client.</summary>
    public async Task<HttpClient> SignInAsync(string email)
    {
        var client = SessionClient();
        var count = Email.Sent.Count;
        (await client.PostAsJsonAsync("/api/auth/magic-link", new { email })).EnsureSuccessStatusCode();
        var link = Regex.Match(Email.Sent[count].Text, @"https?://\S+").Value;
        var response = await client.GetAsync(new Uri(link).PathAndQuery);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }

    public async Task<HttpClient> SignUpWithTenantAsync(string email, string handle)
    {
        var client = await SignInAsync(email);
        var created = await client.PostAsJsonAsync("/api/account/tenant", new { handle, locale = "en", name = "Test User" });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        return client;
    }

    public async Task<Guid> UserIdAsync(HttpClient session) =>
        Guid.Parse((await session.GetFromJsonAsync<JsonObject>("/api/auth/me"))!["id"]!.GetValue<string>());
}

public class AccountTests : IDisposable
{
    private readonly AccountApiFactory _factory = new();

    public void Dispose()
    {
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Magic_link_signs_in_once_and_me_describes_the_user()
    {
        var anonymous = _factory.SessionClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/auth/me")).StatusCode);

        var client = await _factory.SignInAsync("Jane@Example.org");
        var me = await client.GetFromJsonAsync<JsonObject>("/api/auth/me");
        Assert.Equal("jane@example.org", me!["email"]!.GetValue<string>());
        Assert.Null(me["tenantId"]);
        Assert.Equal("free", me["plan"]!["name"]!.GetValue<string>());

        // The link works only once.
        var link = Regex.Match(_factory.Email.Sent[^1].Text, @"https?://\S+").Value;
        var again = await _factory.SessionClient().GetAsync(new Uri(link).PathAndQuery);
        Assert.Contains("error=link_invalid", again.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Magic_link_response_does_not_reveal_accounts_and_is_limited_per_address()
    {
        var client = _factory.SessionClient();
        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/magic-link", new { email = "x@example.org" })).StatusCode);
        Assert.Equal(3, _factory.Email.Sent.Count(m => m.To == "x@example.org"));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/magic-link", new { email = "not-an-email" })).StatusCode);
    }

    [Fact]
    public async Task Magic_link_uses_the_shared_base_url_not_the_host_header()
    {
        var client = _factory.SessionClient("evil.example");
        (await client.PostAsJsonAsync("/api/auth/magic-link", new { email = "victim@example.org" })).EnsureSuccessStatusCode();
        Assert.Contains("https://cv.example.org/api/auth/magic?token=", _factory.Email.Sent[^1].Text);
        Assert.DoesNotContain("evil.example", _factory.Email.Sent[^1].Text);
    }

    [Fact]
    public async Task Configured_providers_are_listed_and_redirect_to_the_provider()
    {
        _factory.Settings["Auth:Google:ClientId"] = "gid";
        _factory.Settings["Auth:Google:ClientSecret"] = "gsecret";
        var client = _factory.SessionClient();
        var providers = await client.GetFromJsonAsync<JsonObject>("/api/auth/providers");
        Assert.Equal(["google"], providers!["providers"]!.AsArray().Select(p => p!.GetValue<string>()));
        Assert.True(providers["magicLink"]!.GetValue<bool>());

        var login = await client.GetAsync("/api/auth/login/google?returnUrl=/admin");
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        var location = login.Headers.Location!.ToString();
        Assert.StartsWith("https://accounts.google.com/", location);
        Assert.Contains(Uri.EscapeDataString("http://cv.example.org/api/signin-google"), location);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/auth/login/github")).StatusCode);
    }

    [Fact]
    public async Task Completing_without_provider_login_fails()
    {
        var response = await _factory.SessionClient().GetAsync("/api/auth/complete?provider=google");
        Assert.Contains("error=provider_failed", response.Headers.Location!.ToString());
    }

    [Theory]
    [InlineData("/admin", "/admin")]
    [InlineData("//evil.example", "/admin")]
    [InlineData("/\\evil.example", "/admin")]
    [InlineData("https://evil.example", "/admin")]
    [InlineData("/admin?tab=account", "/admin?tab=account")]
    [InlineData("/\t/evil.example", "/admin")]
    [InlineData("/\n/evil.example", "/admin")]
    [InlineData("/ /evil.example", "/admin")]
    public void Return_url_must_be_local(string input, string expected) =>
        Assert.Equal(expected, AuthSetup.SafeReturnUrl(input));

    [Fact]
    public async Task An_unverified_address_cannot_be_preregistered_to_take_over_an_account()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var accounts = scope.ServiceProvider.GetRequiredService<AccountService>();
            // An attacker's Microsoft login claims the victim's address (not verified).
            var (attacker, error) = await accounts.SignInAsync("microsoft", "ms-attacker", "victim@example.org", "Mallory", null,
                emailVerified: false, CancellationToken.None);
            Assert.Equal(SignInError.None, error);
            Assert.NotNull(attacker);

            // The real owner signs in with a verified provider: not joined into the attacker's account.
            var (joined, joinError) = await accounts.SignInAsync("google", "g-victim", "victim@example.org", "Victim", null,
                emailVerified: true, CancellationToken.None);
            Assert.Null(joined);
            Assert.Equal(SignInError.AccountExists, joinError);
        }

        // Same for the magic link.
        var client = _factory.SessionClient();
        (await client.PostAsJsonAsync("/api/auth/magic-link", new { email = "victim@example.org" })).EnsureSuccessStatusCode();
        var link = Regex.Match(_factory.Email.Sent[^1].Text, @"https?://\S+").Value;
        Assert.Contains("error=account_exists", (await client.GetAsync(new Uri(link).PathAndQuery)).Headers.Location!.ToString());
    }

    [Fact]
    public async Task Expired_custom_codes_of_another_tenant_are_not_taken_over()
    {
        var admin = _factory.AdminClient();
        var created = await admin.PostAsJsonAsync("/api/admin/tenants/alice/invites",
            new { profile = "full", code = "alice-recruiter-2026", expiresAt = _factory.Clock.GetUtcNow().AddDays(1) });
        created.EnsureSuccessStatusCode();
        _factory.Clock.Advance(TimeSpan.FromDays(2));

        var client = await _factory.SignUpWithTenantAsync("squat@example.org", "squatter");
        var taken = await client.PostAsJsonAsync("/api/admin/tenants/squatter/invites", new { profile = "full", code = "alice-recruiter-2026" });
        Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);
        // The owning tenant may reuse its own expired code.
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/api/admin/tenants/alice/invites", new { profile = "full", code = "alice-recruiter-2026" })).StatusCode);
    }

    [Fact]
    public async Task Onboarding_creates_the_tenant_and_validates_the_handle()
    {
        var client = await _factory.SignInAsync("new@example.org");
        foreach (var (handle, error) in new[] { ("admin", "handle_reserved"), ("alice", "handle_taken"), ("A", "invalid_handle"), ("ab", "invalid_handle"), ("x--y", "invalid_handle") })
        {
            var response = await client.PostAsJsonAsync("/api/account/tenant", new { handle });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(error, (await response.Content.ReadFromJsonAsync<JsonObject>())!["error"]!.GetValue<string>());
        }
        Assert.Equal("handle_taken", (await client.GetFromJsonAsync<JsonObject>("/api/account/handle/alice"))!["error"]!.GetValue<string>());

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/account/tenant", new { handle = "newbie", locale = "de" })).StatusCode);
        Assert.True(File.Exists(Path.Combine(_factory.DataPath, "tenants", "newbie", "cv.de.json")));
        // Only one tenant per user.
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/account/tenant", new { handle = "second" })).StatusCode);

        var tenants = await client.GetFromJsonAsync<JsonArray>("/api/admin/tenants");
        Assert.Equal(["newbie"], tenants!.Select(t => t!["id"]!.GetValue<string>()));
        Assert.Equal("free", tenants[0]!["plan"]!.GetValue<string>());
    }

    [Fact]
    public async Task Users_only_reach_their_own_tenant()
    {
        var client = await _factory.SignUpWithTenantAsync("u1@example.org", "user-one");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/admin/tenants/user-one/invites")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/admin/tenants/alice/invites")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/admin/tenants/alice/files/cv.en.json")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsync("/api/admin/tenants/evil/files/tenant.json", new StringContent("{}"))).StatusCode);
        // Super-admin only endpoints and user management.
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/admin/tenants/user-one/revisions/fetch", new { @ref = "main" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/admin/users")).StatusCode);
        // Without session: 401 (admin key configured).
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.ClientFor(ApiFactory.SharedHost).GetAsync("/api/admin/tenants")).StatusCode);
    }

    [Fact]
    public async Task State_changing_requests_need_the_csrf_header()
    {
        var client = await _factory.SignUpWithTenantAsync("csrf@example.org", "csrf-user");
        client.DefaultRequestHeaders.Remove(Csrf.HeaderName);
        var response = await client.PostAsJsonAsync("/api/admin/tenants/csrf-user/invites", new { profile = "full" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/account/sessions/revoke", null)).StatusCode);
    }

    [Fact]
    public async Task Free_plan_allows_three_active_invites_and_pro_removes_the_limit()
    {
        var client = await _factory.SignUpWithTenantAsync("limit@example.org", "limited");
        for (var i = 0; i < 3; i++)
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/admin/tenants/limited/invites", new { profile = "full", label = $"#{i}" })).StatusCode);
        var fourth = await client.PostAsJsonAsync("/api/admin/tenants/limited/invites", new { profile = "full" });
        Assert.Equal(HttpStatusCode.PaymentRequired, fourth.StatusCode);
        var body = await fourth.Content.ReadFromJsonAsync<JsonObject>();
        Assert.Equal("activeInvites", body!["feature"]!.GetValue<string>());
        Assert.Equal(3, body["limit"]!.GetValue<int>());

        // Revoking one frees a slot.
        var invites = await client.GetFromJsonAsync<JsonArray>("/api/admin/tenants/limited/invites");
        var id = invites![0]!["id"]!.GetValue<string>();
        Assert.True((await client.DeleteAsync($"/api/admin/tenants/limited/invites/{id}")).IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/admin/tenants/limited/invites", new { profile = "full" })).StatusCode);

        // The super-admin is not limited (support).
        Assert.Equal(HttpStatusCode.OK, (await _factory.AdminClient().PostAsJsonAsync("/api/admin/tenants/limited/invites", new { profile = "full" })).StatusCode);

        // Manual plan (e.g. paid via another platform): Pro removes the limit.
        var userId = await _factory.UserIdAsync(client);
        var plan = await _factory.AdminClient().PutAsJsonAsync($"/api/admin/users/{userId}/plan", new { addDays = 3, proForever = false, note = "paid via invoice" });
        Assert.Equal(HttpStatusCode.OK, plan.StatusCode);
        Assert.Equal("pro", (await plan.Content.ReadFromJsonAsync<JsonObject>())!["plan"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/admin/tenants/limited/invites", new { profile = "full" })).StatusCode);

        // The manual change is in the payment history.
        var detail = await _factory.AdminClient().GetFromJsonAsync<JsonObject>($"/api/admin/users/{userId}");
        Assert.Contains(detail!["payments"]!.AsArray(), p => p!["provider"]!.GetValue<string>() == "manual");

        // Back to free when the time is over.
        _factory.Clock.Advance(TimeSpan.FromDays(4));
        Assert.Equal(HttpStatusCode.PaymentRequired, (await client.PostAsJsonAsync("/api/admin/tenants/limited/invites", new { profile = "full" })).StatusCode);
    }

    [Fact]
    public async Task Expired_invites_cannot_be_brought_back_beyond_the_free_limit()
    {
        var client = await _factory.SignUpWithTenantAsync("revive@example.org", "reviver");
        var now = _factory.Clock.GetUtcNow();
        var expiring = await client.PostAsJsonAsync("/api/admin/tenants/reviver/invites", new { profile = "full", expiresAt = now.AddDays(1) });
        var id = (await expiring.Content.ReadFromJsonAsync<JsonObject>())!["invite"]!["id"]!.GetValue<string>();
        _factory.Clock.Advance(TimeSpan.FromDays(2));
        for (var i = 0; i < 3; i++)
            (await client.PostAsJsonAsync("/api/admin/tenants/reviver/invites", new { profile = "full" })).EnsureSuccessStatusCode();

        var extend = await client.PutAsJsonAsync($"/api/admin/tenants/reviver/invites/{id}/settings",
            new { label = "x", expiresAt = _factory.Clock.GetUtcNow().AddDays(10) });
        Assert.Equal(HttpStatusCode.PaymentRequired, extend.StatusCode);

        // Settings of an invite that stays inactive can still be changed; the super-admin may revive it.
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/admin/tenants/reviver/invites/{id}/settings", new { label = "renamed", expiresAt = now.AddDays(1) })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _factory.AdminClient().PutAsJsonAsync($"/api/admin/tenants/reviver/invites/{id}/settings",
            new { label = "x", expiresAt = _factory.Clock.GetUtcNow().AddDays(10) })).StatusCode);
    }

    [Fact]
    public async Task Csrf_header_must_carry_the_expected_value()
    {
        var client = await _factory.SignUpWithTenantAsync("csrf2@example.org", "csrf-two");
        client.DefaultRequestHeaders.Remove(Csrf.HeaderName);
        client.DefaultRequestHeaders.Add(Csrf.HeaderName, "XMLHttpRequest");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/admin/tenants/csrf-two/invites", new { profile = "full" })).StatusCode);
    }

    [Fact]
    public async Task Users_cannot_change_hosts_or_use_short_custom_codes()
    {
        var client = await _factory.SignUpWithTenantAsync("hosts@example.org", "hoster");
        var tenantJson = await client.GetStringAsync("/api/admin/tenants/hoster/files/tenant.json");
        var node = JsonNode.Parse(tenantJson)!.AsObject();
        node["hosts"] = new JsonArray("alice-cv.example.org");
        var response = await client.PutAsync("/api/admin/tenants/hoster/files/tenant.json", new StringContent(node.ToJsonString()));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("hosts_managed", (await response.Content.ReadFromJsonAsync<JsonObject>())!["error"]!.GetValue<string>());

        // Other changes are fine.
        node["hosts"] = new JsonArray();
        node["name"] = "Renamed";
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsync("/api/admin/tenants/hoster/files/tenant.json", new StringContent(node.ToJsonString()))).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/admin/tenants/hoster/invites", new { profile = "full", code = "jobs" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/admin/tenants/hoster/invites", new { profile = "full", code = "hoster-acme-2026" })).StatusCode);
    }

    [Fact]
    public async Task Broken_tenant_json_is_rejected_and_does_not_break_other_tenants()
    {
        var admin = _factory.AdminClient();
        var bad = await admin.PutAsync("/api/admin/tenants/broken/files/tenant.json", new StringContent("""{ "name": "x", "profiles": null }"""));
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);

        // Written directly to the volume: skipped, the others keep working.
        Directory.CreateDirectory(Path.Combine(_factory.DataPath, "tenants", "broken"));
        File.WriteAllText(Path.Combine(_factory.DataPath, "tenants", "broken", "tenant.json"), """{ "name": "x", "hosts": null }""");
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/admin/tenants")).StatusCode);
        var code = await _factory.CreateInviteAsync("bob", new { profile = "full" });
        var visitor = _factory.ClientFor(ApiFactory.SharedHost);
        (await visitor.PostAsJsonAsync("/api/access/redeem", new { code })).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Asset_quota_applies_to_users()
    {
        _factory.Settings["Saas:QuotaFreeMb"] = "1";
        var client = await _factory.SignUpWithTenantAsync("quota@example.org", "quota-user");
        var oneMb = new ByteArrayContent(new byte[700 * 1024]);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsync("/api/admin/tenants/quota-user/files/assets/a.jpg", oneMb)).StatusCode);
        // Replacing the same file counts only once.
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsync("/api/admin/tenants/quota-user/files/assets/a.jpg", new ByteArrayContent(new byte[700 * 1024]))).StatusCode);
        var second = await client.PutAsync("/api/admin/tenants/quota-user/files/assets/b.jpg", new ByteArrayContent(new byte[700 * 1024]));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, second.StatusCode);
    }

    [Fact]
    public async Task Credit_can_only_be_hidden_with_pro()
    {
        var client = await _factory.SignUpWithTenantAsync("credit@example.org", "crediter");
        Assert.Equal(HttpStatusCode.PaymentRequired, (await client.PatchAsJsonAsync("/api/account", new { hideCredit = true })).StatusCode);

        var code = await _factory.CreateInviteAsync("crediter", new { profile = "full" });
        var visitor = _factory.ClientFor(ApiFactory.SharedHost);
        (await visitor.PostAsJsonAsync("/api/access/redeem", new { code })).EnsureSuccessStatusCode();
        Assert.Equal("https://" + ApiFactory.SharedHost, (await visitor.GetFromJsonAsync<JsonObject>("/api/cv"))!["links"]!["platform"]!.GetValue<string>());

        var userId = await _factory.UserIdAsync(client);
        (await _factory.AdminClient().PutAsJsonAsync($"/api/admin/users/{userId}/plan", new { proForever = true })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NoContent, (await client.PatchAsJsonAsync("/api/account", new { hideCredit = true })).StatusCode);
        Assert.Null((await visitor.GetFromJsonAsync<JsonObject>("/api/cv"))!["links"]!["platform"]);

        // Pro ends: the credit is back although the setting stays.
        (await _factory.AdminClient().PutAsJsonAsync($"/api/admin/users/{userId}/plan", new { proForever = false })).EnsureSuccessStatusCode();
        Assert.NotNull((await visitor.GetFromJsonAsync<JsonObject>("/api/cv"))!["links"]!["platform"]);
    }

    [Fact]
    public async Task Blocking_ends_sessions_and_hides_the_cv()
    {
        var client = await _factory.SignUpWithTenantAsync("blocked@example.org", "blocky");
        var code = await _factory.CreateInviteAsync("blocky", new { profile = "full" });
        var visitor = _factory.ClientFor(ApiFactory.SharedHost);
        (await visitor.PostAsJsonAsync("/api/access/redeem", new { code })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, (await visitor.GetAsync("/api/cv")).StatusCode);

        var userId = await _factory.UserIdAsync(client);
        Assert.Equal(HttpStatusCode.NoContent, (await _factory.AdminClient().PostAsync($"/api/admin/users/{userId}/block", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await visitor.GetAsync("/api/cv")).StatusCode);

        // Signing in again is refused.
        var again = _factory.SessionClient();
        (await again.PostAsJsonAsync("/api/auth/magic-link", new { email = "blocked@example.org" })).EnsureSuccessStatusCode();
        var link = Regex.Match(_factory.Email.Sent[^1].Text, @"https?://\S+").Value;
        Assert.Contains("error=blocked", (await again.GetAsync(new Uri(link).PathAndQuery)).Headers.Location!.ToString());

        Assert.Equal(HttpStatusCode.NoContent, (await _factory.AdminClient().PostAsync($"/api/admin/users/{userId}/unblock", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await visitor.GetAsync("/api/cv")).StatusCode);
    }

    [Fact]
    public async Task Sign_out_everywhere_ends_other_sessions()
    {
        var first = await _factory.SignInAsync("multi@example.org");
        var second = await _factory.SignInAsync("multi@example.org");
        Assert.Equal(HttpStatusCode.NoContent, (await first.PostAsync("/api/account/sessions/revoke", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await first.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await second.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Deleting_the_account_removes_tenant_files_and_invites()
    {
        var client = await _factory.SignUpWithTenantAsync("bye@example.org", "goodbye");
        var code = await _factory.CreateInviteAsync("goodbye", new { profile = "full" });

        var export = await client.GetAsync("/api/account/export");
        Assert.Equal("application/zip", export.Content.Headers.ContentType!.MediaType);
        using (var zip = new ZipArchive(await export.Content.ReadAsStreamAsync()))
        {
            Assert.Contains(zip.Entries, e => e.FullName == "account.json");
            Assert.Contains(zip.Entries, e => e.FullName == "cv/tenant.json");
        }

        var wrong = new HttpRequestMessage(HttpMethod.Delete, "/api/account") { Content = JsonContent.Create(new { confirm = "nope" }) };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(wrong)).StatusCode);
        var delete = new HttpRequestMessage(HttpMethod.Delete, "/api/account") { Content = JsonContent.Create(new { confirm = "goodbye" }) };
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(delete)).StatusCode);

        Assert.False(Directory.Exists(Path.Combine(_factory.DataPath, "tenants", "goodbye")));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        var visitor = _factory.ClientFor(ApiFactory.SharedHost);
        Assert.Equal(HttpStatusCode.BadRequest, (await visitor.PostAsJsonAsync("/api/access/redeem", new { code })).StatusCode);
    }

    [Fact]
    public async Task LinkedIn_import_previews_and_applies()
    {
        var client = await _factory.SignUpWithTenantAsync("li@example.org", "linked");
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var w = new StreamWriter(zip.CreateEntry("Profile.csv").Open()))
                w.Write("First Name,Last Name,Headline,Summary\nJane,Doe,Engineer,Hello\n");
            using (var w = new StreamWriter(zip.CreateEntry("Positions.csv").Open()))
                w.Write("Company Name,Title,Description,Location,Started On,Finished On\nACME,Dev,,Vienna,Mar 2020,\n");
        }

        async Task<HttpResponseMessage> Upload(bool apply)
        {
            var form = new MultipartFormDataContent { { new ByteArrayContent(buffer.ToArray()), "file", "export.zip" } };
            return await client.PostAsync($"/api/account/import/linkedin?locale=en&apply={apply.ToString().ToLowerInvariant()}", form);
        }

        var preview = await (await Upload(false)).Content.ReadFromJsonAsync<JsonObject>();
        Assert.Equal(1, preview!["counts"]!["experiences"]!.GetValue<int>());
        Assert.DoesNotContain("ACME", File.ReadAllText(Path.Combine(_factory.DataPath, "tenants", "linked", "cv.en.json")));

        Assert.Equal(HttpStatusCode.OK, (await Upload(true)).StatusCode);
        Assert.Contains("ACME", File.ReadAllText(Path.Combine(_factory.DataPath, "tenants", "linked", "cv.en.json")));

        var notZip = new MultipartFormDataContent { { new ByteArrayContent("hello"u8.ToArray()), "file", "x.zip" } };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/account/import/linkedin", notZip)).StatusCode);
    }

    [Fact]
    public async Task Heatmaps_need_pro_but_basic_statistics_do_not()
    {
        var client = await _factory.SignUpWithTenantAsync("heat@example.org", "heater");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/admin/tenants/heater/analytics/groups")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/admin/tenants/heater/analytics/overview")).StatusCode);
        var locked = await client.GetAsync("/api/admin/tenants/heater/analytics/heatmap");
        Assert.Equal(HttpStatusCode.PaymentRequired, locked.StatusCode);
        Assert.Equal("heatmaps", (await locked.Content.ReadFromJsonAsync<JsonObject>())!["feature"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.PaymentRequired, (await client.GetAsync("/api/admin/tenants/heater/analytics/cv-snapshots/abc")).StatusCode);

        // The super-admin and managed tenants are not limited.
        Assert.Equal(HttpStatusCode.OK, (await _factory.AdminClient().GetAsync("/api/admin/tenants/heater/analytics/heatmap")).StatusCode);

        var userId = await _factory.UserIdAsync(client);
        (await _factory.AdminClient().PutAsJsonAsync($"/api/admin/users/{userId}/plan", new { addDays = 7 })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/admin/tenants/heater/analytics/heatmap")).StatusCode);
    }

    [Fact]
    public async Task Owner_gets_an_email_when_an_invite_is_opened_first()
    {
        var client = await _factory.SignUpWithTenantAsync("notify@example.org", "notifier");
        var code = await _factory.CreateInviteAsync("notifier", new { profile = "full", label = "ACME recruiting" });
        var visitor = _factory.ClientFor(ApiFactory.SharedHost);
        (await visitor.PostAsJsonAsync("/api/access/redeem", new { code })).EnsureSuccessStatusCode();
        (await _factory.ClientFor(ApiFactory.SharedHost).PostAsJsonAsync("/api/access/redeem", new { code })).EnsureSuccessStatusCode();

        await WaitForAsync(() => _factory.Email.Sent.Any(m => m.To == "notify@example.org" && m.Subject == "Your CV was opened"));
        await Task.Delay(200);
        var mails = _factory.Email.Sent.Where(m => m.Subject == "Your CV was opened").ToList();
        Assert.Single(mails);     // only the first opening
        Assert.Contains("ACME recruiting", mails[0].Text);

        // Turned off: no more e-mails.
        Assert.Equal(HttpStatusCode.NoContent, (await client.PatchAsJsonAsync("/api/account", new { notifyOnOpen = false })).StatusCode);
        Assert.False((await client.GetFromJsonAsync<JsonObject>("/api/auth/me"))!["notifyOnOpen"]!.GetValue<bool>());
        var second = await _factory.CreateInviteAsync("notifier", new { profile = "full", label = "Other" });
        (await visitor.PostAsJsonAsync("/api/access/redeem", new { code = second })).EnsureSuccessStatusCode();
        await Task.Delay(300);
        Assert.DoesNotContain(_factory.Email.Sent, m => m.Text.Contains("Other"));
    }

    [Fact]
    public async Task Pro_reminder_is_sent_once_three_days_before_the_end()
    {
        var client = await _factory.SignInAsync("remind@example.org");
        var userId = await _factory.UserIdAsync(client);
        (await _factory.AdminClient().PutAsJsonAsync($"/api/admin/users/{userId}/plan", new { addDays = 7 })).EnsureSuccessStatusCode();
        var notifier = _factory.Services.GetRequiredService<OwnerNotifier>();

        Assert.Equal(0, await notifier.SendProRemindersAsync(CancellationToken.None));
        _factory.Clock.Advance(TimeSpan.FromDays(5));
        Assert.Equal(1, await notifier.SendProRemindersAsync(CancellationToken.None));
        Assert.Equal(0, await notifier.SendProRemindersAsync(CancellationToken.None));
        Assert.Contains(_factory.Email.Sent, m => m.To == "remind@example.org" && m.Subject == "Your Pro pass ends soon");
    }

    [Fact]
    public async Task Super_admin_sees_platform_stats()
    {
        var client = await _factory.SignUpWithTenantAsync("stats@example.org", "statsy");
        var stats = await _factory.AdminClient().GetFromJsonAsync<JsonObject>("/api/admin/stats");
        Assert.Equal(1, stats!["users"]!.GetValue<int>());
        Assert.Equal(1, stats["withTenant"]!.GetValue<int>());
        Assert.Equal(0, stats["pro"]!.GetValue<int>());
        Assert.Single(stats["signupsPerDay"]!.AsArray());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/admin/stats")).StatusCode);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (var i = 0; i < 50 && !condition(); i++) await Task.Delay(100);
        Assert.True(condition());
    }

    [Theory]
    [InlineData("example.com", true)]
    [InlineData("cv.jane-doe.at", true)]
    [InlineData("localhost", false)]
    [InlineData("1.2.3.4", false)]
    [InlineData("-bad.example", false)]
    [InlineData("bad_.example", false)]
    public void Domain_validation(string domain, bool valid) => Assert.Equal(valid, AccountEndpoints.IsValidDomain(domain));

    [Fact]
    public async Task Own_domain_needs_pro()
    {
        var client = await _factory.SignUpWithTenantAsync("domain@example.org", "domainer");
        Assert.Equal(HttpStatusCode.PaymentRequired, (await client.PutAsJsonAsync("/api/account/domain", new { domain = "cv.domainer.example" })).StatusCode);
    }
}

public class BillingTests : IDisposable
{
    private readonly AccountApiFactory _factory = new();

    public void Dispose()
    {
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    private static string Sign(string body, DateTimeOffset at, string secret = AccountApiFactory.WebhookSecret)
    {
        var ts = at.ToUnixTimeSeconds().ToString();
        var h1 = Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{ts}:{body}")));
        return $"ts={ts};h1={h1}";
    }

    private async Task<HttpResponseMessage> PostWebhookAsync(string body, string? signature = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/billing/paddle/webhook") { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.Add(BillingEndpoints.SignatureHeader, signature ?? Sign(body, _factory.Clock.GetUtcNow()));
        return await _factory.ClientFor(ApiFactory.SharedHost).SendAsync(request);
    }

    private static string Transaction(string id, Guid? userId, string priceId = "pri_month") => new JsonObject
    {
        ["event_type"] = "transaction.completed",
        ["data"] = new JsonObject
        {
            ["id"] = id,
            ["currency_code"] = "EUR",
            ["custom_data"] = userId is null ? null : new JsonObject { ["userId"] = userId.ToString() },
            ["items"] = new JsonArray(new JsonObject { ["price"] = new JsonObject { ["id"] = priceId }, ["quantity"] = 1 }),
            ["details"] = new JsonObject { ["totals"] = new JsonObject { ["grand_total"] = "1700" } },
        },
    }.ToJsonString();

    [Fact]
    public void Signature_check()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.True(BillingEndpoints.VerifySignature(Sign("{}", now), "{}", AccountApiFactory.WebhookSecret, now));
        Assert.False(BillingEndpoints.VerifySignature(Sign("{}", now), "{ }", AccountApiFactory.WebhookSecret, now));
        Assert.False(BillingEndpoints.VerifySignature(Sign("{}", now, "other"), "{}", AccountApiFactory.WebhookSecret, now));
        Assert.False(BillingEndpoints.VerifySignature(Sign("{}", now.AddMinutes(-10)), "{}", AccountApiFactory.WebhookSecret, now));
        Assert.False(BillingEndpoints.VerifySignature("ts=1;h1=zz", "{}", AccountApiFactory.WebhookSecret, now));
        Assert.False(BillingEndpoints.VerifySignature("", "{}", AccountApiFactory.WebhookSecret, now));
        // Several h1 during secret rotation: one matching is enough.
        var rotated = Sign("{}", now, "old") + ";h1=" + Sign("{}", now).Split("h1=")[1];
        Assert.True(BillingEndpoints.VerifySignature(rotated, "{}", AccountApiFactory.WebhookSecret, now));
    }

    [Fact]
    public async Task Config_lists_passes_with_price_ids()
    {
        var config = await _factory.ClientFor(ApiFactory.SharedHost).GetFromJsonAsync<JsonObject>("/api/billing/config");
        Assert.Equal("paddle", config!["provider"]!.GetValue<string>());
        Assert.Equal("sandbox", config["environment"]!.GetValue<string>());
        var passes = config["passes"]!.AsArray();
        Assert.Equal(["week", "month", "halfyear", "year"], passes.Select(p => p!["id"]!.GetValue<string>()));
        Assert.Equal("pri_month", passes[1]!["priceId"]!.GetValue<string>());
        Assert.Equal(500, passes[0]!["amount"]!.GetValue<long>());
    }

    [Fact]
    public async Task Completed_transaction_extends_pro_once()
    {
        var client = await _factory.SignInAsync("buyer@example.org");
        var userId = await _factory.UserIdAsync(client);

        Assert.Equal(HttpStatusCode.Unauthorized, (await PostWebhookAsync(Transaction("txn_1", userId), "ts=1;h1=00")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await PostWebhookAsync(Transaction("txn_1", userId))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PostWebhookAsync(Transaction("txn_1", userId))).StatusCode); // retried delivery
        var me = await client.GetFromJsonAsync<JsonObject>("/api/auth/me");
        Assert.Equal("pro", me!["plan"]!["name"]!.GetValue<string>());
        var until = me["plan"]!["proUntil"]!.GetValue<DateTimeOffset>();
        Assert.InRange((until - _factory.Clock.GetUtcNow()).TotalDays, 29.9, 30.1);

        // A second pass is added on top.
        await PostWebhookAsync(Transaction("txn_2", userId));
        me = await client.GetFromJsonAsync<JsonObject>("/api/auth/me");
        Assert.InRange((me!["plan"]!["proUntil"]!.GetValue<DateTimeOffset>() - _factory.Clock.GetUtcNow()).TotalDays, 59.9, 60.1);

        var payments = await client.GetFromJsonAsync<JsonArray>("/api/account/payments");
        Assert.Equal(2, payments!.Count);
        Assert.All(payments, p => Assert.Equal(1700, p!["amount"]!.GetValue<long>()));
    }

    [Fact]
    public async Task Unknown_user_or_price_is_recorded_as_unmatched()
    {
        await PostWebhookAsync(Transaction("txn_x", Guid.NewGuid()));
        await PostWebhookAsync(Transaction("txn_y", null, "pri_unknown"));
        var payments = await _factory.AdminClient().GetFromJsonAsync<JsonArray>("/api/admin/payments");
        Assert.Equal(2, payments!.Count(p => p!["status"]!.GetValue<string>() == "unmatched"));
    }
}
