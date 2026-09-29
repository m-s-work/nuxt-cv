# Deployment on Coolify

The site consists of two containers, defined in [`docker-compose.yml`](../docker-compose.yml):

```
Internet ──► Coolify Traefik (TLS, domains) ──► web (nginx :80) ──┬─► static Nuxt SPA
                                                                  └─► /api/* ──► api (ASP.NET :8080) ──► /data volume
                                                                                   │  ▲
                                                               POST /render        ▼  │ opens http://web/?print=1
                                                                               pdf (Chromium :3000)
```

| Service | Source | Purpose |
|---|---|---|
| `web` | `src/Dockerfile`, `src/nginx.conf.template` | Builds the Nuxt SPA (`nuxt generate`) and serves it with nginx. Proxies `/api/` to the API, keeping the `Host` header (it decides the tenant). |
| `api` | `api/Dockerfile` | C# API: tenant resolution, invites, redaction, assets, PDF cache. Not exposed publicly. |
| `pdf` | `pdf/Dockerfile` | Headless Chromium (Playwright) rendering the CV page to PDF. Internal only, called by the API. |

Requirements for access control and multi-tenancy: [REQUIREMENTS_ACCESS_AND_TENANCY.md](REQUIREMENTS_ACCESS_AND_TENANCY.md).

---

## 1. Create the resource

1. Coolify → *Project* → *+ New* → *Docker Compose* (from the Git repository `m-s-work/nuxt-cv`).
2. Branch: `main`. Base directory: `/`. Compose file: `/docker-compose.yml`.
3. **Domains**: set them on the **`web`** service only, comma-separated, all tenant hosts plus the shared host, e.g.
   `https://cv.velarix.space,https://bob-cv.velarix.space`.
   Leave the `api` service without a domain.
4. **Environment variables** (Coolify → *Environment Variables*):

   | Variable | Example | Notes |
   |---|---|---|
   | `CV_ADMIN_API_KEY` | long random string (`openssl rand -base64 32`) | Enables the admin API. Mark as secret. Empty = admin API disabled. |
   | `CV_SHARED_BASE_URL` | `https://cv.velarix.space` | Used for invite links of tenants that have no own host. |
   | `CV_CLIENT_IP_HEADER` | `CF-Connecting-IP` | Set when traffic arrives through a Cloudflare Tunnel (see below). |
   | `CV_DEMO_INVITE_CODE` | `demo` | Build time: shows a "Try the demo CV" button on the showcase linking to `/?c=demo`. Create the invite with that code (below). |
   | `CV_PDF_RENDERER_URL` | *(default `http://pdf:3000`)* | Set to an empty value to disable PDFs. |
   | `CV_PDF_LAYOUT_VERSION` | `2` | Bump after frontend layout changes so all cached PDFs are re-rendered. |
   | `CV_GIT_TOKEN` | fine-grained GitHub token, *Contents: read* on the CV repo | Lets the API fetch pinned CV versions from a private CV repository again. Mark as secret. Not needed for public repos. |

5. Deploy. Health checks: `web` → `GET /healthz`, `api` → `dotnet CvApi.dll --healthcheck` (both built into the images).

### DNS / Cloudflare Tunnel

The velarix Coolify server runs a `cloudflared` service, i.e. public traffic arrives through a
**Cloudflare Tunnel**. Then:

- Every host (shared + per tenant) needs a *Public Hostname* in the tunnel configuration
  (Cloudflare Zero Trust → Networks → Tunnels), pointing to Traefik (e.g. `http://coolify-proxy:80`),
  or one wildcard hostname `*.velarix.space`. A DNS record alone is not enough.
- Set `CV_CLIENT_IP_HEADER=CF-Connecting-IP`, otherwise the API sees cloudflared's address for every
  visitor and the invite rate limit applies to everyone at once.
- If the server's ports 80/443 are also reachable directly (bypassing Cloudflare), the header could be
  spoofed; this only weakens the rate limit, not access control.

Without a tunnel, every host needs an A/AAAA record pointing to the server (e.g. wildcard
`*.velarix.space`) and Traefik issues Let's Encrypt certificates.

### Persistent data

The named volume `cv-data` is mounted at `/data` in the API container:

