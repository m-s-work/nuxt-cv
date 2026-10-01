import { describe, it, expect } from 'vitest'
import { linkTargets, prettyUrl } from '~/composables/usePrintLinks'
import { printTemplates } from '~/utils/printTemplates'

const key = '0123456789abcdef0123456789abcdef'
const go = 'https://cv.example.org/api/go/{key}?c=CODE'

describe('print links', () => {
  it('uses the original link as clear target and the PDF base as tracked target', () => {
    expect(linkTargets({ url: `/api/go/${key}`, urlTarget: 'https://shop.example.com/' }, go))
      .toEqual({ clear: 'https://shop.example.com/', tracked: `https://cv.example.org/api/go/${key}?c=CODE` })
  })

  it('has no tracked target without a base (public profile, view once, preview)', () => {
    expect(linkTargets({ url: `/api/go/${key}`, urlTarget: 'https://shop.example.com/' }, null).tracked).toBeNull()
    // Admin preview: the original link itself.
    expect(linkTargets({ url: 'https://shop.example.com/' }, go)).toEqual({ clear: 'https://shop.example.com/', tracked: null })
  })

  it('ignores malformed keys and non-web links', () => {
    expect(linkTargets({ url: '/api/go/../x', urlTarget: 'javascript:alert(1)' }, go)).toEqual({ clear: null, tracked: null })
  })

  it('prints readable addresses', () => {
    expect(prettyUrl('https://www.shop.example.com/')).toBe('shop.example.com')
    expect(prettyUrl('http://example.com/a/b')).toBe('example.com/a/b')
  })

  it('lets the owner choose the mode in every template', () => {
    for (const template of Object.values(printTemplates)) {
      const def = template.vars?.vars.links
      expect(def).toMatchObject({ type: 'enum', default: 'qr', options: ['qr', 'tracked', 'clear', 'off'] })
    }
  })
})
