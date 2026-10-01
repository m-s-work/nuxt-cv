import type { Component } from 'vue'
import type { TemplateVarDef, TemplateVarsSchema } from '~/utils/templateVars'
import PrintEditorial from '~/components/print/PrintEditorial.vue'
import PrintClassic from '~/components/print/PrintClassic.vue'
import PrintBanner from '~/components/print/PrintBanner.vue'

const accentSuggestions = ['#1d4ed8', '#29a8e0', '#0f766e', '#b45309', '#be123c', '#6d28d9']

/** Website links of entries, shared by all templates (composables/usePrintLinks.ts). */
const links = {
  type: 'enum',
  default: 'qr',
  label: 'Website links',
  options: ['qr', 'tracked', 'clear', 'off'],
  optionLabels: {
    qr: 'Address + QR code (tracked)',
    tracked: 'Address, tracked link',
    clear: 'Address, untracked',
    off: 'Not shown'
  }
} as const satisfies TemplateVarDef

/**
 * Registry of print/PDF templates. To add one: create components/print/Print<Name>.vue
 * (use usePrintData() for the data), register it here and document it in docs/TEMPLATES.md.
 * preview: first page with the sample tenant, shown when hovering the template in the admin Design tab;
 * regenerate with `node scripts/template-previews.mjs` (see docs/TEMPLATES.md).
 * Names must match ^[a-z0-9][a-z0-9-]{0,31}$ (validated by the API).
 */
export const printTemplates = {
  editorial: {
    name: 'editorial',
    title: 'Editorial',
    description: 'Typeset two-column layout with serif name, photo, sidebar and date gutter.',
    preview: '/templates/editorial.jpg',
    component: PrintEditorial as Component,
    vars: {
      vars: {
        accent: { type: 'color', default: '#1d4ed8', label: 'Accent colour', suggestions: accentSuggestions },
        links
      }
    } satisfies TemplateVarsSchema
  },
  classic: {
    name: 'classic',
    title: 'Classic',
    description: 'Single column, black and white, no photo – compact and ATS-friendly.',
    preview: '/templates/classic.jpg',
    component: PrintClassic as Component,
    vars: { vars: { links } } satisfies TemplateVarsSchema
  },
  banner: {
    name: 'banner',
    title: 'Banner',
    description: 'Dark header band, round photo, full-height navy sidebar, accent initials – full bleed.',
    preview: '/templates/banner.jpg',
    component: PrintBanner as Component,
    vars: {
      vars: {
        accent: { type: 'color', default: '#29a8e0', label: 'Accent colour', suggestions: accentSuggestions },
        sidebar: { type: 'color', default: '#1f3864', label: 'Sidebar', suggestions: ['#1f3864', '#3f4247', '#1e4636', '#5b1f2e', '#efefef'] },
        sidebarText: { type: 'color', default: '#e8edf6', label: 'Sidebar text', suggestions: ['#e8edf6', '#ffffff', '#2b2b2b'] },
        band: { type: 'color', default: '#3b3b3d', label: 'Header band', suggestions: ['#3b3b3d', '#26282b', '#1f3864', '#444444'] },
        chapterColors: { type: 'boolean', default: false, label: 'Different accent colour per chapter' },
        chapterPalette: { type: 'palette', default: ['#29a8e0', '#e8639a', '#f0b429', '#3fb68b', '#8b6cd9'], label: 'Chapter colours' },
        degreesStyle: { type: 'enum', default: 'accent', options: ['accent', 'gradient', 'plain'], label: 'Academic degrees' },
        links
      },
      presets: {
        navy: { label: 'Navy', values: { sidebar: '#1f3864', sidebarText: '#e8edf6', band: '#3b3b3d', accent: '#29a8e0' } },
        graphite: { label: 'Graphite', values: { sidebar: '#3f4247', sidebarText: '#eceef1', band: '#26282b', accent: '#29a8e0' } },
        forest: { label: 'Forest', values: { sidebar: '#1e4636', sidebarText: '#e9f2ec', band: '#2f3a34', accent: '#e0a526' } },
        burgundy: { label: 'Burgundy', values: { sidebar: '#5b1f2e', sidebarText: '#f6e9ec', band: '#2e2a2b', accent: '#d9a441' } },
        light: { label: 'Light', values: { sidebar: '#efefef', sidebarText: '#2b2b2b', band: '#444444', accent: '#29a8e0' } }
      }
    } satisfies TemplateVarsSchema
  }
} as const

export type PrintTemplateName = keyof typeof printTemplates

export const defaultPrintTemplate: PrintTemplateName = 'editorial'

/** Unknown or missing names fall back to the default template. */
export function resolvePrintTemplate(name?: string | null): PrintTemplateName {
  return name && name in printTemplates ? name as PrintTemplateName : defaultPrintTemplate
}
