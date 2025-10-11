# nuxt-cv

CV of a software architect built with Nuxt 4.

## Features

- **Nuxt 4** - Latest version of Nuxt with Vue 3
- **Nuxt UI** - Beautiful and accessible UI components
- **Nuxt i18n** - Internationalization support (English & German)
- **Multi-tenant** - Support for different CV variants
- **Authentication** - Token-based or Basic Auth for admin endpoints
- **Testing** - Comprehensive test setup with Vitest
- **GitHub Pages** - Automated deployment workflow

## Project Structure

```
.
├── .github/
│   ├── copilot-instructions.md  # GitHub Copilot instructions
│   └── workflows/               # CI/CD workflows
│       ├── ci.yml              # Testing and build workflow
│       └── deploy.yml          # GitHub Pages deployment
├── docs/                        # Documentation files
└── src/                         # Nuxt 4 application
    ├── app/
    │   ├── components/          # Vue components
    │   ├── pages/               # Application pages
    │   └── server/              # Backend API
    │       ├── api/             # API endpoints
    │       └── utils/           # Server utilities (auth, tenants)
    ├── docs/                    # Technical documentation
    └── tests/                   # Component and integration tests
```

## Getting Started

### Prerequisites

- Node.js 20.x or higher
- npm

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

4. Start the development server:
   ```bash
   npm run dev
   ```

The application will be available at `http://localhost:3000`

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

The backend provides the following endpoints:

- `GET /api/experiences?tenant=<id>` - Get experiences for a tenant
- `GET /api/studies?tenant=<id>` - Get studies for a tenant
- `GET /api/admin/tenants` - List all tenants (requires authentication)

### Multi-tenant Support

The application supports multiple tenants, allowing you to show different CV content based on the tenant ID. Configure tenants in `src/app/server/utils/tenants.ts`.

Example usage:
```
/api/experiences?tenant=default
```

### Authentication

Admin endpoints can be protected with authentication. Configure in `.env`:

**Token-based authentication:**
```env
AUTH_ENABLED=true
AUTH_TYPE=token
AUTH_TOKEN=your-secret-token
```

**Basic authentication:**
```env
AUTH_ENABLED=true
AUTH_TYPE=basic
AUTH_USERNAME=admin
AUTH_PASSWORD=your-password
```

## Testing

The project uses Vitest with @nuxt/test-utils for testing:

```bash
npm test              # Run all tests
npm run test:ui       # Run tests with UI
npm run test:coverage # Run tests with coverage
```

## Deployment

### GitHub Pages

The project automatically deploys to GitHub Pages on push to the main branch using GitHub Actions.

**Setup:**
1. Go to your repository settings
2. Navigate to Pages
3. Set Source to "GitHub Actions"
4. Push to main branch to trigger deployment

The site will be available at: `https://<username>.github.io/<repository>/`

### Manual Deployment

Generate static files:
```bash
cd src
npm run generate
```

The output will be in `src/.output/public/`

## Internationalization

The application supports multiple languages using @nuxtjs/i18n:

- English (default)
- German

Language can be switched via URL prefix: `/de/` for German.

## Documentation

For more detailed information, see the documentation in the `docs/` directory:

- [Implementation Details](docs/IMPLEMENTATION.md) - Complete requirements and architecture
- [GitHub Pages Setup](docs/GITHUB_PAGES_SETUP.md) - Deployment guide
- [Project Summary](docs/PROJECT_SUMMARY.md) - Feature overview and statistics
- [Development Guide](src/docs/DEVELOPMENT.md) - Development workflow and best practices
- [API Documentation](src/docs/API.md) - API endpoints and usage

## License

MIT
