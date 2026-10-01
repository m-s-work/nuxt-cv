// Regenerates the showcase screenshots (public/showcase/*.jpg, REQUIREMENTS_ACCESS_AND_TENANCY.md R11.2).
// They show the fictional sample tenant `demo` only (api/sample-data), never a real CV.
//
// Needs the local stack (see docs/DEPLOYMENT_COOLIFY.md "Showcase screenshots"):
//   cd pdf && PORT=3100 ALLOWED_ORIGIN=http://localhost:3000 CHROMIUM_PATH=… node server.mjs   (optional, for the PDFs)
//   cd api/CvApi && Pdf__RendererUrl=http://localhost:3100 Pdf__AppBaseUrl=http://localhost:3000 dotnet run
//   cd src && npm run dev
//   cd src && node scripts/showcase-screenshots.mjs
//
// Env: APP (default http://localhost:3000), API (http://localhost:5080), ADMIN_KEY (dev-admin-key), TENANT (demo),
// OUT (public/showcase), CHROMIUM_PATH (else PLAYWRIGHT_BROWSERS_PATH / Playwright's Chromium).
// Without the PDF renderer the PDF images are made from the print layout (?print=1) with Chromium's page.pdf().
import fs from 'node:fs/promises'
import { existsSync, readdirSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { createRequire } from 'node:module'
import { chromium } from 'playwright-core'
import sharp from 'sharp'

const APP = (process.env.APP || 'http://localhost:3000').replace(/\/$/, '')
const API = (process.env.API || 'http://localhost:5080').replace(/\/$/, '')
const ADMIN_KEY = process.env.ADMIN_KEY || 'dev-admin-key'
const TENANT = process.env.TENANT || 'demo'
const QUALITY = 82
const root = path.dirname(path.dirname(fileURLToPath(import.meta.url)))
const outDir = path.resolve(process.env.OUT || path.join(root, 'public', 'showcase'))

/** Chromium: CHROMIUM_PATH, else the newest chromium-* in PLAYWRIGHT_BROWSERS_PATH (or /opt/pw-browsers), else Playwright's default. */
function chromiumPath() {
  if (process.env.CHROMIUM_PATH) return process.env.CHROMIUM_PATH
  for (const dir of [process.env.PLAYWRIGHT_BROWSERS_PATH, '/opt/pw-browsers'].filter(Boolean)) {
    if (!existsSync(dir)) continue
    const builds = readdirSync(dir).filter(name => /^chromium-\d+$/.test(name)).sort().reverse()
    for (const build of builds) {
      const exe = path.join(dir, build, 'chrome-linux', 'chrome')
      if (existsSync(exe)) return exe
    }
  }
  return undefined
}

async function createInvite(profile, label, overrides = {}) {
  const res = await fetch(`${API}/api/admin/tenants/${TENANT}/invites`, {
    method: 'POST',
    headers: { 'X-Admin-Key': ADMIN_KEY, 'Content-Type': 'application/json' },
    // No consent modal on the screenshots: tracking off for these invites.
    body: JSON.stringify({ profile, label: `showcase screenshots: ${label}`, overrides: { ...overrides, tracking: { enabled: false } } })
  })
  if (!res.ok) throw new Error(`create invite (${profile}): ${res.status} ${await res.text()}`)
  const json = await res.json()
  return { id: json.invite.id, code: json.code }
}

async function revokeInvite(invite) {
  await fetch(`${API}/api/admin/tenants/${TENANT}/invites/${invite.id}`, { method: 'DELETE', headers: { 'X-Admin-Key': ADMIN_KEY } })
}

const jpeg = (input, file) => sharp(input).flatten({ background: '#ffffff' }).jpeg({ quality: QUALITY, mozjpeg: true }).toFile(path.join(outDir, file))

const browser = await chromium.launch({ executablePath: chromiumPath() })
const invites = []

/** Opens the CV with an invite in a fresh browser context; waits until it is rendered and the splash is gone. */
async function openCv(invite, { locale = 'en', width = 1440, height = 900, dark = false, mobile = false, query = '' } = {}) {
  const context = await browser.newContext({
    viewport: { width, height },
    deviceScaleFactor: 1,
    colorScheme: dark ? 'dark' : 'light',
    locale: locale === 'de' ? 'de-DE' : 'en-US',
    isMobile: mobile,
    hasTouch: mobile
  })
  const page = await context.newPage()
  const prefix = locale === 'de' ? '/de' : ''
  await page.goto(`${APP}${prefix}/cv?c=${encodeURIComponent(invite.code)}${query}`)
  await page.locator('h1.hero-title').waitFor({ timeout: 30_000 })
  await page.locator('.splash-wrapper').waitFor({ state: 'detached', timeout: 15_000 })
  // The consent modal must never be on a screenshot (tracking is off for these invites; decline just in case).
  const decline = page.getByRole('dialog').getByRole('button', { name: /decline|ablehnen/i })
  if (await decline.count()) await decline.first().click()
  // `npm run dev` adds the Nuxt DevTools button; never on a screenshot.
  await page.addStyleTag({ content: '#nuxt-devtools-container, #nuxt-devtools-anchor { display: none !important; }' })
  await page.evaluate(() => document.fonts.ready)
  await page.waitForTimeout(500)
  return { context, page }
}

/** Scrolls the main content (sidebar + timeline + entries), or a section of it, to the top of the viewport. */
async function scrollToContent(page, selector = '.cv-container') {
  await page.evaluate((sel) => {
    const el = document.querySelector(sel)
    window.scrollTo({ top: el ? el.getBoundingClientRect().top + window.scrollY : window.innerHeight, behavior: 'instant' })
  }, selector)
  await page.waitForTimeout(1200) // sidebar profile fades in on scroll
}

/** First page of a PDF as PNG, rendered with pdf.js in the browser (like scripts/template-previews.mjs). */
async function pdfFirstPage(pdfBuffer, width) {
  const require = createRequire(import.meta.url)
  const pdfjsDir = path.dirname(require.resolve('pdfjs-dist/package.json'))
  const pdfjs = await fs.readFile(path.join(pdfjsDir, 'legacy', 'build', 'pdf.min.mjs'), 'utf8')
  const worker = await fs.readFile(path.join(pdfjsDir, 'legacy', 'build', 'pdf.worker.min.mjs'), 'utf8')
  const page = await browser.newPage()
  await page.route('http://pdfjs.local/**', route => route.fulfill({
    contentType: 'text/javascript',
    body: route.request().url().endsWith('worker.mjs') ? worker : pdfjs
  }))
  await page.route('http://pdfjs.local/', route => route.fulfill({ contentType: 'text/html', body: '<canvas></canvas>' }))
  await page.goto('http://pdfjs.local/')
  const png = await page.evaluate(async ({ pdf, width }) => {
    const pdfjsLib = await import('http://pdfjs.local/pdf.mjs')
    pdfjsLib.GlobalWorkerOptions.workerSrc = 'http://pdfjs.local/pdf.worker.mjs'
    const data = Uint8Array.from(atob(pdf), c => c.charCodeAt(0))
    const doc = await pdfjsLib.getDocument({ data }).promise
    const first = await doc.getPage(1)
    const viewport = first.getViewport({ scale: (width * 2) / first.getViewport({ scale: 1 }).width })
    const canvas = document.querySelector('canvas')
    canvas.width = viewport.width
    canvas.height = viewport.height
    await first.render({ canvas, viewport }).promise
    return canvas.toDataURL('image/png').split(',')[1]
  }, { pdf: pdfBuffer.toString('base64'), width })
  await page.close()
  return sharp(Buffer.from(png, 'base64')).resize({ width }).png().toBuffer()
}

/** The visitor's PDF (GET /api/pdf); without renderer the print layout via page.pdf(). */
async function pdfImage(invite, locale, file) {
  const { context, page } = await openCv(invite, { locale, width: 1240, height: 1754 })
  let pdf
  const res = await context.request.get(`${APP}/api/pdf?locale=${locale}`, { timeout: 120_000 })
  if (res.ok() && (res.headers()['content-type'] || '').includes('pdf')) {
    pdf = await res.body()
    console.log(`${file}: PDF from the renderer`)
  } else {
    console.warn(`${file}: no PDF from the API (${res.status()}), printing ?print=1 in the browser instead`)
    const prefix = locale === 'de' ? '/de' : ''
    await page.goto(`${APP}${prefix}/cv?print=1`)
    await page.waitForFunction(() => window.__CV_READY__ === 'ready', null, { timeout: 30_000 })
    await page.evaluate(() => document.fonts.ready)
    await page.emulateMedia({ media: 'print' })
    pdf = await page.pdf({ format: 'A4', printBackground: true, preferCSSPageSize: true })
  }
  await jpeg(await pdfFirstPage(pdf, 827), file)
  await context.close()
}

/** Phone screenshots composed into one 1440x900 image (three phones on a light background). */
async function mobileComposite(invite) {
  const { context, page } = await openCv(invite, { width: 390, height: 844, mobile: true })
  const shots = []
  shots.push(await page.screenshot())
  for (const selector of ['#experiences-section', '.mobile-sidebar-sections']) {
    await page.evaluate(sel => {
      const el = document.querySelector(sel)
      if (el) window.scrollTo({ top: el.getBoundingClientRect().top + window.scrollY - 56, behavior: 'instant' })
    }, selector)
    await page.waitForTimeout(800)
    shots.push(await page.screenshot())
  }
  await context.close()

  const phones = shots.map((png, i) => `
    <div class="phone" style="margin-top:${i === 1 ? 0 : 48}px"><div class="notch"></div>
      <img src="data:image/png;base64,${png.toString('base64')}"></div>`).join('')
  const html = `<!doctype html><html><head><style>
    html,body{margin:0;width:1440px;height:900px;overflow:hidden}
    body{background:linear-gradient(135deg,#f8fafc 0%,#eef2ff 100%);display:flex;justify-content:center;align-items:flex-start;gap:58px;padding-top:46px;box-sizing:border-box}
    .phone{position:relative;width:340px;height:736px;padding:12px;border-radius:52px;background:#111827;box-shadow:0 30px 60px rgba(15,23,42,.25)}
    .phone img{display:block;width:340px;height:736px;border-radius:40px;object-fit:cover;object-position:top}
    .notch{position:absolute;top:22px;left:50%;transform:translateX(-50%);width:96px;height:26px;border-radius:14px;background:#111827;z-index:2}
  </style></head><body>${phones}</body></html>`
  const frame = await browser.newPage({ viewport: { width: 1440, height: 900 } })
  await frame.setContent(html)
  await frame.waitForTimeout(200)
  await jpeg(await frame.screenshot(), 'cv-mobile.jpg')
  await frame.close()
}

/** The experiences section (full vs. most redacted profile), as on the existing compare-*.jpg. */
async function compare(invite, file) {
  const { context, page } = await openCv(invite)
  // The fixed language switcher would float over the cut-out section.
  await page.addStyleTag({ content: '.language-selector { display: none !important; }' })
  const section = page.locator('#experiences-section')
  await section.scrollIntoViewIfNeeded()
  await page.waitForTimeout(600)
  const png = await section.screenshot()
  const meta = await sharp(png).metadata()
  const image = meta.height > 1070 ? sharp(png).extract({ left: 0, top: 0, width: meta.width, height: 1070 }) : sharp(png)
  await jpeg(await image.png().toBuffer(), file)
  await context.close()
}

try {
  await fs.mkdir(outDir, { recursive: true })
  const full = await createInvite('full', 'full')
  const recruiter = await createInvite('recruiter', 'recruiter', { flags: { hideCompanies: true } })
  const redacted = await createInvite('public', 'anonymised')
  invites.push(full, recruiter, redacted)

  for (const [file, opts] of [
    ['cv-desktop.jpg', {}],
    // German version at the projects (screenshots and logos)
    ['cv-german.jpg', { locale: 'de', section: '#projects-section' }],
    ['cv-dark.jpg', { dark: true }]
  ]) {
    const { context, page } = await openCv(full, opts)
    await scrollToContent(page, opts.section)
    await jpeg(await page.screenshot(), file)
    await context.close()
    console.log(file)
  }

  await mobileComposite(full)
  console.log('cv-mobile.jpg')
  await compare(full, 'compare-full.jpg')
  await compare(redacted, 'compare-public.jpg')
  console.log('compare-full.jpg, compare-public.jpg')
  await pdfImage(recruiter, 'en', 'pdf-recruiter-en.jpg')
  await pdfImage(full, 'de', 'pdf-full-de.jpg')
} finally {
  for (const invite of invites) await revokeInvite(invite)
  await browser.close()
}
