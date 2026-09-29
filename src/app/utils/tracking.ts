/**
 * Pure helpers of the visitor tracking (docs/VISITOR_SESSION_TRACKING.md). The tracker itself lives in
 * composables/useVisitorTracking.ts and only runs after the visitor accepted the consent modal.
 */

/**
 * Version of the consent texts (components/CvConsentModal.vue). Must equal TrackingPolicy.TextVersion in the API;
 * the API hashes it into the policy version, so changing both asks every visitor again (R9.12).
 */
export const CONSENT_TEXT_VERSION = '2026-09-29'

/** Layout breakpoints used to group heatmaps (§6.2). Matches the CV's Tailwind breakpoints. */
export type Breakpoint = 'sm' | 'md' | 'lg' | 'xl'

export function breakpointOf(width: number): Breakpoint {
  if (width < 768) return 'sm'
  if (width < 1024) return 'md'
  if (width < 1280) return 'lg'
  return 'xl'
}

/** Representative viewport width per breakpoint (heatmap rendering in the admin). */
export const BREAKPOINT_WIDTH: Record<Breakpoint, number> = { sm: 390, md: 820, lg: 1180, xl: 1440 }

/** Anchor of an element: its nearest `data-track` ancestor (§6.1). */
export function anchorOf(element: Element | null | undefined): HTMLElement | null {
  return (element?.closest?.('[data-track]') as HTMLElement | null) ?? null
}

/** Position of a point inside a rectangle in percent (0–100, rounded to the 1 % grid, R6.6). */
export function relativePosition(x: number, y: number, rect: { left: number, top: number, width: number, height: number }): [number, number] {
  const px = rect.width > 0 ? ((x - rect.left) / rect.width) * 100 : 0
  const py = rect.height > 0 ? ((y - rect.top) / rect.height) * 100 : 0
  return [clamp(Math.round(px), 0, 100), clamp(Math.round(py), 0, 100)]
}

export function clamp(value: number, min: number, max: number): number {
  return Math.min(max, Math.max(min, value))
}

/** Scroll depth in percent of the page (R5 "scroll"). */
export function scrollDepth(scrollY: number, viewportHeight: number, documentHeight: number): number {
  if (documentHeight <= 0) return 0
  return clamp(Math.round(((scrollY + viewportHeight) / documentHeight) * 100), 0, 100)
}

/**
 * Whether an anchor counts as "in view": at least half of it, or – for anchors taller than the viewport – at least
 * half of the viewport is covered by it.
 */
export function isInView(visibleHeight: number, elementHeight: number, viewportHeight: number): boolean {
  if (elementHeight <= 0) return false
  return visibleHeight >= 0.5 * Math.min(elementHeight, viewportHeight)
}

export type LinkKind = 'mailto' | 'tel' | 'github' | 'linkedin' | 'project' | 'other'

/** Classifies a link target (contact vs. outbound link, R5 "contact" / "link_out"). */
export function linkKind(href: string, currentHost: string): LinkKind | null {
  if (href.startsWith('mailto:')) return 'mailto'
  if (href.startsWith('tel:')) return 'tel'
  let url: URL
  try { url = new URL(href, `https://${currentHost}/`) } catch { return null }
  if (url.host === currentHost || !/^https?:$/.test(url.protocol)) return null
  if (/(^|\.)github\.com$/.test(url.hostname)) return 'github'
  if (/(^|\.)linkedin\.com$/.test(url.hostname)) return 'linkedin'
  return 'project'
}

/** Rage clicks: ≥ 3 clicks within 1 s and 30 px (R5 "rage_click"). */
export function isRageClick(clicks: Array<{ t: number, x: number, y: number }>): boolean {
  if (clicks.length < 3) return false
  const last = clicks.slice(-3)
  const first = last[0]!
  return last[2]!.t - first.t <= 1000 && last.every(c => Math.hypot(c.x - first.x, c.y - first.y) <= 30)
}

/**
 * Whether a clicked element promises an action (dead-click candidate): a pointer cursor or an image, but not a real
 * control (links, buttons, inputs – those are handled by the browser anyway).
 */
export function looksClickable(tagName: string, cursor: string, isControl: boolean): boolean {
  if (isControl) return false
  return cursor === 'pointer' || tagName.toUpperCase() === 'IMG'
}

