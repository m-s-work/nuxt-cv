using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CvApi.Tests;

/// <summary>Runs the API against a temporary copy-free data directory with two tenants.</summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    public const string AdminKey = "test-admin-key";
    public const string AliceHost = "alice-cv.example.org";
    public const string SharedHost = "cv.example.org";

    public string DataPath { get; } = Path.Combine(Path.GetTempPath(), "cvapi-tests-" + Guid.NewGuid().ToString("N"));

    public ApiFactory()
    {
        WriteTenant("alice", """
            {
              "name": "Alice",
              "hosts": ["Alice-CV.example.org:443"],
              "profiles": {
                "public": { "flags": { "hideCompanies": true, "hideTimeframeMonths": true, "hidePhoto": true } },
                "full":   { "grants": ["private"] }
              }
            }
            """,
            """
            {
              "profile": { "name": "Alice", "photoUrl": "/api/assets/alice.jpg" },
              "experiences": [
                { "id": 1, "company": "ACME", "companyAlias": "Big corp", "startDate": "2020-03-15", "endDate": null, "period": "Mar 2020 - now", "logos": ["/api/assets/acme.svg"] },
                { "id": 2, "company": "Secret", "startDate": "2018-01-01", "endDate": "2019-12-31", "requires": ["private"] }
              ]
            }
            """);
        File.WriteAllText(Path.Combine(DataPath, "tenants", "alice", "assets", "alice.jpg"), "jpg");
        File.WriteAllText(Path.Combine(DataPath, "tenants", "alice", "assets", "unlisted.jpg"), "jpg");

        WriteTenant("bob", """
            { "name": "Bob", "hosts": [], "profiles": { "full": {} } }
            """,
            """
            { "profile": { "name": "Bob" }, "experiences": [] }
            """);
    }

    public void WriteCv(string tenant, string locale, string json) =>
        File.WriteAllText(Path.Combine(DataPath, "tenants", tenant, $"cv.{locale}.json"), json);

    public void SetHosts(string tenant, params string[] hosts)
    {
        var file = Path.Combine(DataPath, "tenants", tenant, "tenant.json");
        var node = JsonNode.Parse(File.ReadAllText(file))!.AsObject();
        node["hosts"] = new JsonArray(hosts.Select(h => (JsonNode?)JsonValue.Create(h)).ToArray());
        File.WriteAllText(file, node.ToJsonString());
    }

    public void UpdateTenant(string tenant, Action<JsonObject> change)
    {
        var file = Path.Combine(DataPath, "tenants", tenant, "tenant.json");
        var node = JsonNode.Parse(File.ReadAllText(file))!.AsObject();
        change(node);
        File.WriteAllText(file, node.ToJsonString());
    }

    public void SetPublicProfile(string tenant, string? profile)
    {
        var file = Path.Combine(DataPath, "tenants", tenant, "tenant.json");
        var node = JsonNode.Parse(File.ReadAllText(file))!.AsObject();
        node["publicProfile"] = profile;
        File.WriteAllText(file, node.ToJsonString());
    }

    public HttpClient ClientFor(string host)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri($"http://{host}") });
        return client;
    }

    public async Task<string> CreateInviteAsync(string tenant, object body)
    {
        var admin = ClientFor(SharedHost);
        admin.DefaultRequestHeaders.Add("X-Admin-Key", AdminKey);
        var response = await admin.PostAsJsonAsync($"/api/admin/tenants/{tenant}/invites", body);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        return json!["code"]!.GetValue<string>();
    }

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseSetting("Cv:DataPath", DataPath);
        builder.UseSetting("Cv:ConfigCacheSeconds", "0");
        builder.UseSetting("Cv:RedeemPerMinute", "1000");
        builder.UseSetting("Admin:ApiKey", AdminKey);
        builder.UseSetting("Git:AllowLocalRepos", "true");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try { Directory.Delete(DataPath, recursive: true); } catch (IOException) { }
    }

    private void WriteTenant(string id, string tenantJson, string cvJson)
    {
        var dir = Path.Combine(DataPath, "tenants", id);
        Directory.CreateDirectory(Path.Combine(dir, "assets"));
        File.WriteAllText(Path.Combine(dir, "tenant.json"), tenantJson);
        File.WriteAllText(Path.Combine(dir, "cv.en.json"), cvJson);
    }
}
