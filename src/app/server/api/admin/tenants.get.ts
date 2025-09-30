export default defineEventHandler((event) => {
  // This endpoint requires authentication
  requireAuth(event)

  return {
    tenants: Object.keys(tenants).map(id => ({
      id,
      name: tenants[id].name,
      domain: tenants[id].domain
    }))
  }
})
