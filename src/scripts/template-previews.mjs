// Regenerates the template preview images shown when hovering a template in the admin Design tab
// (public/templates/<name>.jpg). They show the fictional sample tenant only, never a real CV.
//
// Needs the local stack with the PDF renderer (see docs/TEMPLATES.md):
//   cd pdf && PORT=3100 CHROMIUM_PATH=… node server.mjs
//   cd api/CvApi && Pdf__RendererUrl=http://localhost:3100 Pdf__AppBaseUrl=http://localhost:3000 dotnet run
//   cd src && npm run dev
//   cd src && node scripts/template-previews.mjs
//
// Env: API (default http://localhost:5080), ADMIN_KEY (dev-admin-key), TENANT (demo), PROFILE (full),
// CHROMIUM_PATH (Playwright's Chromium if unset).
import fs from 'node:fs/promises'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { createRequire } from 'node:module'
import { chromium } from 'playwright-core'
import sharp from 'sharp'

const API = process.env.API || 'http://localhost:5080'
const ADMIN_KEY = process.env.ADMIN_KEY || 'dev-admin-key'
const TENANT = process.env.TENANT || 'demo'
const PROFILE = process.env.PROFILE || 'full'
const WIDTH = 600 // px of the stored image (A4 portrait)

const root = path.dirname(path.dirname(fileURLToPath(import.meta.url)))
const outDir = path.join(root, 'public', 'templates')
const registry = await fs.readFile(path.join(root, 'app', 'utils', 'printTemplates.ts'), 'utf8')
const names = [...registry.matchAll(/^ {2}([a-z0-9][a-z0-9-]*): \{$/gm)].map(m => m[1])
if (!names.length) throw new Error('no templates found in printTemplates.ts')

const require = createRequire(import.meta.url)
const pdfjsDir = path.dirname(require.resolve('pdfjs-dist/package.json'))
const pdfjs = await fs.readFile(path.join(pdfjsDir, 'legacy', 'build', 'pdf.min.mjs'), 'utf8')
const worker = await fs.readFile(path.join(pdfjsDir, 'legacy', 'build', 'pdf.worker.min.mjs'), 'utf8')

const browser = await chromium.launch({ executablePath: process.env.CHROMIUM_PATH || undefined })
const page = await browser.newPage()
// Serve pdf.js from memory so the page needs no network.
await page.route('http://pdfjs.local/**', route => route.fulfill({
  contentType: 'text/javascript',
  body: route.request().url().endsWith('worker.mjs') ? worker : pdfjs
}))
await page.route('http://pdfjs.local/', route => route.fulfill({ contentType: 'text/html', body: '<canvas></canvas>' }))
await page.goto('http://pdfjs.local/')

await fs.mkdir(outDir, { recursive: true })
for (const name of names) {
  const url = `${API}/api/admin/tenants/${TENANT}/pdf-preview?profile=${PROFILE}&template=${name}&locale=en`
  const res = await fetch(url, { headers: { 'X-Admin-Key': ADMIN_KEY } })
  if (!res.ok) throw new Error(`${name}: ${res.status} ${await res.text()}`)
  const pdf = Buffer.from(await res.arrayBuffer()).toString('base64')

  // Render the first page with pdf.js at 2x the stored width, then downscale for crisp text.
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
  }, { pdf, width: WIDTH })

  const file = path.join(outDir, `${name}.jpg`)
  await sharp(Buffer.from(png, 'base64')).flatten({ background: '#ffffff' }).resize({ width: WIDTH })
    .jpeg({ quality: 82, mozjpeg: true }).toFile(file)
  console.log(`${name}: ${path.relative(root, file)}`)
}
await browser.close()
