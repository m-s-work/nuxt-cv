// Starts the full stack for the end-to-end tests, like docker-compose.yml but without Docker:
//
//   web  – the generated SPA (src/.output/public) + /api proxy (stack/web.mjs, mirrors src/nginx.conf.template)
//   api  – the C# API (api/CvApi) on a fresh copy of api/sample-data
//   pdf  – the PDF renderer (pdf/server.mjs) with Playwright's Chromium
//
// Used as Playwright's webServer (see playwright.config.ts); can also be run on its own: `npm run stack`.
// The frontend is (re)generated when src/ is newer than the last build (E2E_SKIP_BUILD=1 skips that).
import { spawn, spawnSync } from 'node:child_process'
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { chromium } from '@playwright/test'
import { ADMIN_KEY, PORTS } from './config.mjs'

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..')
const stateDir = path.join(root, 'e2e/.stack')
const dataDir = path.join(stateDir, 'data')
const publicDir = path.join(root, 'src/.output/public')
const children = []

function log(message) {
  console.log(`[stack] ${message}`)
}

function newestMtime(dir) {
  let newest = 0
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    if (entry.name === 'node_modules' || entry.name.startsWith('.')) continue
    const full = path.join(dir, entry.name)
    newest = Math.max(newest, entry.isDirectory() ? newestMtime(full) : fs.statSync(full).mtimeMs)
  }
  return newest
}

function buildFrontend() {
  const index = path.join(publicDir, 'index.html')
  const sources = ['app', 'public', 'nuxt.config.ts', 'app.config.ts', 'package-lock.json'].map(p => path.join(root, 'src', p))
  const newestSource = Math.max(...sources.filter(fs.existsSync).map(p => fs.statSync(p).isDirectory() ? newestMtime(p) : fs.statSync(p).mtimeMs))
  if (process.env.E2E_SKIP_BUILD === '1' && fs.existsSync(index)) return
  if (fs.existsSync(index) && fs.statSync(index).mtimeMs >= newestSource) {
    log('frontend build is up to date')
    return
  }
  if (!fs.existsSync(path.join(root, 'src/node_modules'))) run('npm', ['ci'], path.join(root, 'src'))
  log('generating the frontend (npm run generate) …')
  run('npm', ['run', 'generate'], path.join(root, 'src'))
}

function run(command, args, cwd) {
  const result = spawnSync(command, args, { cwd, stdio: 'inherit' })
  if (result.status !== 0) throw new Error(`${command} ${args.join(' ')} failed in ${cwd}`)
}

/** Fresh tenant data for every run: the tests create invites, edit tenant.json, record consent, … */
function seedData() {
  fs.rmSync(dataDir, { recursive: true, force: true })
  fs.mkdirSync(dataDir, { recursive: true })
  fs.cpSync(path.join(root, 'api/sample-data'), dataDir, { recursive: true })
  // Never copy runtime state that a local `dotnet run` may have left in sample-data.
  for (const file of fs.readdirSync(dataDir)) {
    if (file !== 'tenants') fs.rmSync(path.join(dataDir, file), { recursive: true, force: true })
  }
}

function start(name, command, args, { cwd, env }) {
  const child = spawn(command, args, { cwd, env: { ...process.env, ...env }, stdio: ['ignore', 'pipe', 'pipe'] })
  const prefix = line => `[${name}] ${line}`
  const pipe = (stream, out) => stream.on('data', chunk => {
    for (const line of chunk.toString().split('\n')) if (line.trim()) out.write(prefix(line) + '\n')
  })
  pipe(child.stdout, process.stdout)
  pipe(child.stderr, process.stderr)
  child.on('exit', (code, signal) => {
    if (!stopping) {
      log(`${name} exited (${signal ?? code}); stopping the stack`)
      stop(1)
    }
  })
  children.push(child)
  return child
}

async function waitFor(name, url, timeoutMs = 180_000) {
  const until = Date.now() + timeoutMs
  while (Date.now() < until) {
    try {
      if ((await fetch(url)).ok) {
        log(`${name} is up (${url})`)
        return
      }
    } catch { /* not yet */ }
    await new Promise(resolve => setTimeout(resolve, 500))
  }
  throw new Error(`${name} did not become healthy at ${url}`)
}

let stopping = false
function stop(code = 0) {
  if (stopping) return
  stopping = true
  for (const child of children) child.kill('SIGTERM')
  setTimeout(() => process.exit(code), 1000).unref()
}
process.on('SIGINT', () => stop(0))
process.on('SIGTERM', () => stop(0))

const webUrl = `http://localhost:${PORTS.web}`

buildFrontend()
seedData()

// Same Chromium as the tests (no extra download); CHROMIUM_PATH overrides it.
if (!fs.existsSync(path.join(root, 'pdf/node_modules'))) run('npm', ['ci'], path.join(root, 'pdf'))
start('pdf', 'node', ['server.mjs'], {
  cwd: path.join(root, 'pdf'),
  env: { PORT: String(PORTS.pdf), CHROMIUM_PATH: process.env.CHROMIUM_PATH || chromium.executablePath() }
})

start('api', 'dotnet', ['run', '--project', 'CvApi/CvApi.csproj', '-c', 'Release', '--no-launch-profile'], {
  cwd: path.join(root, 'api'),
  env: {
    ASPNETCORE_URLS: `http://127.0.0.1:${PORTS.api}`,
    ASPNETCORE_ENVIRONMENT: 'Production',
    Cv__DataPath: dataDir,
    // "localhost" is the demo tenant's own host; every other host (127.0.0.1) is the shared host.
    Cv__SharedBaseUrl: `http://127.0.0.1:${PORTS.web}`,
    Cv__ConfigCacheSeconds: '0',
    Cv__RedeemPerMinute: '10000',
    Tracking__EventsPerMinute: '100000',
    Tracking__GeoUrl: '',
    Admin__ApiKey: ADMIN_KEY,
    Pdf__RendererUrl: `http://127.0.0.1:${PORTS.pdf}`,
    Pdf__AppBaseUrl: webUrl
  }
})

start('web', 'node', [path.join(root, 'e2e/stack/web.mjs')], {
  cwd: root,
  env: { PORT: String(PORTS.web), PUBLIC_DIR: publicDir, API_UPSTREAM: `http://127.0.0.1:${PORTS.api}` }
})

try {
  await Promise.all([
    waitFor('pdf', `http://127.0.0.1:${PORTS.pdf}/health`),
    waitFor('api', `http://127.0.0.1:${PORTS.api}/api/health`),
    waitFor('web', `${webUrl}/healthz`)
  ])
  await waitFor('web → api', `${webUrl}/api/health`)
  log(`ready: ${webUrl} (demo tenant), http://127.0.0.1:${PORTS.web} (shared host), admin key "${ADMIN_KEY}"`)
} catch (error) {
  log(error.message)
  stop(1)
}
