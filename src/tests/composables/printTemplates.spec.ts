import { describe, it, expect } from 'vitest'
import { printTemplates, resolvePrintTemplate, defaultPrintTemplate } from '~/utils/printTemplates'

describe('resolvePrintTemplate', () => {
  it('returns a registered template', () => {
    expect(resolvePrintTemplate('classic')).toBe('classic')
  })

  it('falls back to the default for unknown or missing names', () => {
    expect(resolvePrintTemplate('does-not-exist')).toBe(defaultPrintTemplate)
    expect(resolvePrintTemplate(null)).toBe(defaultPrintTemplate)
    expect(resolvePrintTemplate(undefined)).toBe(defaultPrintTemplate)
  })

  it('uses API-valid names for every template', () => {
    for (const name of Object.keys(printTemplates)) expect(name).toMatch(/^[a-z0-9][a-z0-9-]{0,31}$/)
  })
})
