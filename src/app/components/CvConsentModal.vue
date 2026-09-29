<script setup lang="ts">
/**
 * Consent modal for the visitor tracking (docs/VISITOR_SESSION_TRACKING.md §9.1).
 * - Friendly first layer; every recorded data point is listed behind "What exactly is recorded" (R9.17).
 * - Both buttons look the same (R9.21); there is no close button that counts as a choice. Only when the modal is
 *   re-opened from the footer and a choice already exists, it can be closed without changing anything.
 * - Wording changes: bump CONSENT_TEXT_VERSION (utils/tracking.ts) and TrackingPolicy.TextVersion in the API together.
 */
import type { CvConsent } from '~/composables/useCv'

const props = defineProps<{ consent: CvConsent, name: string, reopened?: boolean }>()
const emit = defineEmits<{ accept: [], decline: [], close: [] }>()
const { t } = useI18n()

const showDetails = ref(false)
const dialog = ref<HTMLElement | null>(null)
const text = ref<HTMLElement | null>(null)

const params = computed(() => ({
  name: props.name || t('consent.theOwner'),
  controller: props.consent.controller ?? props.name,
  contact: props.consent.contact ?? '',
  identifiersMonths: props.consent.retention?.identifiersMonths ?? 13,
  summaryMonths: props.consent.retention?.summaryMonths ?? 25
}))
const canClose = computed(() => props.reopened && !!props.consent.state)
const hasSignals = computed(() => !!props.consent.signals)

// Keep keyboard focus inside the dialog; first focus on the text, not on a button (R9.22).
function onKeydown(event: KeyboardEvent) {
  if (event.key === 'Escape' && canClose.value) {
    emit('close')
    return
  }
  if (event.key !== 'Tab' || !dialog.value) return
  const focusable = Array.from(dialog.value.querySelectorAll<HTMLElement>('button, a[href], [tabindex]:not([tabindex="-1"])'))
  if (!focusable.length) return
  const first = focusable[0]!
  const last = focusable[focusable.length - 1]!
  if (event.shiftKey && document.activeElement === first) { last.focus(); event.preventDefault() }
  else if (!event.shiftKey && document.activeElement === last) { first.focus(); event.preventDefault() }
}

onMounted(() => nextTick(() => text.value?.focus()))
</script>

