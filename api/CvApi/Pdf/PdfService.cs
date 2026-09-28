using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using CvApi.Access;
using CvApi.Redaction;
using CvApi.Tenants;

namespace CvApi.Pdf;

/// <summary>Renders a URL to PDF (the internal renderer container, or a fake in tests).</summary>
public interface IPdfRenderer
{
    Task<byte[]> RenderAsync(Uri url, IReadOnlyDictionary<string, string> cookies, CancellationToken ct);

    /// <summary>Build identity of the renderer (GET /version), e.g. { commit, builtAt }.</summary>
    Task<System.Text.Json.Nodes.JsonObject?> VersionAsync(CancellationToken ct);
}

public sealed class PdfRenderException(string message) : Exception(message);

/// <summary>Calls the renderer service (pdf/server.mjs): POST /render { url, cookies } → application/pdf.</summary>
public sealed class HttpPdfRenderer(HttpClient http) : IPdfRenderer
{
    public async Task<System.Text.Json.Nodes.JsonObject?> VersionAsync(CancellationToken ct) =>
        await http.GetFromJsonAsync<System.Text.Json.Nodes.JsonObject>("version", ct);

    public async Task<byte[]> RenderAsync(Uri url, IReadOnlyDictionary<string, string> cookies, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync("render", new
        {
            url = url.ToString(),
            cookies = cookies.Select(c => new { name = c.Key, value = c.Value }),
        }, ct);
        if (!response.IsSuccessStatusCode)
            throw new PdfRenderException($"renderer returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(ct)}");
        return await response.Content.ReadAsByteArrayAsync(ct);
    }
}

public sealed record PdfResult(byte[] Content, bool FromCache, string FileName);

public sealed record PdfRenderOutcome(string Locale, bool Ok, bool FromCache, long? Bytes, string? Error);

