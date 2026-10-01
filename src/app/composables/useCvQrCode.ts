import QRCode from 'qrcode'

/** Last two labels of a host name ("cv.alice.example.org" -> "example.org"). No public suffix list: approximate. */
function baseDomain(host: string): string {
  return host.split('.').slice(-2).join('.')
}

/** Internal (non-public) host such as the compose service "web" the PDF renderer opens: no dot, not an IP. */
function isInternalHost(hostname: string): boolean {
  return hostname !== '' && !hostname.includes('.') && !hostname.includes(':')
}

/**
 * Decides whether a `?qr=` value may be used as QR / "online version" target, so a crafted link
 * (`?print=1&qr=https://evil.example`) cannot point the printed CV at a foreign site.
 *
 * Accepted:
 * - the current page's origin (same-origin URL),
 * - on an internal host (the PDF renderer opens the app via http://web/...; no visitor can be sent there):
 *   any https URL – the API passes the public tenant URL (any custom domain),
 * - otherwise an https URL whose host equals the platform host (`links.platform`) or shares the
 *   registrable domain of the current host or the platform host.
 * Never accepted: other protocols, URLs with credentials.
 */
export function isAllowedQrTarget(value: string | null | undefined, page: { origin: string, hostname: string }, platform?: string | null): boolean {
  if (!value) return false
  let target: URL
  try {
    target = new URL(value)
  } catch {
    return false
  }
  if (target.username || target.password) return false
  if (target.origin === page.origin) return true
  if (target.protocol !== 'https:') return false
  if (isInternalHost(page.hostname)) return true

  const host = target.hostname.toLowerCase()
  let platformHost: string | null = null
  try {
    platformHost = platform ? new URL(platform).hostname.toLowerCase() : null
  } catch { /* invalid platform URL */ }
  if (platformHost && host === platformHost) return true

  const domains = [page.hostname.toLowerCase(), platformHost]
    .filter((h): h is string => !!h && h.includes('.'))
    .map(baseDomain)
  return domains.some(domain => host === domain || host.endsWith('.' + domain))
}

/**
 * QR code (data URL) pointing to the online CV.
 * In PDF rendering (?print=1) the page runs on an internal URL; the API passes the public URL –
 * including the linked QR invite code – as ?qr= (validated by isAllowedQrTarget).
 */
export function useCvQrCode(size = 200) {
  const dataUrl = ref('')
  const url = ref('')
  const { links } = useCv()

  async function update() {
    const params = new URLSearchParams(window.location.search)
    const publicUrl = params.has('print') ? params.get('qr') : null
    url.value = isAllowedQrTarget(publicUrl, window.location, links.value?.platform)
      ? publicUrl!
      : new URL(window.location.pathname, window.location.origin).toString()
    try {
      dataUrl.value = await QRCode.toDataURL(url.value, { width: size, margin: 1, color: { dark: '#000000', light: '#FFFFFF' } })
    } catch (error) {
      console.error('Failed to generate QR code:', error)
    }
  }

  onMounted(() => {
    update()
    // links.platform arrives with /api/cv; re-check once it is known.
    watch(() => links.value?.platform, () => update())
  })

  return { dataUrl, url }
}
