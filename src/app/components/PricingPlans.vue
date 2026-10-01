<script setup lang="ts">
// Free vs Pro and the prepaid Pro passes (docs/REQUIREMENTS_SAAS.md §4). Public: lists public features only –
// analytics features are never mentioned here (REQUIREMENTS_ACCESS_AND_TENANCY.md R11.4, SAAS S4.4).
// compact: plan cards only, with "from … / week" and a link to /pricing (used on the showcase).
import { formatMoney, priceRows } from '~/utils/pricing'

const props = withDefaults(defineProps<{ compact?: boolean }>(), { compact: false })

const { t, locale } = useI18n()
const localePath = useLocalePath()
const { config, loaded } = useBillingConfig()

const rows = computed(() => priceRows(config.value.passes, locale.value))
const cheapest = computed(() => [...rows.value].sort((a, b) => a.perWeek - b.perWeek)[0])
const freePrice = computed(() => formatMoney(0, config.value.passes[0]?.currency ?? 'EUR', locale.value, { whole: true }))

const freeFeatures = computed(() => [
  t('pricing.free.cv'),
  t('pricing.free.pdf'),
  t('pricing.free.privacy'),
  t('pricing.free.languages'),
  t('pricing.free.invites', { n: config.value.freeMaxActiveInvites }),
  t('pricing.free.storage')
])
const proFeatures = computed(() => [
  t('pricing.pro.invites'),
  t('pricing.pro.domain'),
  t('pricing.pro.credit'),
  t('pricing.pro.storage')
])

function passLabel(row: { period: string | null, days: number }) {
  return row.period ? t(`pricing.period.${row.period}`) : t('pricing.period.days', { n: row.days })
}

const check = 'M5 13l4 4L19 7'
</script>

<template>
  <div class="space-y-10" data-testid="pricing-plans">
    <div class="grid gap-6 md:grid-cols-2">
      <!-- Free -->
      <div class="flex flex-col rounded-2xl border border-gray-200 bg-white p-6 shadow-sm dark:border-gray-700 dark:bg-gray-800" data-testid="plan-free">
        <h3 class="text-xl font-bold">{{ t('pricing.free.title') }}</h3>
        <p class="mt-1 text-sm text-gray-600 dark:text-gray-400">{{ t('pricing.free.subtitle') }}</p>
        <p class="mt-4 text-4xl font-bold">{{ freePrice }}</p>
        <ul class="mt-6 flex-1 space-y-2 text-sm">
          <li v-for="feature in freeFeatures" :key="feature" class="flex gap-2">
            <svg class="mt-0.5 h-4 w-4 shrink-0 text-green-600" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" :d="check" /></svg>
            <span>{{ feature }}</span>
          </li>
        </ul>
        <NuxtLink
          :to="localePath('/login')" data-testid="plan-free-cta"
          class="mt-6 block rounded-lg bg-blue-600 px-4 py-2.5 text-center font-semibold text-white hover:bg-blue-700"
        >
          {{ t('pricing.free.cta') }}
        </NuxtLink>
      </div>

      <!-- Pro -->
      <div class="flex flex-col rounded-2xl border-2 border-blue-600 bg-white p-6 shadow-sm dark:bg-gray-800" data-testid="plan-pro">
        <h3 class="text-xl font-bold">{{ t('pricing.pro.title') }}</h3>
        <p class="mt-1 text-sm text-gray-600 dark:text-gray-400">{{ t('pricing.pro.subtitle') }}</p>
        <p class="mt-4 transition-opacity" :class="loaded ? 'opacity-100' : 'opacity-60'">
          <span class="text-sm text-gray-600 dark:text-gray-400">{{ t('pricing.from') }}</span>
          <span class="mx-1 text-4xl font-bold" data-testid="pro-from">{{ cheapest?.perWeekText }}</span>
          <span class="text-sm text-gray-600 dark:text-gray-400">{{ t('pricing.perWeek') }}</span>
        </p>
        <p class="mt-6 text-sm font-medium">{{ t('pricing.pro.everything') }}</p>
        <ul class="mt-2 flex-1 space-y-2 text-sm">
          <li v-for="feature in proFeatures" :key="feature" class="flex gap-2">
            <svg class="mt-0.5 h-4 w-4 shrink-0 text-blue-600 dark:text-blue-400" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" :d="check" /></svg>
            <span>{{ feature }}</span>
          </li>
        </ul>
        <NuxtLink
          v-if="props.compact" :to="localePath('/pricing')" data-testid="plan-pro-details"
          class="mt-6 block rounded-lg border border-blue-600 px-4 py-2.5 text-center font-semibold text-blue-700 hover:bg-blue-50 dark:text-blue-300 dark:hover:bg-blue-950"
        >
          {{ t('pricing.seePasses') }}
        </NuxtLink>
        <p v-else class="mt-6 text-sm text-gray-600 dark:text-gray-400">{{ t('pricing.choosePass') }}</p>
      </div>
    </div>

    <!-- Pro passes -->
    <div v-if="!props.compact" class="space-y-4">
      <div class="grid gap-4 sm:grid-cols-2 lg:grid-cols-4" :class="loaded ? 'opacity-100' : 'opacity-60'">
        <div
          v-for="row in rows" :key="row.id" :data-testid="`pass-${row.id}`"
          class="relative flex flex-col rounded-xl border border-gray-200 bg-white p-5 text-center dark:border-gray-700 dark:bg-gray-800"
        >
          <span
            v-if="row.saving" class="absolute -top-3 left-1/2 -translate-x-1/2 rounded-full bg-green-600 px-2.5 py-0.5 text-xs font-semibold text-white"
          >{{ t('pricing.save', { n: row.saving }) }}</span>
          <h4 class="font-semibold">{{ passLabel(row) }}</h4>
          <p class="mt-3">
            <span class="text-3xl font-bold" data-testid="pass-per-week">{{ row.perWeekText }}</span>
            <span class="text-sm text-gray-600 dark:text-gray-400"> {{ t('pricing.perWeek') }}</span>
          </p>
          <p class="mt-1 text-sm text-gray-500 dark:text-gray-400" data-testid="pass-total">
            {{ t('pricing.total', { price: row.totalText }) }}
          </p>
          <NuxtLink
            :to="localePath('/login')"
            class="mt-4 block rounded-lg bg-blue-600 px-3 py-2 text-sm font-semibold text-white hover:bg-blue-700"
          >
            {{ t('pricing.buy') }}
          </NuxtLink>
        </div>
      </div>
      <p class="text-sm text-gray-600 dark:text-gray-400" data-testid="pricing-prepaid">{{ t('pricing.prepaid') }}</p>
    </div>
  </div>
