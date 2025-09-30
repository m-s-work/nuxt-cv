# Development Guide

## Project Structure

```
src/
├── app/                      # Nuxt application
│   ├── app.vue              # Root component
│   ├── components/          # Vue components
│   │   ├── CvExperiences.vue
│   │   └── CvStudies.vue
│   ├── pages/               # Page components (file-based routing)
│   │   └── index.vue
│   └── server/              # Backend API
│       ├── api/             # API endpoints
│       │   ├── admin/       # Protected admin endpoints
│       │   ├── experiences.get.ts
│       │   └── studies.get.ts
│       └── utils/           # Server utilities
│           ├── auth.ts      # Authentication logic
│           └── tenants.ts   # Multi-tenant configuration
├── docs/                    # Documentation
├── public/                  # Static assets
├── tests/                   # Test files
│   ├── components/         # Component tests
│   └── server/             # Server tests
├── nuxt.config.ts          # Nuxt configuration
├── package.json            # Dependencies
└── vitest.config.ts        # Test configuration
```

## Key Features

### 1. Nuxt 4 with SSG (Static Site Generation)

The application is configured to generate static HTML pages suitable for GitHub Pages deployment.

Configuration in `nuxt.config.ts`:
```typescript
export default defineNuxtConfig({
  ssr: false,  // Client-side rendering for static hosting
  app: {
    baseURL: process.env.NUXT_APP_BASE_URL || '/',
  }
})
```

### 2. Internationalization (i18n)

The app supports multiple languages using `@nuxtjs/i18n`:
- English (default)
- German

**Component-level translations:**
```vue
<template>
  <h1>{{ t('cv.title') }}</h1>
</template>

<i18n lang="json">
{
  "en": { "cv": { "title": "CV" } },
  "de": { "cv": { "title": "Lebenslauf" } }
}
</i18n>
```

### 3. Nuxt UI Components

The application uses `@nuxt/ui` for pre-built components:
- `UCard` - Card component
- `UBadge` - Badge component
- `UContainer` - Container component

### 4. Multi-Tenant Backend

The backend supports serving different CV data based on tenant ID.

**Example usage:**
```typescript
// In tenants.ts
export const tenants = {
  default: { /* default CV data */ },
  client_a: { /* client A specific data */ }
}

// API call
GET /api/experiences?tenant=client_a
```

### 5. Authentication System

Protected endpoints can be secured with:
- Token-based authentication
- Basic authentication

## Development Workflow

### 1. Setup

```bash
cd src
npm install
cp .env.example .env
```

### 2. Development Server

```bash
npm run dev
```

Visit: http://localhost:3000

### 3. Building for Production

```bash
npm run build        # Build SSR version
npm run generate     # Generate static site
```

### 4. Testing

```bash
npm test             # Run all tests
npm run test:ui      # Run tests with UI
```

### 5. Preview Production Build

```bash
npm run preview
```

## Adding New Features

### Adding a New Component

1. Create component in `app/components/`:
```vue
<!-- app/components/CvSkills.vue -->
<script setup lang="ts">
const { t } = useI18n()

const skills = ref([
  { name: 'JavaScript', level: 'Expert' }
])
</script>

<template>
  <section>
    <h2>{{ t('skills.title') }}</h2>
    <div v-for="skill in skills" :key="skill.name">
      {{ skill.name }}: {{ skill.level }}
    </div>
  </section>
</template>

<i18n lang="json">
{
  "en": { "skills": { "title": "Skills" } },
  "de": { "skills": { "title": "Fähigkeiten" } }
}
</i18n>
```

2. Use in page:
```vue
<template>
  <CvSkills />
</template>
```

### Adding a New API Endpoint

1. Create endpoint file:
```typescript
// app/server/api/skills.get.ts
export default defineEventHandler((event) => {
  const query = getQuery(event)
  const tenantId = query.tenant as string | undefined
  
  const tenant = getTenant(tenantId)
  
  return {
    skills: tenant.skills || []
  }
})
```

2. Add to tenant configuration:
```typescript
// app/server/utils/tenants.ts
export interface TenantConfig {
  // ... existing fields
  skills?: Skill[]
}
```

### Adding Tests

```typescript
// tests/components/CvSkills.spec.ts
import { describe, it, expect } from 'vitest'

describe('CvSkills', () => {
  it('should exist', () => {
    expect(true).toBe(true)
  })
})
```

## Best Practices

1. **Use TypeScript** - All files should be TypeScript
2. **Component Composition** - Use Composition API with `<script setup>`
3. **i18n** - All user-facing text should be translatable
4. **Responsive Design** - Use Tailwind CSS classes for responsive layouts
5. **Server Security** - Always validate and sanitize input on server endpoints
6. **Testing** - Write tests for new features
7. **Git Commits** - Use conventional commits format

## Troubleshooting

### Build Errors

If you encounter font-related warnings during build, they can be safely ignored. The build will still complete successfully.

### Test Failures

Ensure Nuxt context is properly mocked for component tests using i18n.

### Static Site Generation Issues

Make sure `ssr: false` is set in `nuxt.config.ts` for GitHub Pages deployment.