```
/data/app.db       invites (SQLite)
/data/tracking.db  visitor tracking (SQLite, see docs/VISITOR_SESSION_TRACKING.md)
/data/geo/         optional city.mmdb + asn.mmdb for local IP → location lookup
/data/keys/        cookie-signing keys – losing them logs out every invitee
/data/tenants/<id>/tenant.json, cv.<locale>.json, assets/
```

Back up this volume (Coolify → *Persistent Storage* / scheduled backups, or `docker cp`).
If you prefer editing files over SSH, replace the named volume in Coolify's storage settings
with a bind mount (e.g. `/srv/nuxt-cv-data:/data`); the directory must be writable by UID `1654`
(the `app` user of the .NET image): `chown -R 1654:1654 /srv/nuxt-cv-data`.

---

## 2. Add a tenant

Tenants can be managed completely through the admin UI at `https://<shared host>/admin`
(sign in with `CV_ADMIN_API_KEY`) or through the admin API directly (no shell access needed).
Examples use `KEY=<CV_ADMIN_API_KEY>` and `API=https://cv.velarix.space/api`.

```bash
# tenant settings: hosts, profiles, public access (see api/sample-data/tenants/demo/tenant.json)
curl -X PUT -H "X-Admin-Key: $KEY" --data-binary @tenant.json  $API/admin/tenants/bob/files/tenant.json
# master CV per locale
curl -X PUT -H "X-Admin-Key: $KEY" --data-binary @cv.en.json   $API/admin/tenants/bob/files/cv.en.json
curl -X PUT -H "X-Admin-Key: $KEY" --data-binary @cv.de.json   $API/admin/tenants/bob/files/cv.de.json
# assets referenced as "/api/assets/<file>" in the CV
curl -X PUT -H "X-Admin-Key: $KEY" --data-binary @photo.jpg    $API/admin/tenants/bob/files/assets/photo.jpg

# check the result
curl -H "X-Admin-Key: $KEY" $API/admin/tenants
curl -H "X-Admin-Key: $KEY" "$API/admin/tenants/bob/preview?profile=recruiter&locale=en"
```

Changes are picked up within ~10 seconds, no redeploy needed. A new tenant **host** additionally
needs to be added to the `web` service's domains in Coolify (and DNS).

## 3. Invites

```bash
# create: returns the code ONCE plus the ready-to-send link
curl -X POST -H "X-Admin-Key: $KEY" -H "Content-Type: application/json" \
  -d '{ "profile": "recruiter", "label": "ACME – Jane Doe", "expiresInDays": 30, "maxUses": 3,
        "overrides": { "flags": { "hideCompanies": true } } }' \
  $API/admin/tenants/bob/invites
# → { "code": "t9Ev8zv3tMXcFhEYD5br8w", "link": "https://bob-cv.velarix.space/?c=t9Ev8zv3tMXcFhEYD5br8w",
#     "pdf": [ { "locale": "de", "ok": true, "bytes": 196196 }, { "locale": "en", "ok": true, "bytes": 191782 } ] }
# "pdf" shows immediately whether the PDFs could be rendered. Retry a failed render:
curl -X POST -H "X-Admin-Key: $KEY" $API/admin/tenants/bob/invites/<id>/pdf

# fixed, guessable code for a public demo (only for fictional/public content)
curl -X POST -H "X-Admin-Key: $KEY" -H "Content-Type: application/json" \
  -d '{ "profile": "full", "label": "Public demo", "code": "demo" }' \
  $API/admin/tenants/demo/invites

# list / revoke
curl -H "X-Admin-Key: $KEY" $API/admin/tenants/bob/invites
curl -X DELETE -H "X-Admin-Key: $KEY" $API/admin/tenants/bob/invites/<id>
```

---

## 4. Is the deployment up to date?

**Software.** Each image carries the Git commit it was built from (Coolify: *Configuration → Advanced →
Include Source Commit in Build* must be on; otherwise the commit reads `unknown`).

```bash
curl -s https://cv.velarix.space/version.json        # web (Nuxt build)
curl -s https://cv.velarix.space/api/version         # api + pdf renderer
git rev-parse origin/main                            # expected commit
```

**CV data.** Hashes of the files on the server, comparable with `sha256sum` on the files in Git:

