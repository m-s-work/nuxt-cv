using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using MaxMind.GeoIP2;
using MaxMind.GeoIP2.Exceptions;

namespace CvApi.Tracking;

public sealed record GeoInfo(string? Country, string? Region, string? City, long? Asn, string? AsOrg)
{
    public static GeoInfo None { get; } = new(null, null, null, null, null);
}

/// <summary>
/// IP → location / network lookup (R3.7), never through an external service:
/// <list type="bullet">
/// <item>the internal <c>geo</c> container (compose service, <c>Tracking:GeoUrl</c>), which downloads and refreshes
/// the DB-IP Lite databases itself, or</item>
/// <item>MaxMind-format files in the data volume ({DataPath}/geo/city.mmdb, asn.mmdb) when no URL is configured.</item>
/// </list>
/// Failures simply mean no location data for that session.
/// </summary>
public sealed class GeoLookup : IDisposable
{
    public const string HttpClientName = "geo";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(6);
    private const int MaxCacheEntries = 10_000;

    private readonly DatabaseReader? _city;
    private readonly DatabaseReader? _asn;
    private readonly string? _url;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<GeoLookup> _logger;
    private readonly TimeProvider _time;
    private readonly ConcurrentDictionary<string, (DateTimeOffset At, GeoInfo Info)> _cache = new();

    public GeoLookup(IConfiguration configuration, IHttpClientFactory http, TimeProvider time, ILogger<GeoLookup> logger)
    {
        _http = http;
        _time = time;
        _logger = logger;
        _url = configuration["Tracking:GeoUrl"] is { Length: > 0 } url ? url.TrimEnd('/') : null;
        if (_url is not null) return;
        var dir = Path.Combine(Path.GetFullPath(configuration["Cv:DataPath"] ?? "/data"), "geo");
        _city = Open(configuration["Tracking:GeoCityDb"] ?? Path.Combine(dir, "city.mmdb"), logger);
        _asn = Open(configuration["Tracking:GeoAsnDb"] ?? Path.Combine(dir, "asn.mmdb"), logger);
    }

    /// <summary>"service" (geo container), "files" (local databases) or null (no location data).</summary>
    public string? Source => _url is not null ? "service" : _city is not null || _asn is not null ? "files" : null;

    public bool Available => Source is not null;

    public async Task<GeoInfo> LookupAsync(IPAddress? ip, CancellationToken ct)
    {
        if (ip is null) return GeoInfo.None;
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        if (_url is null) return LookupFiles(ip);

        var key = ip.ToString();
        var now = _time.GetUtcNow();
        if (_cache.TryGetValue(key, out var cached) && now - cached.At < CacheDuration) return cached.Info;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(2));
            var client = _http.CreateClient(HttpClientName);
            var result = await client.GetFromJsonAsync<GeoInfo>($"{_url}/lookup?ip={Uri.EscapeDataString(key)}", timeout.Token)
                ?? GeoInfo.None;
            if (_cache.Count >= MaxCacheEntries) _cache.Clear();
            _cache[key] = (now, result);
            return result;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            _logger.LogWarning("Geo lookup failed: {Message}", ex.Message);
            return GeoInfo.None;
        }
    }

    /// <summary>Database releases reported by the geo container, for the admin (null without service).</summary>
    public async Task<object?> StatusAsync(CancellationToken ct)
    {
        if (_url is null) return null;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(2));
            return await _http.CreateClient(HttpClientName).GetFromJsonAsync<System.Text.Json.Nodes.JsonObject>($"{_url}/health", timeout.Token);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            return new { error = "unreachable" };
        }
    }

    private GeoInfo LookupFiles(IPAddress ip)
    {
        string? country = null, region = null, city = null, org = null;
        long? asn = null;
        try
        {
            if (_city is not null && _city.TryCity(ip, out var c) && c is not null)
            {
                country = c.Country?.IsoCode;
                region = c.MostSpecificSubdivision?.Name;
                city = c.City?.Name;
            }
            if (_asn is not null && _asn.TryAsn(ip, out var a) && a is not null)
            {
                asn = a.AutonomousSystemNumber;
                org = a.AutonomousSystemOrganization;
            }
        }
        catch (Exception ex) when (ex is GeoIP2Exception or InvalidOperationException or InvalidCastException)
        {
            // Wrong database type or corrupt file: no location data for this session.
        }
        return new GeoInfo(country, region, city, asn, org);
    }

    private static DatabaseReader? Open(string path, ILogger logger)
    {
        if (!File.Exists(path)) return null;
        try
        {
            return new DatabaseReader(path);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Cannot open geo database {Path}", path);
            return null;
        }
    }

    public void Dispose()
    {
        _city?.Dispose();
        _asn?.Dispose();
    }
}
