using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CvApi.Access;
using CvApi.Pdf;
using CvApi.Redaction;
using CvApi.Tenants;
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

    /// <param name="Repo">Git remote of the CV repo and <paramref name="Path"/> the tenant folder in it, so pruned revisions can be fetched again.</param>
    public sealed record RegisterRevisionRequest(string Sha, string? Message, DateTimeOffset? CommittedAt, string? Repo, string? Path);

    public sealed record FetchRevisionRequest(string Ref);

    /// <summary>Revision: SHA/prefix = pin, "" = current CV even if the profile is pinned, null = follow the profile.</summary>
    public sealed record PinRequest(string? Revision);

    public sealed record CreateInviteRequest(
        string Profile,
        string? Label,
        DateTimeOffset? ExpiresAt,
        int? ExpiresInDays,
        int? MaxUses,
        AccessPolicy? Overrides);

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
            locales = TenantStore.Locales(t),
            // Profiles pinned to a CV revision in tenant.json.
            pins = t.Config.Profiles.Where(p => !string.IsNullOrWhiteSpace(p.Value.Revision))
                .ToDictionary(p => p.Key, p => p.Value.Revision!.Trim()),
        }));

        // CV revisions (git commits registered by tools/cv-sync.sh). "outdated" = the current CV differs from it.
        admin.MapGet("/tenants/{tenantId}/revisions", (string tenantId, TenantStore tenants) =>
        {
            if (tenants.Get(tenantId) is not { } tenant) return Results.NotFound();
            var index = RevisionStore.Read(tenant);
            var live = RevisionStore.ContentHash(tenant.Directory);
            return Results.Ok(new
            {
                current = index.Current,
                source = index.Source,
                // The live files were changed after the last registered revision (e.g. edited in the admin UI).
                modified = index.Revisions.FirstOrDefault(r => r.Sha == index.Current)?.ContentHash is { } h && h != live,
                revisions = index.Revisions.Select(r => new
                {
                    sha = r.Sha,
                    message = r.Message,
                    committedAt = r.CommittedAt,
                    registeredAt = r.RegisteredAt,
                    outdated = r.ContentHash != live,
                    refs = index.Refs?.Where(x => x.Value == r.Sha).Select(x => x.Key).ToList() ?? [],
                }),
            });
        });

        admin.MapPost("/tenants/{tenantId}/revisions", async (string tenantId, RegisterRevisionRequest body, TenantStore tenants,
            AppDbContext db, TimeProvider time, GitRevisionFetcher git, CancellationToken ct) =>
        {
            if (tenants.Get(tenantId) is not { } tenant) return Results.NotFound();
            var sha = body.Sha?.Trim().ToLowerInvariant() ?? "";
            if (!RevisionStore.ShaRegex().IsMatch(sha)) return Results.BadRequest(new { error = "invalid_sha" });
            if (TenantStore.Locales(tenant).Count == 0) return Results.BadRequest(new { error = "no_cv" });
            RevisionSource? source = null;
            if (!string.IsNullOrWhiteSpace(body.Repo))
            {
                source = new RevisionSource(StripCredentials(body.Repo.Trim()), body.Path?.Trim().Trim('/') ?? "");
                if (!git.IsValidSource(source)) return Results.BadRequest(new { error = "invalid_source" });
            }
            var revision = RevisionStore.Register(tenant, sha, body.Message, body.CommittedAt, time.GetUtcNow(), source);
            await PruneRevisionsAsync(tenant, db, time.GetUtcNow(), ct);
            return Results.Ok(new { sha = revision.Sha, message = revision.Message, committedAt = revision.CommittedAt, registeredAt = revision.RegisteredAt });
        });

        // Fetch a revision (SHA, tag or branch) from the tenant's git repo again, e.g. after it was pruned.
        admin.MapPost("/tenants/{tenantId}/revisions/fetch", async (string tenantId, FetchRevisionRequest body, TenantStore tenants,
            GitRevisionFetcher git, CancellationToken ct) =>
        {
            if (tenants.Get(tenantId) is not { } tenant) return Results.NotFound();
            if (RevisionStore.Resolve(tenant, body.Ref ?? "") is { } known) return Results.Ok(new { sha = known });
            var result = await git.FetchAsync(tenant, body.Ref ?? "", ct);
            return result.Sha is { } sha ? Results.Ok(new { sha }) : Results.BadRequest(new { error = result.Error });
        });

        // Profile definitions of a tenant (for the admin UI's invite form and preview).
        admin.MapGet("/tenants/{tenantId}/profiles", (string tenantId, TenantStore tenants) =>
            tenants.Get(tenantId) is { } tenant ? Results.Ok(tenant.Config.Profiles) : Results.NotFound());

        admin.MapGet("/tenants/{tenantId}/invites", async (string tenantId, TenantStore tenants, AppDbContext db,
            AccessService access, IConfiguration config, CancellationToken ct) =>
        {
            if (tenants.Get(tenantId) is not { } tenant) return Results.NotFound();
            var invites = await db.Invites.Where(i => i.TenantId == tenantId).ToListAsync(ct);
            return Results.Ok(invites.OrderByDescending(i => i.CreatedAt).Select(i => ToDto(i, access.RevealCode(i), tenant, config)));
        });

        admin.MapPost("/tenants/{tenantId}/invites", async (string tenantId, CreateInviteRequest body,
            TenantStore tenants, AppDbContext db, IConfiguration config, TimeProvider time,
            AccessService access, PdfService pdf, GitRevisionFetcher git, CancellationToken ct) =>
        {
            var tenant = tenants.Get(tenantId);
            if (tenant is null) return Results.NotFound();
            if (!tenant.Config.Profiles.ContainsKey(body.Profile))
                return Results.BadRequest(new { error = "unknown_profile", profiles = tenant.Config.Profiles.Keys });
            if (body.MaxUses is <= 0) return Results.BadRequest(new { error = "invalid_max_uses" });
            if (body.Overrides is { Revision: { Length: > 0 } pin })
            {
                var (sha, pinError) = await EnsureRevisionAsync(tenant, pin, git, ct);
                if (sha is null) return Results.BadRequest(new { error = "unknown_revision", detail = pinError });
                body.Overrides.Revision = sha;
            }

            var now = time.GetUtcNow();
            var code = InviteCodes.Generate();
            var invite = new Invite
            {
                TenantId = tenant.Id,
                Profile = body.Profile,
                CodeHash = InviteCodes.Hash(code),
                CodeProtected = access.ProtectCode(code),
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

            return Results.Ok(new { invite = ToDto(invite, code, tenant, config), code, link = BuildLink(tenant, config, code), pdf = pdfOutcomes });
        });

        admin.MapDelete("/tenants/{tenantId}/invites/{id:guid}", async (string tenantId, Guid id, AppDbContext db,
            TimeProvider time, PdfService pdf, AccessService access, TenantStore tenants, IConfiguration config, CancellationToken ct) =>
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
            if (tenants.Get(tenantId) is { } revokedTenant) await PruneRevisionsAsync(revokedTenant, db, now, ct);
            return Results.Ok(ToDto(invite, access.RevealCode(invite), tenants.Get(tenantId), config));
        });

        // Pin an invite (and its QR invite) to a CV revision, e.g. to update an outdated pin.
        admin.MapPut("/tenants/{tenantId}/invites/{id:guid}/revision", async (string tenantId, Guid id, PinRequest body,
            TenantStore tenants, AppDbContext db, AccessService access, IConfiguration config, TimeProvider time,
            GitRevisionFetcher git, CancellationToken ct) =>
        {
            if (tenants.Get(tenantId) is not { } tenant) return Results.NotFound();
            var invite = await db.Invites.SingleOrDefaultAsync(i => i.Id == id && i.TenantId == tenantId, ct);
            if (invite is null) return Results.NotFound();

            var revision = body.Revision?.Trim();
            if (revision is { Length: > 0 })
            {
                var (sha, pinError) = await EnsureRevisionAsync(tenant, revision, git, ct);
                if (sha is null) return Results.BadRequest(new { error = "unknown_revision", detail = pinError });
                revision = sha;
            }

            foreach (var target in await db.Invites.Where(i => i.Id == id || i.ParentId == id).ToListAsync(ct))
            {
                var overrides = AccessService.ParseOverrides(target.OverridesJson) ?? new AccessPolicy();
                overrides.Revision = revision;
                target.OverridesJson = JsonSerializer.Serialize(overrides, TenantStore.FileJsonOptions);
            }
            await db.SaveChangesAsync(ct);
            await PruneRevisionsAsync(tenant, db, time.GetUtcNow(), ct);
            return Results.Ok(ToDto(invite, access.RevealCode(invite), tenant, config));
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
            TenantStore tenants, IConfiguration config, AppDbContext db, TimeProvider time, GitRevisionFetcher git, CancellationToken ct) =>
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

            var dir = TenantDir(config, tenantId);
            var target = Path.Combine(dir, path);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await File.WriteAllBytesAsync(target, buffer.ToArray(), ct);
            tenants.Invalidate();
            // Profile pins may have changed.
            if (path == "tenant.json" && tenants.Get(tenantId) is { } changed)
            {
                // Profiles pinned to a revision that is not stored (anymore): fetch it from git. Failures show up
                // as "unknown" pins in the admin UI and can be retried there.
                foreach (var pin in changed.Config.Profiles.Values.Select(p => p.Revision).OfType<string>().Where(p => p.Trim().Length > 0).Distinct())
                    await EnsureRevisionAsync(changed, pin, git, ct);
                await PruneRevisionsAsync(changed, db, time.GetUtcNow(), ct);
            }
            return Results.NoContent();
        });

        admin.MapGet("/tenants/{tenantId}/files", (string tenantId, IConfiguration config) =>
        {
            if (!TenantIdRegex().IsMatch(tenantId)) return Results.BadRequest(new { error = "invalid_tenant_id" });
            var dir = TenantDir(config, tenantId);
            if (!Directory.Exists(dir)) return Results.NotFound();
            var files = Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(dir, f).Replace(Path.DirectorySeparatorChar, '/'))
                .Where(p => AllowedFileRegex().IsMatch(p))
                .Order(StringComparer.Ordinal)
                .Select(p =>
                {
                    var info = new FileInfo(Path.Combine(dir, p));
                    return new { path = p, size = info.Length, modifiedAt = new DateTimeOffset(info.LastWriteTimeUtc) };
                });
            return Results.Ok(files);
        });

        admin.MapGet("/tenants/{tenantId}/files/{**path}", (string tenantId, string path, IConfiguration config) =>
        {
            if (!TenantIdRegex().IsMatch(tenantId) || !AllowedFileRegex().IsMatch(path)) return Results.BadRequest(new { error = "invalid_path" });
            var file = Path.Combine(TenantDir(config, tenantId), path);
            if (!File.Exists(file)) return Results.NotFound();
            if (!ContentTypes.TryGetContentType(file, out var contentType)) contentType = "application/octet-stream";
            return Results.File(file, contentType);
        });

        // tenant.json cannot be deleted here (it defines the tenant); CV files and assets can.
        admin.MapDelete("/tenants/{tenantId}/files/{**path}", (string tenantId, string path, IConfiguration config, TenantStore tenants) =>
        {
            if (!TenantIdRegex().IsMatch(tenantId) || !AllowedFileRegex().IsMatch(path) || path == "tenant.json")
                return Results.BadRequest(new { error = "invalid_path" });
            var file = Path.Combine(TenantDir(config, tenantId), path);
            if (!File.Exists(file)) return Results.NotFound();
            File.Delete(file);
            tenants.Invalidate();
            return Results.NoContent();
        });

        admin.MapGet("/tenants/{tenantId}/preview", (string tenantId, string profile, string? locale, string? revision, TenantStore tenants) =>
        {
            var tenant = tenants.Get(tenantId);
            if (tenant is null) return Results.NotFound();
            if (!tenant.Config.Profiles.TryGetValue(profile, out var policy))
                return Results.BadRequest(new { error = "unknown_profile" });
            // revision: a SHA, "current" (ignore the profile's pin) or omitted (the profile's own view).
            var effective = tenant.PolicyFor(profile, policy,
                revision is null ? null : new AccessPolicy { Revision = revision == "current" ? "" : revision });
            if (tenants.LoadCv(tenant, locale, effective.Revision) is not { } loaded) return Results.NotFound();
            return Results.Ok(new { locale = loaded.Locale, revision = effective.Revision, cv = CvRedactor.Redact(loaded.Cv, effective) });
        });
    }

    private static readonly Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider ContentTypes = new();

    private static string TenantDir(IConfiguration config, string tenantId) =>
        Path.Combine(Path.GetFullPath(config["Cv:DataPath"] ?? "/data"), "tenants", tenantId);

    // Codes are not secret towards the admin: they are shown with their link on every listing.
    private static object ToDto(Invite i, string? code, Tenant? tenant, IConfiguration config) => new
    {
        id = i.Id,
        code,
        link = code is not null && tenant is not null ? BuildLink(tenant, config, code) : null,
        // Effective CV pin: the invite's own (override) or its profile's; null = follows the current CV.
        revision = PinOf(i, tenant) is { Length: > 0 } pin ? pin : null,
        pinnedBy = AccessService.ParseOverrides(i.OverridesJson)?.Revision is not null ? "invite"
            : PinOf(i, tenant) is not null ? "profile" : null,
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

    /// <summary>
    /// Keeps only CV snapshots still in use: the current revision, pins of profiles and of active invites
    /// (QR invites follow their parent). Called after anything that can release a pin.
    /// </summary>
    private static async Task PruneRevisionsAsync(Tenant tenant, AppDbContext db, DateTimeOffset now, CancellationToken ct)
    {
        var pins = tenant.Config.Profiles.Values.Select(p => p.Revision).OfType<string>().ToList();
        var invites = await db.Invites.Where(i => i.TenantId == tenant.Id && i.ParentId == null && i.RevokedAt == null).ToListAsync(ct);
        pins.AddRange(invites.Where(i => i.IsActive(now))
            .Select(i => AccessService.ParseOverrides(i.OverridesJson)?.Revision).OfType<string>());
        RevisionStore.Prune(tenant, pins);
    }

    /// <summary>Full SHA of a pin: from the stored revisions, otherwise fetched from the tenant's git repo.</summary>
    private static async Task<(string? Sha, string? Error)> EnsureRevisionAsync(Tenant tenant, string pin, GitRevisionFetcher git, CancellationToken ct)
    {
        if (RevisionStore.Resolve(tenant, pin) is { } known) return (known, null);
        var result = await git.FetchAsync(tenant, pin, ct);
        return (result.Sha, result.Error);
    }

    /// <summary>Removes user:password from an https URL, so no credentials end up in index.json.</summary>
    private static string StripCredentials(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.UserInfo.Length > 0
            ? new UriBuilder(uri) { UserName = "", Password = "" }.Uri.ToString()
            : url;

    private static string? PinOf(Invite i, Tenant? tenant)
    {
        var own = AccessService.ParseOverrides(i.OverridesJson)?.Revision;
        if (own is not null) return own.Trim();
        return tenant is not null && tenant.Config.Profiles.TryGetValue(i.Profile, out var p) && !string.IsNullOrWhiteSpace(p.Revision)
            ? p.Revision.Trim()
            : null;
    }

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
