import { createHash } from 'node:crypto'
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

  test('website links printed into the PDF are tracked per PDF', async ({ request, admin }) => {
    const site = `https://shop.example.com/${unique('demo')}`
    const original = await admin.readFile('demo', 'cv.en.json')
    try {
      const cv = JSON.parse(original)
      cv.projects[0].url = site
      await admin.writeFile('demo', 'cv.en.json', JSON.stringify(cv, null, 2))
      const invite = await admin.createInvite('demo', { profile: 'full', label: unique('pdf-links') })
      expect(invite.pdf?.every(p => p.ok)).toBeTruthy()

      // The PDF's QR invite: its code is printed into the PDF's tracked links (/api/go/<key>?c=<code>).
      const qrInvite = async () => (await (await admin.http.get('api/admin/tenants/demo/invites')).json())
        .find((i: { parentId?: string, source?: string }) => i.parentId === invite.id && i.source === 'pdf-qr')
      const { code } = await qrInvite()
      const key = createHash('sha256').update(site).digest('hex').slice(0, 32)

      // A click from the printed PDF: redirect without cookie, counted for this PDF, the code is not used up.
      const click = await request.get(`/api/go/${key}?c=${code}`, { maxRedirects: 0 })
      expect(click.status()).toBe(302)
      expect(click.headers().location).toBe(site)
      const after = await qrInvite()
      expect(after.useCount).toBe(0)
      expect(after.linkClicks).toEqual([expect.objectContaining({ url: site, count: 1 })])
    } finally {
      await admin.writeFile('demo', 'cv.en.json', original)
    }
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
