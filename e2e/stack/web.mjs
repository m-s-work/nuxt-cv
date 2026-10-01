// Minimal stand-in for the `web` container (src/nginx.conf.template): serves the generated SPA and
// proxies /api/ to the API with the original Host header (the host decides the tenant).
import http from 'node:http'
import fs from 'node:fs'
import path from 'node:path'

const PORT = Number(process.env.PORT || 4173)
const PUBLIC_DIR = path.resolve(process.env.PUBLIC_DIR || 'src/.output/public')
const API = new URL(process.env.API_UPSTREAM || 'http://127.0.0.1:5181')

const TYPES = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.mjs': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.json': 'application/json',
  '.svg': 'image/svg+xml',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.jpeg': 'image/jpeg',
  '.webp': 'image/webp',
  '.ico': 'image/x-icon',
  '.woff': 'font/woff',
  '.woff2': 'font/woff2',
  '.txt': 'text/plain; charset=utf-8'
}

const SECURITY_HEADERS = {
  'Referrer-Policy': 'no-referrer',
  'X-Content-Type-Options': 'nosniff',
  // Like nginx: same-origin frames only (admin web preview, heatmap).
  'X-Frame-Options': 'SAMEORIGIN',
  'X-Robots-Tag': 'noindex, nofollow'
}

function proxy(req, res) {
  const upstream = http.request({
    hostname: API.hostname,
    port: API.port,
    method: req.method,
    path: req.url,
    headers: req.headers
  }, (response) => {
    res.writeHead(response.statusCode ?? 502, { ...response.headers, ...SECURITY_HEADERS })
    response.pipe(res)
  })
  upstream.on('error', () => {
    res.writeHead(502, SECURITY_HEADERS)
    res.end('bad gateway')
  })
  req.pipe(upstream)
}

/** try_files $uri $uri/ /index.html */
function resolveFile(urlPath) {
  const decoded = decodeURIComponent(urlPath)
  const candidate = path.join(PUBLIC_DIR, path.normalize(decoded))
  if (!candidate.startsWith(PUBLIC_DIR)) return null
  for (const file of [candidate, path.join(candidate, 'index.html')]) {
    if (fs.existsSync(file) && fs.statSync(file).isFile()) return file
  }
  return urlPath.startsWith('/_nuxt/') ? null : path.join(PUBLIC_DIR, 'index.html')
}

http.createServer((req, res) => {
  const url = new URL(req.url ?? '/', 'http://localhost')
  if (url.pathname === '/healthz') {
    res.writeHead(200, { 'Content-Type': 'text/plain' })
    res.end('ok')
    return
  }
  if (url.pathname.startsWith('/api/')) return proxy(req, res)

  const file = resolveFile(url.pathname)
  if (!file) {
    res.writeHead(404, SECURITY_HEADERS)
    res.end('not found')
    return
  }
  res.writeHead(200, {
    ...SECURITY_HEADERS,
    'Content-Type': TYPES[path.extname(file)] ?? 'application/octet-stream',
    'Cache-Control': url.pathname.startsWith('/_nuxt/') ? 'public, max-age=31536000' : 'no-cache'
  })
  fs.createReadStream(file).pipe(res)
}).listen(PORT, () => console.log(`web on http://localhost:${PORT} (api ${API.origin})`))
