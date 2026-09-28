<script setup lang="ts">
// Print / PDF output. Picks the template the API resolved for this visitor
// (invite > profile > tenant > default, see docs/TEMPLATES.md) and renders it.
// Only visible in print media; the screen layout is hidden there.
import { printTemplates, resolvePrintTemplate } from '~/utils/printTemplates'
import { printLabels, type PrintLocale } from '~/utils/printLabels'
import { resolveTemplateVars } from '~/utils/templateVars'

const { locale } = useI18n()
const { cv, templates } = useCv()

const template = computed(() => printTemplates[resolvePrintTemplate(templates.value?.pdf)])
// Template variables: template defaults → preset → owner's values (validated against the template's schema).
const vars = computed(() => resolveTemplateVars(template.value.vars, templates.value?.pdfVars))

// Footer text for the PDF renderer (pdf/server.mjs reads it for the running footer).
watchEffect(() => {
  const name = cv.value?.profile?.name
  if (import.meta.client && name) {
    const labels = printLabels[(locale.value in printLabels ? locale.value : 'en') as PrintLocale]
    ;(window as unknown as { __CV_PDF_FOOTER__?: string }).__CV_PDF_FOOTER__ = `${name} · ${labels.cv}`
  }
})
</script>

<template>
  <component :is="template.component" :vars="vars" :data-print-template="template.name" />
</template>