<template>
  <div
    class="fixed inset-0 z-[100] flex items-end sm:items-center justify-center bg-gray-900/40 dark:bg-black/60 backdrop-blur-sm p-4 print:hidden"
    data-testid="consent-modal"
    @keydown="onKeydown"
  >
    <div
      ref="dialog"
      role="dialog"
      aria-modal="true"
      aria-labelledby="consent-title"
      aria-describedby="consent-text"
      class="w-full max-w-lg max-h-[90vh] overflow-y-auto rounded-2xl bg-white dark:bg-gray-900 shadow-2xl ring-1 ring-gray-200 dark:ring-gray-700 p-6 sm:p-8"
    >
      <div class="flex items-start justify-between gap-4">
        <h2 id="consent-title" class="text-xl font-semibold text-gray-900 dark:text-white">{{ t('consent.title') }}</h2>
        <button
          v-if="canClose" type="button" class="text-gray-400 hover:text-gray-600 dark:hover:text-gray-200"
          :aria-label="t('consent.close')" @click="emit('close')"
        >
          <UIcon name="i-lucide-x" class="size-5" />
        </button>
      </div>

      <div id="consent-text" ref="text" tabindex="-1" class="mt-4 space-y-3 text-gray-700 dark:text-gray-300 leading-relaxed outline-none">
        <p>{{ t('consent.intro', params) }}</p>
        <p>{{ t('consent.stays', params) }}</p>
        <p v-if="hasSignals" class="text-sm text-gray-500 dark:text-gray-400">{{ t('consent.signals') }}</p>
      </div>

      <!-- The two real promises about the data, illustrated (R9.20: bundled icons only). -->
      <ul class="mt-5 grid grid-cols-1 sm:grid-cols-2 gap-3">
        <li class="flex items-center gap-3 rounded-xl bg-gray-50 dark:bg-gray-800 px-4 py-3">
          <UIcon name="i-lucide-server" class="size-6 shrink-0 text-primary" />
          <span class="text-sm font-medium text-gray-800 dark:text-gray-200">{{ t('consent.pointServer', params) }}</span>
        </li>
        <li class="flex items-center gap-3 rounded-xl bg-gray-50 dark:bg-gray-800 px-4 py-3">
          <UIcon name="i-lucide-shield-check" class="size-6 shrink-0 text-primary" />
          <span class="text-sm font-medium text-gray-800 dark:text-gray-200">{{ t('consent.pointThirdParties') }}</span>
        </li>
      </ul>

      <!-- Equal buttons: same size, colour and weight (R9.21). -->
      <div class="mt-6 grid grid-cols-1 sm:grid-cols-2 gap-3">
        <UButton
          color="primary" variant="soft" size="lg" block icon="i-lucide-check"
          :label="t('consent.accept')" data-testid="consent-accept" @click="emit('accept')"
        />
        <UButton
          color="primary" variant="soft" size="lg" block icon="i-lucide-arrow-right"
          :label="t('consent.decline')" data-testid="consent-decline" @click="emit('decline')"
        />
      </div>

      <button
        type="button"
        class="mt-5 inline-flex items-center gap-1 text-sm text-primary hover:underline"
        :aria-expanded="showDetails"
        aria-controls="consent-details"
        @click="showDetails = !showDetails"
      >
        {{ t('consent.details') }}
        <UIcon :name="showDetails ? 'i-lucide-chevron-up' : 'i-lucide-chevron-down'" class="size-4" />
      </button>

      <div v-if="showDetails" id="consent-details" class="mt-4 space-y-4 text-sm text-gray-600 dark:text-gray-400" data-testid="consent-details">
        <section>
          <h3 class="font-semibold text-gray-800 dark:text-gray-200">{{ t('consent.whatTitle') }}</h3>
          <ul class="mt-1 list-disc pl-5 space-y-1">
            <li>{{ t('consent.whatReading') }}</li>
            <li>{{ t('consent.whatVisit') }}</li>
            <li>{{ t('consent.whatTechnical') }}</li>
          </ul>
        </section>
        <section>
          <h3 class="font-semibold text-gray-800 dark:text-gray-200">{{ t('consent.whyTitle') }}</h3>
          <p>{{ t('consent.why', params) }}</p>
        </section>
        <section>
          <h3 class="font-semibold text-gray-800 dark:text-gray-200">{{ t('consent.whoTitle') }}</h3>
          <p>{{ t('consent.who', params) }}</p>
        </section>
        <section>
          <h3 class="font-semibold text-gray-800 dark:text-gray-200">{{ t('consent.howLongTitle') }}</h3>
          <p>{{ t('consent.howLong', params) }}</p>
        </section>
        <section>
          <h3 class="font-semibold text-gray-800 dark:text-gray-200">{{ t('consent.legalTitle') }}</h3>
          <p>{{ t('consent.legal', params) }}</p>
        </section>
      </div>
    </div>
  </div>
</template>

