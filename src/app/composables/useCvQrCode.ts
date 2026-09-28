import QRCode from 'qrcode'

/**
 * QR code (data URL) pointing to the online CV.
 * In PDF rendering (?print=1) the page runs on an internal URL; the API passes the public URL –
 * including the linked QR invite code – as ?qr=.
 */
export function useCvQrCode(size = 200) {
  const dataUrl = ref('')
  const url = ref('')

  onMounted(async () => {
    const params = new URLSearchParams(window.location.search)
    const publicUrl = params.has('print') ? params.get('qr') : null
    url.value = publicUrl && /^https?:\/\//.test(publicUrl)
      ? publicUrl
      : new URL(window.location.pathname, window.location.origin).toString()
    try {
      dataUrl.value = await QRCode.toDataURL(url.value, { width: size, margin: 1, color: { dark: '#000000', light: '#FFFFFF' } })
    } catch (error) {
      console.error('Failed to generate QR code:', error)
    }
  })

  return { dataUrl, url }
}
