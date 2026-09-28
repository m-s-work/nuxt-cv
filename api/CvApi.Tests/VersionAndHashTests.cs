using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace CvApi.Tests;

public sealed class VersionAndHashTests : IDisposable
{
    private readonly PdfApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private static string Sha(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private HttpClient Admin()
    {
        var admin = _factory.ClientFor(ApiFactory.SharedHost);
        admin.DefaultRequestHeaders.Add("X-Admin-Key", ApiFactory.AdminKey);
        return admin;
    }

    [Fact]
    public async Task Version_reports_api_and_renderer_builds()
    {
        var body = await _factory.ClientFor(ApiFactory.SharedHost).GetFromJsonAsync<JsonObject>("/api/version");
        Assert.Equal("unknown", body!["api"]!["commit"]!.GetValue<string>());       // no SOURCE_COMMIT in tests
        Assert.Equal("renderer-sha", body["pdf"]!["commit"]!.GetValue<string>());
    }

    [Fact]
    public async Task Tenant_hashes_match_sha256sum_of_the_files()
    {
        var dir = Path.Combine(_factory.DataPath, "tenants", "alice");
        var body = await Admin().GetFromJsonAsync<JsonObject>("/api/admin/tenants/alice/hash");
        var files = body!["files"]!.AsObject();

        Assert.Equal(["assets/alice.jpg", "assets/unlisted.jpg", "cv.de.json", "cv.en.json", "tenant.json"],
            files.Select(f => f.Key).ToArray());
        foreach (var (path, hash) in files)
            Assert.Equal(Sha(File.ReadAllBytes(Path.Combine(dir, path))), hash!.GetValue<string>());

        // Combined hash = sha256 over `sha256sum`-style lines ("<hash>  <path>\n", sorted by path).
        var lines = string.Concat(files.Select(f => $"{f.Value!.GetValue<string>()}  {f.Key}\n"));
        Assert.Equal(Sha(Encoding.UTF8.GetBytes(lines)), body["combined"]!.GetValue<string>());
    }

    [Fact]
    public async Task Combined_hash_changes_when_the_cv_changes()
    {
        var before = (await Admin().GetFromJsonAsync<JsonArray>("/api/admin/tenants"))!
            .Single(t => t!["id"]!.GetValue<string>() == "alice")!["dataHash"]!.GetValue<string>();

        _factory.WriteCv("alice", "en", """{ "profile": { "name": "Alice v2" } }""");

        var after = (await Admin().GetFromJsonAsync<JsonObject>("/api/admin/tenants/alice/hash"))!["combined"]!.GetValue<string>();
        Assert.NotEqual(before, after);
        Assert.Equal(HttpStatusCode.NotFound, (await Admin().GetAsync("/api/admin/tenants/nobody/hash")).StatusCode);
    }

    [Fact]
    public async Task Cv_response_contains_the_hash_of_the_redacted_cv()
    {
        var admin = Admin();
        var created = await (await admin.PostAsJsonAsync("/api/admin/tenants/alice/invites", new { profile = "full" })).Content.ReadFromJsonAsync<JsonObject>();
        var visitor = _factory.ClientFor(ApiFactory.SharedHost);
        await visitor.PostAsJsonAsync("/api/access/redeem", new { code = created!["code"]!.GetValue<string>() });

        var body = await visitor.GetFromJsonAsync<JsonObject>("/api/cv");
        var cvJson = body!["cv"]!.ToJsonString();
        Assert.Equal(Sha(Encoding.UTF8.GetBytes(cvJson)), body["cvHash"]!.GetValue<string>());
    }
}
