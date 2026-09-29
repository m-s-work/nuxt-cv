<script setup lang="ts">
/**
 * Non-blocking notice of the "notice" consent mode (docs/VISITOR_SESSION_TRACKING.md §9.2), for visitors outside
 * the EU/EEA/UK/CH: tracking already runs; the notice says so plainly and offers the details and an opt-out.
 */
defineProps<{ name: string }>()
const emit = defineEmits<{ details: [], 'opt-out': [], close: [] }>()
const { t } = useI18n()

const button = 'rounded-lg px-3 py-1.5 text-sm font-medium ring-1 transition-colors '
  + 'bg-white/10 ring-white/30 hover:bg-white/20 focus-visible:outline-2 focus-visible:outline-white'
</script>

<template>
  <div
    role="region" :aria-label="t('notice.label')" data-testid="tracking-notice"
    class="fixed inset-x-3 bottom-3 z-[100] mx-auto max-w-3xl rounded-xl bg-gray-900/95 text-white shadow-2xl p-4 print:hidden
           sm:flex sm:items-center sm:gap-4"
  >
    <UIcon name="i-lucide-shield-check" class="hidden sm:block size-6 shrink-0 text-blue-300" />
    <p class="text-sm leading-relaxed flex-1">{{ t('notice.text', { name: name || t('notice.theOwner') }) }}</p>
    <div class="mt-3 sm:mt-0 flex gap-2 flex-wrap shrink-0">
      <button type="button" :class="button" @click="emit('details')">{{ t('notice.details') }}</button>
      <button type="button" :class="button" data-testid="notice-opt-out" @click="emit('opt-out')">{{ t('notice.optOut') }}</button>
      <button type="button" :class="button" @click="emit('close')">{{ t('notice.ok') }}</button>
    </div>
  </div>
</template>

<i18n lang="json">
{
  "en": {
    "notice": {
      "label": "Privacy notice",
      "theOwner": "the CV owner",
      "text": "To see which parts of this CV are most helpful, {name} records how it is read – your reading behaviour on this page and technical details of your visit and device. Stored on {name}'s own server, never passed on to third parties.",
      "details": "What exactly is recorded",
      "optOut": "Opt out",
      "ok": "OK"
    }
  },
  "de": {
    "notice": {
      "label": "Datenschutzhinweis",
      "theOwner": "der Inhaber des Lebenslaufs",
      "text": "Damit {name} sieht, welche Teile dieses Lebenslaufs am hilfreichsten sind, wird erfasst, wie er gelesen wird – Ihr Leseverhalten auf dieser Seite und technische Daten Ihres Besuchs und Geräts. Gespeichert auf dem eigenen Server von {name}, niemals an Dritte weitergegeben.",
      "details": "Was genau erfasst wird",
      "optOut": "Widersprechen",
      "ok": "OK"
    }
  }
}
</i18n>
