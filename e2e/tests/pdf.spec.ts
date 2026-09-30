import { expect, test, unique, waitForCv } from './fixtures'

// PDF of exactly the visitor's view, rendered by the pdf service (headless Chromium) via the API.
test.describe('PDF', () => {
  test('visitor downloads the PDF of the CV', async ({ page, admin }) => {
    const invite = await admin.createInvite('demo', { profile: 'recruiter', label: unique('pdf') })
    // Creating an invite renders its PDFs right away (one per CV locale).
    expect(invite.pdf?.map(p => p.locale).sort()).toEqual(['de', 'en'])
    expect(invite.pdf?.every(p => p.ok)).toBeTruthy()

    await page.goto(`/?c=${invite.code}`)
    await waitForCv(page, 'Max Mustermann')

    const download = page.waitForEvent('download', { timeout: 90_000 })
    await page.getByRole('button', { name: 'Download PDF' }).first().click()
    const file = await download
    expect(file.suggestedFilename()).toMatch(/\.pdf$/)
    const content = await file.createReadStream().then(async (stream) => {
      const chunks: Buffer[] = []
      for await (const chunk of stream) chunks.push(chunk as Buffer)
      return Buffer.concat(chunks)
    })
    expect(content.subarray(0, 5).toString()).toBe('%PDF-')
    expect(content.length).toBeGreaterThan(10_000)
  })

  test('public visitor gets the PDF from the API, then from the cache', async ({ request }) => {
    const first = await request.get('/api/pdf?locale=en', { timeout: 90_000 })
    expect(first.status()).toBe(200)
    expect(first.headers()['content-type']).toBe('application/pdf')
    expect((await first.body()).subarray(0, 5).toString()).toBe('%PDF-')

    const second = await request.get('/api/pdf?locale=en')
    expect(second.headers()['x-pdf-cache']).toBe('hit')
  })

  test('print view signals readiness to the renderer', async ({ page }) => {
    await page.goto('/?print=1')
    await page.waitForFunction(() => (window as unknown as { __CV_READY__?: string }).__CV_READY__, null, { timeout: 30_000 })
    expect(await page.evaluate(() => (window as unknown as { __CV_READY__?: string }).__CV_READY__)).toBe('ready')
  })
})
