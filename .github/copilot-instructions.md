# Copilot Instructions for nuxt-cv

## Project Overview

This is a CV (Curriculum Vitae) application built with Nuxt 4, featuring internationalization, multi-tenant support, and GitHub Pages deployment.

## Technology Stack

- **Framework**: Nuxt 4 with Vue 3.5
- **UI**: Nuxt UI components
- **Internationalization**: @nuxtjs/i18n (English and German)
- **Testing**: Vitest with @nuxt/test-utils
- **Deployment**: GitHub Pages (static site generation)
- **Languages**: TypeScript, Vue

## Project Structure

The project has a specific structure:

```
nuxt-cv/
├── .github/workflows/     # CI/CD workflows
├── docs/                  # All documentation files (MD files go here)
├── src/                   # Nuxt 4 application
│   ├── app/
│   │   ├── components/   # Vue components
│   │   ├── pages/        # Application pages
│   │   └── server/       # Backend API
│   │       ├── api/      # API endpoints
│   │       └── utils/    # Server utilities (auth, tenants)
│   ├── docs/             # Technical documentation (API, DEVELOPMENT)
│   └── tests/            # Component and integration tests
```

## File Organization Rules

### Documentation Files

**All Markdown (.md) files should be placed in the `./docs` directory** at the repository root, with the following exceptions:

- `README.md` at repository root
- `src/README.md` for src-specific documentation
- Files in `src/docs/` for technical API and development documentation

When creating new documentation:
- General documentation → `./docs/`
- Technical/API documentation → `./src/docs/`
- Keep README files at their respective root levels

## Coding Standards

### TypeScript and Vue

1. **Use TypeScript**: All new code should be TypeScript
2. **Composition API**: Use `<script setup>` syntax for Vue components
3. **Component Structure**:
   ```vue
   <script setup lang="ts">
   // Imports and logic
   </script>

   <template>
     <!-- Template code -->
   </template>

   <i18n lang="json">
   {
     "en": { ... },
     "de": { ... }
   }
   </i18n>
   ```

### Internationalization (i18n)

- All user-facing text must be translatable
- Support both English and German
- Use component-level i18n blocks
- Access translations with `useI18n()` and `t()` function

### Server-Side Code

- Location: `src/app/server/`
- API endpoints: Use `defineEventHandler`
- Utilities: Place in `src/app/server/utils/`
- Multi-tenant: Configure in `src/app/server/utils/tenants.ts`
- Authentication: Implement in `src/app/server/utils/auth.ts`

### Testing

- Framework: Vitest with @nuxt/test-utils
- Test location: `src/tests/`
- Component tests: `src/tests/components/`
- Server tests: `src/tests/server/`
- Always write tests for new features
- Ensure Nuxt context is properly mocked for i18n components

## Development Workflow

### Working Directory

All development work is in the `src/` directory:

```bash
cd src
npm install
npm run dev
```

### Common Commands

- `npm run dev` - Start development server
- `npm run build` - Build for production
- `npm run generate` - Generate static site for GitHub Pages
- `npm test` - Run all tests
- `npm run test:ui` - Run tests with UI

### Configuration Files

- `src/nuxt.config.ts` - Nuxt configuration
- `src/vitest.config.ts` - Test configuration
- `src/.env` - Environment variables (not committed)
- `src/.env.example` - Environment template (committed)

### Git Workflow

#### Branch Naming Convention

All PR branches should follow this naming pattern:
- **Format**: `<type>/<scope>/<pr-number>-<description>`
- **Examples**:
  - `feature/copilot/69-added-feature-xyz`
  - `fix/ui/123-button-alignment`
  - `docs/readme/456-update-installation-guide`

The PR number should be included in the branch name to easily track the associated issue or pull request.

#### Commit Messages

Use **Conventional Commits** format for all commit messages following the specification at https://www.conventionalcommits.org/en/v1.0.0/

**Format**: `<type>[optional scope]: <description>`

**Common types**:
- `feat`: A new feature
- `fix`: A bug fix
- `docs`: Documentation only changes
- `style`: Changes that don't affect the meaning of the code (white-space, formatting, etc)
- `refactor`: A code change that neither fixes a bug nor adds a feature
- `test`: Adding missing tests or correcting existing tests
- `chore`: Changes to the build process or auxiliary tools

**Examples**:
- `feat(ui): add dark mode toggle`
- `fix(api): handle null tenant query parameter`
- `docs: update installation instructions`
- `test(components): add tests for LanguageSwitcher`

## Key Features and Guidelines

### Multi-tenant System

- Configure tenants in `src/app/server/utils/tenants.ts`
- Each tenant has its own CV data (experiences, studies)
- Access via query parameter: `?tenant=<id>`
- Default tenant is used when no tenant is specified

### Authentication

- Token-based or Basic Auth supported
- Protected endpoints in `src/app/server/api/admin/`
- Configuration via environment variables
- Always validate and sanitize input on server endpoints

### Static Site Generation

- SSR is disabled (`ssr: false`) for GitHub Pages compatibility
- Use `npm run generate` to create static output
- Output directory: `src/.output/public/`
- Base URL configurable via `NUXT_APP_BASE_URL` environment variable

### GitHub Actions

- CI workflow: Tests and builds on push/PR
- Deploy workflow: Automatically deploys to GitHub Pages on push to main
- Both workflows are in `.github/workflows/`

## Best Practices

1. **Use TypeScript** - All files should be TypeScript
2. **Component Composition** - Use Composition API with `<script setup>`
3. **i18n** - All user-facing text should be translatable
4. **Responsive Design** - Use Tailwind CSS classes for responsive layouts
5. **Server Security** - Always validate and sanitize input on server endpoints
6. **Testing** - Write tests for new features
7. **Git Workflow** - Follow branch naming conventions and use conventional commits format (see Git Workflow section)

## Adding New Features

### New Component

1. Create in `src/app/components/`
2. Use `<script setup lang="ts">`
3. Include i18n block with English and German translations
4. Add corresponding test in `src/tests/components/`

### New API Endpoint

1. Create in `src/app/server/api/`
2. Use `defineEventHandler`
3. Support multi-tenant via query parameters
4. Add tests in `src/tests/server/`
5. Update API documentation in `src/docs/API.md`

### New Page

1. Create in `src/app/pages/`
2. Use file-based routing
3. Include i18n support
4. Ensure responsive design

## Documentation Updates

When making significant changes:

- Update `src/docs/API.md` for API changes
- Update `src/docs/DEVELOPMENT.md` for development workflow changes
- Keep root `README.md` current with feature overview
- Place new general documentation in `./docs/`

## Common Pitfalls

- Don't forget to add i18n for all user-facing text
- Remember `ssr: false` is required for GitHub Pages
- Always test components with Nuxt context mocked
- Font warnings during build can be safely ignored
- Ensure all API endpoints support multi-tenant query parameter

## Environment Variables

See `src/.env.example` for all available configuration options:

- `NUXT_APP_BASE_URL` - Base URL for the application
- `AUTH_ENABLED` - Enable/disable authentication
- `AUTH_TYPE` - Authentication type (token or basic)
- `AUTH_TOKEN` - Token for token-based auth
- `AUTH_USERNAME` - Username for basic auth
- `AUTH_PASSWORD` - Password for basic auth
