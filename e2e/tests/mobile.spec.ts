import { devices } from '@playwright/test'
import { expect, test, waitForCv } from './fixtures'

test.use({ ...devices['Pixel 7'] })

// Phones: one column, the sidebar sections (details, languages, PDF button) follow the content.
test('CV on a phone', async ({ page }) => {
  await page.goto('/')
  await waitForCv(page, 'Max Mustermann')
  await expect(page.locator('#experiences-section')).toBeVisible()
  const mobileSections = page.locator('.mobile-sidebar-sections')
  await mobileSections.scrollIntoViewIfNeeded()
  await expect(mobileSections).toBeVisible()
  await expect(mobileSections.getByRole('button', { name: 'Download PDF' })).toBeVisible()
  await expect(page.getByRole('button', { name: 'Switch to Deutsch' })).toBeVisible()
})
