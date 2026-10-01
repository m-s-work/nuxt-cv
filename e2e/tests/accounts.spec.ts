import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import type { Page } from '@playwright/test'
import { ADMIN_KEY, expect, SHARED_URL, test, unique } from './fixtures'

// Accounts (docs/REQUIREMENTS_SAAS.md): sign-in page, magic-link sign-in with onboarding, super-admin Users tab.
// The local stack writes magic links to e2e/.stack/api.log (Email__LogLinks); against an external stack the
// sign-in part is skipped.
const API_LOG = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../.stack/api.log')

/** The newest sign-in link the API logged for this address. */
async function magicLinkFor(email: string): Promise<string> {
  let link = ''
  await expect.poll(() => {
    const log = fs.readFileSync(API_LOG, 'utf8')
    const at = log.lastIndexOf(`to ${email}`)
    const match = at >= 0 ? /https?:\/\/\S+\/auth\/magic\?token=[\w-]+/.exec(log.slice(at)) : null
    link = match?.[0] ?? ''
    return link
  }, { timeout: 15_000 }).not.toBe('')
  return link
}

async function signInWithKey(page: Page) {
  await page.goto(`${SHARED_URL}/admin`)
  await page.getByPlaceholder('Admin key').fill(ADMIN_KEY)
  await page.getByRole('button', { name: 'Sign in with admin key' }).click()
  await expect(page.getByRole('button', { name: 'Log out' })).toBeVisible()
}

test.describe('accounts', () => {
  test('the sign-in page offers the e-mail link and shows errors, in English and German', async ({ page }) => {
    await page.goto(`${SHARED_URL}/login?error=link_invalid`)
    await expect(page.getByRole('heading', { name: 'Sign in' })).toBeVisible()
    await expect(page.getByTestId('login-error')).toContainText('invalid, expired or was already used')
    await expect(page.getByLabel('E-mail address')).toBeVisible()
    await expect(page.getByRole('button', { name: 'Send me a sign-in link' })).toBeVisible()
    await expect(page.getByRole('link', { name: 'terms of service' })).toHaveAttribute('href', '/legal/terms')
    await expect(page.locator('meta[name="robots"]')).toHaveAttribute('content', /noindex/)

    await page.goto(`${SHARED_URL}/de/login`)
    await expect(page.getByRole('heading', { name: 'Anmelden' })).toBeVisible()
    await expect(page.getByRole('button', { name: 'Anmeldelink senden' })).toBeVisible()
  })

  test('signs up with a magic link, creates the CV, and the super-admin finds the user', async ({ page, browser }) => {
    test.skip(!fs.existsSync(API_LOG), 'needs the local e2e stack (magic links in e2e/.stack/api.log)')
    const email = `${unique('user')}@example.com`
    const handle = unique('e2e').toLowerCase().slice(0, 31).replace(/-+$/, '')

    // Sign in with an e-mail link.
    await page.goto(`${SHARED_URL}/login`)
    await page.getByLabel('E-mail address').fill(email)
    await page.getByRole('button', { name: 'Send me a sign-in link' }).click()
    await expect(page.getByTestId('magic-link-sent')).toBeVisible()
    await page.goto(await magicLinkFor(email))

    // Onboarding: handle, CV, starting point.
    await expect(page.getByTestId('onboarding')).toBeVisible()
    await page.getByPlaceholder('jane-doe').fill(handle)
    await expect(page.getByTestId('handle-available')).toBeVisible()
    await page.getByRole('button', { name: 'Create my CV' }).click()
    await expect(page.getByRole('heading', { name: 'How do you want to start?' })).toBeVisible()
    await page.getByTestId('start-starter').click()
    await page.getByRole('button', { name: 'Go to my dashboard' }).click()

    // The dashboard of the new user: own tenant only, Account tab with the Free plan.
    await expect(page.locator('main code').first()).toHaveText(handle)
    await expect(page.getByRole('combobox', { name: 'Tenant' })).toHaveCount(0)
    await page.getByRole('tab', { name: 'Account' }).click()
    await expect(page.getByTestId('plan-card')).toContainText('Free plan')
    await expect(page.getByTestId('usage-invites')).toHaveText(/0 \/ 3/)

    // The super-admin sees the user and grants Pro.
    const admin = await (await browser.newContext()).newPage()
    await signInWithKey(admin)
    await admin.getByRole('tab', { name: 'Users' }).click()
    await expect(admin.getByTestId('platform-stats')).toBeVisible()
    await admin.getByLabel('Search users').fill(email)
    const row = admin.locator('tr', { hasText: email })
    await expect(row).toBeVisible()
    await expect(row).toContainText(handle)
    await row.click()
    await admin.getByRole('button', { name: '+30 days' }).click()
    await expect(row.getByText('Pro', { exact: true })).toBeVisible()

    // …which the user sees after a reload.
    await page.reload()
    await page.getByRole('tab', { name: 'Account' }).click()
    await expect(page.getByTestId('plan-card')).toContainText('Pro plan')
  })
})
