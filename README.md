# nuxt-cv

CV of a software architect built with Nuxt 4.

## Features

- **Nuxt 4** - Latest version of Nuxt with Vue 3
- **Nuxt UI** - Beautiful and accessible UI components
- **Nuxt i18n** - Internationalization support (English & German)
- **Multi-tenant** - Hostname and/or invite code decide which person's CV is shown
- **Invite-gated** - No CV is public by default; invites select a redaction profile
- **Redaction** - Per-field visibility plus global flags (`hideCompanies`, `hideTimeframeMonths`, ...)
- **C# backend** - ASP.NET Core API (`api/`) serving the redacted CV JSON
- **Testing** - Vitest (frontend) and xUnit (API)
- **PDF per invite** - Rendered on invite creation, cached, re-rendered when the CV changes
- **Coolify** - Docker Compose deployment (nginx + API + PDF renderer)

## Project Structure

```
.
├── .github/workflows/           # CI (tests/build)
├── docker-compose.yml           # Coolify / Docker deployment (web + api)
├── docs/                        # Documentation (requirements, deployment, design notes)
├── pdf/                         # PDF renderer service (headless Chromium, internal)
├── api/                         # C# backend
│   ├── CvApi/                   # ASP.NET Core API (tenants, invites, redaction)
│   ├── CvApi.Tests/             # xUnit tests
│   └── sample-data/             # Sample tenants for local development
└── src/                         # Nuxt 4 application (static SPA, no CV data)
    ├── app/
    │   ├── components/          # Vue components
    │   ├── composables/         # useCv() loads the CV from the API
    │   └── pages/               # Application pages
    ├── Dockerfile               # SPA build + nginx (proxies /api)
    └── tests/                   # Component and unit tests
```

## Getting Started

### Prerequisites

- Node.js 22.x
- npm
- .NET 10 SDK (for the API)

### Installation

1. Navigate to the src directory:
   ```bash
   cd src
   ```

2. Install dependencies:
   ```bash
   npm install
   ```

3. Copy the example environment file:
   ```bash
   cp .env.example .env
   ```

4. Start the API (separate terminal, uses `api/sample-data`):
   ```bash
   cd api/CvApi && dotnet run
   ```

5. Start the development server:
   ```bash
   npm run dev
   ```

The application will be available at `http://localhost:3000` (sample tenant `demo`).
`http://127.0.0.1:3000` behaves like the shared host and requires an invite, see
[docs/DEPLOYMENT_COOLIFY.md](docs/DEPLOYMENT_COOLIFY.md#5-local-development).

### Development

```bash
npm run dev          # Start development server
npm run build        # Build for production
npm run generate     # Generate static site
npm run preview      # Preview production build
npm test             # Run tests
npm run test:ui      # Run tests with UI
```

## Components

### Frontend Components

- **CvExperiences** - Display professional experience/jobs
- **CvStudies** - Display education/studies

### Backend API

The C# API in `api/` is the single source of CV data. It resolves the tenant from the hostname
and/or invite code (`?c=<code>`), applies the profile's redaction and returns the CV JSON.

- Requirements: [docs/REQUIREMENTS_ACCESS_AND_TENANCY.md](docs/REQUIREMENTS_ACCESS_AND_TENANCY.md)
- Endpoints: see §8 of the requirements document

## Testing

The project uses Vitest with @nuxt/test-utils for testing:

```bash
npm test              # Run all tests
npm run test:ui       # Run tests with UI
npm run test:coverage # Run tests with coverage
```

API tests (xUnit):

```bash
cd api && dotnet test
```

## Deployment

Deployed on [Coolify](https://coolify.io) via `docker-compose.yml` (nginx serving the SPA and
proxying `/api` to the C# API). See [docs/DEPLOYMENT_COOLIFY.md](docs/DEPLOYMENT_COOLIFY.md).

GitHub Pages is no longer supported: a static host cannot gate access to the CV.

## Internationalization

The application supports multiple languages using @nuxtjs/i18n:

- English (default)
- German

Language can be switched via URL prefix: `/de/` for German.

## Documentation

For more detailed information, see the documentation in the `docs/` directory:

- [Access & Multi-Tenancy Requirements](docs/REQUIREMENTS_ACCESS_AND_TENANCY.md) - Tenants, invites, redaction, API
- [Coolify Deployment](docs/DEPLOYMENT_COOLIFY.md) - Deployment, tenant & invite management
- [Implementation Details](docs/IMPLEMENTATION.md) - Complete requirements and architecture
- [Project Summary](docs/PROJECT_SUMMARY.md) - Feature overview and statistics
- [Development Guide](src/docs/DEVELOPMENT.md) - Development workflow and best practices

## License

MIT
