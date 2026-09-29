// Internal IP -> location / network lookup. Only the API calls it (no public domain), so visitor IPs never
// leave the server (docs/VISITOR_SESSION_TRACKING.md R3.7, R9.18).
//
// GET /lookup?ip=203.0.113.7  ->  { ip, country, region, city, asn, asOrg }   (fields null when unknown)
// GET /health                 ->  { status: "ok", databases: { city, asn } }  (200 even before the first download)
// GET /version                ->  { commit, builtAt, databases }
//
// Databases: DB-IP Lite "city" and "asn" (MaxMind format, CC BY 4.0, https://db-ip.com), downloaded into
// DATA_DIR on start and refreshed when a new month's release is available. Sources are configurable
// (GEO_CITY_URL / GEO_ASN_URL, "{yyyy}" and "{mm}" are replaced), e.g. for GeoLite2 mirrors or tests.
import http from 'node:http'
import fs from 'node:fs'
import path from 'node:path'
import zlib from 'node:zlib'
import net from 'node:net'
import { Reader } from 'maxmind'

const PORT = Number(process.env.PORT || 3100)
const DATA_DIR = process.env.DATA_DIR || '/data'
const REFRESH_HOURS = Number(process.env.GEO_REFRESH_HOURS || 24)
const SOURCES = {
  city: process.env.GEO_CITY_URL || 'https://download.db-ip.com/free/dbip-city-lite-{yyyy}-{mm}.mmdb.gz',
  asn: process.env.GEO_ASN_URL || 'https://download.db-ip.com/free/dbip-asn-lite-{yyyy}-{mm}.mmdb.gz'
}

const readers = { city: null, asn: null }
const log = (...args) => console.log(new Date().toISOString(), ...args)

function readMeta() {
  try { return JSON.parse(fs.readFileSync(path.join(DATA_DIR, 'meta.json'), 'utf8')) } catch { return {} }
}

function writeMeta(meta) {
  fs.writeFileSync(path.join(DATA_DIR, 'meta.json'), JSON.stringify(meta, null, 2))
}

/** Releases to try, newest first: this month, then last month (a new month is published a few days late). */
export function releases(now = new Date()) {
  const months = [0, 1].map(back => new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth() - back, 1)))
  return months.map(d => `${d.getUTCFullYear()}-${String(d.getUTCMonth() + 1).padStart(2, '0')}`)
}

export function sourceUrl(template, release) {
  const [yyyy, mm] = release.split('-')
  return template.replaceAll('{yyyy}', yyyy).replaceAll('{mm}', mm)
}

function load(kind) {
  const file = path.join(DATA_DIR, `${kind}.mmdb`)
  if (!fs.existsSync(file)) return
  try {
    readers[kind] = new Reader(fs.readFileSync(file))
  } catch (err) {
    log(`cannot open ${file}:`, err.message)
  }
}

/** Downloads a database unless the stored one is already the newest available release. */
async function refresh(kind) {
  const meta = readMeta()
  for (const release of releases()) {
    if (meta[kind]?.release === release && readers[kind]) return
    const url = sourceUrl(SOURCES[kind], release)
    let response
    try {
      response = await fetch(url, { signal: AbortSignal.timeout(120_000) })
    } catch (err) {
      log(`${kind}: download failed (${err.message})`)
      return
    }
    if (response.status === 404 || response.status === 403) continue // not published yet: try the previous month
    if (!response.ok) { log(`${kind}: ${url} -> HTTP ${response.status}`); return }
    let data = Buffer.from(await response.arrayBuffer())
    if (url.endsWith('.gz')) data = zlib.gunzipSync(data)
    try {
      new Reader(data) // validate before replacing the current file
    } catch (err) {
      log(`${kind}: ${url} is not a valid database (${err.message})`)
      return
    }
    const file = path.join(DATA_DIR, `${kind}.mmdb`)
    fs.writeFileSync(`${file}.tmp`, data)
    fs.renameSync(`${file}.tmp`, file)
    writeMeta({ ...readMeta(), [kind]: { release, source: url, downloadedAt: new Date().toISOString() } })
    load(kind)
    log(`${kind}: loaded release ${release}`)
    return
  }
  log(`${kind}: no release available yet; keeping ${meta[kind]?.release ?? 'none'}`)
}

async function refreshAll() {
  for (const kind of Object.keys(SOURCES)) {
    try { await refresh(kind) } catch (err) { log(`${kind}: refresh failed`, err) }
  }
}

/** Location and network of an IP; unknown parts are null. */
export function lookup(ip) {
  const city = readers.city?.get(ip) ?? null
  const asn = readers.asn?.get(ip) ?? null
  const subdivision = city?.subdivisions?.[city.subdivisions.length - 1]
  return {
    ip,
    country: city?.country?.iso_code ?? null,
    region: subdivision?.names?.en ?? null,
    city: city?.city?.names?.en ?? null,
    asn: asn?.autonomous_system_number ?? null,
    asOrg: asn?.autonomous_system_organization ?? null
  }
}

function databases() {
  const meta = readMeta()
  return Object.fromEntries(Object.keys(SOURCES).map(kind => [kind, readers[kind] ? meta[kind]?.release ?? 'local' : null]))
}

function readBuild(name) {
  try { return fs.readFileSync(new URL(`./${name}`, import.meta.url), 'utf8').trim() } catch { return null }
}

function send(res, status, body) {
  res.writeHead(status, { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' })
  res.end(JSON.stringify(body))
}

export function createServer() {
  return http.createServer((req, res) => {
    const url = new URL(req.url, 'http://geo')
    if (req.method !== 'GET') return send(res, 405, { error: 'method_not_allowed' })
    if (url.pathname === '/health') return send(res, 200, { status: 'ok', databases: databases() })
    if (url.pathname === '/version') {
      return send(res, 200, { commit: readBuild('SOURCE_COMMIT') ?? 'unknown', builtAt: readBuild('BUILD_TIME'), databases: databases() })
    }
    if (url.pathname === '/lookup') {
      const ip = url.searchParams.get('ip') ?? ''
      if (!net.isIP(ip)) return send(res, 400, { error: 'invalid_ip' })
      try {
        return send(res, 200, lookup(ip))
      } catch {
        return send(res, 200, { ip, country: null, region: null, city: null, asn: null, asOrg: null })
      }
    }
    return send(res, 404, { error: 'not_found' })
  })
}

export async function start() {
  fs.mkdirSync(DATA_DIR, { recursive: true })
  load('city')
  load('asn')
  const server = createServer()
  await new Promise(resolve => server.listen(PORT, resolve))
  log(`geo lookup listening on :${PORT}, databases`, databases())
  await refreshAll()
  const timer = setInterval(refreshAll, REFRESH_HOURS * 3600_000)
  timer.unref()
  return server
}

if (import.meta.url === `file://${process.argv[1]}`) start()