/** Random URL-safe id (session / tab ids). */
export function randomId(bytes = 16): string {
  const data = new Uint8Array(bytes)
  crypto.getRandomValues(data)
  return btoa(String.fromCharCode(...data)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')
}

/** Maps a timeline entry id ("exp-3", "study-1", …) to its anchor ("experience:3"). */
export function timelineAnchor(entryId: string | number): string | undefined {
  const match = /^(exp|study|project|other)-(.+)$/.exec(String(entryId))
  if (!match) return undefined
  return `${match[1] === 'exp' ? 'experience' : match[1]}:${match[2]}`
}

/**
 * SHA-256 as lower-case hex. Pure JS, because `crypto.subtle` is missing outside secure contexts (plain-HTTP
 * development hosts); used for fingerprint components, which are only ever sent as hashes (R3.8).
 */
export function sha256(text: string): string {
  const k = [
    0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5, 0x3956c25b, 0x59f111f1, 0x923f82a4, 0xab1c5ed5, 0xd807aa98, 0x12835b01,
    0x243185be, 0x550c7dc3, 0x72be5d74, 0x80deb1fe, 0x9bdc06a7, 0xc19bf174, 0xe49b69c1, 0xefbe4786, 0x0fc19dc6, 0x240ca1cc,
    0x2de92c6f, 0x4a7484aa, 0x5cb0a9dc, 0x76f988da, 0x983e5152, 0xa831c66d, 0xb00327c8, 0xbf597fc7, 0xc6e00bf3, 0xd5a79147,
    0x06ca6351, 0x14292967, 0x27b70a85, 0x2e1b2138, 0x4d2c6dfc, 0x53380d13, 0x650a7354, 0x766a0abb, 0x81c2c92e, 0x92722c85,
    0xa2bfe8a1, 0xa81a664b, 0xc24b8b70, 0xc76c51a3, 0xd192e819, 0xd6990624, 0xf40e3585, 0x106aa070, 0x19a4c116, 0x1e376c08,
    0x2748774c, 0x34b0bcb5, 0x391c0cb3, 0x4ed8aa4a, 0x5b9cca4f, 0x682e6ff3, 0x748f82ee, 0x78a5636f, 0x84c87814, 0x8cc70208,
    0x90befffa, 0xa4506ceb, 0xbef9a3f7, 0xc67178f2
  ]
  const bytes = new TextEncoder().encode(text)
  const bitLength = bytes.length * 8
  const padded = new Uint8Array(((bytes.length + 9 + 63) >> 6) << 6)
  padded.set(bytes)
  padded[bytes.length] = 0x80
  const view = new DataView(padded.buffer)
  view.setUint32(padded.length - 8, Math.floor(bitLength / 0x100000000))
  view.setUint32(padded.length - 4, bitLength >>> 0)

  const h = [0x6a09e667, 0xbb67ae85, 0x3c6ef372, 0xa54ff53a, 0x510e527f, 0x9b05688c, 0x1f83d9ab, 0x5be0cd19]
  const w = new Uint32Array(64)
  const rotr = (x: number, n: number) => (x >>> n) | (x << (32 - n))
  for (let offset = 0; offset < padded.length; offset += 64) {
    for (let i = 0; i < 16; i++) w[i] = view.getUint32(offset + i * 4)
    for (let i = 16; i < 64; i++) {
      const s0 = rotr(w[i - 15]!, 7) ^ rotr(w[i - 15]!, 18) ^ (w[i - 15]! >>> 3)
      const s1 = rotr(w[i - 2]!, 17) ^ rotr(w[i - 2]!, 19) ^ (w[i - 2]! >>> 10)
      w[i] = (w[i - 16]! + s0 + w[i - 7]! + s1) >>> 0
    }
    let [a, b, c, d, e, f, g, hh] = h as [number, number, number, number, number, number, number, number]
    for (let i = 0; i < 64; i++) {
      const S1 = rotr(e, 6) ^ rotr(e, 11) ^ rotr(e, 25)
      const ch = (e & f) ^ (~e & g)
      const t1 = (hh + S1 + ch + k[i]! + w[i]!) >>> 0
      const S0 = rotr(a, 2) ^ rotr(a, 13) ^ rotr(a, 22)
      const maj = (a & b) ^ (a & c) ^ (b & c)
      const t2 = (S0 + maj) >>> 0
      hh = g; g = f; f = e; e = (d + t1) >>> 0; d = c; c = b; b = a; a = (t1 + t2) >>> 0
    }
    h[0] = (h[0]! + a) >>> 0; h[1] = (h[1]! + b) >>> 0; h[2] = (h[2]! + c) >>> 0; h[3] = (h[3]! + d) >>> 0
    h[4] = (h[4]! + e) >>> 0; h[5] = (h[5]! + f) >>> 0; h[6] = (h[6]! + g) >>> 0; h[7] = (h[7]! + hh) >>> 0
  }
  return h.map(x => x.toString(16).padStart(8, '0')).join('')
}

/** Fixed list of fonts probed for the fingerprint (R3.8). */
const PROBE_FONTS = [
  'Arial', 'Calibri', 'Cambria', 'Consolas', 'Courier New', 'Georgia', 'Helvetica Neue', 'Menlo', 'Monaco', 'Segoe UI',
  'San Francisco', 'Roboto', 'Ubuntu', 'DejaVu Sans', 'Noto Sans', 'Tahoma', 'Trebuchet MS', 'Verdana', 'Fira Code', 'Source Code Pro'
]

function probeFonts(): string {
  const canvas = document.createElement('canvas')
  const ctx = canvas.getContext('2d')
  if (!ctx) return ''
  const sample = 'mmmmmmmmmmlli10OQ@#'
  const width = (font: string) => { ctx.font = `72px ${font}`; return ctx.measureText(sample).width }
  const base = { monospace: width('monospace'), serif: width('serif'), 'sans-serif': width('sans-serif') }
  return PROBE_FONTS.filter(font =>
    (Object.keys(base) as Array<keyof typeof base>).some(fallback => width(`'${font}',${fallback}`) !== base[fallback])).join(',')
}

function canvasHash(): string {
  try {
    const canvas = document.createElement('canvas')
    canvas.width = 240
    canvas.height = 60
    const ctx = canvas.getContext('2d')
    if (!ctx) return ''
    ctx.textBaseline = 'top'
    ctx.font = '16px Arial'
    ctx.fillStyle = '#f60'
    ctx.fillRect(100, 1, 62, 20)
    ctx.fillStyle = '#069'
    ctx.fillText('CV fingerprint ✓ 1.0', 2, 15)
    ctx.fillStyle = 'rgba(102, 204, 0, 0.7)'
    ctx.fillText('CV fingerprint ✓ 1.0', 4, 17)
    return sha256(canvas.toDataURL())
  } catch { return '' }
}

function webglRenderer(): string {
  try {
    const gl = document.createElement('canvas').getContext('webgl') as WebGLRenderingContext | null
    if (!gl) return ''
    const info = gl.getExtension('WEBGL_debug_renderer_info')
    return info
      ? `${gl.getParameter(info.UNMASKED_VENDOR_WEBGL)}|${gl.getParameter(info.UNMASKED_RENDERER_WEBGL)}`
      : `${gl.getParameter(gl.VENDOR)}|${gl.getParameter(gl.RENDERER)}`
  } catch { return '' }
}

/**
 * Browser fingerprint (R3.8, R3.12): stable traits, each hashed. Only called after consent. Components keep their
 * position, so the server can compute a similarity when single traits change.
 */
export function collectFingerprint(): { fp: string, fpParts: string[] } {
  const nav = navigator as Navigator & { deviceMemory?: number, globalPrivacyControl?: boolean, userAgentData?: { platform?: string, mobile?: boolean } }
  const traits = [
    nav.userAgent,
    `${nav.userAgentData?.platform ?? nav.platform}|${nav.userAgentData?.mobile ?? ''}`,
    (nav.languages ?? [nav.language]).join(','),
    Intl.DateTimeFormat().resolvedOptions().timeZone ?? String(new Date().getTimezoneOffset()),
    `${screen.width}x${screen.height}x${screen.colorDepth}|${window.devicePixelRatio}`,
    `${nav.deviceMemory ?? ''}|${nav.hardwareConcurrency ?? ''}`,
    `${nav.maxTouchPoints ?? 0}|${'ontouchstart' in window}`,
    probeFonts(),
    canvasHash(),
    webglRenderer(),
    `${nav.doNotTrack ?? ''}|${nav.globalPrivacyControl ?? ''}`
  ]
  const fpParts = traits.map(t => sha256(t))
  return { fp: sha256(fpParts.join('|')), fpParts }
}
