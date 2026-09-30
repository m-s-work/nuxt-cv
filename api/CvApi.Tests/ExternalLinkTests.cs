using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using CvApi.Media;
using Microsoft.Extensions.DependencyInjection;

namespace CvApi.Tests;

public sealed class ExternalLinkTests : IDisposable
{
    private const string Logo = "https://img.example.com/logo.png";
    private const string Page = "https://img.example.com/page.html";
    private const string Shop = "https://www.shop.example.com/";
    private const string SecretSite = "https://secret.example.com/";

    private readonly ApiFactory _factory = new();
    private readonly FakeOrigin _origin = new();

    public ExternalLinkTests()
    {
        _factory.ExtraServices = services => services.AddHttpClient(ExternalMedia.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => _origin);
        _factory.SetPublicProfile("alice", "public");
        _factory.WriteCv("alice", "en", $$"""
            {
              "profile": { "name": "Alice" },
              "experiences": [
                { "id": 1, "company": "ACME", "startDate": "2020", "endDate": null, "url": "https://acme.example.com", "logos": ["{{Logo}}", "{{Page}}"] },
                { "id": 2, "company": "Secret", "startDate": "2018", "endDate": "2019", "requires": ["private"], "url": "{{SecretSite}}" }
              ],
              "projects": [ { "id": 1, "name": "Shop", "startDate": "2021", "endDate": "2022", "url": "{{Shop}}", "screenshots": ["{{Logo}}"] } ]
            }
            """);
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task External_images_and_links_are_delivered_as_proxied_paths()
    {
        var cv = await CvOf(await FullClient());

        var project = cv["projects"]![0]!;
        Assert.Equal(ExternalLinks.GoPrefix + ExternalLinks.Key(Shop), project["url"]!.GetValue<string>());
        Assert.Equal("shop.example.com", project["urlHost"]!.GetValue<string>());
        Assert.Equal(ExternalLinks.MediaPrefix + ExternalLinks.Key(Logo), project["screenshots"]![0]!.GetValue<string>());
        Assert.Equal(ExternalLinks.GoPrefix + ExternalLinks.Key(SecretSite), cv["experiences"]![1]!["url"]!.GetValue<string>());
        Assert.DoesNotContain("example.com/", cv.ToJsonString());

        // The public profile hides companies: their websites would name them. The project (no client) keeps its link.
        var publicCv = await CvOf(_factory.ClientFor(ApiFactory.AliceHost));
        Assert.Null(publicCv["experiences"]![0]!["url"]);
        Assert.NotNull(publicCv["projects"]![0]!["url"]);
    }

    [Fact]
    public async Task Media_is_fetched_once_cached_and_limited_to_images_in_the_visitors_cv()
    {
        var client = await FullClient();
        var logo = await client.GetAsync(ExternalLinks.MediaPrefix + ExternalLinks.Key(Logo));
        Assert.Equal(HttpStatusCode.OK, logo.StatusCode);
        Assert.Equal("image/png", logo.Content.Headers.ContentType!.MediaType);
        Assert.Equal("png", await logo.Content.ReadAsStringAsync());
        Assert.Contains("sandbox", logo.Headers.GetValues("Content-Security-Policy").Single());

        (await client.GetAsync(ExternalLinks.MediaPrefix + ExternalLinks.Key(Logo))).EnsureSuccessStatusCode();
        Assert.Equal(1, _origin.Requests.Count(r => r == Logo));

        // Not an image, not in the CV, not a key: nothing.
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(ExternalLinks.MediaPrefix + ExternalLinks.Key(Page))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(ExternalLinks.MediaPrefix + ExternalLinks.Key("https://evil.example.com/x.png"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(ExternalLinks.MediaPrefix + "../../etc/passwd")).StatusCode);
        Assert.DoesNotContain("https://evil.example.com/x.png", _origin.Requests);

        // Without access: nothing.
        var anonymous = _factory.ClientFor(ApiFactory.SharedHost);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync(ExternalLinks.MediaPrefix + ExternalLinks.Key(Logo))).StatusCode);
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

    [Theory]
    [InlineData("8.8.8.8", true)]
    [InlineData("2606:4700::1111", true)]
    [InlineData("127.0.0.1", false)]
    [InlineData("10.1.2.3", false)]
    [InlineData("172.20.0.5", false)]
    [InlineData("192.168.1.1", false)]
    [InlineData("169.254.169.254", false)]
    [InlineData("::1", false)]
    [InlineData("fd00::1", false)]
    [InlineData("::ffff:127.0.0.1", false)]
    public void Only_public_addresses_are_fetched(string address, bool expected) =>
        Assert.Equal(expected, ExternalMedia.IsPublic(IPAddress.Parse(address)));

    private async Task<HttpClient> FullClient()
    {
        var code = await _factory.CreateInviteAsync("alice", new { profile = "full" });
        var client = _factory.ClientFor(ApiFactory.SharedHost);
        (await client.PostAsJsonAsync("/api/access/redeem", new { code })).EnsureSuccessStatusCode();
        return client;
    }

    private static async Task<JsonNode> CvOf(HttpClient client) =>
        (await client.GetFromJsonAsync<JsonObject>("/api/cv?locale=en"))!["cv"]!;

    private sealed class FakeOrigin : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var url = request.RequestUri!.ToString();
            lock (Requests) Requests.Add(url);
            var (body, type) = url == Logo ? ("png", "image/png") : ("<html></html>", "text/html");
            var content = new StringContent(body);
            content.Headers.ContentType = new(type);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }
}