/// <summary>
/// One PDF per grant (invite, or tenant public profile) and locale, cached under {DataPath}/pdf.
/// The cache key contains a hash of the redacted CV, so any change to the CV, the profile or the
/// invite overrides makes the cached PDF obsolete and it is re-rendered on the next request.
/// See docs/REQUIREMENTS_ACCESS_AND_TENANCY.md §12.
/// </summary>
public sealed class PdfService(
    IConfiguration configuration,
    TenantStore tenants,
    AccessService access,
    IServiceProvider services,
    ILogger<PdfService> logger)
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new();

    public bool Enabled => !string.IsNullOrEmpty(configuration["Pdf:RendererUrl"]);

    private string DataPath => Path.GetFullPath(configuration["Cv:DataPath"] ?? "/data");

    /// <summary>Returns the cached PDF if it matches the current CV, otherwise renders (and caches) a new one.</summary>
    public async Task<PdfResult?> GetOrRenderAsync(AccessGrant grant, string? requestedLocale, CancellationToken ct)
    {
        if (tenants.LoadCv(grant.Tenant, requestedLocale) is not { } loaded) return null;
        var (master, locale) = loaded;

        // The render URL carries the QR target (public host + QR invite code), so a changed host or
        // base URL makes the cached PDF stale as well.
        var renderUrl = await RenderUrlAsync(grant, locale, ct);
        var redacted = CvRedactor.Redact(master, grant.Policy);
        var fileName = PdfFileName.For(redacted["profile"]?["name"]?.GetValue<string>(), locale);
        var hash = ContentHash(redacted.ToJsonString(), locale, renderUrl,
            grant.Templates.Pdf + System.Text.Json.JsonSerializer.Serialize(grant.Templates.PdfVars));
        var file = CacheFile(grant, locale);
        var hashFile = file + ".sha256";

        var gate = Locks.GetOrAdd(file, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            if (File.Exists(file) && File.Exists(hashFile) && await File.ReadAllTextAsync(hashFile, ct) == hash)
                return new PdfResult(await File.ReadAllBytesAsync(file, ct), FromCache: true, fileName);

            var pdf = await RenderAsync(grant, renderUrl, ct);
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            await File.WriteAllBytesAsync(file, pdf, ct);
            await File.WriteAllTextAsync(hashFile, hash, ct);
            return new PdfResult(pdf, FromCache: false, fileName);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Renders without touching the cache (admin preview, e.g. to compare templates).</summary>
    public async Task<byte[]?> RenderPreviewAsync(AccessGrant grant, string? requestedLocale, CancellationToken ct)
    {
        if (tenants.LoadCv(grant.Tenant, requestedLocale) is not { } loaded) return null;
        return await RenderAsync(grant, await RenderUrlAsync(grant, loaded.Locale, ct), ct);
    }

    /// <summary>Renders all locales of a grant (used on invite creation so failures show up immediately).</summary>
    public async Task<IReadOnlyList<PdfRenderOutcome>> RenderAllLocalesAsync(AccessGrant grant, CancellationToken ct)
    {
        var outcomes = new List<PdfRenderOutcome>();
        foreach (var locale in Locales(grant.Tenant))
        {
            try
            {
                var result = await GetOrRenderAsync(grant, locale, ct);
                outcomes.Add(result is null
                    ? new PdfRenderOutcome(locale, false, false, null, "cv not found")
                    : new PdfRenderOutcome(locale, true, result.FromCache, result.Content.Length, null));
            }
            catch (Exception ex) when (ex is PdfRenderException or HttpRequestException or TaskCanceledException or IOException)
            {
                logger.LogError(ex, "PDF rendering failed for tenant {Tenant}, profile {Profile}, locale {Locale}",
                    grant.Tenant.Id, grant.ProfileName, locale);
                outcomes.Add(new PdfRenderOutcome(locale, false, false, null, ex.Message));
            }
        }
        return outcomes;
    }

    public void DeleteCached(string tenantId, Guid inviteId)
    {
        var dir = Path.Combine(DataPath, "pdf", tenantId);
        if (!Directory.Exists(dir)) return;
        foreach (var file in Directory.EnumerateFiles(dir, $"invite-{inviteId:N}.*"))
            File.Delete(file);
    }

    public static IEnumerable<string> Locales(Tenant tenant) =>
        Directory.EnumerateFiles(tenant.Directory, "cv.*.json")
            .Select(f => Path.GetFileName(f)["cv.".Length..^".json".Length])
            .Order(StringComparer.Ordinal);

    private async Task<Uri> RenderUrlAsync(AccessGrant grant, string locale, CancellationToken ct)
    {
        var appBase = (configuration["Pdf:AppBaseUrl"] ?? "http://web").TrimEnd('/');
        // Nuxt i18n: default locale "en" has no prefix (prefix_except_default).
        var path = locale == (configuration["Pdf:DefaultUiLocale"] ?? "en") ? "/" : $"/{locale}";
        // The QR code in the PDF must point to the public site, not to the internal render URL.
        // For invites it carries a linked QR invite code, so scanning the printed PDF opens the same view.
        var publicUrl = PublicUrl(grant.Tenant, path);
        if (publicUrl is not null && grant.Invite is { } invite)
            publicUrl += "?c=" + await access.GetOrCreateQrCodeAsync(invite, ct);
        return new Uri($"{appBase}{path}?print=1" + (publicUrl is null ? "" : $"&qr={Uri.EscapeDataString(publicUrl)}"));
    }

    private async Task<byte[]> RenderAsync(AccessGrant grant, Uri url, CancellationToken ct)
    {
        var renderer = services.GetRequiredService<IPdfRenderer>();
        var cookies = new Dictionary<string, string> { [AccessService.RenderCookieName] = access.CreateRenderTicket(grant) };
        var pdf = await renderer.RenderAsync(url, cookies, ct);
        if (pdf.Length < 5 || Encoding.ASCII.GetString(pdf, 0, 5) != "%PDF-")
            throw new PdfRenderException("renderer did not return a PDF");
        return pdf;
    }

    private string? PublicUrl(Tenant tenant, string path)
    {
        var baseUrl = tenant.Config.Hosts.FirstOrDefault() is { } host
            ? $"https://{TenantStore.NormalizeHost(host)}"
            : configuration["Cv:SharedBaseUrl"]?.TrimEnd('/');
        return string.IsNullOrEmpty(baseUrl) ? null : baseUrl + path;
    }

    private string CacheFile(AccessGrant grant, string locale)
    {
        var key = grant.Invite is { } invite ? $"invite-{invite.Id:N}" : $"public-{grant.ProfileName}";
        return Path.Combine(DataPath, "pdf", grant.Tenant.Id, $"{key}.{locale}.pdf");
    }

    /// <summary>
    /// Layout version (bump Pdf:LayoutVersion after UI changes) + locale + render URL (QR target) + redacted CV.
    /// </summary>
    private string ContentHash(string redactedCv, string locale, Uri renderUrl, string? template) => Convert.ToHexStringLower(SHA256.HashData(
        Encoding.UTF8.GetBytes($"{configuration["Pdf:LayoutVersion"]}\n{locale}\n{template}\n{renderUrl}\n{redactedCv}")));
}
