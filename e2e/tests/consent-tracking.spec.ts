import { expect, test, unique, waitForCv } from './fixtures'

// Visitor tracking with consent (docs/VISITOR_SESSION_TRACKING.md): the bob tenant has a privacy controller
// and tracking enabled, so invitees see the consent modal first.
test.describe('consent and visitor tracking', () => {
  test('accepting consent starts tracking, the owner sees the visit', async ({ page, admin }) => {
    const label = unique('consent-accept')
    const invite = await admin.createInvite('bob', { profile: 'full', label })

    await page.goto(invite.link)
    const modal = page.getByTestId('consent-modal')
    await expect(modal).toBeVisible({ timeout: 15_000 })
    await expect(modal).toContainText('Bob Builder')

    const consent = page.waitForResponse(r => r.url().endsWith('/api/consent') && r.request().method() === 'POST')
    const events = page.waitForResponse(r => r.url().includes('/api/events') && r.request().method() === 'POST', { timeout: 30_000 })
    await page.getByTestId('consent-accept').click()
    expect((await consent).ok()).toBeTruthy()
    await expect(modal).toHaveCount(0)
    await waitForCv(page, 'Bob Builder')

    // Some reading activity; the tracker sends its batch every few seconds.
    await page.mouse.move(400, 400)
    await page.mouse.wheel(0, 1500)
    await page.mouse.move(600, 500)
    expect((await events).ok()).toBeTruthy()

    // Owner view: consent statistics and the visit's analytics group.
    const stats = await admin.http.get('api/admin/tenants/bob/analytics/consent')
    expect(stats.ok()).toBeTruthy()
    const groups = await admin.http.get('api/admin/tenants/bob/analytics/groups')
    expect(groups.ok()).toBeTruthy()
    expect(JSON.stringify(await groups.json())).toContain(label)
  })

  test('declining consent keeps the CV readable without tracking', async ({ page, admin }) => {
    const invite = await admin.createInvite('bob', { profile: 'full', label: unique('consent-decline') })

    const tracked: string[] = []
    page.on('request', (r) => { if (r.url().includes('/api/events')) tracked.push(r.url()) })

    await page.goto(invite.link)
    await expect(page.getByTestId('consent-modal')).toBeVisible({ timeout: 15_000 })
    await page.getByTestId('consent-decline').click()
    await expect(page.getByTestId('consent-modal')).toHaveCount(0)
    await waitForCv(page, 'Bob Builder')

    await page.mouse.wheel(0, 1500)
    await page.reload()
    await waitForCv(page, 'Bob Builder')
    // The choice is remembered: no modal again, and nothing was tracked.
    await expect(page.getByTestId('consent-modal')).toHaveCount(0)
    expect(tracked).toEqual([])

    // The footer "Privacy" link re-opens the modal.
    await page.getByTestId('privacy-link').click()
    await expect(page.getByTestId('consent-modal')).toBeVisible()
  })

  test('profile with tracking off shows no consent modal', async ({ page, admin }) => {
    const invite = await admin.createInvite('bob', { profile: 'anonymous', label: unique('anonymous') })
    const cvResponse = page.waitForResponse(r => r.url().includes('/api/cv?'))
    await page.goto(invite.link)
    const body = await (await cvResponse).json()
    await waitForCv(page, 'Bob Builder')
    expect(body.consent.required).toBe(false)
    await expect(page.getByTestId('consent-modal')).toHaveCount(0)
    // anonymous hides companies and contact details.
    expect(JSON.stringify(body.cv)).not.toContain('bob@example.com')
    expect(JSON.stringify(body.cv)).not.toContain('Bob Construction Ltd.')
  })
})
