# Requirements: Multi-Tenant CV Access & Invite Codes

This document defines how the CV site decides **which CV** is shown and **how much of it**
a visitor may see. The backend (C# / ASP.NET Core, `api/`) is the single source of truth;
the Nuxt frontend (`src/`) contains **no CV data** and renders whatever the API returns.

Keywords MUST / SHOULD / MAY follow RFC 2119.

---

## 1. Terms

| Term | Meaning |
|---|---|
| **Tenant** | One person's CV (e.g. `bob`). Has its own master CV, profiles, assets, hostnames and settings. |
| **Master CV** | The complete CV of a tenant, one JSON file per locale (`cv.en.json`, `cv.de.json`). Contains everything, including private fields. |
| **Profile** | A named redaction policy of a tenant (e.g. `recruiter`, `full`, `public`). Decides which parts of the master CV are delivered. |
| **Invite** | A secret code handed to a person. Belongs to exactly one tenant and one profile, optionally with per-invite overrides, expiry and usage limit. |
| **Tenant host** | A hostname assigned to one tenant, e.g. `bob-cv.velarix.space`. |
| **Shared host** | A hostname not assigned to any tenant, e.g. `cv.velarix.space`. Only invite codes decide the tenant there. |

---

## 2. Tenant resolution

The tenant is resolved from the **hostname** and/or the **invite code**:

| Request | Result |
|---|---|
| Tenant host, no invite | Tenant from host. Visitor gets the tenant's *public profile* if one is configured, otherwise **no access**. |
| Tenant host + valid invite of the **same** tenant | Tenant from host, profile from invite. |
| Tenant host + invite of **another** tenant | Invite is rejected (treated as invalid). |
| Shared host + valid invite | Tenant and profile from invite. |
| Shared host, no (valid) invite | **Showcase page** (§11) with invite-code entry. No tenant is revealed, not even its existence. |

- R2.1 Hostnames MUST be matched case-insensitively and without port.
- R2.2 A hostname MUST belong to at most one tenant. Conflicting configuration MUST be reported at load time and the conflicting host ignored.
- R2.3 The frontend MUST NOT decide the tenant; it only forwards the host (implicitly) and the invite code.
- R2.4 Examples:
  - `https://bob-cv.velarix.space` → tenant `bob`, public profile (if enabled).
  - `https://bob-cv.velarix.space/?c=K3x...` → tenant `bob`, profile of that invite.
  - `https://cv.velarix.space/?c=23gfiuash...` → tenant and profile of that invite.
  - `https://cv.velarix.space` → showcase page (§11).

---

## 3. Access gating

- R3.1 **No CV is public by default.** Without a valid invite, a visitor sees no CV: on a tenant host a
  neutral "invitation required" page, on the shared host the showcase (§11). Both offer invite-code entry.
- R3.2 A tenant MAY enable public access by setting `publicProfile` in its settings. This only
  applies on that tenant's **own hosts** (never on the shared host).
- R3.3 An **invalid, expired, revoked or exhausted** invite is treated exactly like "no invite":
  the visitor gets the public profile if enabled, otherwise the no-access page.
  The response MUST NOT reveal *why* a code is invalid beyond a generic "invite not valid".
- R3.4 Access MUST be re-checked on **every** API request (not only at redemption), so that
  revoking an invite or changing its expiry takes effect immediately.
- R3.5 All CV responses MUST carry `Cache-Control: private, no-store` and
  `X-Robots-Tag: noindex, nofollow`.

---

## 4. Invites

- R4.1 Invite codes MUST be generated server-side with ≥128 bit entropy, URL-safe (base64url, ~22 chars).
  Exception: the admin MAY choose a code (`code`, 4–64 characters `A-Z a-z 0-9 - _`), e.g. `demo`.
  Such codes are guessable and MUST only be used for demo or otherwise public content. A code can be in use
  by one active invite at a time; revoking the invite releases the code.
- R4.2 Codes are looked up by their SHA-256 hash. The plain code is additionally stored encrypted
  (ASP.NET data protection, keys in `/data/keys`) so the owner can view and copy code and link again at any
  time in the admin API/UI; codes are not secret towards the admin. They MUST NOT be sent to anyone else.
  Invites created before this change only have the hash; their code cannot be shown.
- R4.3 An invite has: `tenant`, `profile`, `label` (who it is for), optional `expiresAt`,
  optional `maxUses`, optional `overrides` (see §5.4), `createdAt`, `revokedAt`, `useCount`, `lastUsedAt`,
  and for derived invites `parentId` + `source` (§12, R12.10).
- R4.4 The invite link format is `https://<host>/?c=<code>`. `<host>` is the tenant's primary host
  if it has one, otherwise the shared host.
- R4.5 Redemption: the frontend sends the code once (`POST /api/access/redeem`); the API sets an
  `HttpOnly; Secure; SameSite=Lax` cookie containing a signed reference to the invite and the
  frontend removes `c` from the URL. The code MUST NOT be stored in browser storage.
- R4.6 `useCount` counts redemptions (devices/browsers), not page views. When `maxUses` is reached,
  further **redemptions** fail; existing sessions stay valid until expiry/revocation.
- R4.7 The redemption endpoint MUST be rate-limited per client IP.
- R4.8 Invite codes MUST NOT be logged.

---

## 5. Redaction: profiles, per-field visibility and global flags

The master CV contains everything. For every request the API computes a **redacted copy**
from the master CV and the effective profile. Redaction is applied server-side only;
removed data MUST NOT be present in the response at all.

### 5.1 Global flags (per profile, overridable per invite)

| Flag | Effect |
|---|---|
| `hideCompanies` | Company names are replaced by `companyAlias` (e.g. "Automotive supplier") or removed. Company logos/images of experiences are removed. |
| `hideTimeframeDays` | Dates are reduced to month precision (`2020-03-15` → `2020-03`). |
| `hideTimeframeMonths` | Dates are reduced to year precision (`2020-03-15` → `2020`). Implies `hideTimeframeDays`. |
| `hidePhoto` | Profile photo(s) are removed (`photoUrl` and every other `profile.photo*` field). |
| `hideContactDetails` | E-mail and phone are removed. |
| `hideBirthDate` | Birth date is removed. |
| `hideMedia` | All images, screenshots and logos are removed. |

**Public profile defaults to hide.** For the profile named as a tenant's `publicProfile`, every flag it
does not set (and an invite override does not set) counts as `true`: public visitors only see what the
owner explicitly allowed with `"<flag>": false`. For all other profiles unset flags count as `false`.
This applies wherever the profile is used (public view, its PDF, invites of that profile, admin preview).

When any timeframe flag is active, hand-written `period` texts are removed (they could leak
the hidden precision); the frontend formats periods from the (reduced) dates.

### 5.2 Per-field visibility (in the master CV)

- Any object in the master CV MAY carry `"requires": ["<grant>", ...]`. The object is only
  delivered if the effective profile grants at least one of the listed grants.
- Any object MAY carry `"fieldRequires": { "<field>": ["<grant>", ...] }` to protect single fields
  the same way.
- Objects/fields without `requires` are visible to every profile.
- `requires` / `fieldRequires` MUST be stripped from every response.

### 5.3 Per-field hiding (in the profile)

- A profile MAY list `hiddenFields` as dot paths, e.g. `details.phone`,
  `experiences.description`, `projects`. A path segment applied to an array applies to every element.

### 5.4 Profile definition & invite overrides

```jsonc
// /data/tenants/bob/tenant.json
{
  "name": "Bob Builder",
  "hosts": ["bob-cv.velarix.space"],
  "defaultLocale": "en",
  "publicProfile": null,            // e.g. "public" to enable public access on bob's hosts
  "profiles": {
    "public":    { "flags": { "hideCompanies": true, "hideTimeframeMonths": true, "hidePhoto": true,
                              "hideContactDetails": true, "hideBirthDate": true },
                   "hiddenFields": ["projects"] },
    "recruiter": { "grants": ["contact"], "flags": { "hideTimeframeDays": true, "hideBirthDate": true } },
    "full":      { "grants": ["contact", "private"] }
  }
}
```

- Invite `overrides` use the same shape (`flags`, `hiddenFields`, `grants`):
  flags set in the override replace the profile's value, `hiddenFields` are **added**,
  `grants` (if set) **replace** the profile's grants.

---

## 6. Localisation

- R6.1 Each tenant has one master CV file per locale (`cv.<locale>.json`). If the requested locale
  does not exist, the tenant's `defaultLocale` is used.
- R6.2 The UI texts stay in the frontend i18n files; only CV content comes from the API.

---

## 7. Assets (photos, logos, screenshots)

- R7.1 Tenant assets live in `/data/tenants/<id>/assets/` and are served via `GET /api/assets/<file>`.
- R7.2 An asset is only served if it is **referenced by the redacted CV of the current visitor**
  (so `hidePhoto` also blocks direct URL access to the photo).
- R7.3 No personal images may live in the frontend's `public/` folder.

---

## 8. API surface

| Method & path | Auth | Purpose |
|---|---|---|
| `POST /api/access/redeem` `{ code }` | – (rate-limited) | Validate invite, set access cookie. `204` or `400 { error: "invalid_invite" }`. |
| `POST /api/access/logout` | – | Clear access cookie. |
| `GET /api/cv?locale=de` | cookie / host | `200 { access, cv }` or `403 { error: "no_access", host: "shared" \| "tenant" }`. `host` lets the frontend choose showcase vs. neutral page; it never names a tenant. |
| `GET /api/assets/{file}` | cookie / host | Asset if referenced by the visitor's redacted CV, else `404`. |
| `GET /api/pdf?locale=de` | cookie / host | PDF of exactly the visitor's view (§12). `X-Pdf-Cache: hit\|miss`. `404 pdf_disabled` without renderer, `502 pdf_failed` on render errors. |
| `GET /api/health` | – | Liveness for Coolify. |
| `GET /api/version` | – | Deployed build: `{ api: { commit, builtAt }, pdf: { commit, builtAt } }`. The web container serves `/version.json` (`{ commit, builtAt }`). |
| `GET /api/admin/tenants/{tenant}/hash` | admin key | SHA-256 per data file (`tenant.json`, `cv.<locale>.json`, `assets/*`) + `combined`. |
| `GET /api/admin/tenants` | admin key | List tenants, hosts, profiles, locales (existing `cv.<locale>.json`), profile pins, `dataHash`. |
| `GET /api/admin/tenants/{tenant}/profiles` | admin key | Profile definitions (`grants`, `flags`, `hiddenFields`) of the tenant. |
| `GET/POST /api/admin/tenants/{tenant}/invites` | admin key | List / create invites. Every invite includes its `code` and `link` (if stored, R4.2). The list includes linked QR invites (`source: "pdf-qr"`, `parentId`). |
| `DELETE /api/admin/tenants/{tenant}/invites/{id}` | admin key | Revoke invite (also deletes its cached PDFs). |
| `POST /api/admin/tenants/{tenant}/invites/{id}/pdf` | admin key | Re-render the invite's PDFs, returns per-locale outcome. |
| `PUT /api/admin/tenants/{tenant}/files/{path}` | admin key | Upload `tenant.json`, `cv.<locale>.json` (validated JSON) or `assets/<file>`. Creates the tenant if needed; takes effect immediately. |
| `GET /api/admin/tenants/{tenant}/files` | admin key | List the tenant's files (`path`, `size`, `modifiedAt`). |
| `GET /api/admin/tenants/{tenant}/files/{path}` | admin key | Download one of these files (for editing). |
| `DELETE /api/admin/tenants/{tenant}/files/{path}` | admin key | Delete a `cv.<locale>.json` or asset. `tenant.json` cannot be deleted. |
| `GET /api/admin/tenants/{tenant}/preview?profile=x&locale=en[&revision=<sha>\|current]` | admin key | Show redacted CV for a profile (optionally of a registered revision, §14). |
| `GET /api/admin/tenants/{tenant}/pdf-preview?profile=x&template=y&locale=en[&vars=…][&revision=<sha\|tag>\|current]` | admin key | Render a PDF of a profile in any template and CV version (not cached). |
| `GET/POST /api/admin/tenants/{tenant}/revisions` | admin key | List stored CV revisions (`current`, `modified`, `source`, per revision `outdated`, `changes` (files changed since, same SHA-256 as `…/hash`), `refs`) / register the current CV files as revision `{ sha, message?, committedAt?, repo?, path? }` (§14). |
| `POST /api/admin/tenants/{tenant}/revisions/fetch` `{ ref }` | admin key | Fetch a revision (SHA, tag or branch) from the tenant's git repo again (§14). |
| `PUT /api/admin/tenants/{tenant}/invites/{id}/revision` `{ revision }` | admin key | Pin an invite (and its QR invite) to a revision; `""` = current CV, `null` = follow the profile (§14). |

- Admin endpoints require header `X-Admin-Key` matching `Admin__ApiKey`. If no key is configured,
  admin endpoints are disabled (`404`).

`access` object in `/api/cv`:

```json
{ "tenant": "bob", "profile": "recruiter", "viaInvite": true, "label": "ACME recruiting", "expiresAt": "2026-12-31T00:00:00Z" }
```

`/api/cv` also returns `features: { pdf: true|false }` so the frontend only offers the PDF download when available,
and `cvHash`: the SHA-256 of the compact JSON of the returned `cv` (for tests and deployment checks).

---

## 9. Data layout (persistent volume `/data`)

```
/data
├── app.db                       # SQLite: invites (all tenants)
├── pdf/<tenant>/                # rendered PDFs: invite-<id>.<locale>.pdf / public-<profile>.<locale>.pdf (+ .sha256)
├── keys/                        # ASP.NET Data Protection keys (cookie signing) – MUST persist
└── tenants/
    └── bob/
        ├── tenant.json          # hosts, profiles, publicProfile, defaultLocale
        ├── cv.en.json           # master CV (English)
        ├── cv.de.json           # master CV (German)
        ├── assets/              # photos, logos, screenshots
        └── revisions/           # CV snapshots per git commit (§14)
            ├── index.json       # current revision, git source, refs + list (sha, message, committedAt, SHA-256 per file)
            └── <sha>/           # cv.<locale>.json + assets/ (only revisions still in use)
```

Tenant files are re-read automatically (short cache), so CV edits need no redeploy.
A sample tenant lives in `api/sample-data/`.

---

## 10. Non-goals (for now)

- No user accounts or passwords for visitors.
- No per-visitor analytics beyond `useCount` / `lastUsedAt`.

---

## 11. Showcase (shared host without invite)

- R11.1 Visitors of the shared host without a valid invite see a showcase page: what the product does,
  example screenshots, the public feature list and an invite-code form.
- R11.2 Screenshots MUST only show the sample tenant (`api/sample-data`), never a real CV. They are static
  files in `src/public/showcase/` and are regenerated from the sample tenant when the UI changes.
- R11.3 The feature list MUST only contain **public** features (e.g. JSON-based CV, field-level gating,
  privacy flags, invite links, multi-tenancy, languages, timeline, technology filter, print/PDF, dark mode).
- R11.4 **Analytics and tracking features (e.g. heatmap tracking, visitor statistics, invite usage insights)
  MUST NOT be mentioned** on the showcase or anywhere visible to invitees. They are for the CV owner only.
- R11.5 On a tenant host the showcase is never shown (the neutral page is used), so tenant hosts do not
  advertise the platform.

---

## 12. PDF per invite

Every invite (and a tenant's public profile) gets a PDF that contains **exactly the redacted view** of
that invite – never more. PDFs are rendered by a separate container (`pdf`, headless Chromium).

- R12.1 **Rendering container.** `docker-compose.yml` contains the internal service `pdf` (`pdf/server.mjs`,
  Playwright/Chromium). It has no public domain; only the API calls it (`POST /render`).
- R12.2 **Render ticket.** The API gives the renderer a signed, 2-minute cookie (`cv_render`) naming tenant,
  profile and invite. The renderer opens the app via the internal host (`http://web/?print=1`); the API
  resolves access from the ticket exactly like for the invitee (revoked/expired invites are refused).
- R12.3 **Render on invite creation.** Creating an invite renders its PDF for every locale of the tenant
  **synchronously**, and the create response contains the per-locale outcome
  (`pdf: [{ locale, ok, bytes, error }]`), so rendering problems are visible immediately. A failed render
  does not prevent the invite from being created; it can be retried via the admin API.
- R12.4 **Cache & staleness.** PDFs are cached in `/data/pdf`. Each cache entry stores a SHA-256 of
  (layout version, locale, render URL incl. QR target, redacted CV JSON). A PDF is **obsolete** when that hash no longer matches
  – e.g. the CV, the profile, the invite overrides, the tenant's host or the shared base URL changed – or `Pdf__LayoutVersion` was bumped after a
  frontend layout change.
- R12.5 **Render on request.** `GET /api/pdf` returns the cached PDF if current; otherwise it renders it
  on the request (typically 3–10 s). Concurrent requests for the same PDF share one rendering.
- R12.6 **Loading message.** While the download request runs, the frontend shows a loading state with the
  message that the PDF is being generated for the current version of the CV and may take a few seconds.
- R12.7 **Print mode.** With `?print=1` the frontend skips the splash screen and sets
  `window.__CV_READY__` to `ready` / `no-access` / `error` once the page is complete; the renderer waits for it.
  The QR code in the PDF points to the public URL (tenant host or shared base URL), passed as `?qr=`.
- R12.10 **QR invite.** For an invite's PDF, the QR code contains a **linked invite** (`?c=<code>`):
  - created on the first rendering, reused for all later renderings and locales;
  - same tenant, profile, overrides and expiry as its parent; revoked together with its parent, and only
    usable while the parent is active;
  - marked `source: "pdf-qr"` with `parentId`, so the owner sees in the admin API how often the printed
    PDF was scanned (`useCount`, `lastUsedAt`). This marker is owner-only and never sent to invitees;
  - its plain code is stored encrypted (data protection, like every invite's code, R4.2) so re-rendered
    PDFs can embed it again;
  - the PDF of a QR invite embeds its own code (no QR-of-QR chains).
  Public-profile PDFs (no invite) link to the public URL without a code.
- R12.8 Revoking an invite deletes its cached PDFs.
- R12.12 **Links & file name.** Every PDF template contains a clickable link to the online version (same
  target as the QR code, incl. the QR invite code) and a "Created with <platform>" credit linking to the shared
  site (`CV_SHARED_BASE_URL`, returned as `links.platform` by `/api/cv`). Downloads are named
  `cv-<name>-<locale>.pdf` (umlauts transliterated, `cv-<locale>.pdf` if the name is hidden).
- R12.11 **Templates.** Print/PDF output uses a selectable template (`editorial` default, `classic`), chosen per
  tenant, profile or invite (invite overrides allowed unless `allowInviteTemplateOverride: false`).
  Details and how to add templates: [TEMPLATES.md](TEMPLATES.md).
- R12.9 **Typeset print layout.** Print and PDF use a dedicated layout (`src/app/components/CvPrint.vue`),
  not the screen layout: A4 with 16/15/18/15 mm margins (`@page`), type scale in pt, bundled fonts
  (Source Serif 4 for name/intro, Inter for text – no network access needed), masthead with photo and
  labelled contact grid, sidebar (skills, languages, licences, QR code) and main column with a date gutter
  (experience, education, projects, further stations, newest first). Entries never break across pages.
  The renderer prints at 100 % scale and adds a running footer (`<name> · Curriculum Vitae`, page x / y)
  from `window.__CV_PDF_FOOTER__`. Browser printing (Ctrl+P) uses the same layout without the footer.
  After layout changes bump `CV_PDF_LAYOUT_VERSION` so cached PDFs are re-rendered.

---

## 13. Admin UI

- R13.1 The SPA contains an owner-only admin page at `/admin`. It is a client of the admin API (§8) and
  has no privileges of its own: without a valid `X-Admin-Key` it shows only a sign-in form, and if the
  server has no admin key configured it reports that the admin API is disabled.
- R13.2 The admin key is entered by the owner and kept in `sessionStorage` of that tab only (never in
  cookies or `localStorage`, never in the URL). "Log out" clears it.
- R13.3 Features: select / create tenants; list invites with status (active, revoked, expired, exhausted),
  usage and linked QR invites; create invites (profile, label, expiry, max. redemptions, per-invite
  overrides) showing code, link and PDF render outcome; code and link of every invite stay visible and
  copyable in the list; revoke; re-render PDFs; edit `tenant.json`
  and `cv.<locale>.json` (comments and trailing commas allowed, as on the server); upload, view and delete
  assets; preview any profile, locale and stored CV version either as data (the redacted JSON exactly as
  delivered) or as PDF in any template and colour set (`…/pdf-preview`, needs the renderer); pin invites to
  CV versions and see outdated pins (§14).
- R13.5 Template builder ("Design" tab): choose the PDF template, colour set and template variables for the
  tenant or a profile, generated from the template's variable schema, with a live PDF preview for any
  profile and locale. Saving edits only `templates` of that scope in `tenant.json` (comments kept) and
  stores only the preset plus values that differ from it. See `docs/TEMPLATES.md`.
- R13.4 The admin page is never linked from the CV, the no-access page or the showcase, is `noindex`,
  and does not show the splash screen or language selector. Its UI theme (Nuxt UI) is loaded only in the
  admin page's own CSS chunk, so the public pages are unaffected.

---

## 14. CV versions: pinning to a git commit

The CV files usually live in a Git repository and are deployed with `tools/cv-sync.sh` (see `docs/FEATURES.md`).
A CV *variant* (an invite, or a profile) can be pinned to the CV as it was at a commit, e.g. the version sent
with an application.

- R14.1 **Registering.** After uploading, `cv-sync.sh` registers the deployed CV under its git commit
  (`POST …/revisions`, SHA from `git rev-parse HEAD` of the tenant folder or `CV_REVISION`, plus the HTTPS URL of
  the repo and the tenant folder in it). The API copies the
  tenant's current `cv.<locale>.json` files and `assets/` into `revisions/<sha>/` and marks it as current. Registering the
  same SHA again replaces its snapshot.
- R14.2 **Pinning.** `revision` (full SHA, unique prefix ≥ 7, or a tag/branch name) can be set on a profile in `tenant.json` or as an
  invite override (on creation or later via `PUT …/invites/{id}/revision`). The invite's pin replaces the
  profile's; `""` on an invite means "current CV" even if its profile is pinned. Invite pins are stored as the
  full SHA; a revision that is not stored is fetched from git first (R14.6), `400 unknown_revision` if that
  fails. QR invites follow their parent.
- R14.3 **Serving.** A pinned grant gets the master CV and the assets of the snapshot (all endpoints: CV,
  assets, PDF, admin preview); redaction (profile, flags, overrides) is applied as usual with the **current**
  `tenant.json`. If a pinned snapshot does not exist, the current CV and assets are served and the admin UI
  reports the pin as unknown.
- R14.4 **Outdated warning.** A pin is *outdated* when the snapshot's CV files or assets differ from the current
  ones. Each snapshot stores the SHA-256 of every file (`cv.<locale>.json`, `assets/*`), the same values as
  `GET …/hash` / `cv-sync.sh --verify`; comparing them file by file also tells which files changed (so commits that do not change the tenant's CV do not count). The admin UI shows the current
  revision, flags manual edits made after it ("changed since"), marks outdated or unknown pins on invites and
  profiles and lists the changed files, counts outdated active invites, and offers "Pin to current" per invite. Invitees are never told.
- R14.5 **Retention.** Only snapshots in use are kept: the current revision, revisions pinned by a profile in
  `tenant.json`, and revisions pinned by an active (not revoked, not expired) invite. Unused snapshots are
  deleted after registering a revision, revoking or re-pinning an invite, and uploading `tenant.json`.
  A deleted revision is fetched from git again when it is needed (R14.6).
- R14.6 **Fetching from git.** A revision that is not stored (never registered, or pruned) is fetched from the
  tenant's git repo again: when an invite is created or re-pinned with it, when an uploaded `tenant.json`
  pins a profile to it, and via "Fetch from git" in the admin UI (`POST …/revisions/fetch`). The API fetches
  only that commit (`git fetch --depth=1 <repo> <ref>` into a cache under `/data/git/<tenant>`), extracts the
  tenant folder (`cv.<locale>.json`, `assets/`) and stores it as a non-current snapshot; tags/branches are
  remembered with the commit they resolved to. Only HTTPS repos are used; private repos need
  `Git__Token` (read-only token, sent as HTTP basic auth, never on the command line). Visitor requests never
  trigger a fetch: an unknown pin serves the current CV until the revision is fetched.

