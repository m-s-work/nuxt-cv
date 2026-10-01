<script setup lang="ts">
// Footer of the public product pages: pricing and the legal pages (imprint, privacy policy, terms).
const { t } = useI18n()
const localePath = useLocalePath()
const links = [
  { to: '/pricing', key: 'pricing' },
  { to: '/legal/imprint', key: 'imprint' },
  { to: '/legal/privacy', key: 'privacy' },
  { to: '/legal/terms', key: 'terms' }
] as const
</script>

<template>
  <footer class="border-t border-gray-200 dark:border-gray-800 print:hidden" data-testid="public-footer">
    <div class="mx-auto flex max-w-6xl flex-col gap-4 px-4 py-8 text-sm text-gray-500 dark:text-gray-400 sm:flex-row sm:items-center sm:justify-between">
      <p>{{ t('publicFooter.tagline') }}</p>
      <nav :aria-label="t('publicFooter.label')">
        <ul class="flex flex-wrap gap-x-5 gap-y-2">
          <li v-for="link in links" :key="link.key">
            <NuxtLink :to="localePath(link.to)" class="hover:text-gray-900 hover:underline dark:hover:text-white" :data-testid="`footer-${link.key}`">
              {{ t(`publicFooter.${link.key}`) }}
            </NuxtLink>
          </li>
        </ul>
      </nav>
    </div>
  </footer>
</template>

<i18n lang="json">
{
  "en": {
    "publicFooter": {
      "tagline": "Invite-only CVs – share your CV with exactly the people you choose.",
      "label": "Legal and pricing",
      "pricing": "Pricing",
      "imprint": "Imprint",
      "privacy": "Privacy policy",
      "terms": "Terms of service"
    }
  },
  "de": {
    "publicFooter": {
      "tagline": "Lebensläufe nur auf Einladung – teile deinen Lebenslauf mit genau den Personen, die du auswählst.",
      "label": "Rechtliches und Preise",
      "pricing": "Preise",
      "imprint": "Impressum",
      "privacy": "Datenschutzerklärung",
      "terms": "Nutzungsbedingungen"
    }
  }
}
</i18n>
