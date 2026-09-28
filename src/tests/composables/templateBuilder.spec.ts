import { describe, it, expect } from 'vitest'
import { minimalVars, readTemplatesSelection, writeTemplatesSelection } from '~/utils/templateBuilder'
import { printTemplates } from '~/utils/printTemplates'

const tenantJson = `{
  // Bob's CV
  "name": "Bob",
  "templates": { "pdf": "editorial" },
  "profiles": {
    "recruiter": { "grants": ["contact"] }, // for recruiters
  }
}
`

describe('readTemplatesSelection', () => {
  it('reads tenant and profile scope from JSONC', () => {
    expect(readTemplatesSelection(tenantJson, { kind: 'tenant' })).toEqual({ pdf: 'editorial' })
    expect(readTemplatesSelection(tenantJson, { kind: 'profile', name: 'recruiter' })).toEqual({})
  })

  it('rejects invalid JSON', () => {
    expect(() => readTemplatesSelection('{ broken', { kind: 'tenant' })).toThrow()
  })
})

describe('writeTemplatesSelection', () => {
  it('replaces only the templates property and keeps comments', () => {
    const text = writeTemplatesSelection(tenantJson, { kind: 'tenant' }, { pdf: 'banner', pdfVars: { preset: 'graphite' } })
    expect(text).toContain('// Bob\'s CV')
    expect(text).toContain('// for recruiters')
    expect(readTemplatesSelection(text, { kind: 'tenant' })).toEqual({ pdf: 'banner', pdfVars: { preset: 'graphite' } })
  })

  it('adds a profile-level choice and removes it again', () => {
    const scope = { kind: 'profile', name: 'recruiter' } as const
    const added = writeTemplatesSelection(tenantJson, scope, { pdf: 'classic' })
    expect(readTemplatesSelection(added, scope)).toEqual({ pdf: 'classic' })
    expect(readTemplatesSelection(added, { kind: 'tenant' })).toEqual({ pdf: 'editorial' })

    const removed = writeTemplatesSelection(added, scope, {})
    expect(readTemplatesSelection(removed, scope)).toEqual({})
    expect(removed).toContain('// for recruiters')
  })
})

describe('minimalVars', () => {
  const schema = printTemplates.banner.vars

  it('stores nothing when everything is default', () => {
    expect(minimalVars(schema, undefined, { accent: '#29a8e0', chapterColors: false })).toEqual({})
  })

  it('stores the preset and only values that differ from it', () => {
    expect(minimalVars(schema, 'graphite', { sidebar: '#3f4247', accent: '#e0a526', chapterColors: true }))
      .toEqual({ preset: 'graphite', accent: '#e0a526', chapterColors: true })
  })

  it('ignores unknown presets and templates without variables', () => {
    expect(minimalVars(schema, 'nope', {})).toEqual({})
    expect(minimalVars(undefined, 'graphite', { accent: '#fff' })).toEqual({})
  })
})
