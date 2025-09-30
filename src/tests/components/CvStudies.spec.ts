import { describe, it, expect } from 'vitest'
import { getTenant } from '~/server/utils/tenants'

describe('Tenant System', () => {
  it('should return default tenant', () => {
    const tenant = getTenant()
    expect(tenant.id).toBe('default')
    expect(tenant.name).toBe('Default CV')
  })

  it('should have experiences', () => {
    const tenant = getTenant()
    expect(tenant.experiences).toBeDefined()
    expect(tenant.experiences.length).toBeGreaterThan(0)
  })

  it('should have studies', () => {
    const tenant = getTenant()
    expect(tenant.studies).toBeDefined()
    expect(tenant.studies.length).toBeGreaterThan(0)
  })
})

