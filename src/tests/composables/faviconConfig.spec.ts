import { describe, it, expect } from 'vitest'
import { readFavicon, svgDataUrl, writeFavicon } from '~/utils/faviconConfig'

const tenantJson = `{
  // Bob's CV
  "name": "Bob",
  "favicon": { "symbol": "terminal", "color": 42 }, // icon
  "profiles": {},
}
`

describe('readFavicon', () => {
  it('reads string fields only', () => {
    expect(readFavicon(tenantJson)).toEqual({ symbol: 'terminal' })
    expect(readFavicon('{ "name": "x" }')).toEqual({})
  })

  it('rejects invalid JSON', () => {
    expect(() => readFavicon('{ broken')).toThrow()
  })
})

describe('writeFavicon', () => {
  it('replaces only the favicon property and keeps comments', () => {
    const text = writeFavicon(tenantJson, { symbol: 'lambda', color: 'amber', background: '#fff' })
    expect(text).toContain('// Bob\'s CV')
    expect(readFavicon(text)).toEqual({ symbol: 'lambda', color: 'amber', background: '#fff' })
  })

  it('adds the property when missing and removes it when empty', () => {
    const added = writeFavicon('{\n  "name": "x"\n}\n', { symbol: 'code' })
    expect(readFavicon(added)).toEqual({ symbol: 'code' })
    expect(writeFavicon(added, {})).not.toContain('favicon')
  })
})

describe('svgDataUrl', () => {
  it('encodes the markup', () => {
    expect(svgDataUrl('<svg>#</svg>')).toBe('data:image/svg+xml;charset=utf-8,%3Csvg%3E%23%3C%2Fsvg%3E')
  })
})
