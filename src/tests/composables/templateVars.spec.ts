import { describe, it, expect } from 'vitest'
import { resolveTemplateVars, type TemplateVarsSchema } from '~/utils/templateVars'
import { printTemplates } from '~/utils/printTemplates'

const schema: TemplateVarsSchema = {
  vars: {
    accent: { type: 'color', default: '#111111', label: 'Accent' },
    sidebar: { type: 'color', default: '#222222', label: 'Sidebar' },
    chapterColors: { type: 'boolean', default: false, label: 'Chapter colours' },
    palette: { type: 'palette', default: ['#000000'], label: 'Palette' },
    degrees: { type: 'enum', default: 'accent', options: ['accent', 'gradient'], label: 'Degrees' }
  },
  presets: {
    dark: { label: 'Dark', values: { sidebar: '#333333', accent: '#444444' } }
  }
}

describe('resolveTemplateVars', () => {
  it('starts from the template defaults', () => {
    expect(resolveTemplateVars(schema, null)).toEqual({
      accent: '#111111', sidebar: '#222222', chapterColors: false, palette: ['#000000'], degrees: 'accent'
    })
  })

  it('applies preset, then explicit values', () => {
    const vars = resolveTemplateVars(schema, { preset: 'dark', accent: '#abcdef', chapterColors: true })
    expect(vars.sidebar).toBe('#333333')
    expect(vars.accent).toBe('#abcdef')
    expect(vars.chapterColors).toBe(true)
  })

  it('ignores unknown keys and values of the wrong type (no CSS injection)', () => {
    const vars = resolveTemplateVars(schema, {
      accent: 'red; background: url(x)', sidebar: 42, chapterColors: 'yes', palette: ['#fff', 'nope'],
      degrees: 'rainbow', unknown: '#ffffff', preset: 'missing'
    })
    expect(vars).toEqual(resolveTemplateVars(schema, null))
  })

  it('every preset of the registered templates only sets declared, valid vars', () => {
    for (const template of Object.values(printTemplates)) {
      for (const preset of Object.values(template.vars?.presets ?? {})) {
        for (const [key, value] of Object.entries(preset.values)) {
          const resolved = resolveTemplateVars(template.vars, { [key]: value })
          expect(resolved[key], `${template.name}.${key}`).toEqual(value)
        }
      }
    }
  })
})
