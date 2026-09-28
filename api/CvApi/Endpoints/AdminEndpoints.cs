using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CvApi.Access;
using CvApi.Pdf;
using CvApi.Redaction;
using CvApi.Tenants;
using CvApi.Versioning;
using Microsoft.EntityFrameworkCore;

namespace CvApi.Endpoints;

public static partial class AdminEndpoints
{
    public const string HeaderName = "X-Admin-Key";
    private const long MaxUploadBytes = 10 * 1024 * 1024;

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,62}$")]
    private static partial Regex TenantIdRegex();

    [GeneratedRegex(@"^(tenant\.json|cv\.[a-z]{2}(-[A-Z]{2})?\.json|assets/[A-Za-z0-9][A-Za-z0-9._-]{0,127})$")]
    private static partial Regex AllowedFileRegex();

    public sealed record CreateInviteRequest(
        string Profile,
        string? Label,
        DateTimeOffset? ExpiresAt,
        int? ExpiresInDays,
        int? MaxUses,
        AccessPolicy? Overrides,
        string? Code = null);

    public static void MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/admin").AddEndpointFilter(RequireAdminKey);

        admin.MapGet("/tenants", (TenantStore tenants) => tenants.All.Select(t => new
        {
            id = t.Id,
            name = t.Config.Name,
            hosts = t.Config.Hosts,
            defaultLocale = t.Config.DefaultLocale,
            publicProfile = t.Config.PublicProfile,
            profiles = t.Config.Profiles.Keys,
            dataHash = TenantHashes.Compute(t).Combined,
        }));

        // SHA-256 of every data file of a tenant (compare with `sha256sum` in the CV repository).
        admin.MapGet("/tenants/{tenantId}/hash", (string tenantId, TenantStore tenants) =>
        {
            if (tenants.Get(tenantId) is not { } tenant) return Results.NotFound();
            var hashes = TenantHashes.Compute(tenant);
            return Results.Ok(new { tenant = tenant.Id, combined = hashes.Combined, files = hashes.Files });
        });

        admin.MapGet("/tenants/{tenantId}/invites", async (string tenantId, TenantStore tenants, AppDbContext db, CancellationToken ct) =>
        {
            if (tenants.Get(tenantId) is null) return Results.NotFound();
            var invites = await db.Invites.Where(i => i.TenantId == tenantId).ToListAsync(ct);
            return Results.Ok(invites.OrderByDescending(i => i.CreatedAt).Select(ToDto));
        });

        admin.MapPost("/tenants/{tenantId}/invites", async (string tenantId, CreateInviteRequest body,
            TenantStore tenants, AppDbContext db, IConfiguration config, TimeProvider time,
            AccessService access, PdfService pdf, CancellationToken ct) =>
        {
            var tenant = tenants.Get(tenantId);
            if (tenant is null) return Results.NotFound();
            if (!tenant.Config.Profiles.ContainsKey(body.Profile))
                return Results.BadRequest(new { error = "unknown_profile", profiles = tenant.Config.Profiles.Keys });
            if (body.MaxUses is <= 0) return Results.BadRequest(new { error = "invalid_max_uses" });

            var now = time.GetUtcNow();
            var code = body.Code?.Trim() is { Length: > 0 } custom ? custom : InviteCodes.Generate();
            if (body.Code is not null)
            {
                if (!InviteCodes.IsValidCustom(code))
                    return Results.BadRequest(new { error = "invalid_code", rule = "4-64 characters: A-Z a-z 0-9 - _" });
                var hash = InviteCodes.Hash(code);
                var existing = await db.Invites.SingleOrDefaultAsync(i => i.CodeHash == hash, ct);
                if (existing is not null && existing.RevokedAt is null)
                    return Results.Conflict(new { error = "code_taken" });
                // A revoked invite releases its code (e.g. re-create "demo" with other settings).
                if (existing is not null) existing.CodeHash = $"released:{existing.Id:N}";
            }
            var invite = new Invite
            {
                TenantId = tenant.Id,
                Profile = body.Profile,
                CodeHash = InviteCodes.Hash(code),
                Label = body.Label ?? "",
                OverridesJson = body.Overrides is null ? null : JsonSerializer.Serialize(body.Overrides, TenantStore.FileJsonOptions),
                CreatedAt = now,
                ExpiresAt = body.ExpiresAt ?? (body.ExpiresInDays is { } days ? now.AddDays(days) : null),
                MaxUses = body.MaxUses,
            };
            db.Invites.Add(invite);
            await db.SaveChangesAsync(ct);

            // Render the PDFs right away so rendering problems show up now, not when the recipient clicks.
            IReadOnlyList<PdfRenderOutcome>? pdfOutcomes = null;
            if (pdf.Enabled && access.GrantFor(invite) is { } grant)
                pdfOutcomes = await pdf.RenderAllLocalesAsync(grant, ct);

            // The plain code is only ever returned here.
            return Results.Ok(new { invite = ToDto(invite), code, link = BuildLink(tenant, config, code), pdf = pdfOutcomes });
        });

        admin.MapDelete("/tenants/{tenantId}/invites/{id:guid}", async (string tenantId, Guid id, AppDbContext db,
            TimeProvider time, PdfService pdf, CancellationToken ct) =>
        {
            var invite = await db.Invites.SingleOrDefaultAsync(i => i.Id == id && i.TenantId == tenantId, ct);
            if (invite is null) return Results.NotFound();
            var now = time.GetUtcNow();
            invite.RevokedAt ??= now;
            // Derived invites (QR code in the PDF) are revoked together with their parent.
            foreach (var child in await db.Invites.Where(i => i.ParentId == id).ToListAsync(ct))
            {
                child.RevokedAt ??= now;
                pdf.DeleteCached(tenantId, child.Id);
            }
            await db.SaveChangesAsync(ct);
            pdf.DeleteCached(tenantId, id);
            return Results.Ok(ToDto(invite));
        });

        // (Re-)render the PDFs of an invite, e.g. after fixing a rendering problem or changing the CV.
        admin.MapPost("/tenants/{tenantId}/invites/{id:guid}/pdf", async (string tenantId, Guid id, AppDbContext db,
            AccessService access, PdfService pdf, TimeProvider time, CancellationToken ct) =>
        {
            if (!pdf.Enabled) return Results.NotFound(new { error = "pdf_disabled" });
            var invite = await db.Invites.SingleOrDefaultAsync(i => i.Id == id && i.TenantId == tenantId, ct);
            if (invite is null || !await access.IsActiveAsync(invite, time.GetUtcNow(), ct)) return Results.NotFound();
            if (access.GrantFor(invite) is not { } grant) return Results.NotFound();
            return Results.Ok(new { pdf = await pdf.RenderAllLocalesAsync(grant, ct) });
        });

        // File management, so tenants can be maintained without shell access to the volume.
        // tenant.json and cv.<locale>.json must be valid JSON; assets are stored as-is.
        admin.MapPut("/tenants/{tenantId}/files/{**path}", async (string tenantId, string path, HttpRequest request,
            TenantStore tenants, IConfiguration config, CancellationToken ct) =>
        {
            if (!TenantIdRegex().IsMatch(tenantId)) return Results.BadRequest(new { error = "invalid_tenant_id" });
            if (!AllowedFileRegex().IsMatch(path)) return Results.BadRequest(new { error = "invalid_path" });

            using var buffer = new MemoryStream();
            await request.Body.CopyToAsync(buffer, ct);
            if (buffer.Length > MaxUploadBytes) return Results.BadRequest(new { error = "too_large" });

            if (path.EndsWith(".json", StringComparison.Ordinal))
            {
                try
                {
                    var doc = JsonDocument.Parse(buffer.ToArray(), new JsonDocumentOptions
                    {
                        CommentHandling = JsonCommentHandling.Skip,
                        AllowTrailingCommas = true,
                    });
                    if (path == "tenant.json")
                        JsonSerializer.Deserialize<TenantConfig>(doc, TenantStore.FileJsonOptions);
                }
                catch (JsonException ex)
                {
                    return Results.BadRequest(new { error = "invalid_json", detail = ex.Message });
                }
            }

            var dir = Path.Combine(Path.GetFullPath(config["Cv:DataPath"] ?? "/data"), "tenants", tenantId);
            var target = Path.Combine(dir, path);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await File.WriteAllBytesAsync(target, buffer.ToArray(), ct);
            return Results.NoContent();
        });

        // PDF preview of a profile in any template (not cached), e.g. to choose a template.
        admin.MapGet("/tenants/{tenantId}/pdf-preview", async (string tenantId, string profile, string? template, string? locale,
            string? vars, TenantStore tenants, PdfService pdf, CancellationToken ct) =>
        {
            if (!pdf.Enabled) return Results.NotFound(new { error = "pdf_disabled" });
            var tenant = tenants.Get(tenantId);
            if (tenant is null) return Results.NotFound();
            if (template is not null && TemplateResolver.Valid(template) is null)
                return Results.BadRequest(new { error = "invalid_template" });
            // vars: JSON object of template variables, e.g. {"preset":"graphite","chapterColors":true}
            Dictionary<string, JsonElement>? pdfVars = null;
            if (!string.IsNullOrEmpty(vars))
            {
                try { pdfVars = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(vars); }
                catch (JsonException) { return Results.BadRequest(new { error = "invalid_vars" }); }
            }
            if (AccessService.GrantForProfile(tenant, profile, template, pdfVars) is not { } grant)
                return Results.BadRequest(new { error = "unknown_profile" });

            var content = await pdf.RenderPreviewAsync(grant, locale, ct);
            return content is null
                ? Results.NotFound()
                : Results.File(content, "application/pdf", $"preview-{profile}-{grant.Templates.Pdf ?? "default"}.pdf");
        });

        admin.MapGet("/tenants/{tenantId}/preview", (string tenantId, string profile, string? locale, TenantStore tenants) =>
        {
            var tenant = tenants.Get(tenantId);
            if (tenant is null) return Results.NotFound();
            if (!tenant.Config.Profiles.TryGetValue(profile, out var policy))
                return Results.BadRequest(new { error = "unknown_profile" });
            if (tenants.LoadCv(tenant, locale) is not { } loaded) return Results.NotFound();
            return Results.Ok(new { locale = loaded.Locale, cv = CvRedactor.Redact(loaded.Cv, EffectivePolicy.From(policy)) });
        });
    }

    private static object ToDto(Invite i) => new
    {
        id = i.Id,
        tenant = i.TenantId,
        profile = i.Profile,
        label = i.Label,
        overrides = AccessService.ParseOverrides(i.OverridesJson),
        createdAt = i.CreatedAt,
        expiresAt = i.ExpiresAt,
        revokedAt = i.RevokedAt,
        maxUses = i.MaxUses,
        useCount = i.UseCount,
        lastUsedAt = i.LastUsedAt,
        parentId = i.ParentId,
        source = i.Source,
    };

    private static string BuildLink(Tenant tenant, IConfiguration config, string code)
    {
        var baseUrl = tenant.Config.Hosts.FirstOrDefault() is { } host
            ? $"https://{TenantStore.NormalizeHost(host)}"
            : config["Cv:SharedBaseUrl"]?.TrimEnd('/');
        return string.IsNullOrEmpty(baseUrl) ? $"/?c={code}" : $"{baseUrl}/?c={code}";
    }

    private static async ValueTask<object?> RequireAdminKey(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var expected = http.RequestServices.GetRequiredService<IConfiguration>()["Admin:ApiKey"];
        // Admin API is disabled entirely when no key is configured.
        if (string.IsNullOrEmpty(expected)) return Results.NotFound();

        var provided = http.Request.Headers[HeaderName].ToString();
        var ok = CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(provided)),
            SHA256.HashData(Encoding.UTF8.GetBytes(expected)));
        return ok ? await next(context) : Results.Unauthorized();
    }
}