<i18n lang="json">
{
  "en": {
    "consent": {
      "theOwner": "the CV owner",
      "title": "Welcome!",
      "intro": "{name} is glad you are taking a look. To find out which parts of this CV are most helpful to readers like you, {name} would like to learn how it is read – your reading behaviour on this page and a few technical details of your visit and device.",
      "stays": "Everything stays with {name}: it is stored on {name}'s own server and never passed on to third parties. You can read the CV either way and change your choice any time.",
      "signals": "Your browser asks websites not to track you – you decide here.",
      "pointServer": "Stays on {name}'s server",
      "pointThirdParties": "Never shared with third parties",
      "accept": "Accept",
      "decline": "Continue without accepting",
      "close": "Close",
      "details": "What exactly is recorded",
      "whatTitle": "What is recorded",
      "whatReading": "How you read: time on the page and in each section, scrolling, clicks, mouse movement, text you select or copy (only its length, never the text), opened images and links, PDF download and printing.",
      "whatVisit": "Your visit: the invite link you used, date and time, language, screen size, device type, browser and operating system.",
      "whatTechnical": "Technical details: your IP address with approximate location and network provider, and a characteristic of your browser and device (a so-called fingerprint) to recognise a returning visit.",
      "whyTitle": "Why",
      "why": "So {name} can see which content matters to readers and improve the CV.",
      "whoTitle": "Who",
      "who": "{controller}, {contact}. Stored on {name}'s own server; never passed on to third parties.",
      "howLongTitle": "How long",
      "howLong": "Details of your visit and device: {identifiersMonths} months after your last visit; summaries: {summaryMonths} months. Your accept/decline choice is kept (invite and time only) as proof.",
      "legalTitle": "Legal basis",
      "legal": "Your consent (Art. 6(1)(a) GDPR). You can withdraw it at any time via \"Privacy\" at the bottom of the page; you may request access to or deletion of your data from {contact} and complain to a data protection authority."
    }
  },
  "de": {
    "consent": {
      "theOwner": "der Inhaber des Lebenslaufs",
      "title": "Willkommen!",
      "intro": "Schön, dass Sie vorbeischauen. Um herauszufinden, welche Teile dieses Lebenslaufs für Leser wie Sie am hilfreichsten sind, möchte {name} erfahren, wie er gelesen wird – Ihr Leseverhalten auf dieser Seite und einige technische Daten Ihres Besuchs und Geräts.",
      "stays": "Alles bleibt bei {name}: Die Daten liegen auf dem eigenen Server von {name} und werden niemals an Dritte weitergegeben. Sie können den Lebenslauf in jedem Fall lesen und Ihre Wahl jederzeit ändern.",
      "signals": "Ihr Browser bittet Websites, Sie nicht zu verfolgen – Sie entscheiden hier.",
      "pointServer": "Bleibt auf dem Server von {name}",
      "pointThirdParties": "Nie an Dritte weitergegeben",
      "accept": "Zustimmen",
      "decline": "Ohne Zustimmung fortfahren",
      "close": "Schließen",
      "details": "Was genau erfasst wird",
      "whatTitle": "Was erfasst wird",
      "whatReading": "Wie Sie lesen: Verweildauer auf der Seite und in den Abschnitten, Scrollen, Klicks, Mausbewegungen, markierter oder kopierter Text (nur die Länge, nie der Text), geöffnete Bilder und Links, PDF-Download und Drucken.",
      "whatVisit": "Ihr Besuch: der verwendete Einladungslink, Datum und Uhrzeit, Sprache, Bildschirmgröße, Gerätetyp, Browser und Betriebssystem.",
      "whatTechnical": "Technische Daten: Ihre IP-Adresse mit ungefährem Standort und Netzanbieter sowie ein Merkmal Ihres Browsers und Geräts (ein sogenannter Fingerprint), um einen erneuten Besuch zu erkennen.",
      "whyTitle": "Warum",
      "why": "Damit {name} sieht, welche Inhalte für Leser wichtig sind, und den Lebenslauf verbessern kann.",
      "whoTitle": "Wer",
      "who": "{controller}, {contact}. Gespeichert auf dem eigenen Server von {name}; niemals an Dritte weitergegeben.",
      "howLongTitle": "Wie lange",
      "howLong": "Daten zu Besuch und Gerät: {identifiersMonths} Monate nach Ihrem letzten Besuch; Zusammenfassungen: {summaryMonths} Monate. Ihre Wahl (Zustimmung oder Ablehnung) wird als Nachweis aufbewahrt (nur Einladung und Zeitpunkt).",
      "legalTitle": "Rechtsgrundlage",
      "legal": "Ihre Einwilligung (Art. 6 Abs. 1 lit. a DSGVO). Sie können sie jederzeit über \"Datenschutz\" am Ende der Seite widerrufen; Auskunft oder Löschung Ihrer Daten erhalten Sie über {contact}, außerdem können Sie sich bei einer Datenschutzbehörde beschweren."
    }
  }
}
</i18n>
