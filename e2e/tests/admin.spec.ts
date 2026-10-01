import type { Page } from '@playwright/test'
import { ADMIN_KEY, expect, SHARED_URL, test, unique, waitForCv } from './fixtures'

// Owner tool (/admin): everything goes through the admin API with the key entered here.
async function signIn(page: Page, tenant?: string) {
  await page.goto('/admin')
  await page.getByPlaceholder('Admin key').fill(ADMIN_KEY)
  await page.getByRole('button', { name: 'Sign in' }).click()
  await expect(page.getByRole('button', { name: 'Log out' })).toBeVisible()
  if (tenant) await selectTenant(page, tenant)
}

async function selectTenant(page: Page, tenant: string) {
  await page.getByRole('combobox', { name: 'Tenant' }).click()
  await page.getByRole('option', { name: new RegExp(`\\(${tenant}\\)`) }).click()
  await expect(page.locator('main h2').first()).toBeVisible()
  await expect(page.locator('main code').first()).toHaveText(tenant)
}

test.describe('admin', () => {
  test('rejects a wrong key, signs in and out', async ({ page }) => {
    await page.goto('/admin')
    await page.getByPlaceholder('Admin key').fill('wrong-key')
    await page.getByRole('button', { name: 'Sign in' }).click()
    await expect(page.getByText('Admin key rejected.')).toBeVisible()

    await signIn(page, 'demo')
    await expect(page.getByRole('heading', { name: 'Max Mustermann (demo)' })).toBeVisible()
    await page.getByRole('button', { name: 'Log out' }).click()
    await expect(page.getByPlaceholder('Admin key')).toBeVisible()
  })

  test('creates an invite that opens the CV, then revokes it', async ({ page, browser }) => {
    await signIn(page, 'bob')
    const label = unique('admin-invite')

    await page.getByPlaceholder('ACME recruiting').fill(label)
    // "anonymous": no consent modal for the recipient.
    await page.getByRole('combobox', { name: 'Profile' }).click()
    await page.getByRole('option', { name: 'anonymous', exact: true }).click()
    await page.getByRole('button', { name: 'Create invite' }).click()
    const link = (await page.getByTestId('invite-link').textContent({ timeout: 60_000 }))!.trim()
    // On the shared host "/" is the showcase: invite links open /cv (since #105).
    expect(link).toContain(`${SHARED_URL}/cv?c=`)
    await expect(page.getByText(`Invite created for "${label}"`)).toBeVisible()

    // The recipient opens the link in their own browser.
    const visitor = await browser.newContext()
    const visitorPage = await visitor.newPage()
    await visitorPage.goto(link)
    await waitForCv(visitorPage, 'Bob Builder')

    // The redemption shows up in the list; revoke it.
    await page.getByRole('button', { name: 'Reload invites' }).click()
    const row = page.locator('tr', { hasText: label }).first()
    await expect(row).toBeVisible()
    page.once('dialog', dialog => dialog.accept())
    await row.getByRole('button', { name: 'Revoke' }).click()
    await expect(page.locator('tr', { hasText: label })).toHaveCount(0)

    // Revoking ends the recipient's access.
    await visitorPage.reload()
    await expect(visitorPage.getByPlaceholder('Invite code')).toBeVisible()
    await visitor.close()
  })

  test('edits a CV file and the change is live for visitors', async ({ page, admin }) => {
    const original = await admin.readFile('bob', 'cv.en.json')
    const title = unique('Chief Site Engineer')
    try {
      await signIn(page, 'bob')
      await page.getByRole('tab', { name: 'Files' }).click()
      await page.getByRole('button', { name: /cv\.en\.json/ }).click()
      const editor = page.getByRole('textbox', { name: 'File content' })
      await expect(editor).toHaveValue(/Site Engineer/)
      await editor.fill((await editor.inputValue()).replace('"Site Engineer"', `"${title}"`))
      await page.getByRole('button', { name: 'Save', exact: true }).click()
      await expect(page.getByRole('button', { name: 'Save', exact: true })).toBeDisabled()

      const invite = await admin.createInvite('bob', { profile: 'anonymous', label: unique('edit') })
      await page.goto(invite.link)
      await waitForCv(page, 'Bob Builder')
      await expect(page.getByText(title).filter({ visible: true }).first()).toBeVisible()
    } finally {
      await admin.writeFile('bob', 'cv.en.json', original)
    }
  })

  test('previews the redacted CV of a profile', async ({ page }) => {
    await signIn(page, 'demo')
    await page.getByRole('tab', { name: 'Edit' }).click()
    await page.getByRole('tab', { name: 'Data' }).click()

    await page.getByRole('combobox', { name: 'Preview profile' }).click()
    await page.getByRole('option', { name: 'public', exact: true }).click()
    await expect(page.getByTestId('preview-json')).toContainText('Max Mustermann')
    await expect(page.getByTestId('preview-json')).not.toContainText('max.mustermann@example.com')

    await page.getByRole('combobox', { name: 'Preview profile' }).click()
    await page.getByRole('option', { name: 'full', exact: true }).click()
    await expect(page.getByTestId('preview-json')).toContainText('max.mustermann@example.com')
  })

  test('edits the CV graphically with a live web preview, without saving', async ({ page, admin }) => {
    const before = await admin.readFile('bob', 'cv.en.json')
    const draftName = unique('Draft Bob')
    await signIn(page, 'bob')
    await page.getByRole('tab', { name: 'Edit' }).click()

    // The Web view is the CV page itself, as the chosen profile sees it.
    const frame = page.frameLocator('[data-testid=web-preview]')
    await expect(frame.locator('h1').first()).toContainText('Bob Builder')

    // Typing in the editor updates the preview (redacted by the API) before anything is saved.
    const profile = page.getByTestId('cv-block-profile')
    await profile.getByRole('textbox').first().fill(draftName)
    await expect(page.getByTestId('preview-draft-note')).toBeVisible()
    await expect(frame.locator('h1').first()).toContainText(draftName)

    page.once('dialog', dialog => dialog.accept())
    await page.getByRole('button', { name: 'Revert' }).click()
    await expect(frame.locator('h1').first()).toContainText('Bob Builder')
    expect(await admin.readFile('bob', 'cv.en.json')).toBe(before)
  })

  test('chooses the PDF template and the favicon of a tenant', async ({ page, admin }) => {
    // Pick values that differ from the stored ones (saving is only possible after a change; the stack may be reused).
    const before = await admin.readFile('bob', 'tenant.json')
    const symbol = before.includes('"lambda"') ? 'braces' : 'lambda'
    const template = /"pdf":\s*"classic"/.test(before) ? 'banner' : 'classic'

    await signIn(page, 'bob')
    await page.getByRole('tab', { name: 'Design' }).click()

    // Favicon picker: saved into tenant.json and served by /api/favicon.svg.
    await page.getByTestId(`favicon-symbol-${symbol}`).click()
    await page.getByTestId('save-favicon').click()
    await expect(page.getByTestId('favicon-message')).toContainText('Saved to tenant.json')
    expect(await admin.readFile('bob', 'tenant.json')).toContain(`"${symbol}"`)

    // PDF template for the whole tenant.
    await expect(page.getByTestId('template-builder')).toBeVisible()
    await page.getByTestId(`template-${template}`).click()
    await page.getByTestId('save-templates').click()
    await expect(page.getByTestId('templates-message')).toBeVisible()
    await expect.poll(async () => admin.readFile('bob', 'tenant.json')).toMatch(new RegExp(`"pdf":\\s*"${template}"`))

    // Visitors get the new template (and PDFs are rendered with it).
    const invite = await admin.createInvite('bob', { profile: 'anonymous', label: unique('template') })
    const cvResponse = page.waitForResponse(r => r.url().includes('/api/cv?'))
    await page.goto(invite.link)
    expect((await (await cvResponse).json()).templates.pdf).toBe(template)
  })

  test('shows the analytics of a tenant', async ({ page }) => {
    await signIn(page, 'bob')
    await page.getByRole('tab', { name: 'Analytics' }).click()
    await expect(page.getByTestId('tracking-settings')).toBeVisible()
    await expect(page.getByTestId('consent-stats')).toBeVisible()
  })
})
