# End-to-end tests

Playwright tests against the **full stack**: the generated SPA, the C# API and the PDF renderer, on a fresh copy of
the sample data (`api/sample-data`: tenants `demo` and `bob`). Nothing is mocked.

## Run locally

Requirements: Node 22, .NET 10 SDK.

```bash
cd e2e
npm ci
npx playwright install chromium   # once (skip in Claude Code cloud sessions: PLAYWRIGHT_BROWSERS_PATH=/opt/pw-browsers)
npx playwright test               # starts the stack, runs all tests
npx playwright test admin         # one file
npx playwright test --ui          # interactive
```

`stack/start.mjs` (Playwright's `webServer`) starts, without Docker:

| Service | URL | What |
|---|---|---|
| web | http://localhost:4173 | `src/.output/public` + `/api` proxy (`stack/web.mjs`, like `src/nginx.conf.template`) |
| api | http://127.0.0.1:5181 | `dotnet run` of `api/CvApi`, data in `e2e/.stack/data`, admin key `e2e-admin-key` |
| pdf | http://127.0.0.1:3181 | `pdf/server.mjs` with Playwright's Chromium |

- The frontend is regenerated (`npm run generate`) when `src/` is newer than the last build; `E2E_SKIP_BUILD=1` skips that.
- The data directory is recreated on every start. Locally a running stack is reused (`reuseExistingServer`), so tests
  must not depend on a pristine state; `npm run stack` starts it on its own (e.g. to keep it running between runs).
- `E2E_STACK_LOGS=1` shows the service logs; ports: `E2E_WEB_PORT`, `E2E_API_PORT`, `E2E_PDF_PORT`.

Two hostnames of the same web server model the real deployment: **localhost** is the demo tenant's own host (public
profile without invite), **127.0.0.1** is the shared host (showcase; invite links of tenants without own host).

## Against Docker Compose (or any deployment)

Set `E2E_BASE_URL` to test a running stack instead of starting one. It must serve the sample data with the admin key
`e2e-admin-key` (or set `E2E_ADMIN_KEY`); `e2e/docker-compose.e2e.yml` does that for the real containers:

```bash
docker compose -p cv-e2e -f docker-compose.yml -f e2e/docker-compose.e2e.yml up -d --build --wait
cd e2e && E2E_BASE_URL=http://localhost:8080 npx playwright test
docker compose -p cv-e2e -f docker-compose.yml -f e2e/docker-compose.e2e.yml down -v
```

`E2E_SHARED_URL` defaults to `E2E_BASE_URL` with `localhost` replaced by `127.0.0.1`.

## What is covered (happy paths)

| File | Feature |
|---|---|
| `public-cv.spec.ts` | Public CV on the tenant host, server-side redaction, language switch, favicon |
| `invites.spec.ts` | Showcase + invite form, invite links (code removed from URL, profile grants, assets), tenants without own host, revoke, view once |
| `pdf.spec.ts` | PDF download of the visitor's view, rendering on invite creation, PDF cache, print mode readiness |
| `consent-tracking.spec.ts` | Consent modal accept/decline, tracking events, owner analytics, profiles without tracking |
| `admin.spec.ts` | Admin login, invites (create, open, revoke), file editor, preview per profile, favicon + PDF template, analytics |
| `api.spec.ts` | Health/version, no access on the shared host, redeem/logout, asset access rule, admin key |
| `mobile.spec.ts` | Phone layout |
| `public-pages.spec.ts` | Showcase sign-up / pricing / legal links, `/pricing` (Free, Pro, four passes), legal pages |

Tests run one after another (`workers: 1`) because they share one backend. Use `unique()` labels and data that does
not depend on other tests; restore files you change (see the file editor test).
