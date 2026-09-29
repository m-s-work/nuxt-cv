// Runs the service against MaxMind's public test databases (downloaded from GitHub, served gzipped by a local
// fake "DB-IP"), so no real IP data is needed. Skipped when GitHub is not reachable.
import { test, before, after } from 'node:test'
import assert from 'node:assert/strict'
import http from 'node:http'
import zlib from 'node:zlib'
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'

const FIXTURES = {
  city: 'https://raw.githubusercontent.com/maxmind/MaxMind-DB/main/test-data/GeoLite2-City-Test.mmdb',
  asn: 'https://raw.githubusercontent.com/maxmind/MaxMind-DB/main/test-data/GeoLite2-ASN-Test.mmdb'
}

let fixtures = null
let upstream
let service
let base
const requested = []

before(async () => {
  try {
    fixtures = Object.fromEntries(await Promise.all(Object.entries(FIXTURES).map(async ([kind, url]) =>
      [kind, zlib.gzipSync(Buffer.from(await (await fetch(url)).arrayBuffer()))])))
  } catch {
    return
  }
  // Fake download server: only last month's release exists (this month is "not published yet").
  const { releases } = await import('./server.mjs')
  const [thisMonth, lastMonth] = releases()
  upstream = http.createServer((req, res) => {
    requested.push(req.url)
    const match = /^\/(city|asn)-(\d{4}-\d{2})\.mmdb\.gz$/.exec(req.url)
    if (!match || match[2] !== lastMonth) { res.writeHead(404); return res.end() }
    res.end(fixtures[match[1]])
  })
  await new Promise(r => upstream.listen(0, r))
  const up = `http://127.0.0.1:${upstream.address().port}`
  assert.notEqual(thisMonth, lastMonth)

  process.env.PORT = '0'
  process.env.DATA_DIR = fs.mkdtempSync(path.join(os.tmpdir(), 'geo-'))
  process.env.GEO_CITY_URL = `${up}/city-{yyyy}-{mm}.mmdb.gz`
  process.env.GEO_ASN_URL = `${up}/asn-{yyyy}-{mm}.mmdb.gz`
  const { start } = await import(`./server.mjs?${Date.now()}`)
  service = await start()
  base = `http://127.0.0.1:${service.address().port}`
})

after(() => { service?.close(); upstream?.close() })

test('downloads the newest published release and looks up location and network', async (t) => {
  if (!fixtures) return t.skip('test databases not reachable')
  const health = await (await fetch(`${base}/health`)).json()
  assert.ok(health.databases.city && health.databases.asn)
  assert.ok(requested.some(u => u.startsWith('/city-')))

  const london = await (await fetch(`${base}/lookup?ip=81.2.69.142`)).json()
  assert.equal(london.country, 'GB')
  assert.equal(london.city, 'London')
  const telstra = await (await fetch(`${base}/lookup?ip=1.128.0.1`)).json()
  assert.equal(telstra.asn, 1221)
  assert.equal(telstra.asOrg, 'Telstra Pty Ltd')
  const unknown = await (await fetch(`${base}/lookup?ip=10.0.0.1`)).json()
  assert.equal(unknown.country, null)
})

test('rejects invalid input', async (t) => {
  if (!fixtures) return t.skip('test databases not reachable')
  assert.equal((await fetch(`${base}/lookup?ip=not-an-ip`)).status, 400)
  assert.equal((await fetch(`${base}/other`)).status, 404)
})

test('builds release URLs', async () => {
  const { releases, sourceUrl } = await import('./server.mjs')
  assert.deepEqual(releases(new Date(Date.UTC(2026, 0, 15))), ['2026-01', '2025-12'])
  assert.equal(sourceUrl('https://x/dbip-city-lite-{yyyy}-{mm}.mmdb.gz', '2026-01'), 'https://x/dbip-city-lite-2026-01.mmdb.gz')
})
