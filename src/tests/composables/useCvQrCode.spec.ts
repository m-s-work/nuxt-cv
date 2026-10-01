import { describe, it, expect } from 'vitest'
import { isAllowedQrTarget } from '~/composables/useCvQrCode'

const page = (url: string) => {
  const u = new URL(url)
  return { origin: u.origin, hostname: u.hostname }
}

describe('isAllowedQrTarget', () => {
  it('rejects missing and malformed values', () => {
    expect(isAllowedQrTarget(null, page('https://cv.example.org/cv'))).toBe(false)
    expect(isAllowedQrTarget('', page('https://cv.example.org/cv'))).toBe(false)
    expect(isAllowedQrTarget('not a url', page('https://cv.example.org/cv'))).toBe(false)
  })

  it('accepts same-origin URLs', () => {
    expect(isAllowedQrTarget('https://cv.example.org/de?c=abc', page('https://cv.example.org/cv'))).toBe(true)
    expect(isAllowedQrTarget('http://localhost:3000/cv', page('http://localhost:3000/cv'))).toBe(true)
  })

  it('rejects foreign hosts on a public page', () => {
    expect(isAllowedQrTarget('https://evil.example.com/', page('https://cv.example.org/cv'), 'https://cv.platform.io')).toBe(false)
    expect(isAllowedQrTarget('https://example.org.evil.com/', page('https://cv.example.org/cv'))).toBe(false)
  })

  it('accepts the platform host and hosts sharing a registrable domain', () => {
    expect(isAllowedQrTarget('https://cv.platform.io/cv?c=x', page('https://alice.example.org/'), 'https://cv.platform.io')).toBe(true)
    expect(isAllowedQrTarget('https://www.example.org/', page('https://cv.example.org/cv'))).toBe(true)
    expect(isAllowedQrTarget('https://alice.platform.io/', page('https://cv.example.org/cv'), 'https://cv.platform.io')).toBe(true)
  })

  it('accepts the public tenant URL when rendered on the internal host (PDF renderer)', () => {
    expect(isAllowedQrTarget('https://cv.alice.dev/?c=qr123', page('http://web/cv?print=1'))).toBe(true)
    expect(isAllowedQrTarget('https://cv.platform.io/de/cv', page('http://web/de/cv?print=1'), 'https://cv.platform.io')).toBe(true)
  })

  it('rejects non-https protocols and credentials, even on the internal host', () => {
    expect(isAllowedQrTarget('javascript:alert(1)', page('http://web/cv'))).toBe(false)
    expect(isAllowedQrTarget('http://cv.alice.dev/', page('http://web/cv'))).toBe(false)
    expect(isAllowedQrTarget('https://user:pw@cv.alice.dev/', page('http://web/cv'))).toBe(false)
    expect(isAllowedQrTarget('https://user@cv.example.org/', page('https://cv.example.org/cv'))).toBe(false)
  })
})
