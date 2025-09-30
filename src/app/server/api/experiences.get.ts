export default defineEventHandler((event) => {
  const query = getQuery(event)
  const tenantId = query.tenant as string | undefined

  const tenant = getTenant(tenantId)
  
  return {
    experiences: tenant.experiences
  }
})
