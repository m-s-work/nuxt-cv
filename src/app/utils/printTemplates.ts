import type { Component } from 'vue'
import PrintEditorial from '~/components/print/PrintEditorial.vue'
import PrintClassic from '~/components/print/PrintClassic.vue'

/**
 * Registry of print/PDF templates. To add one: create components/print/Print<Name>.vue
 * (use usePrintData() for the data), register it here and document it in docs/TEMPLATES.md.
 * Names must match ^[a-z0-9][a-z0-9-]{0,31}$ (validated by the API).
 */
export const printTemplates = {
  editorial: {
    name: 'editorial',
    title: 'Editorial',
    description: 'Typeset two-column layout with serif name, photo, sidebar and date gutter.',
    component: PrintEditorial as Component
  },
  classic: {
    name: 'classic',
    title: 'Classic',
    description: 'Single column, black and white, no photo – compact and ATS-friendly.',
    component: PrintClassic as Component
  }
} as const

export type PrintTemplateName = keyof typeof printTemplates

export const defaultPrintTemplate: PrintTemplateName = 'editorial'

/** Unknown or missing names fall back to the default template. */
export function resolvePrintTemplate(name?: string | null): PrintTemplateName {
  return name && name in printTemplates ? name as PrintTemplateName : defaultPrintTemplate
}
