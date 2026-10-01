# Requirements: Accounts, Plans and Billing (SaaS)

Turns the single-owner CV host into a self-service product: people sign up, build their CV, share it with
invite links and can buy Pro time. Builds on [`REQUIREMENTS_ACCESS_AND_TENANCY.md`](REQUIREMENTS_ACCESS_AND_TENANCY.md)
(tenants, invites, redaction), which stays the source of truth for everything visitors see.

Keywords MUST / SHOULD / MAY follow RFC 2119.

---

## 1. Roles

| Role | Who | How authenticated |
|---|---|---|
| **Visitor / invitee** | Recruiter etc. opening an invite link | Invite cookie (unchanged) |
| **User** | CV owner with an account; owns exactly one tenant | Account session cookie `cv_session` |
| **Super-admin** | Operator of the platform | `X-Admin-Key` (unchanged, `Admin__ApiKey`) |

- S1.1 A user owns at most one tenant (their CV). The tenant id is the user's chosen **handle**.
- S1.2 Tenants without an owner (created by the super-admin, e.g. `demo`, the operator's own CV) are
  **managed tenants**: no plan limits apply.
- S1.3 The super-admin can do everything a user can, on every tenant, plus user management (§6).

---

## 2. Sign-up and sign-in

- S2.1 **Providers.** Sign-in with Google, Microsoft, GitHub and LinkedIn (OpenID Connect / OAuth 2.0), each
  enabled only when its client id and secret are configured (`Auth__Google__ClientId`, …). `GET /api/auth/providers`
  lists the enabled ones; the login page shows only those.
- S2.2 **Magic link (optional).** `POST /api/auth/magic-link { email }` sends a one-time sign-in link (15 min,
  single use, stored hashed) via an HTTP e-mail API (Resend, `Email__ResendApiKey`, `Email__From`). Without an e-mail
  key the provider is disabled, unless `Email__LogLinks=true` (development), which writes the link to the log.
  The response is always `204`, so it does not reveal whether an account exists. Rate-limited per IP and per address.
- S2.3 **Account linking.** A login is found by (provider, subject). On first login with a new provider, the
  account with the same **verified** e-mail address is used (Google, GitHub, LinkedIn, magic link verify the
  address). Microsoft logins are never linked by e-mail (its `email` claim is not verified for every account
  type); if the address is taken, sign-in fails with `account_exists` and the user signs in with the original method.
- S2.4 **Session.** `cv_session` is an `HttpOnly; Secure; SameSite=Lax` cookie (data-protection encrypted, 30 days,
  sliding). Every request re-checks the user in the database: blocked or deleted users and sessions older than the
  user's `SecurityStamp` (changed on block, "sign out everywhere", deletion) are rejected immediately.
- S2.5 **CSRF.** State-changing requests authenticated by the session cookie MUST carry the header
  `X-Requested-With: cv` (the SPA always sends it); cross-site forms cannot set it.
- S2.6 **Return URL.** `returnUrl` of the login endpoints MUST be a local path (`/…`, not `//…`); anything else
  becomes `/admin`.
- S2.7 `GET /api/auth/me` returns the user (`id, email, name, avatarUrl, tenantId, plan, logins`) or `401`.
  `POST /api/auth/logout` ends the session.

---

## 3. Onboarding: the user's tenant

- S3.1 After the first sign-in the user has no tenant. The dashboard (`/admin`) asks for a **handle**
  (`^[a-z0-9][a-z0-9-]{2,30}$`, not reserved: `admin`, `api`, `www`, `cv`, `app`, `demo`, `mail`, `help`, `support`,
  `status`, … and not used by any tenant directory) and the CV language, then creates the tenant:
  `tenant.json` with the profiles `full`, `recruiter` (hides birth date, day precision) and `public`
  (not enabled as public profile), and a starter `cv.<locale>.json` with the user's name.
- S3.2 **Tenant host.** If `Saas__TenantHostSuffix` is set (e.g. `cv.velarix.space`, needs wildcard DNS and a
  wildcard domain in Coolify), the tenant gets `<handle>.<suffix>` as host. Otherwise invite links use the shared
  host (`/cv?c=…`).
- S3.3 **Import.** The user can start from: an empty starter CV, a **LinkedIn data export** (§7), or a CV JSON file.
- S3.4 **Server-managed fields.** Users cannot change `hosts` in `tenant.json`: on upload by a user the server
  keeps the stored value (custom domains are set via §5.4). Users cannot create other tenants.
- S3.5 Features that use the operator's credentials are super-admin only: registering / fetching CV revisions
  from git (`…/revisions`, `…/revisions/fetch`; the git token belongs to the operator).
- S3.6 **Storage quota.** Assets per tenant: Free 20 MB, Pro 200 MB (`Saas__QuotaFreeMb`, `Saas__QuotaProMb`).
  Uploads over the quota fail with `413 quota_exceeded`.

---

## 4. Plans

| | Free (forever) | Pro |
|---|---|---|
| CV, all profiles, redaction, PDF, all templates, languages | ✓ | ✓ |
| Active invites (not revoked, not expired; used-up codes count while their visitors keep access; QR invites not counted) | **3** | unlimited |
| "Created with …" credit in PDFs | shown | can be removed |
| Visitor statistics: visits, sessions, time on CV, devices, per-invite reach | ✓ | ✓ |
| Heatmaps, attention per section/entry, technology intent, session replay timeline | – | ✓ |
| Own domain | – | ✓ |
| Asset storage | 20 MB | 200 MB |

- S4.1 Limits are enforced **server-side**, also when an inactive invite would become active again (rearm, later
  expiry) (`402 { error: "plan_limit", limit, feature }`); the UI shows an upgrade hint.
- S4.2 **Downgrade.** When Pro ends nothing is deleted: existing invites keep working until they expire, but no new
  invite can be created while 3 or more are active; the credit is shown again; the own domain stops resolving
  (visitors see the shared host's showcase) until Pro is active again; heat data keeps being recorded (with consent),
  so it is visible again after upgrading.
- S4.3 Effective plan: `pro` if `proUntil > now` or `proForever`, else `free`. Managed tenants count as `pro`.
- S4.4 R11.4 of the tenancy requirements still applies: **analytics features are never advertised on public
  pages** (showcase, pricing). The plan comparison naming heatmaps is only shown inside the signed-in dashboard.

### 4.1 Pricing: Pro passes

Pro is sold as **prepaid passes** (one-time payments, no subscription, no auto-renewal – nothing to cancel).
Buying a pass while Pro is active extends it. Default prices (configurable, `Billing__Passes`):

| Pass | Days | Price | Shown as |
|---|---|---|---|
| 1 week | 7 | €5 | €5.00 / week |
| 1 month | 30 | €17 | €3.97 / week |
| 6 months | 182 | €78 | €3.00 / week |
| 1 year | 365 | €104 | €1.99 / week |

The pricing UI shows the price per week prominently and the total below it, with the saving against the weekly pass.

---

## 5. Billing (Paddle)

Paddle is the merchant of record (handles VAT, invoices, refunds). The integration is off until configured.

- S5.1 **Config.** `Billing__Paddle__Environment` (`sandbox` | `production`), `Billing__Paddle__ClientToken`
  (public, for Paddle.js), `Billing__Paddle__WebhookSecret`, and per pass `Billing__Passes__<n>__PaddlePriceId`.
  `GET /api/billing/config` returns `{ provider: "paddle" | null, environment, clientToken, passes: [{ id, days, amount, currency, priceId }] }`.
- S5.2 **Checkout.** The dashboard opens the Paddle.js overlay checkout with the pass's `priceId`, the user's e-mail
  and `customData: { userId }`. No card data touches our servers.
- S5.3 **Webhook.** `POST /api/billing/paddle/webhook` verifies `Paddle-Signature` (`ts=…;h1=…`, HMAC-SHA256 of
  `ts:rawBody` with the webhook secret, max. 5 min clock skew; invalid → `401`). On `transaction.completed` it adds
  the pass's days to the user's Pro time (`proUntil = max(now, proUntil) + days`). Idempotent per transaction id.
  Unknown users or prices are recorded as payments with `status: "unmatched"` for the super-admin.
- S5.4 Refunds / chargebacks (`adjustment.created` / `adjustment.updated` with action `refund` or `chargeback`, approved)
  are recorded; the super-admin adjusts Pro time manually (§6).
- S5.5 Every payment is stored (`Payments`: provider, external id, user, pass, amount, currency, days, status, createdAt)
  and listed for the user (`GET /api/account/payments`) and the super-admin. Invoices come from Paddle by e-mail.

### 5.4 Own domain (Pro)

- S5.6 `PUT /api/account/domain { domain }` sets one own domain. It is accepted when its DNS (A/AAAA/CNAME) resolves
  to the same addresses as `Saas__DomainTarget` (default: the shared host), it is not the shared host or another
  tenant's host, and the user has Pro. `DELETE` removes it. The domain is added to the tenant's hosts; the operator
  must also add it to the `web` service in Coolify (shown as instruction). Without Pro the domain is ignored by tenant
  resolution (§4 S4.2).

---

## 6. Super-admin: user management

The admin page with the admin key gets a **Users** tab.

- S6.1 List / search users: e-mail, name, handle (tenant), providers, created, last login, plan, Pro until, blocked.
- S6.2 **Set plan manually** (e.g. paid via another platform, gift, partner deal): `PUT /api/admin/users/{id}/plan
  { proUntil, proForever, note }` – stored with source `manual` and the note, and recorded as a payment row
  (`provider: "manual"`) for the history. Quick actions: +7 / +30 / +365 days, forever, back to free.
- S6.3 Block / unblock (blocked users cannot sign in; their sessions end; their CV stays online unless the tenant is
  also disabled – blocking hides the CV: tenant resolution skips tenants of blocked users).
- S6.4 Delete a user (§8 deletion).
- S6.5 Payments list incl. unmatched webhook payments.

---

## 7. LinkedIn import

LinkedIn's API does not give third parties positions, education or skills, so the import uses LinkedIn's own data export.

- S7.1 The user requests "Get a copy of your data" on LinkedIn and uploads the ZIP:
  `POST /api/account/import/linkedin?locale=en` (multipart, max. 20 MB). The API parses `Profile.csv`, `Positions.csv`,
  `Education.csv`, `Skills.csv`, `Languages.csv`, `Certifications.csv`, `Projects.csv`, `Email Addresses.csv`,
  `PhoneNumbers.csv` and returns the generated CV JSON plus warnings. With `?apply=true` it is written as
  `cv.<locale>.json`; the UI asks before overwriting an existing CV.
- S7.2 The ZIP is read in memory and not stored. Only known CSV files are read; per-file size is capped (zip bombs).
- S7.3 "Sign in with LinkedIn" fills name, e-mail and photo only.

---

## 8. Privacy and legal

- S8.1 **Export.** `GET /api/account/export` returns a ZIP with the user's account data (JSON), payments and all tenant
  files (GDPR Art. 20).
- S8.2 **Deletion.** `DELETE /api/account` (confirmation: the handle) deletes the user, logins, the tenant directory,
  its invites, cached PDFs and tracking data. Payment records are kept anonymised (bookkeeping duty).
- S8.3 Pages `/legal/terms`, `/legal/privacy`, `/legal/imprint` with operator details from
  `NUXT_PUBLIC_LEGAL_*` build variables; the showcase footer and login page link them. Texts are templates the operator
  must review.
- S8.4 Account data lives in `/data/accounts.db` (separate SQLite file, like `tracking.db`).

---

## 9. E-mail notifications to owners

Sent through the e-mail provider of §2.2 (nothing is sent without one), only to owners with an account.

- S9.1 **Invite opened.** On the first redemption of an invite (or the first scan of its PDF's QR code) the owner gets
  "Your CV was opened" with the invite's label. Later redemptions send nothing. Users can turn it off
  (`PATCH /api/account { notifyOnOpen: false }`); default on. The visitor is not told.
- S9.2 **Pro ends soon.** 3 days before `proUntil` the owner gets one reminder per pass (not for "forever" plans).
- S9.3 Sending runs in the background; a failing e-mail provider never delays or breaks a visitor's request.

---

## 10. API summary

| Method & path | Auth | Purpose |
|---|---|---|
| `GET /api/auth/providers` | – | Enabled sign-in methods. |
| `GET /api/auth/login/{provider}?returnUrl=` | – | Start OAuth sign-in. |
| `POST /api/auth/magic-link` `{ email, returnUrl? }` | – (rate-limited) | Send sign-in link (§2.2). |
| `GET /api/auth/magic?token=` | – | Redeem sign-in link. |
| `GET /api/auth/me` · `POST /api/auth/logout` | session | Current user · sign out. |
| `POST /api/account/tenant` `{ handle, locale, name? }` | session | Create the user's tenant (§3). |
| `GET /api/account` · `PATCH /api/account` `{ name, hideCredit, notifyOnOpen }` | session | Account, plan, usage and limits. |
| `POST /api/account/import/linkedin` | session | LinkedIn ZIP import (§7). |
| `PUT/DELETE /api/account/domain` | session, Pro | Own domain (§5.4). |
| `GET /api/account/payments` | session | Own payments. |
| `GET /api/account/export` · `DELETE /api/account` `{ confirm: <handle> }` | session | GDPR export · deletion (§8). |
| `POST /api/account/sessions/revoke` | session | Sign out on all other devices (§2 S2.4). |
| `GET /api/account/handle/{handle}` | session | Is a handle available (`error`: `invalid_handle`, `handle_reserved`, `handle_taken`)? |
| `GET /api/billing/config` | – | Passes and Paddle client config. |
| `POST /api/billing/paddle/webhook` | Paddle signature | Payment events (§5.3). |
| `GET /api/admin/users` · `GET/DELETE /api/admin/users/{id}` | admin key | User management (§6). |
| `PUT /api/admin/users/{id}/plan` · `POST …/block` · `POST …/unblock` | admin key | Plan / block (§6). |
| `GET /api/admin/payments` | admin key | All payments (§6). |
| `GET /api/admin/stats?days=30` | admin key | Users, Pro users, active invites, sign-ups per day, revenue per currency, refunds, unmatched payments. |
| `/api/admin/tenants/{tenant}/…` (existing) | admin key **or** session of the tenant's owner | Tenant admin API; other tenants answer `404`. |
