import QRCode from 'qrcode'

/**
 * Website links of CV entries in the PDF (docs/REQUIREMENTS_ACCESS_AND_TENANCY.md §7.2). The owner picks the mode
 * with the template variable `links`:
 * - `qr`: the clear address as text (opens the website directly) plus a QR code to the tracked link,
 * - `tracked`: the clear address as text, opening the tracked link,
 * - `clear`: the clear address, untracked,
 * - `off`: no links.
 * Tracked links go through /api/go on the public site with the PDF's QR code (the API passes the base as ?go=,
 * "{key}" filled in per link) and are counted per PDF. Without it (public profile, view-once invite, admin preview)
 * links are printed untracked.
 */
export type PrintLinkMode = 'qr' | 'tracked' | 'clear' | 'off'

export interface PrintLink {
  text: string
  href: string
  /** QR code (data URL) in mode `qr`. */
  qr?: string
}

export interface LinkedEntry {
  url?: string
  urlTarget?: string
  urlHost?: string
  urlLabel?: string
}

const GO_PREFIX = '/api/go/'

/** Readable address: without protocol, "www." and trailing slash. */
export function prettyUrl(url: string): string {
  return url.replace(/^https?:\/\//i, '').replace(/^www\./i, '').replace(/\/$/, '')
}

/** Clear and tracked target of an entry's link (pure, for tests). */
export function linkTargets(entry: LinkedEntry, goTemplate: string | null): { clear: string | null, tracked: string | null } {
  const absolute = (value?: string) => value && /^https?:\/\//i.test(value) ? value : null
  const key = entry.url?.startsWith(GO_PREFIX) ? entry.url.slice(GO_PREFIX.length) : null
  return {
    clear: absolute(entry.urlTarget) ?? absolute(entry.url),
    tracked: goTemplate && key && /^[0-9a-f]{32}$/.test(key) ? goTemplate.replace('{key}', key) : null
  }
}

export function usePrintLinks(mode: () => PrintLinkMode) {
  const goTemplate = ref<string | null>(null)
  const qrCodes = ref<Record<string, string>>({})

  onMounted(() => {
    const params = new URLSearchParams(window.location.search)
    const go = params.has('print') ? params.get('go') : null
    goTemplate.value = go && /^https?:\/\//.test(go) && go.includes('{key}') ? go : null
  })

  function qr(url: string): string | undefined {
    if (!(url in qrCodes.value)) {
      // Deferred: called while rendering.
      queueMicrotask(() => {
        if (url in qrCodes.value) return
        qrCodes.value[url] = ''
        QRCode.toDataURL(url, { width: 160, margin: 0, color: { dark: '#000000', light: '#FFFFFF' } })
          .then((data) => { qrCodes.value[url] = data })
          .catch(() => { /* no QR code, the text link remains */ })
      })
    }
    return qrCodes.value[url] || undefined
  }

  function link(entry: LinkedEntry): PrintLink | null {
    const current = mode()
    if (current === 'off') return null
    const { clear, tracked } = linkTargets(entry, goTemplate.value)
    const target = clear ?? tracked
    if (!target) return null
    const text = entry.urlLabel || (clear ? prettyUrl(clear) : entry.urlHost ?? prettyUrl(target))
    if (current === 'clear') return { text, href: target }
    if (current === 'tracked') return { text, href: tracked ?? target }
    return { text, href: clear ?? target, qr: qr(tracked ?? target) }
  }

  return { link }
}
