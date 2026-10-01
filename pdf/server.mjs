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
// Only pages of the app are rendered (the API passes Pdf__AppBaseUrl, http://web in compose):
// render URLs with any other origin are rejected, so the renderer cannot be used to fetch other sites.
const ALLOWED_ORIGIN = new URL(process.env.ALLOWED_ORIGIN || 'http://web').origin
const MAX_BODY_BYTES = 64 * 1024

// Page size and margins come from the page's @page rule (A4, see src/app/app.vue); the print
// layout (src/app/components/CvPrint.vue) is typeset in pt/mm, so no scaling.
const PDF_OPTIONS = {
  format: 'A4',
  printBackground: true,
  preferCSSPageSize: true
}

const escapeHtml = text => String(text).replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c])

// Running footer in the bottom page margin: "<name> · Curriculum Vitae" left, page numbers right.
// Text comes from window.__CV_PDF_FOOTER__ (set by the page). A template with a full-height sidebar sets
// window.__CV_PDF_FOOTER_SIDE__ = { width, height, background, color } (height = the bottom page margin) so the sidebar continues through the
// footer (the margin area is not part of the page content) and the name sits on the sidebar colour.
const CSS_LENGTH = /^\d+(\.\d+)?(mm|pt|px|cm|in)$/
const CSS_COLOR = /^(#[0-9a-fA-F]{3,8}|[a-z]+)$/

function footerTemplate(text, side) {
  const width = side && CSS_LENGTH.test(side.width) ? side.width : null
  const height = side && CSS_LENGTH.test(side.height) ? side.height : '14mm'
  const background = width && CSS_COLOR.test(side.background) ? side.background : null
  const color = background && CSS_COLOR.test(side.color) ? side.color : null
  const base = 'font-family:system-ui,sans-serif;font-size:7pt;letter-spacing:0.02em;color:#9aa1ad'
  const pages = '<span class="pageNumber"></span>&thinsp;/&thinsp;<span class="totalPages"></span>'
  if (!background) {
    return `<div style="width:100%;margin:0 15mm;display:flex;justify-content:space-between;${base}">
    <span>${escapeHtml(text)}</span>
    <span>${pages}</span>
  </div>`
  }
  return `<style>html,body{margin:0;padding:0;-webkit-print-color-adjust:exact;print-color-adjust:exact}</style>
  <div style="position:absolute;left:0;right:0;bottom:0;height:${height};display:flex;align-items:center;${base}">
    <div style="position:absolute;top:0;bottom:0;left:0;width:${width};background:${background}"></div>
    <span style="position:relative;width:${width};box-sizing:border-box;padding:0 6mm 0 9mm;color:${color ?? '#e8edf6'};opacity:0.75;
      white-space:nowrap;overflow:hidden;text-overflow:ellipsis">${escapeHtml(text)}</span>
    <span style="position:relative;margin-left:auto;padding-right:14mm">${pages}</span>
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
  let target
  try { target = new URL(url) } catch { throw new RenderError(400, 'invalid url') }
  if (!['http:', 'https:'].includes(target.protocol)) throw new RenderError(400, 'unsupported protocol')
  if (target.origin !== ALLOWED_ORIGIN) throw new RenderError(400, 'origin not allowed')

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
    const footerSide = await page.evaluate(() => window.__CV_PDF_FOOTER_SIDE__ || null)
    return await page.pdf({
      ...PDF_OPTIONS,
      displayHeaderFooter: Boolean(footer),
      headerTemplate: '<span></span>',
      footerTemplate: footerTemplate(footer, footerSide)
    })
  } finally {
    await context.close()
  }
}

function readJson(req) {
  return new Promise((resolve, reject) => {
    let body = ''
    let tooLarge = false
    req.setEncoding('utf8')
    req.on('data', (chunk) => {
      if (tooLarge) return
      body += chunk
      if (Buffer.byteLength(body) > MAX_BODY_BYTES) {
        // Stop reading: drop the buffered body and close the connection.
        tooLarge = true
        body = ''
        reject(new RenderError(413, 'body too large'))
        req.destroy()
      }
    })
    req.on('end', () => {
      if (tooLarge) return
      try { resolve(JSON.parse(body || '{}')) } catch { reject(new RenderError(400, 'invalid json')) }
    })
    req.on('error', reject)
  })
}

const server = http.createServer(async (req, res) => {
  try {
    if (req.method === 'GET' && req.url === '/version') {
      res.writeHead(200, { 'Content-Type': 'application/json' }).end(JSON.stringify({
        // Baked into the image at build time (the runtime env may be overridden by Coolify).
        commit: (() => { try { const c = fs.readFileSync(new URL('./SOURCE_COMMIT', import.meta.url), 'utf8').trim(); if (c && c !== 'unknown') return c } catch {} return process.env.SOURCE_COMMIT || 'unknown' })(),
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
