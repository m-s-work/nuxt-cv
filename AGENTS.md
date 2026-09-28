# AGENTS.md

Instructions for AI coding agents (Claude Code, Copilot, Codex, …) working in this repository.

## Track work with the task tool (required)

Any request with more than one step MUST be tracked with the agent's task tool, not only in chat text.
In Claude Code these are `TaskCreate`, `TaskUpdate` and `TaskList`:

1. On receiving the request (and whenever the user adds requests mid-work), create one task per
   deliverable, for example:
   `TaskCreate({ subject: "Implement PDF rendering container", description: "Playwright renderer service in compose, render on invite creation, cache per invite", activeForm: "Implementing PDF rendering" })`
2. Before starting a task: `TaskUpdate({ taskId: "3", status: "in_progress" })`.
3. Mark it `completed` only when it is fully done and verified (tests pass, build works).
   If blocked, keep it `in_progress` and create a follow-up task describing the blocker.
4. Use `TaskList` after finishing a task to pick the next one and to make sure nothing was forgotten.

Other agents: use your equivalent (e.g. `TodoWrite`, a checklist in the PR description).

## Branches, commits and pull requests (Conventional Commits)

All names follow [Conventional Commits](https://www.conventionalcommits.org/).
Types: `feat`, `fix`, `docs`, `refactor`, `test`, `ci`, `build`, `chore`, `perf`, `style`.

| What | Format | Example |
|---|---|---|
| Branch | `<type>/<issue-or-pr-nr>-<short-kebab-description>` | `feat/71-pdf-per-invite`, `fix/4-fix-pdf-is-empty` |
| PR title | `<type>(<optional scope>): <description>` | `feat(pdf): render a PDF per invite code` |
| Commit message | `<type>(<optional scope>): <description>` (+ body) | `fix(print): keep entries on one page` |

- Use the number of the issue the work belongs to; create the issue first if there is none.
  Without any issue or PR number, leave the number out: `docs/agents-naming`.
- Reference the issue in the PR description (`Closes #71`).
- Breaking changes: `feat!: …` or a `BREAKING CHANGE:` footer.

### No force pushes

- **Never force-push** (`git push --force`, `--force-with-lease`, deleting and re-pushing a branch)
  and never rewrite history that is already pushed (no rebase/amend/squash of pushed commits).

### Merging into `main`

- PRs are merged into `main` as **squash merge**; the squash commit message is the PR title
  (Conventional Commits, see above).
- Use a **rebase merge** only when the individual commits add real value on `main`
  (each is a self-contained, meaningful Conventional Commit).

## Repository map

| Path | What |
|---|---|
| `src/` | Nuxt 4 SPA (static, **contains no CV data**; loads it via `useCv()` from `/api/cv`) |
| `api/CvApi/` | C# ASP.NET Core API: tenants, invites, redaction, assets |
| `api/CvApi.Tests/` | xUnit tests |
| `api/sample-data/` | Sample tenants (`demo`, `bob`) for development and showcase screenshots |
| `pdf/` | Internal PDF renderer (Playwright/Chromium), called by the API |
| `docker-compose.yml` | Deployment (Coolify): `web` (nginx + SPA), `api`, `pdf` |
| `docs/REQUIREMENTS_ACCESS_AND_TENANCY.md` | Source of truth for tenancy, invites, redaction, API, showcase |
| `docs/DEPLOYMENT_COOLIFY.md` | Deployment and operations |
| `docs/VISITOR_SESSION_TRACKING.md` | Plan: visitor/session tracking, heatmap, interest signals (owner-only) |

## Commands

```bash
cd api && dotnet test                 # API tests
cd api/CvApi && dotnet run            # API on http://localhost:5080 (sample data, admin key "dev-admin-key")
cd src && npm test -- --run           # frontend tests
cd src && npm run dev                 # frontend on http://localhost:3000 (proxies /api)
cd src && npm run generate            # production build (static)
```

Run the relevant tests and the build before committing.

## Rules

- Never put real CV data into `src/` or the repository; CV content lives in the API's data volume.
  Only `api/sample-data` (fictional) is committed.
- Redaction happens server-side only. Hidden data must never be sent to the browser.
- The showcase lists public features only. Analytics / tracking features (e.g. heatmaps) must never
  be mentioned there (requirements §11).
- Update `docs/REQUIREMENTS_ACCESS_AND_TENANCY.md` when behaviour or API changes.
