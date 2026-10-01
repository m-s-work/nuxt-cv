import { expect, test, waitForCv } from './fixtures'

// Demo tenant on its own host ("localhost"): the public profile is shown without an invite.
test.describe('public CV on the tenant host', () => {
  test('shows the redacted public CV', async ({ page }) => {
    const cvResponse = page.waitForResponse(r => r.url().includes('/api/cv?') && r.request().method() === 'GET')
    await page.goto('/')
    const body = await (await cvResponse).json()

    await waitForCv(page, 'Max Mustermann')
    await expect(page.locator('#skills-section')).toBeVisible()
    await expect(page.locator('#experiences-section')).toBeVisible()
    await expect(page.locator('#studies-section')).toBeVisible()

    // Redaction happens on the server: hidden data never reaches the browser.
    expect(body.access).toMatchObject({ tenant: 'demo', profile: 'public', viaInvite: false })
    expect(body.cv.details.email).toBeUndefined()
    expect(body.cv.details.phone).toBeUndefined()
    expect(body.cv.profile.photoUrl).toBeUndefined()
    expect(body.cv.projects).toBeUndefined()
    expect(JSON.stringify(body)).not.toContain('max.mustermann@example.com')
    await expect(page.locator('#projects-section')).toHaveCount(0)
    await expect(page.getByText('max.mustermann@example.com')).toHaveCount(0)

    // Public profile of this tenant: no tracking, so no consent modal.
    await expect(page.getByTestId('consent-modal')).toHaveCount(0)
  })

  test('switches the language', async ({ page }) => {
    await page.goto('/')
    await waitForCv(page, 'Max Mustermann')
    const german = page.waitForResponse(r => r.url().includes('/api/cv?locale=de'))
    await page.getByRole('button', { name: 'Switch to Deutsch' }).click()
    expect((await german).ok()).toBeTruthy()
    await expect(page).toHaveURL(/\/de\/?$/)
    await expect(page.getByRole('button', { name: 'Switch to Deutsch' })).toHaveAttribute('aria-current', 'true')

    await page.getByRole('button', { name: 'Switch to English' }).click()
    await expect(page).toHaveURL(/:\d+\/?$/)
    await expect(page.getByRole('button', { name: 'Switch to English' })).toHaveAttribute('aria-current', 'true')
  })

  test('opens the German CV directly', async ({ page }) => {
    const cvResponse = page.waitForResponse(r => r.url().includes('/api/cv?'))
    await page.goto('/de')
    const body = await (await cvResponse).json()
    expect(body.locale).toBe('de')
    await waitForCv(page, 'Max Mustermann')
  })

  test('serves the tenant favicon', async ({ request }) => {
    const response = await request.get('/api/favicon.svg')
    expect(response.ok()).toBeTruthy()
    expect(response.headers()['content-type']).toContain('image/svg+xml')
    expect(await response.text()).toContain('<svg')
  })
})
