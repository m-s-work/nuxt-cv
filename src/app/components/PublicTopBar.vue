<script setup lang="ts">
// Top bar of the public product pages (showcase, pricing, legal). The language selector (app.vue) is fixed
// in the top-right corner, so the bar keeps that space free (right padding).
withDefaults(defineProps<{ dark?: boolean, cta?: boolean }>(), { dark: false, cta: true })

const { t } = useI18n()
const localePath = useLocalePath()
</script>

<template>
  <header
    class="relative z-40 print:hidden" :class="dark ? 'text-white' : 'border-b border-gray-200 bg-white text-gray-900 dark:border-gray-800 dark:bg-gray-900 dark:text-white'"
    data-testid="public-top-bar"
  >
    <div class="mx-auto flex h-[4.5rem] max-w-6xl items-center gap-3 pl-4 pr-[6.5rem] sm:gap-5 sm:pr-36">
      <NuxtLink :to="localePath('/')" class="mr-auto flex items-center gap-2 font-semibold" data-testid="public-home">
        <span
          class="flex h-8 w-8 items-center justify-center rounded-lg text-sm font-bold"
          :class="dark ? 'bg-white text-blue-700' : 'bg-blue-600 text-white'" aria-hidden="true"
        >CV</span>
        <span class="hidden sm:inline">{{ t('topBar.brand') }}</span>
      </NuxtLink>
      <NuxtLink
        :to="localePath('/pricing')" class="hidden text-sm font-medium hover:underline sm:inline"
        :class="dark ? 'text-blue-100 hover:text-white' : 'text-gray-600 hover:text-gray-900 dark:text-gray-300 dark:hover:text-white'"
        data-testid="public-pricing"
      >
        {{ t('topBar.pricing') }}
      </NuxtLink>
      <NuxtLink
        :to="localePath('/login')" class="text-sm font-medium hover:underline"
        :class="dark ? 'text-blue-100 hover:text-white' : 'text-gray-600 hover:text-gray-900 dark:text-gray-300 dark:hover:text-white'"
        data-testid="public-sign-in"
      >
        {{ t('topBar.signIn') }}
      </NuxtLink>
      <NuxtLink
        v-if="cta" :to="localePath('/login')"
        class="hidden rounded-lg px-3 py-1.5 text-sm font-semibold md:inline-block"
        :class="dark ? 'bg-white text-blue-700 hover:bg-blue-50' : 'bg-blue-600 text-white hover:bg-blue-700'"
      >
        {{ t('topBar.create') }}
      </NuxtLink>
    </div>
  </header>
</template>

<i18n lang="json">
{
  "en": {
    "topBar": {
      "brand": "Invite-only CVs",
      "pricing": "Pricing",
      "signIn": "Sign in",
      "create": "Create your CV – free"
    }
  },
  "de": {
    "topBar": {
      "brand": "Lebensläufe nur auf Einladung",
      "pricing": "Preise",
      "signIn": "Anmelden",
      "create": "Lebenslauf erstellen – gratis"
    }
  }
}
</i18n>
