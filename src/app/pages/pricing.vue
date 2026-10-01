<script setup lang="ts">
// Public pricing: Free vs Pro passes (docs/REQUIREMENTS_SAAS.md §4). Buying happens in the dashboard after
// signing in, so every button leads to /login. Public page: no analytics features (R11.4, S4.4).
const { t } = useI18n()
const localePath = useLocalePath()
useSplashScreen().hideSplash()

useSeoMeta({
  title: () => t('pricingPage.metaTitle'),
  description: () => t('pricingPage.metaDescription')
})

const faq = ['prepaid', 'end', 'payment', 'free'] as const
</script>

<template>
  <div class="min-h-screen bg-gray-50 text-gray-900 dark:bg-gray-900 dark:text-white">
    <PublicTopBar />
    <main class="mx-auto max-w-6xl px-4 py-12 lg:py-16">
      <div class="mb-10 max-w-3xl">
        <h1 class="text-4xl font-bold">{{ t('pricingPage.title') }}</h1>
        <p class="mt-3 text-lg text-gray-600 dark:text-gray-400">{{ t('pricingPage.subtitle') }}</p>
      </div>
      <PricingPlans />

      <section class="mt-16 max-w-3xl">
        <h2 class="mb-6 text-2xl font-bold">{{ t('pricingPage.faqTitle') }}</h2>
        <dl class="space-y-6">
          <div v-for="item in faq" :key="item">
            <dt class="font-semibold">{{ t(`pricingPage.faq.${item}.q`) }}</dt>
            <dd class="mt-1 text-gray-600 dark:text-gray-400">{{ t(`pricingPage.faq.${item}.a`) }}</dd>
          </div>
        </dl>
        <p class="mt-8 text-sm text-gray-600 dark:text-gray-400">
          <NuxtLink :to="localePath('/')" class="text-blue-700 hover:underline dark:text-blue-300">{{ t('pricingPage.back') }}</NuxtLink>
          ·
          <NuxtLink :to="localePath('/legal/terms')" class="text-blue-700 hover:underline dark:text-blue-300">{{ t('pricingPage.terms') }}</NuxtLink>
        </p>
      </section>
    </main>
    <PublicFooter />
  </div>
</template>

<i18n lang="json">
{
  "en": {
    "pricingPage": {
      "metaTitle": "Pricing",
      "metaDescription": "Free forever, or Pro as a prepaid pass – no subscription, no automatic renewal.",
      "title": "Simple pricing",
      "subtitle": "Start for free. Get Pro for as long as you need it – as a prepaid pass, without a subscription.",
      "faqTitle": "Questions",
      "faq": {
        "prepaid": {
          "q": "Is Pro a subscription?",
          "a": "No. A pass is a one-time payment for a fixed number of days. It does not renew automatically, so there is nothing to cancel. Buying another pass while Pro is active adds its days to your remaining time."
        },
        "end": {
          "q": "What happens when my pass ends?",
          "a": "Nothing is deleted. Your CV stays online and existing invite links keep working until they expire. Pro extras such as your own domain and removing the PDF credit pause until you buy another pass, and new invite links can be created as long as you are within the free limit."
        },
        "payment": {
          "q": "How do I pay?",
          "a": "Sign in and buy a pass in your dashboard. Payment is handled by Paddle, our reseller and merchant of record: Paddle processes the payment, sends the invoice and handles refunds. We never see your card details. Prices include VAT."
        },
        "free": {
          "q": "Is the free plan really free?",
          "a": "Yes – no credit card, no time limit. It includes all templates, PDF export, privacy profiles and several languages."
        }
      },
      "back": "Back to the start page",
      "terms": "Terms of service"
    }
  },
  "de": {
    "pricingPage": {
      "metaTitle": "Preise",
      "metaDescription": "Für immer gratis, oder Pro als vorausbezahlter Pass – kein Abo, keine automatische Verlängerung.",
      "title": "Einfache Preise",
      "subtitle": "Starte gratis. Hol dir Pro, solange du es brauchst – als vorausbezahlten Pass, ohne Abo.",
      "faqTitle": "Fragen",
      "faq": {
        "prepaid": {
          "q": "Ist Pro ein Abo?",
          "a": "Nein. Ein Pass ist eine Einmalzahlung für eine feste Anzahl an Tagen. Er verlängert sich nicht automatisch, es gibt also nichts zu kündigen. Ein weiterer Pass während einer aktiven Pro-Zeit hängt seine Tage an die verbleibende Zeit an."
        },
        "end": {
          "q": "Was passiert, wenn mein Pass endet?",
          "a": "Es wird nichts gelöscht. Dein Lebenslauf bleibt online und bestehende Einladungslinks funktionieren bis zu ihrem Ablauf weiter. Pro-Extras wie die eigene Domain oder das Entfernen des PDF-Hinweises pausieren bis zum nächsten Pass, und neue Einladungslinks kannst du innerhalb des Gratis-Limits anlegen."
        },
        "payment": {
          "q": "Wie bezahle ich?",
          "a": "Melde dich an und kaufe einen Pass in deinem Dashboard. Die Zahlung wickelt Paddle ab, unser Wiederverkäufer (Merchant of Record): Paddle verarbeitet die Zahlung, stellt die Rechnung aus und kümmert sich um Rückerstattungen. Deine Kartendaten sehen wir nie. Preise inkl. USt."
        },
        "free": {
          "q": "Ist der Gratis-Plan wirklich gratis?",
          "a": "Ja – ohne Kreditkarte und ohne Zeitlimit. Er enthält alle Vorlagen, PDF-Export, Datenschutz-Profile und mehrere Sprachen."
        }
      },
      "back": "Zurück zur Startseite",
      "terms": "Nutzungsbedingungen"
    }
  }
}
</i18n>
