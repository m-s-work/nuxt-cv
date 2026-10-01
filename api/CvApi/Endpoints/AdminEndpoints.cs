using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CvApi.Access;
using CvApi.Accounts;
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

    /// <param name="Repo">Git remote of the CV repo and <paramref name="Path"/> the tenant folder in it, so pruned revisions can be fetched again.</param>
    public sealed record RegisterRevisionRequest(string Sha, string? Message, DateTimeOffset? CommittedAt, string? Repo, string? Path);

    public sealed record FetchRevisionRequest(string Ref);

    /// <summary>Unsaved CV draft to redact for a profile (admin editor preview).</summary>
    public sealed record DraftPreviewRequest(string Profile, string? Locale, JsonObject? Cv);

    /// <summary>Revision: SHA/prefix = pin, "" = current CV even if the profile is pinned, null = follow the profile.</summary>
    public sealed record PinRequest(string? Revision);

    public sealed record CreateInviteRequest(
        string Profile,
        string? Label,
        DateTimeOffset? ExpiresAt,
        int? ExpiresInDays,
        int? MaxUses,
        AccessPolicy? Overrides,
        string? Code = null,
        bool ViewOnce = false,
        int? ViewOnceMinutes = null);

    /// <summary>
    /// Settings of an invite that can be changed later; replaces all of them (null = none / unlimited / not view-once).
    /// </summary>
    public sealed record UpdateInviteRequest(string? Label, DateTimeOffset? ExpiresAt, int? MaxUses, int? ViewOnceMinutes);

    /// <summary>Longest view-once grace window.</summary>
    public const int MaxViewOnceMinutes = 7 * 24 * 60;

    /// <summary>Default grace window of a view-once invite for the browser that opened it.</summary>
    public const int DefaultViewOnceMinutes = 30;

    public static void MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/admin").AddEndpointFilter(RequireAdminKey);

        // Favicon catalogue for the picker: every symbol drawn in the given colours (defaults if not set),
        // so the admin UI previews exactly what /api/favicon.svg serves.
        admin.MapGet("/favicon", (string? color, string? background) => Results.Ok(new
        {
            defaults = new { symbol = Favicon.DefaultSymbol, color = Favicon.DefaultColor, background = Favicon.DefaultBackground },
            colors = Favicon.Colors,
            symbols = Favicon.Glyphs.Select(g => new
            {
                name = g.Name,
                glyph = g.Glyph,
                svg = Favicon.Svg(new FaviconConfig { Symbol = g.Name, Color = color, Background = background }),
            }),
        }));

        // Super-admin: all tenants; a user: only their own (SaaS §1).
        admin.MapGet("/tenants", (HttpContext ctx, TenantStore tenants, TenantOwners owners) => AdminCaller.Of(ctx).Visible(tenants.All).Select(t => new
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
            dataHash = TenantHashes.Compute(t).Combined,
            plan = owners.For(t.Id).Plan,
        }));

        // SHA-256 of every data file of a tenant (compare with `sha256sum` in the CV repository).
        admin.MapGet("/tenants/{tenantId}/hash", (string tenantId, TenantStore tenants) =>
        {
            if (tenants.Get(tenantId) is not { } tenant) return Results.NotFound();
            var hashes = TenantHashes.Compute(tenant);
            return Results.Ok(new { tenant = tenant.Id, combined = hashes.Combined, files = hashes.Files });
        });

        // CV revisions (git commits registered by tools/cv-sync.sh). "outdated" = the current CV differs from it.
        admin.MapGet("/tenants/{tenantId}/revisions", (string tenantId, TenantStore tenants) =>
        {
            if (tenants.Get(tenantId) is not { } tenant) return Results.NotFound();
            var index = RevisionStore.Read(tenant);
            var live = TenantHashes.DataFiles(tenant.Directory);
            var current = index.Revisions.FirstOrDefault(r => r.Sha == index.Current);
            return Results.Ok(new
            {
                current = index.Current,
                source = index.Source,
                // The live files were changed after the current revision was registered (e.g. edited in the admin UI).
                modified = current is not null && TenantHashes.Diff(current.Files, live).Count > 0,
                revisions = index.Revisions.Select(r =>
                {
                    // What changed in the current CV since this revision (empty = up to date).
                    var changes = TenantHashes.Diff(r.Files, live);
                    return new
                    {
                        sha = r.Sha,
                        message = r.Message,
                        committedAt = r.CommittedAt,
                        registeredAt = r.RegisteredAt,
                        outdated = changes.Count > 0,
                        changes = changes.Select(c => new { path = c.Path, change = c.Change }),
                        refs = index.Refs?.Where(x => x.Value == r.Sha).Select(x => x.Key).ToList() ?? [],
                    };
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
        }).WithMetadata(SuperAdminOnly.Instance);

        // Fetch a revision (SHA, tag or branch) from the tenant's git repo again, e.g. after it was pruned.
        admin.MapPost("/tenants/{tenantId}/revisions/fetch", async (string tenantId, FetchRevisionRequest body, TenantStore tenants,
            GitRevisionFetcher git, CancellationToken ct) =>
        {
            if (tenants.Get(tenantId) is not { } tenant) return Results.NotFound();
            if (RevisionStore.Resolve(tenant, body.Ref ?? "") is { } known) return Results.Ok(new { sha = known });
            var result = await git.FetchAsync(tenant, body.Ref ?? "", ct);
            return result.Sha is { } sha ? Results.Ok(new { sha }) : Results.BadRequest(new { error = result.Error });
        }).WithMetadata(SuperAdminOnly.Instance);

        // Profile definitions of a tenant (for the admin UI's invite form and preview).
        admin.MapGet("/tenants/{tenantId}/profiles", (string tenantId, TenantStore tenants) =>
            tenants.Get(tenantId) is { } tenant ? Results.Ok(tenant.Config.Profiles) : Results.NotFound());

        admin.MapGet("/tenants/{tenantId}/invites", async (string tenantId, TenantStore tenants, AppDbContext db,
            AccessService access, IConfiguration config, CancellationToken ct) =>
        {
            if (tenants.Get(tenantId) is not { } tenant) return Results.NotFound();
            var invites = await db.Invites.Where(i => i.TenantId == tenantId).ToListAsync(ct);
            return Results.Ok(invites.OrderByDescending(i => i.CreatedAt).Select(i => ToDto(i, access.RevealCode(i), tenant, config, tenants)));
        });

        admin.MapPost("/tenants/{tenantId}/invites", async (string tenantId, CreateInviteRequest body,
            TenantStore tenants, AppDbContext db, IConfiguration config, TimeProvider time,
            AccessService access, PdfService pdf, GitRevisionFetcher git, TenantOwners owners, HttpContext ctx, CancellationToken ct) =>
        {
            var tenant = tenants.Get(tenantId);
            if (tenant is null) return Results.NotFound();
            var caller = AdminCaller.Of(ctx);
            // Plan limit (SaaS §4); the super-admin may exceed it, e.g. for support.
            if (!caller.IsSuperAdmin && owners.For(tenant.Id).MaxActiveInvites is { } maxInvites
                && await AccountService.ActiveInviteCountAsync(db, tenant.Id, time.GetUtcNow(), ct) >= maxInvites)
                return AccountEndpoints.PlanLimit("activeInvites", maxInvites);
            // Users share one code namespace: chosen codes must be long enough not to be guessed or squatted.
            if (!caller.IsSuperAdmin && body.Code?.Trim() is { Length: > 0 and < 8 })
                return Results.BadRequest(new { error = "invalid_code", rule = "8-64 characters: A-Z a-z 0-9 - _" });
            if (!tenant.Config.Profiles.ContainsKey(body.Profile))
                return Results.BadRequest(new { error = "unknown_profile", profiles = tenant.Config.Profiles.Keys });
            if (body.MaxUses is <= 0) return Results.BadRequest(new { error = "invalid_max_uses" });
            if (body.ViewOnceMinutes is <= 0 or > MaxViewOnceMinutes) return Results.BadRequest(new { error = "invalid_view_once_minutes" });
            if (body.Overrides is { Revision: { Length: > 0 } pin })
            {
                var (sha, pinError) = await EnsureRevisionAsync(tenant, pin, git, ct);
                if (sha is null) return Results.BadRequest(new { error = "unknown_revision", detail = pinError });
                body.Overrides.Revision = sha;
            }

            var now = time.GetUtcNow();
            var code = body.Code?.Trim() is { Length: > 0 } custom ? custom : InviteCodes.Generate();
            if (body.Code is not null)
            {
                if (!InviteCodes.IsValidCustom(code))
                    return Results.BadRequest(new { error = "invalid_code", rule = "4-64 characters: A-Z a-z 0-9 - _" });
                var hash = InviteCodes.Hash(code);
                var existing = await db.Invites.SingleOrDefaultAsync(i => i.CodeHash == hash, ct);
                if (existing is not null && existing.IsActive(time.GetUtcNow()))
                    return Results.Conflict(new { error = "code_taken" });
                // A revoked or expired invite releases its code (R4.1, e.g. re-create "demo" with other settings).
                if (existing is not null) existing.CodeHash = $"released:{existing.Id:N}";
            }
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
                ViewOnceMinutes = body.ViewOnce || body.ViewOnceMinutes is not null
                    ? body.ViewOnceMinutes ?? DefaultViewOnceMinutes
                    : null,
            };
            db.Invites.Add(invite);
            await db.SaveChangesAsync(ct);

            // Render the PDFs right away so rendering problems show up now, not when the recipient clicks.
            IReadOnlyList<PdfRenderOutcome>? pdfOutcomes = null;
            if (pdf.Enabled && access.GrantFor(invite) is { } grant)
                pdfOutcomes = await pdf.RenderAllLocalesAsync(grant, ct);

            return Results.Ok(new { invite = ToDto(invite, code, tenant, config, tenants), code, link = BuildLink(tenant, config, tenants, code), pdf = pdfOutcomes });
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
            return Results.Ok(ToDto(invite, access.RevealCode(invite), tenants.Get(tenantId), config, tenants));
        });

        // Change label, expiry, max. redemptions and view once of an invite (its QR invite follows).
        admin.MapPut("/tenants/{tenantId}/invites/{id:guid}/settings", async (string tenantId, Guid id, UpdateInviteRequest body,
            TenantStore tenants, AppDbContext db, AccessService access, PdfService pdf, IConfiguration config, TimeProvider time,
            CancellationToken ct) =>
        {
            var invite = await db.Invites.SingleOrDefaultAsync(i => i.Id == id && i.TenantId == tenantId, ct);
            if (invite is null) return Results.NotFound();
            if (invite.RevokedAt is not null) return Results.Conflict(new { error = "revoked" });
            if (invite.Source == InviteSources.PdfQr) return Results.BadRequest(new { error = "edit_the_parent_invite" });
            if (body.MaxUses is <= 0) return Results.BadRequest(new { error = "invalid_max_uses" });
            if (body.ViewOnceMinutes is <= 0 or > MaxViewOnceMinutes) return Results.BadRequest(new { error = "invalid_view_once_minutes" });

            var children = await db.Invites.Where(i => i.ParentId == id && i.RevokedAt == null).ToListAsync(ct);
            var now = time.GetUtcNow();
            if (body.ViewOnceMinutes is { } minutes)
            {
                if (!invite.IsViewOnce)
                {
                    // Turned on: earlier sessions end and the next opening is the one. The printed QR code
                    // must not outlive it either.
                    invite.Rearm();
                    // A fresh token that no browser has yet: earlier sessions stay ended even if view once is
                    // turned off again before anyone opened it.
                    invite.ViewOnceToken = InviteCodes.Generate();
                    foreach (var child in children)
                    {
                        child.RevokedAt = now;
                        pdf.DeleteCached(tenantId, child.Id);
                    }
                    pdf.DeleteCached(tenantId, id);
                }
                else if (invite.ViewOnceUntil is not null && invite.LastUsedAt is { } openedAt)
                    invite.ViewOnceUntil = openedAt.AddMinutes(minutes);   // window counts from the opening
                invite.ViewOnceMinutes = minutes;
            }
            else if (invite.IsViewOnce)
            {
                // Turned off: the browser that opened it keeps access like with a normal invite. The token stays,
                // so sessions that ended when view once was turned on do not come back.
                invite.ViewOnceMinutes = null;
                invite.ViewOnceUntil = null;
                pdf.DeleteCached(tenantId, id);
            }

            invite.Label = body.Label?.Trim() ?? "";
            invite.ExpiresAt = body.ExpiresAt;
            invite.MaxUses = body.MaxUses;
            foreach (var child in children.Where(c => c.RevokedAt is null))
            {
                child.Label = invite.Label;
                child.ExpiresAt = invite.ExpiresAt;
            }
            await db.SaveChangesAsync(ct);
            return Results.Ok(ToDto(invite, access.RevealCode(invite), tenants.Get(tenantId), config, tenants));
        });

        // Make a used-up code redeemable again: resets the use count and a used view-once state. The browser
        // that opened a view-once invite loses access; normal invites keep their existing sessions.
        admin.MapPost("/tenants/{tenantId}/invites/{id:guid}/rearm", async (string tenantId, Guid id,
            TenantStore tenants, AppDbContext db, AccessService access, IConfiguration config, CancellationToken ct) =>
        {
            var invite = await db.Invites.SingleOrDefaultAsync(i => i.Id == id && i.TenantId == tenantId, ct);
            if (invite is null) return Results.NotFound();
            if (invite.RevokedAt is not null) return Results.Conflict(new { error = "revoked" });
            invite.Rearm();
            await db.SaveChangesAsync(ct);
            return Results.Ok(ToDto(invite, access.RevealCode(invite), tenants.Get(tenantId), config, tenants));
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
            return Results.Ok(ToDto(invite, access.RevealCode(invite), tenant, config, tenants));
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
            TenantStore tenants, IConfiguration config, AppDbContext db, TimeProvider time, GitRevisionFetcher git,
            TenantOwners owners, HttpContext ctx, CancellationToken ct) =>
        {
            var caller = AdminCaller.Of(ctx);
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
                    if (doc.RootElement.ValueKind != JsonValueKind.Object)
                        return Results.BadRequest(new { error = "invalid_json", detail = "The file must contain a JSON object." });
                    if (path == "tenant.json")
                    {
                        var parsed = JsonSerializer.Deserialize<TenantConfig>(doc, TenantStore.FileJsonOptions);
                        if (TenantStore.Validate(parsed) is { } invalid) return Results.BadRequest(new { error = "invalid_tenant", detail = invalid });
                        // Hosts are managed by the platform for users (own domain via /account/domain, SaaS §3 S3.4).
                        if (!caller.IsSuperAdmin && !HostsUnchanged(tenants.Get(tenantId), parsed!))
                            return Results.BadRequest(new { error = "hosts_managed" });
                    }
                }
                catch (JsonException ex)
                {
                    return Results.BadRequest(new { error = "invalid_json", detail = ex.Message });
                }
            }
            else if (tenants.Get(tenantId) is { } quotaTenant)
            {
                // Asset storage quota of the plan (SaaS §3 S3.6).
                var existingSize = File.Exists(Path.Combine(quotaTenant.Directory, path)) ? new FileInfo(Path.Combine(quotaTenant.Directory, path)).Length : 0;
                var quota = owners.For(quotaTenant.Id).QuotaBytes;
                if (!caller.IsSuperAdmin && AccountService.AssetBytes(quotaTenant) - existingSize + buffer.Length > quota)
                    return Results.Json(new { error = "quota_exceeded", quota }, statusCode: StatusCodes.Status413PayloadTooLarge);
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

        // PDF preview of a profile in any template (not cached), e.g. to choose a template.
        // revision: as for /preview – a SHA/tag, "current" or omitted (the profile's own view).
        admin.MapGet("/tenants/{tenantId}/pdf-preview", async (string tenantId, string profile, string? template, string? locale,
            string? vars, string? revision, TenantStore tenants, PdfService pdf, CancellationToken ct) =>
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
            if (AccessService.GrantForProfile(tenant, profile, template, pdfVars, revision == "current" ? "" : revision) is not { } grant)
                return Results.BadRequest(new { error = "unknown_profile" });

            var content = await pdf.RenderPreviewAsync(grant, locale, ct);
            return content is null
                ? Results.NotFound()
                : Results.File(content, "application/pdf", $"preview-{profile}-{grant.Templates.Pdf ?? "default"}.pdf");
        });

        admin.MapGet("/tenants/{tenantId}/preview", (string tenantId, string profile, string? locale, string? revision, TenantStore tenants) =>
        {
            var tenant = tenants.Get(tenantId);
            if (tenant is null) return Results.NotFound();
            if (!tenant.Config.Profiles.ContainsKey(profile))
                return Results.BadRequest(new { error = "unknown_profile" });
            // revision: a SHA/tag, "current" (ignore the profile's pin) or omitted (the profile's own view).
            var effective = AccessService.GrantForProfile(tenant, profile, revision: revision == "current" ? "" : revision)!.Policy;
            if (tenants.LoadCv(tenant, locale, effective.Revision) is not { } loaded) return Results.NotFound();
            return Results.Ok(new { locale = loaded.Locale, revision = effective.Revision, cv = CvRedactor.Redact(loaded.Cv, effective) });
        });

        // Unsaved draft of a CV file (admin editor): redacted exactly as the profile would see it, nothing is stored.
        // Pins are ignored – a draft is always a change of the current CV.
        admin.MapPost("/tenants/{tenantId}/preview", (string tenantId, DraftPreviewRequest request, TenantStore tenants) =>
        {
            var tenant = tenants.Get(tenantId);
            if (tenant is null) return Results.NotFound();
            if (!tenant.Config.Profiles.ContainsKey(request.Profile))
                return Results.BadRequest(new { error = "unknown_profile" });
            if (request.Cv is null) return Results.BadRequest(new { error = "invalid_cv" });
            var effective = AccessService.GrantForProfile(tenant, request.Profile, revision: "")!.Policy;
            var draft = (JsonObject)request.Cv.DeepClone();
            return Results.Ok(new { locale = request.Locale, revision = (string?)null, cv = CvRedactor.Redact(draft, effective) });
        });
    }

    private static readonly Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider ContentTypes = new();

    private static string TenantDir(IConfiguration config, string tenantId) =>
        Path.Combine(Path.GetFullPath(config["Cv:DataPath"] ?? "/data"), "tenants", tenantId);

    // Codes are not secret towards the admin: they are shown with their link on every listing.
    private static object ToDto(Invite i, string? code, Tenant? tenant, IConfiguration config, TenantStore tenants) => new
    {
        id = i.Id,
        code,
        link = code is not null && tenant is not null ? BuildLink(tenant, config, tenants, code) : null,
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
        viewOnceMinutes = i.ViewOnceMinutes,
        viewOnceUntil = i.ViewOnceUntil,
        parentId = i.ParentId,
        source = i.Source,
        // Clicks on website links printed into this PDF (pdf-qr invites, §7.2).
        linkClicks = LinkClicks.Parse(i.LinkClicksJson).Values.OrderByDescending(e => e.Count)
            .Select(e => new { url = e.Url, count = e.Count, lastAt = e.LastAt }),
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

    /// <summary>Invite link (R4.4): "/" on the tenant's own host, "/cv" on the shared host ("/" is the showcase there).</summary>
    /// <remarks>Without own host and without Cv:SharedBaseUrl the link is relative ("/cv?c=…"); the admin UI completes it.</remarks>
    private static string BuildLink(Tenant tenant, IConfiguration config, TenantStore tenants, string code) =>
        tenants.PrimaryHost(tenant) is { } host
            ? $"https://{host}/?c={code}"
            : $"{config["Cv:SharedBaseUrl"]?.TrimEnd('/')}/cv?c={code}";

    /// <summary>
    /// Admin API access: the super-admin (X-Admin-Key) or a signed-in user for their own tenant (SaaS §1).
    /// Other tenants and super-admin-only endpoints answer 404 to users, so they do not learn what exists.
    /// </summary>
    internal static async ValueTask<object?> RequireAdminKey(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        http.Response.Headers.CacheControl = "private, no-store";
        var expected = http.RequestServices.GetRequiredService<IConfiguration>()["Admin:ApiKey"];
        var provided = http.Request.Headers[HeaderName].ToString();
        if (provided.Length > 0)
        {
            // Admin API is disabled entirely when no key is configured.
            if (string.IsNullOrEmpty(expected)) return Results.NotFound();
            var ok = CryptographicOperations.FixedTimeEquals(
                SHA256.HashData(Encoding.UTF8.GetBytes(provided)),
                SHA256.HashData(Encoding.UTF8.GetBytes(expected)));
            if (!ok) return Results.Unauthorized();
            http.Items[typeof(AdminCaller)] = AdminCaller.SuperAdmin;
            return await next(context);
        }

        if (await AuthSetup.CurrentUserAsync(http) is not { } user)
            return string.IsNullOrEmpty(expected) && !http.Request.Cookies.ContainsKey(AuthSetup.SessionCookieName)
                ? Results.NotFound()
                : Results.Unauthorized();
        if (!Csrf.IsSafe(http)) return Results.BadRequest(new { error = "csrf" });
        if (http.GetEndpoint()?.Metadata.GetMetadata<SuperAdminOnly>() is not null) return Results.NotFound();
        if (http.GetRouteValue("tenantId") is string tenantId
            && !string.Equals(tenantId, user.TenantId, StringComparison.OrdinalIgnoreCase))
            return Results.NotFound();
        http.Items[typeof(AdminCaller)] = new AdminCaller(false, user);
        return await next(context);
    }

    /// <summary>A user may not change the hosts of their tenant (own domains go through /account/domain).</summary>
    private static bool HostsUnchanged(Tenant? current, TenantConfig updated)
    {
        var before = (current?.Config.Hosts ?? []).Select(TenantStore.NormalizeHost).Order(StringComparer.Ordinal);
        var after = (updated.Hosts ?? []).Select(TenantStore.NormalizeHost).Order(StringComparer.Ordinal);
        return before.SequenceEqual(after);
    }
}

/// <summary>Who calls the admin API: the super-admin (admin key) or a user (session, own tenant only).</summary>
public sealed record AdminCaller(bool IsSuperAdmin, User? User)
{
    public static readonly AdminCaller SuperAdmin = new(true, null);

    /// <summary>Set by the admin filter; without it (endpoint not behind the filter) nobody gets privileges.</summary>
    public static AdminCaller Of(HttpContext http) =>
        http.Items.TryGetValue(typeof(AdminCaller), out var caller) && caller is AdminCaller c ? c : new AdminCaller(false, null);

    public IEnumerable<Tenant> Visible(IEnumerable<Tenant> tenants) =>
        IsSuperAdmin ? tenants : tenants.Where(t => string.Equals(t.Id, User?.TenantId, StringComparison.OrdinalIgnoreCase));
}

/// <summary>Endpoint metadata: only the super-admin may call it (e.g. git access with the operator's token).</summary>
public sealed class SuperAdminOnly
{
    public static readonly SuperAdminOnly Instance = new();
}
