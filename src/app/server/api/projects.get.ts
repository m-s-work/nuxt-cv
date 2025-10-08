import { getTenant } from '~/server/utils/tenants'

export default defineEventHandler((event) => {
  const query = getQuery(event)
  const tenantId = query.tenant as string | undefined

  const tenant = getTenant(tenantId)
  
  return {
    projects: tenant.projects
  }
})
