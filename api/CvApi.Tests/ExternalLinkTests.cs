using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using CvApi.Links;

namespace CvApi.Tests;

public sealed class ExternalLinkTests : IDisposable
{
    private const string Shop = "https://www.shop.example.com/";
    private const string SecretSite = "https://secret.example.com/";

    private readonly ApiFactory _factory = new();

    public ExternalLinkTests()
    {
        _factory.SetPublicProfile("alice", "public");
        _factory.WriteCv("alice", "en", $$"""
            {
              "profile": { "name": "Alice" },
              "experiences": [
                { "id": 1, "company": "ACME", "startDate": "2020", "endDate": null, "url": "https://acme.example.com" },
                { "id": 2, "company": "Secret", "startDate": "2018", "endDate": "2019", "requires": ["private"], "url": "{{SecretSite}}" }
              ],
              "projects": [ { "id": 1, "name": "Shop", "startDate": "2021", "endDate": "2022", "url": "{{Shop}}" } ]
            }
            """);
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Website_links_are_delivered_as_redirects_through_the_api()
    {
        var cv = await CvOf(await FullClient());

        var project = cv["projects"]![0]!;
        Assert.Equal(ExternalLinks.GoPrefix + ExternalLinks.Key(Shop), project["url"]!.GetValue<string>());
        Assert.Equal("shop.example.com", project["urlHost"]!.GetValue<string>());
        Assert.Equal(ExternalLinks.GoPrefix + ExternalLinks.Key(SecretSite), cv["experiences"]![1]!["url"]!.GetValue<string>());
        Assert.DoesNotContain("example.com/", cv.ToJsonString());

        // The public profile hides companies: their websites would name them. The project (no client) keeps its link.
        var publicCv = await CvOf(_factory.ClientFor(ApiFactory.AliceHost));
        Assert.Null(publicCv["experiences"]![0]!["url"]);
        Assert.NotNull(publicCv["projects"]![0]!["url"]);
    }

    [Fact]
    public async Task Go_redirects_only_to_links_the_visitor_may_see()
    {
        var client = _factory.CreateClient(new() { BaseAddress = new Uri($"http://{ApiFactory.AliceHost}"), AllowAutoRedirect = false });
        var response = await client.GetAsync(ExternalLinks.GoPrefix + ExternalLinks.Key(Shop));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(Shop, response.Headers.Location!.ToString());

        // Hidden entry (requires "private") and hidden company website: not reachable for the public profile.
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(ExternalLinks.GoPrefix + ExternalLinks.Key(SecretSite))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(ExternalLinks.GoPrefix + ExternalLinks.Key("https://acme.example.com"))).StatusCode);
    }

    private async Task<HttpClient> FullClient()
    {
        var code = await _factory.CreateInviteAsync("alice", new { profile = "full" });
        var client = _factory.ClientFor(ApiFactory.SharedHost);
        (await client.PostAsJsonAsync("/api/access/redeem", new { code })).EnsureSuccessStatusCode();
        return client;
    }

    private static async Task<JsonNode> CvOf(HttpClient client) =>
        (await client.GetFromJsonAsync<JsonObject>("/api/cv?locale=en"))!["cv"]!;

}
