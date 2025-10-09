<script setup lang="ts">
const { locale, locales } = useI18n()
const switchLocalePath = useSwitchLocalePath()
const { t } = useI18n()

const allLocales = computed(() => {
  return locales.value.map((loc) => ({
    ...loc,
    isActive: loc.code === locale.value
  }))
})
</script>

<template>
  <div class="language-selector space-y-4">
    <h2 class="text-2xl font-bold text-gray-900 dark:text-white print:text-black mb-4">
      {{ t('language.title') }}
    </h2>
    
    <div class="flex flex-wrap gap-2">
      <NuxtLink
        v-for="loc in allLocales"
        :key="loc.code"
        :to="switchLocalePath(loc.code)"
        :class="[
          'inline-flex items-center px-3 py-1.5 rounded-md text-sm font-medium transition-colors',
          loc.isActive 
            ? 'bg-primary-600 text-white dark:bg-primary-500' 
            : 'bg-gray-200 text-gray-700 hover:bg-gray-300 dark:bg-gray-700 dark:text-gray-300 dark:hover:bg-gray-600',
          'print:!bg-gray-100 print:!text-black'
        ]"
      >
        {{ loc.code.toUpperCase() }}
      </NuxtLink>
    </div>
  </div>
</template>

<i18n lang="json">
{
  "en": {
    "language": {
      "title": "Language"
    }
  },
  "de": {
    "language": {
      "title": "Sprache"
    }
  }
}
</i18n>
