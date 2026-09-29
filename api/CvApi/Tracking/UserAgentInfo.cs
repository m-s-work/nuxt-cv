namespace CvApi.Tracking;

/// <summary>Coarse device class, browser and OS family from a user agent (R3.4) – no version details.</summary>
public sealed record UserAgentInfo(string Device, string Browser, string Os)
{
    public static UserAgentInfo Parse(string? ua)
    {
        ua ??= "";
        bool Has(string s) => ua.Contains(s, StringComparison.OrdinalIgnoreCase);

        var os = Has("Windows") ? "Windows"
            : Has("iPhone") || Has("iPad") || Has("iPod") ? "iOS"
            : Has("Mac OS X") || Has("Macintosh") ? "macOS"
            : Has("Android") ? "Android"
            : Has("CrOS") ? "ChromeOS"
            : Has("Linux") ? "Linux"
            : "other";

        var browser = Has("Edg/") ? "Edge"
            : Has("OPR/") || Has("Opera") ? "Opera"
            : Has("Firefox/") || Has("FxiOS") ? "Firefox"
            : Has("Chrome/") || Has("CriOS") ? "Chrome"
            : Has("Safari/") ? "Safari"
            : "other";

        var device = Has("iPad") || Has("Tablet") || (Has("Android") && !Has("Mobile")) ? "tablet"
            : Has("Mobi") || Has("iPhone") ? "mobile"
            : "desktop";

        return new UserAgentInfo(device, browser, os);
    }
}
