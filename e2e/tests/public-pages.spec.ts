import { test, expect, SHARED_URL } from './fixtures'

// Public product pages: showcase CTA, pricing (REQUIREMENTS_SAAS.md §4) and legal pages
// (REQUIREMENTS_ACCESS_AND_TENANCY.md R11.1, R11.6).
test.describe('public pages', () => {
  test('showcase links sign-up, pricing and the legal pages', async ({ page }) => {
    await page.goto(`${SHARED_URL}/`)
    await expect(page.getByTestId('showcase-create')).toHaveAttribute('href', '/login')
    await expect(page.getByTestId('public-sign-in')).toHaveAttribute('href', '/login')
    await expect(page.getByTestId('footer-pricing')).toHaveAttribute('href', '/pricing')
    await expect(page.getByTestId('footer-imprint')).toHaveAttribute('href', '/legal/imprint')
  })

  test('pricing shows Free, Pro and the four Pro passes', async ({ page }) => {
    await page.goto('/pricing')
    await expect(page.getByRole('heading', { level: 1 })).toHaveText('Simple pricing')
    await expect(page.getByTestId('plan-free')).toBeVisible()
    await expect(page.getByTestId('plan-pro')).toBeVisible()
    await expect(page.getByTestId('pass-per-week')).toHaveCount(4)
    await expect(page.getByTestId('pass-total')).toHaveCount(4)
    await expect(page.getByTestId('pricing-prepaid')).toContainText('no subscription')
    // Buying happens in the dashboard after signing in.
    await expect(page.getByRole('link', { name: 'Get Pro' }).first()).toHaveAttribute('href', '/login')
  })

  test('imprint renders and the footer links all legal pages', async ({ page }) => {
    await page.goto('/legal/imprint')
    await expect(page.getByRole('heading', { level: 1 })).toHaveText('Imprint')
    await expect(page.getByTestId('footer-terms')).toHaveAttribute('href', '/legal/terms')
    await page.getByTestId('footer-privacy').click()
    await expect(page.getByRole('heading', { level: 1 })).toHaveText('Privacy policy')
  })
})
