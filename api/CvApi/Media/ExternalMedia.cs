using System.Net;
using System.Net.Sockets;

namespace CvApi.Media;

/// <summary>
/// Fetches and caches external images referenced by CVs (see <see cref="ExternalLinks"/>). Only images, at most
/// 10 MB, only from public addresses. Cached on disk; a stale copy is served when the origin is unreachable.
/// </summary>
public sealed class ExternalMedia
{
    public const string HttpClientName = "external-media";
    public const long MaxBytes = 10 * 1024 * 1024;

    private static readonly HashSet<string> ImageTypes =
        ["image/png", "image/jpeg", "image/gif", "image/webp", "image/avif", "image/svg+xml", "image/x-icon", "image/vnd.microsoft.icon"];

    private readonly IHttpClientFactory _http;
    private readonly TimeProvider _time;
    private readonly ILogger<ExternalMedia> _logger;
    private readonly string _dir;
    private readonly TimeSpan _ttl;

    public ExternalMedia(IHttpClientFactory http, IConfiguration config, TimeProvider time, ILogger<ExternalMedia> logger)
    {
        _http = http;
        _time = time;
        _logger = logger;
        _dir = Path.Combine(Path.GetFullPath(config["Cv:DataPath"] ?? "/data"), "cache", "external-media");
        _ttl = TimeSpan.FromHours(config.GetValue("Cv:ExternalMediaCacheHours", 24));
    }

    public sealed record CachedFile(string Path, string ContentType);

    /// <summary>The cached image of a URL (fetched if missing or older than the cache time), or null.</summary>
    public async Task<CachedFile?> GetAsync(string url, CancellationToken ct)
    {
        var key = ExternalLinks.Key(url);
        var file = Path.Combine(_dir, key);
        var typeFile = file + ".type";
        var cached = File.Exists(file) && File.Exists(typeFile) ? new CachedFile(file, await File.ReadAllTextAsync(typeFile, ct)) : null;
        if (cached is not null && _time.GetUtcNow() - File.GetLastWriteTimeUtc(file) < _ttl) return cached;

        try
        {
            using var response = await _http.CreateClient(HttpClientName).GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            var type = response.Content.Headers.ContentType?.MediaType?.ToLowerInvariant();
            if (!response.IsSuccessStatusCode || type is null || !ImageTypes.Contains(type))
            {
                _logger.LogWarning("External image {Url}: HTTP {Status}, type {Type}", url, (int)response.StatusCode, type);
                return cached;
            }
            if (response.Content.Headers.ContentLength > MaxBytes) return cached;

            Directory.CreateDirectory(_dir);
            var temp = Path.Combine(_dir, $"{key}.{Guid.NewGuid():N}.tmp");
            try
            {
                await using (var source = await response.Content.ReadAsStreamAsync(ct))
                await using (var target = File.Create(temp))
                {
                    var buffer = new byte[81920];
                    long total = 0;
                    int read;
                    while ((read = await source.ReadAsync(buffer, ct)) > 0)
                    {
                        total += read;
                        if (total > MaxBytes) return cached;
                        await target.WriteAsync(buffer.AsMemory(0, read), ct);
                    }
                }
                File.Move(temp, file, overwrite: true);
                File.SetLastWriteTimeUtc(file, _time.GetUtcNow().UtcDateTime);
                await File.WriteAllTextAsync(typeFile, type, ct);
                return new CachedFile(file, type);
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException && !ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "External image {Url} could not be fetched", url);
            return cached;
        }
    }

    /// <summary>HTTP handler that only connects to public addresses (no loopback, private or link-local networks).</summary>
    /// <remarks>Connects directly (no system proxy): the address check has to see the real destination.</remarks>
    public static SocketsHttpHandler PublicOnlyHandler() => new()
    {
        UseProxy = false,
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = 3,
        ConnectTimeout = TimeSpan.FromSeconds(5),
        ConnectCallback = async (context, ct) =>
        {
            var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, ct);
            var address = addresses.FirstOrDefault(IsPublic)
                ?? throw new HttpRequestException($"{context.DnsEndPoint.Host} has no public address");
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), ct);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        },
    };

    public static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any)) return false;
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
            return !(address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6UniqueLocal || address.IsIPv6Multicast);
        var b = address.GetAddressBytes();
        return !(b[0] == 10 || b[0] == 0 || b[0] >= 224
            || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
            || (b[0] == 192 && b[1] == 168)
            || (b[0] == 169 && b[1] == 254)
            || (b[0] == 100 && b[1] >= 64 && b[1] <= 127));
    }
}