</template>

<i18n lang="json">
{
  "en": {
    "pricing": {
      "from": "from",
      "perWeek": "/ week",
      "save": "save {n} %",
      "total": "{price} in total",
      "buy": "Get Pro",
      "seePasses": "See Pro passes",
      "choosePass": "Choose a pass below – you buy it in your dashboard after signing in.",
      "prepaid": "Prepaid passes: no subscription, no automatic renewal, nothing to cancel. Buying another pass extends your Pro time. Prices include VAT; payment is handled by our reseller Paddle. When a pass ends, your CV and invite links stay – only the Pro extras pause.",
      "period": {
        "week": "1 week",
        "month": "1 month",
        "halfYear": "6 months",
        "year": "1 year",
        "days": "{n} days"
      },
      "free": {
        "title": "Free",
        "subtitle": "Forever – no credit card needed.",
        "cv": "Your CV online, with all templates",
        "pdf": "PDF export",
        "privacy": "Privacy profiles: decide per invite what is visible",
        "languages": "Several languages",
        "invites": "Up to {n} active invite links",
        "storage": "20 MB for photos and logos",
        "cta": "Create your CV – free"
      },
      "pro": {
        "title": "Pro",
        "subtitle": "For an active job search.",
        "everything": "Everything in Free, plus:",
        "invites": "Unlimited invite links",
        "domain": "Your own domain",
        "credit": "Remove the “Created with …” credit from PDFs",
        "storage": "200 MB storage"
      }
    }
  },
  "de": {
    "pricing": {
      "from": "ab",
      "perWeek": "/ Woche",
      "save": "{n} % sparen",
      "total": "{price} insgesamt",
      "buy": "Pro holen",
      "seePasses": "Pro-Pässe ansehen",
      "choosePass": "Wähle unten einen Pass – gekauft wird er nach der Anmeldung in deinem Dashboard.",
      "prepaid": "Vorausbezahlte Pässe: kein Abo, keine automatische Verlängerung, nichts zu kündigen. Ein weiterer Pass verlängert deine Pro-Zeit. Preise inkl. USt.; die Zahlung wickelt unser Wiederverkäufer Paddle ab. Endet ein Pass, bleiben dein Lebenslauf und deine Einladungslinks – nur die Pro-Extras pausieren.",
      "period": {
        "week": "1 Woche",
        "month": "1 Monat",
        "halfYear": "6 Monate",
        "year": "1 Jahr",
        "days": "{n} Tage"
      },
      "free": {
        "title": "Gratis",
        "subtitle": "Für immer – ohne Kreditkarte.",
        "cv": "Dein Lebenslauf online, mit allen Vorlagen",
        "pdf": "PDF-Export",
        "privacy": "Datenschutz-Profile: pro Einladung festlegen, was sichtbar ist",
        "languages": "Mehrere Sprachen",
        "invites": "Bis zu {n} aktive Einladungslinks",
        "storage": "20 MB für Fotos und Logos",
        "cta": "Lebenslauf erstellen – gratis"
      },
      "pro": {
        "title": "Pro",
        "subtitle": "Für die aktive Jobsuche.",
        "everything": "Alles aus Gratis, dazu:",
        "invites": "Unbegrenzt viele Einladungslinks",
        "domain": "Eigene Domain",
        "credit": "Hinweis „Erstellt mit …“ aus PDFs entfernen",
        "storage": "200 MB Speicher"
      }
    }
  }
}
</i18n>
