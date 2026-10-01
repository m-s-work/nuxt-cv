# Features

Catalogue of the product's features. **Visibility** says where a feature may be advertised:

- **public**: may be shown on the showcase (shared host, `src/app/components/CvShowcase.vue`)
- **owner**: only for the CV owner / admin; never mentioned on the showcase or to invitees
  (see [requirements §11](REQUIREMENTS_ACCESS_AND_TENANCY.md#11-showcase-shared-host-without-invite))

| Feature | Visibility | Status |
|---|---|---|
| [CV as Code](#cv-as-code) | public | ✅ implemented |
| [Git workflow](#git-workflow) | public | ✅ implemented (sync tool + CI example) |
| Field-level gating (`requires` / `fieldRequires` / `hiddenFields`) | public | ✅ |
| Privacy switches (`hideCompanies`, `hideTimeframeDays/Months`, `hidePhoto`, …) | public | ✅ |
| Personal invite links (expiry, usage limit, instant revocation) | public | ✅ |
| Multiple people (own subdomain or shared host + invite code) | public | ✅ |
| [PDF per invite](#pdf-per-invite) | public | ✅ implemented, not yet deployed |
| Selectable PDF templates (per person, profile or invite) | public | ✅ `editorial`, `classic`, `banner` |
| Template variables: colour sets, colours, toggles (e.g. chapter colours) | public | ✅ |
| Template builder UI (admin "Design" tab, live PDF preview) | owner | ✅ |
| Graphical CV editor with live web / data / PDF preview per profile (admin "Edit" tab) | owner | ✅ |
| Website links on entries (redirect through the API) | public | ✅ |
| Selectable web templates | public | 📝 planned (#77) |
| Multilingual content and UI (EN/DE) | public | ✅ |
| Interactive timeline, technology filter | public | ✅ |
| Print layout with QR code | public | ✅ (print layout needs polish, see PDF follow-ups) |
| Responsive, dark mode, screenshots & logos with image viewer | public | ✅ |
| Self-hosted (Docker, Coolify) | public | ✅ |
| QR code in PDF opens the same view (linked QR invite) | public | ✅ implemented, not yet deployed |
| QR scan tracking per invite (`source: "pdf-qr"`, use count) | owner | ✅ admin API only |
| Admin API (tenants, files, invites, preview, PDF re-render) | owner | ✅ |
| Deployment checks: software version (`/api/version`, `/version.json`) and CV data hashes (`cv-sync.sh --verify`) | owner | ✅ |
| Admin web interface | owner | ⏳ not started |
| Analytics (e.g. heatmap tracking, invite usage insights) | owner | ⏳ planned, must stay hidden |

---

## CV as Code

Like *Infrastructure as Code* describes servers in versioned files instead of clicking through
consoles, *CV as Code* describes a CV **and who may see which part of it** in plain files:

| File | Contains | IaC analogy |
|---|---|---|
| `cv.<locale>.json` | the complete master CV per language, with `requires` / `fieldRequires` markers on confidential parts | resource definitions |
| `tenant.json` | hosts, profiles (grants, flags, hidden fields), public access, favicon | policies / environments |
| `assets/` | photo, logos, screenshots | artifacts |

Properties:

- **Single source of truth**: one master CV; every view (recruiter, anonymised, full, public) is
  *derived* by the API at request time, nothing is copied or maintained twice.
- **Declarative redaction**: what a recipient sees is declared (`hideCompanies: true`,
  `requires: ["private"]`), not hand-edited per recipient.
- **Verifiable deployment**: `tools/cv-sync.sh --verify` compares the SHA-256 of every file with the
  server; `/api/version` shows which commit of the software runs.
- **Validated before deployment**: `tools/cv-sync.sh --check` validates JSON, file names and that
  every referenced asset exists; the API validates again on upload.
- **Idempotent deployment**: `tools/cv-sync.sh` uploads the folder via the admin API; running it
  twice changes nothing. Changes are live within ~10 seconds, no rebuild or redeploy.
- **Runtime state stays out of Git**: invites (codes are secrets), usage counters and rendered PDFs
  live in the API's data volume, like state files are kept out of IaC repositories.

## Git workflow

Keep the CV files in a (private) Git repository and let CI deploy them:

```
tenants/<tenant-id>/tenant.json
tenants/<tenant-id>/cv.en.json
tenants/<tenant-id>/cv.de.json
tenants/<tenant-id>/assets/…
```

- **History**: `git log -p tenants/bob/cv.en.json` shows how the CV evolved; tags mark the version
  sent with an application (`git tag application-acme-2026-10`).
- **Review**: changes go through pull requests; CI validates them (`cv-sync.sh --check`) before merge.
- **Deploy**: a push to `main` runs `cv-sync.sh`, which uploads the files to the API.
  Example workflow: [`docs/examples/cv-repo-deploy.yml`](examples/cv-repo-deploy.yml)
  (secrets `CV_API_URL`, `CV_ADMIN_API_KEY`).
- **Pinned versions**: `cv-sync.sh` registers every deploy under its commit SHA. An invite (or a profile)
  can be pinned to that SHA, so a recipient keeps seeing the version that was sent (text and images); the
  admin UI warns when the CV has changed since and offers to move the pin to the current version. Only
  versions still pinned (plus the current one) are kept on the server; older ones (or tags like
  `application-acme-2026-10`) are fetched from the CV repository again when an invite is pinned to them.
- **Rollback**: `git revert` + push restores the previous CV everywhere; cached PDFs become stale
  automatically (their hash no longer matches) and are re-rendered on the next request.
- **Diff-friendly**: one JSON value per line (2-space indentation) keeps diffs readable; `//` comments are allowed.

```bash
# local
tools/cv-sync.sh --check tenants/bob                               # validate only
CV_API_URL=https://cv.velarix.space/api CV_ADMIN_API_KEY=… \
  tools/cv-sync.sh tenants/bob                                     # deploy
```

Not (yet) supported: deleting remote files that were removed from Git (remove them via the data
volume), and pull-based sync (API watching a repository).

## PDF per invite

Every invite gets a PDF of exactly its view, rendered by a separate Chromium container when the
invite is created (failures are reported in the create response) and re-rendered automatically when
the CV changes. Details: [requirements §12](REQUIREMENTS_ACCESS_AND_TENANCY.md#12-pdf-per-invite).
