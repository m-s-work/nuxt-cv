# API Documentation

## Endpoints

### Public Endpoints

#### GET /api/experiences
Get all experiences for a tenant.

**Query Parameters:**
- `tenant` (optional): Tenant ID. Defaults to "default"

**Response:**
```json
{
  "experiences": [
    {
      "id": 1,
      "company": "Tech Company Inc.",
      "position": "Senior Software Architect",
      "period": "2020 - Present",
      "description": "Leading architecture design...",
      "technologies": ["Nuxt", "Vue.js", "Node.js"]
    }
  ]
}
```

#### GET /api/studies
Get all studies/education for a tenant.

**Query Parameters:**
- `tenant` (optional): Tenant ID. Defaults to "default"

**Response:**
```json
{
  "studies": [
    {
      "id": 1,
      "institution": "Technical University",
      "degree": "Master of Science in Computer Science",
      "period": "2015 - 2017",
      "focus": "Software Engineering & Distributed Systems"
    }
  ]
}
```

### Protected Endpoints

#### GET /api/admin/tenants
List all available tenants. **Requires authentication.**

**Authentication:**
- Header: `Authorization: Bearer <token>` (if AUTH_TYPE=token)
- Header: `Authorization: Basic <base64_credentials>` (if AUTH_TYPE=basic)

**Response:**
```json
{
  "tenants": [
    {
      "id": "default",
      "name": "Default CV",
      "domain": null
    }
  ]
}
```

## Multi-Tenant Configuration

Tenants are configured in `src/app/server/utils/tenants.ts`.

### Adding a New Tenant

```typescript
export const tenants: Record<string, TenantConfig> = {
  default: { /* ... */ },
  company_a: {
    id: 'company_a',
    name: 'Company A CV',
    domain: 'company-a.example.com',
    experiences: [
      // Company A specific experiences
    ],
    studies: [
      // Same or different studies
    ]
  }
}
```

### Using Tenants

Frontend:
```javascript
// Fetch experiences for a specific tenant
const { data } = await useFetch('/api/experiences', {
  query: { tenant: 'company_a' }
})
```

## Authentication

Authentication can be enabled via environment variables:

### Token-based Authentication

```env
AUTH_ENABLED=true
AUTH_TYPE=token
AUTH_TOKEN=your-secret-token
```

### Basic Authentication

```env
AUTH_ENABLED=true
AUTH_TYPE=basic
AUTH_USERNAME=admin
AUTH_PASSWORD=secure-password
```

### Testing Protected Endpoints

```bash
# Token auth
curl -H "Authorization: Bearer your-secret-token" \
  http://localhost:3000/api/admin/tenants

# Basic auth
curl -u admin:secure-password \
  http://localhost:3000/api/admin/tenants
```
