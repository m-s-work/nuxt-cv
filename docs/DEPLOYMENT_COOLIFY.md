# Deployment on Coolify

The site consists of two containers, defined in [`docker-compose.yml`](../docker-compose.yml):

```
Internet ──► Coolify Traefik (TLS, domains) ──► web (nginx :80) ──┬─► static Nuxt SPA
                                                                  └─► /api/* ──► api (ASP.NET :8080) ──► /data volume
```

| Service | Source | Purpose |
|---|---|---|
| `web` | `src/Dockerfile`, `src/nginx.conf.template` | Builds the Nuxt SPA (`nuxt generate`) and serves it with nginx. Proxies `/api/` to the API, keeping the `Host` header (it decides the tenant). |
| `api` | `api/Dockerfile` | C# API: tenant resolution, invites, redaction, assets. Not exposed publicly. |

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

5. Deploy. Health checks: `web` → `GET /healthz`, `api` → `dotnet CvApi.dll --healthcheck` (both built into the images).

### DNS

Every host (shared + per tenant) needs an A/AAAA record pointing to the Coolify server,
e.g. a wildcard `*.velarix.space`. Coolify/Traefik issues the Let's Encrypt certificates.

### Persistent data

The named volume `cv-data` is mounted at `/data` in the API container:

```
/data/app.db       invites (SQLite)
/data/keys/        cookie-signing keys – losing them logs out every invitee
/data/tenants/<id>/tenant.json, cv.<locale>.json, assets/
```

Back up this volume (Coolify → *Persistent Storage* / scheduled backups, or `docker cp`).
If you prefer editing files over SSH, replace the named volume in Coolify's storage settings
with a bind mount (e.g. `/srv/nuxt-cv-data:/data`); the directory must be writable by UID `1654`
(the `app` user of the .NET image): `chown -R 1654:1654 /srv/nuxt-cv-data`.

---

## 2. Add a tenant

Tenants can be managed completely through the admin API (no shell access needed).
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
# → { "code": "t9Ev8zv3tMXcFhEYD5br8w", "link": "https://bob-cv.velarix.space/?c=t9Ev8zv3tMXcFhEYD5br8w", ... }

# list / revoke
curl -H "X-Admin-Key: $KEY" $API/admin/tenants/bob/invites
curl -X DELETE -H "X-Admin-Key: $KEY" $API/admin/tenants/bob/invites/<id>
```

---

## 4. Security notes

- The admin API is reachable through the public domain but requires `X-Admin-Key`. Use a long random key.
  To keep it off the internet entirely, leave `CV_ADMIN_API_KEY` empty and only set it temporarily when needed.
- nginx logs requests **without** query strings and sends `Referrer-Policy: no-referrer`, so invite codes
  do not leak into logs or to third parties. The app removes `?c=` from the address bar after redeeming.
- All pages and API responses send `X-Robots-Tag: noindex, nofollow`; CV responses are `Cache-Control: private, no-store`.
- Invite redemption is rate-limited per client IP (`Cv__RedeemPerMinute`, default 10). The client IP is the
  right-most `X-Forwarded-For` entry set by Traefik and passed through unchanged by nginx.
- If Cloudflare (proxied) sits in front of Coolify, the right-most entry becomes a Cloudflare IP; configure
  Traefik's trusted IPs / `CF-Connecting-IP` handling before relying on the rate limit.

---

## 5. Local development

```bash
# API (sample tenants "demo" on localhost and "bob" via invite; admin key "dev-admin-key")
cd api/CvApi && dotnet run                 # http://localhost:5080/api

# Frontend (proxies /api to the API, keeps the Host header)
cd src && npm install && npm run dev       # http://localhost:3000
```

- `http://localhost:3000` → tenant `demo` (public profile enabled in the sample).
- `http://127.0.0.1:3000` → behaves like the shared host (no access without invite).
- Create an invite: `curl -X POST -H "X-Admin-Key: dev-admin-key" -H "Content-Type: application/json" -d '{"profile":"full"}' http://localhost:5080/api/admin/tenants/bob/invites`
  and open `http://127.0.0.1:3000/?c=<code>`.

Full stack with Docker: `docker compose up --build`, then put tenant files into the volume via the admin API
(set `CV_ADMIN_API_KEY` in a `.env` next to `docker-compose.yml`).

Tests: `cd api && dotnet test` (API) and `cd src && npm test` (frontend).
