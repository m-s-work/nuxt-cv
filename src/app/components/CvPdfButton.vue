<script setup lang="ts">
// Downloads the PDF of exactly this visitor's view. The API serves it from cache; if the CV changed
// since the last rendering it is rendered on request, which takes a few seconds – hence the message.
const { t, locale } = useI18n()
const { features, cv } = useCv()
const apiBase = useRuntimeConfig().public.apiBase as string

const loading = ref(false)
const failed = ref(false)

async function download() {
  if (loading.value) return
  loading.value = true
  failed.value = false
  try {
    const blob = await $fetch<Blob>(`${apiBase}/pdf`, {
      query: { locale: locale.value },
      responseType: 'blob',
      credentials: 'include'
    })
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = cvPdfFileName(cv.value?.profile?.name, locale.value)
    document.body.appendChild(link)
    link.click()
    link.remove()
    setTimeout(() => URL.revokeObjectURL(url), 10_000)
    trackEvent('pdf', { locale: locale.value, ok: true })
  } catch {
    failed.value = true
    trackEvent('pdf', { locale: locale.value, ok: false })
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div v-if="features.pdf" class="print:hidden space-y-2">
    <button
      type="button"
      :disabled="loading"
      class="w-full inline-flex items-center justify-center gap-2 rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-70"
      @click="download"
    >
      <svg v-if="loading" class="h-4 w-4 animate-spin" fill="none" viewBox="0 0 24 24" aria-hidden="true">
        <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4" />
        <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v4a4 4 0 00-4 4H4z" />
      </svg>
      <svg v-else class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 10v6m0 0l-3-3m3 3l3-3M6 20h12a2 2 0 002-2V8l-6-6H6a2 2 0 00-2 2v14a2 2 0 002 2z" />
      </svg>
      {{ loading ? t('pdf.loading') : t('pdf.download') }}
    </button>
    <p v-if="loading" class="text-xs text-gray-600 dark:text-gray-400" role="status">
      {{ t('pdf.loadingHint') }}
    </p>
    <p v-if="failed" class="text-xs text-red-600 dark:text-red-400" role="alert">
      {{ t('pdf.failed') }}
    </p>
  </div>
</template>

<i18n lang="json">
{
  "en": {
    "pdf": {
      "download": "Download PDF",
      "loading": "Preparing PDF…",
      "loadingHint": "The PDF is being generated for the current version of this CV. This can take a few seconds.",
      "failed": "The PDF could not be generated. Please try again later."
    }
  },
  "de": {
    "pdf": {
      "download": "PDF herunterladen",
      "loading": "PDF wird erstellt…",
      "loadingHint": "Das PDF wird für die aktuelle Version dieses Lebenslaufs erzeugt. Das kann einige Sekunden dauern.",
      "failed": "Das PDF konnte nicht erstellt werden. Bitte versuche es später erneut."
    }
  }
}
</i18n>
