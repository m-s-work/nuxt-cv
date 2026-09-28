// Internal PDF renderer. Only the API calls it (no public domain).
//
// POST /render  { url, cookies: [{ name, value }], timeoutMs? }  ->  application/pdf
// GET  /health
// GET  /version  { commit, builtAt } of this image
//
// The page signals readiness via window.__CV_READY__ ("ready" | "no-access" | "error"),
// set by the frontend when opened with ?print=1.
import http from 'node:http'
import fs from 'node:fs'
import { chromium } from 'playwright-core'

const PORT = Number(process.env.PORT || 3000)
const MAX_CONCURRENT = Number(process.env.MAX_CONCURRENT || 2)
const DEFAULT_TIMEOUT_MS = Number(process.env.RENDER_TIMEOUT_MS || 45000)

// Page size and margins come from the page's @page rule (A4, see src/app/app.vue); the print
// layout (src/app/components/CvPrint.vue) is typeset in pt/mm, so no scaling.
const PDF_OPTIONS = {
  format: 'A4',
  printBackground: true,
  preferCSSPageSize: true
}

const escapeHtml = text => String(text).replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c])

// Running footer in the bottom page margin: "<name> · Curriculum Vitae" left, page numbers right.
// Text comes from window.__CV_PDF_FOOTER__ (set by the page).
function footerTemplate(text) {
  return `<div style="width:100%;margin:0 15mm;display:flex;justify-content:space-between;
    font-family:system-ui,sans-serif;font-size:7pt;color:#9aa1ad;letter-spacing:0.02em">
    <span>${escapeHtml(text)}</span>
    <span><span class="pageNumber"></span>&thinsp;/&thinsp;<span class="totalPages"></span></span>
  </div>`
}

let browserPromise = null
function getBrowser() {
  if (!browserPromise) {
    browserPromise = chromium.launch({
      executablePath: process.env.CHROMIUM_PATH || undefined,
      args: ['--disable-dev-shm-usage']
    }).then((browser) => {
      browser.on('disconnected', () => { browserPromise = null })
      return browser
    }).catch((error) => {
      browserPromise = null
      throw error
    })
  }
  return browserPromise
}

let running = 0
const queue = []
async function withSlot(fn) {
  if (running >= MAX_CONCURRENT) await new Promise(resolve => queue.push(resolve))
  running++
  try {
    return await fn()
  } finally {
    running--
    queue.shift()?.()
  }
}

class RenderError extends Error {
  constructor(status, message) { super(message); this.status = status }
}

async function render({ url, cookies = [], timeoutMs = DEFAULT_TIMEOUT_MS }) {
  const target = new URL(url)
  if (!['http:', 'https:'].includes(target.protocol)) throw new RenderError(400, 'unsupported protocol')

  const browser = await getBrowser()
  const context = await browser.newContext({ viewport: { width: 1280, height: 1800 }, locale: 'en-US' })
  try {
    await context.addCookies(cookies.map(c => ({
      name: String(c.name),
      value: String(c.value),
      url: target.origin,
      httpOnly: true,
      secure: target.protocol === 'https:',
      sameSite: 'Lax'
    })))
    const page = await context.newPage()
    await page.emulateMedia({ media: 'print' })
    await page.goto(target.toString(), { waitUntil: 'networkidle', timeout: timeoutMs })
    await page.waitForFunction(() => window.__CV_READY__, null, { timeout: timeoutMs })
    const state = await page.evaluate(() => window.__CV_READY__)
    if (state !== 'ready') throw new RenderError(422, `page not renderable: ${state}`)
    await page.evaluate(() => document.fonts.ready)
    const footer = await page.evaluate(() => window.__CV_PDF_FOOTER__ || '')
    return await page.pdf({
      ...PDF_OPTIONS,
      displayHeaderFooter: Boolean(footer),
      headerTemplate: '<span></span>',
      footerTemplate: footerTemplate(footer)
    })
  } finally {
    await context.close()
  }
}

function readJson(req) {
  return new Promise((resolve, reject) => {
    let body = ''
    req.setEncoding('utf8')
    req.on('data', (chunk) => {
      body += chunk
      if (body.length > 64 * 1024) reject(new RenderError(413, 'body too large'))
    })
    req.on('end', () => {
      try { resolve(JSON.parse(body || '{}')) } catch { reject(new RenderError(400, 'invalid json')) }
    })
    req.on('error', reject)
  })
}

const server = http.createServer(async (req, res) => {
  try {
    if (req.method === 'GET' && req.url === '/version') {
      res.writeHead(200, { 'Content-Type': 'application/json' }).end(JSON.stringify({
        commit: process.env.SOURCE_COMMIT || 'unknown',
        builtAt: (() => { try { return fs.readFileSync(new URL('./BUILD_TIME', import.meta.url), 'utf8').trim() } catch { return null } })()
      }))
      return
    }
    if (req.method === 'GET' && req.url === '/health') {
      res.writeHead(200, { 'Content-Type': 'text/plain' }).end('ok')
      return
    }
    if (req.method === 'POST' && req.url === '/render') {
      const body = await readJson(req)
      if (!body.url) throw new RenderError(400, 'url required')
      const started = Date.now()
      const pdf = await withSlot(() => render(body))
      console.log(`rendered ${new URL(body.url).pathname} in ${Date.now() - started} ms (${pdf.length} bytes)`)
      res.writeHead(200, { 'Content-Type': 'application/pdf', 'Content-Length': pdf.length }).end(pdf)
      return
    }
    res.writeHead(404).end()
  } catch (error) {
    const status = error instanceof RenderError ? error.status : 502
    console.error('render failed:', error.message)
    if (!res.headersSent) res.writeHead(status, { 'Content-Type': 'application/json' })
    res.end(JSON.stringify({ error: error.message }))
  }
})

server.listen(PORT, () => console.log(`pdf renderer listening on :${PORT}`))

for (const signal of ['SIGINT', 'SIGTERM']) {
  process.on(signal, async () => {
    server.close()
    if (browserPromise) await (await browserPromise).close().catch(() => {})
    process.exit(0)
  })
}
