using System.Net;
using MaxMind.GeoIP2;
using MaxMind.GeoIP2.Exceptions;

namespace CvApi.Tracking;

public sealed record GeoInfo(string? Country, string? Region, string? City, long? Asn, string? AsOrg);

/// <summary>
/// Local IP → location / network lookup (R3.7). Reads MaxMind-format databases from the data volume
/// (DB-IP Lite or GeoLite2: {DataPath}/geo/city.mmdb and {DataPath}/geo/asn.mmdb). No external service is called;
/// missing files simply mean no location data.
/// </summary>
public sealed class GeoLookup : IDisposable
{
    private readonly DatabaseReader? _city;
    private readonly DatabaseReader? _asn;

    public GeoLookup(IConfiguration configuration, ILogger<GeoLookup> logger)
    {
        var dir = Path.Combine(Path.GetFullPath(configuration["Cv:DataPath"] ?? "/data"), "geo");
        _city = Open(configuration["Tracking:GeoCityDb"] ?? Path.Combine(dir, "city.mmdb"), logger);
        _asn = Open(configuration["Tracking:GeoAsnDb"] ?? Path.Combine(dir, "asn.mmdb"), logger);
    }

    public bool Available => _city is not null || _asn is not null;

    public GeoInfo Lookup(IPAddress? ip)
    {
        if (ip is null) return new GeoInfo(null, null, null, null, null);
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
