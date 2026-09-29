<script setup lang="ts">
/**
 * Shown to the visitor of a view-once invite: the link is used up, only this browser can still
 * read the CV until the grace window ends (docs/REQUIREMENTS_ACCESS_AND_TENANCY.md §4).
 */
const props = defineProps<{ until: string }>()
const { t, locale } = useI18n()
const open = ref(true)

const time = computed(() => new Date(props.until).toLocaleTimeString(locale.value, { hour: '2-digit', minute: '2-digit' }))
</script>

<template>
  <div
    v-if="open" role="status" data-testid="view-once-notice"
    class="fixed inset-x-3 top-3 z-[100] mx-auto max-w-xl rounded-xl bg-amber-50 text-amber-900 ring-1 ring-amber-200 shadow-lg
           dark:bg-amber-950/95 dark:text-amber-100 dark:ring-amber-800 px-4 py-3 print:hidden flex items-start gap-3"
  >
    <UIcon name="i-lucide-eye" class="size-5 shrink-0 mt-0.5" />
    <p class="text-sm leading-relaxed flex-1">{{ t('viewOnce.text', { time }) }}</p>
    <button
      type="button" class="shrink-0 rounded p-0.5 hover:bg-amber-100 dark:hover:bg-amber-900" :aria-label="t('viewOnce.close')"
      @click="open = false"
    >
      <UIcon name="i-lucide-x" class="size-4" />
    </button>
  </div>
</template>

<i18n lang="json">
{
  "en": {
    "viewOnce": {
      "text": "This link could only be opened once. You can view this CV in this browser until {time}; after that, and on other devices, the link no longer works.",
      "close": "Close"
    }
  },
  "de": {
    "viewOnce": {
      "text": "Dieser Link konnte nur einmal geöffnet werden. Du kannst den Lebenslauf in diesem Browser bis {time} ansehen; danach und auf anderen Geräten funktioniert der Link nicht mehr.",
      "close": "Schließen"
    }
  }
}
</i18n>