```bash
curl -s -H "X-Admin-Key: $KEY" $API/admin/tenants/bob/hash
tools/cv-sync.sh --verify tenants/bob     # exit 1 and a list of differing files if not up to date

# combined hash, reproducible in the shell (same value as "combined"):
cd tenants/bob && find . -type f \( -name tenant.json -o -name 'cv.*.json' -o -path './assets/*' \) \
  | sed 's#^\./##' | LC_ALL=C sort | xargs sha256sum | sha256sum
```

`GET /api/admin/tenants` lists the combined `dataHash` of every tenant. `/api/cv` returns `cvHash`, the hash of
exactly the redacted CV the visitor receives.

## 5. Visitor tracking

Owner-only reading statistics with a consent modal (docs/VISITOR_SESSION_TRACKING.md). Off until enabled per tenant:

1. Add the controller to `tenant.json`: `"privacy": { "controller": "Your Name", "contact": "privacy@example.org" }`.
   Visitors then see the consent modal; nothing is recorded before they accept.
2. Optional: switch it off for a profile (`"tracking": { "enabled": false }` in the profile) or per invite (admin UI,
   "Consent modal & visitor tracking").
   Readers outside the EU/EEA/UK/CH (`"consent": "notice"`) or who already agreed elsewhere (`"consent": "prior"`,
   with `consentNote`) can be tracked without the modal; set it per invite in the admin UI (§9.2 of the tracking doc).
3. Optional location data: put MaxMind-format databases into the volume, e.g. DB-IP Lite (free, CC BY 4.0):
   `dbip-city-lite-<yyyy-mm>.mmdb` as `/data/geo/city.mmdb` and `dbip-asn-lite-<yyyy-mm>.mmdb` as
   `/data/geo/asn.mmdb` (restart the API after updating them). Lookups happen locally; no IP leaves the server.
4. Reports: admin UI → *Analytics*. Data is deleted automatically after the retention periods (`tracking.retention`).
5. Have the consent texts reviewed before going live; changing them (`CONSENT_TEXT_VERSION` in
   `src/app/utils/tracking.ts` and `TrackingPolicy.TextVersion` in the API) asks every visitor again.

## 6. Security notes

- The admin API is reachable through the public domain but requires `X-Admin-Key`. Use a long random key.
  To keep it off the internet entirely, leave `CV_ADMIN_API_KEY` empty and only set it temporarily when needed.
- nginx logs requests **without** query strings and sends `Referrer-Policy: no-referrer`, so invite codes
  do not leak into logs or to third parties. The app removes `?c=` from the address bar after redeeming.
- All pages and API responses send `X-Robots-Tag: noindex, nofollow`; CV responses are `Cache-Control: private, no-store`.
- Invite redemption is rate-limited per client IP (`Cv__RedeemPerMinute`, default 10). The client IP is the
  right-most `X-Forwarded-For` entry set by Traefik and passed through unchanged by nginx.
- If Cloudflare (proxy or tunnel) sits in front of Coolify, set `CV_CLIENT_IP_HEADER=CF-Connecting-IP`.

---

## 7. Local development

```bash
# API (sample tenants "demo" on localhost and "bob" via invite; admin key "dev-admin-key")
cd api/CvApi && dotnet run                 # http://localhost:5080/api

# optional: PDF renderer (needs a Chromium; then start the API with the two Pdf__ variables)
cd pdf && npm install && PORT=3100 CHROMIUM_PATH=/path/to/chromium node server.mjs
#   Pdf__RendererUrl=http://localhost:3100 Pdf__AppBaseUrl=http://localhost:3000 dotnet run

# Frontend (proxies /api to the API, keeps the Host header)
cd src && npm install && npm run dev       # http://localhost:3000
```

- `http://localhost:3000` → tenant `demo` (public profile enabled in the sample).
- `http://localhost:3000/admin` → admin UI (key `dev-admin-key`).
- `http://127.0.0.1:3000` → behaves like the shared host (no access without invite).
- Create an invite: `curl -X POST -H "X-Admin-Key: dev-admin-key" -H "Content-Type: application/json" -d '{"profile":"full"}' http://localhost:5080/api/admin/tenants/bob/invites`
  and open `http://127.0.0.1:3000/?c=<code>`.

Full stack with Docker: `docker compose up --build`, then put tenant files into the volume via the admin API
(set `CV_ADMIN_API_KEY` in a `.env` next to `docker-compose.yml`).

Tests: `cd api && dotnet test` (API) and `cd src && npm test` (frontend).
