import { ADMIN_KEY, expect, SHARED_URL, test, unique } from './fixtures'

// Direct API checks of the deployed stack (health, versions, access rules).
test.describe('API', () => {
  test('health and version', async ({ request }) => {
    expect((await request.get('/api/health')).ok()).toBeTruthy()
    const version = await (await request.get('/api/version')).json()
    expect(version.api).toBeTruthy()
    // The PDF renderer is configured and reachable through the API.
    expect(version.pdf?.error).toBeUndefined()
  })

  test('shared host without invite has no access', async ({ playwright }) => {
    const shared = await playwright.request.newContext({ baseURL: SHARED_URL })
    const response = await shared.get('/api/cv')
    expect(response.status()).toBe(403)
    expect(await response.json()).toEqual({ error: 'no_access', host: 'shared' })
    await shared.dispose()
  })

  test('redeem + logout via the API', async ({ playwright, admin }) => {
    const invite = await admin.createInvite('bob', { profile: 'anonymous', label: unique('api') })
    const visitor = await playwright.request.newContext({ baseURL: SHARED_URL })
    expect((await visitor.post('/api/access/redeem', { data: { code: invite.code } })).status()).toBe(204)
    const cv = await visitor.get('/api/cv?locale=en')
    expect(cv.status()).toBe(200)
    expect((await cv.json()).access).toMatchObject({ tenant: 'bob', profile: 'anonymous', viaInvite: true })

    expect((await visitor.post('/api/access/logout')).status()).toBe(204)
    expect((await visitor.get('/api/cv')).status()).toBe(403)
    await visitor.dispose()
  })

  test('assets are only served when the visitor may see them', async ({ request }) => {
    // The public demo profile hides the photo, so the photo asset is not served either.
    expect((await request.get('/api/assets/profile-small.jpg')).status()).toBe(404)
  })

  test('admin API requires the key', async ({ request }) => {
    expect((await request.get('/api/admin/tenants')).status()).toBe(401)
    const tenants = await request.get('/api/admin/tenants', { headers: { 'X-Admin-Key': ADMIN_KEY } })
    expect(tenants.ok()).toBeTruthy()
    expect((await tenants.json()).map((t: { id: string }) => t.id)).toEqual(expect.arrayContaining(['bob', 'demo']))
  })
})
