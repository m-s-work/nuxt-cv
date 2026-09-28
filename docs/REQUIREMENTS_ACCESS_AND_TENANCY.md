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
- R4.2 Codes MUST be stored only as a SHA-256 hash; the plain code is shown once, at creation.
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
| `GET /api/admin/tenants` | admin key | List tenants, hosts, profiles. |
| `GET/POST /api/admin/tenants/{tenant}/invites` | admin key | List / create invites. Create returns code + link once. The list includes linked QR invites (`source: "pdf-qr"`, `parentId`). |
| `DELETE /api/admin/tenants/{tenant}/invites/{id}` | admin key | Revoke invite (also deletes its cached PDFs). |
| `POST /api/admin/tenants/{tenant}/invites/{id}/pdf` | admin key | Re-render the invite's PDFs, returns per-locale outcome. |
| `PUT /api/admin/tenants/{tenant}/files/{path}` | admin key | Upload `tenant.json`, `cv.<locale>.json` (validated JSON) or `assets/<file>`. Creates the tenant if needed. |
| `GET /api/admin/tenants/{tenant}/preview?profile=x&locale=en` | admin key | Show redacted CV for a profile. |

- Admin endpoints require header `X-Admin-Key` matching `Admin__ApiKey`. If no key is configured,
  admin endpoints are disabled (`404`).

`access` object in `/api/cv`:

```json
{ "tenant": "bob", "profile": "recruiter", "viaInvite": true, "label": "ACME recruiting", "expiresAt": "2026-12-31T00:00:00Z" }
```

`/api/cv` also returns `features: { pdf: true|false }` so the frontend only offers the PDF download when available.

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
        └── assets/              # photos, logos, screenshots
```

Tenant files are re-read automatically (short cache), so CV edits need no redeploy.
A sample tenant lives in `api/sample-data/`.

---

## 10. Non-goals (for now)

- No web admin UI; tenants, files and invites are managed via the admin API (curl / scripts).
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
  (layout version, locale, redacted CV JSON). A PDF is **obsolete** when that hash no longer matches
  – e.g. the CV, the profile or the invite overrides changed – or `Pdf__LayoutVersion` was bumped after a
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
  - its plain code is stored encrypted (data protection) so re-rendered PDFs can embed it again;
    regular invites keep storing only the hash;
  - the PDF of a QR invite embeds its own code (no QR-of-QR chains).
  Public-profile PDFs (no invite) link to the public URL without a code.
- R12.8 Revoking an invite deletes its cached PDFs.
- R12.9 **Typeset print layout.** Print and PDF use a dedicated layout (`src/app/components/CvPrint.vue`),
  not the screen layout: A4 with 16/15/18/15 mm margins (`@page`), type scale in pt, bundled fonts
  (Source Serif 4 for name/intro, Inter for text – no network access needed), masthead with photo and
  labelled contact grid, sidebar (skills, languages, licences, QR code) and main column with a date gutter
  (experience, education, projects, further stations, newest first). Entries never break across pages.
  The renderer prints at 100 % scale and adds a running footer (`<name> · Curriculum Vitae`, page x / y)
  from `window.__CV_PDF_FOOTER__`. Browser printing (Ctrl+P) uses the same layout without the footer.
  After layout changes bump `CV_PDF_LAYOUT_VERSION` so cached PDFs are re-rendered.

