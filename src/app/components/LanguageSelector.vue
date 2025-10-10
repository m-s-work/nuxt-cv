<script setup lang="ts">
const { locale, locales } = useI18n()
const switchLocalePath = useSwitchLocalePath()

const availableLocales = computed(() => {
  return locales.value.filter((i) => i.code !== locale.value)
})

const currentLocale = computed(() => {
  return locales.value.find((i) => i.code === locale.value)
})
</script>

<template>
  <div class="language-selector print:hidden">
    <div class="flex items-center gap-2">
      <!-- Current language display -->
      <span class="text-sm font-medium text-gray-700 dark:text-gray-300">
        {{ currentLocale?.code.toUpperCase() }}
      </span>
      
      <!-- Language switcher buttons -->
      <div class="flex gap-1">
        <NuxtLink
          v-for="locale in availableLocales"
          :key="locale.code"
          :to="switchLocalePath(locale.code)"
          class="px-2 py-1 text-sm rounded-md bg-gray-200 dark:bg-gray-700 hover:bg-blue-600 hover:text-white dark:hover:bg-blue-600 transition-colors"
        >
          {{ locale.code.toUpperCase() }}
        </NuxtLink>
      </div>
    </div>
  </div>
</template>

<style scoped>
.language-selector {
  position: fixed;
  top: 1rem;
  left: 1rem;
  z-index: 50;
  background-color: rgba(255, 255, 255, 0.95);
  padding: 0.5rem 1rem;
  border-radius: 0.5rem;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
}

.dark .language-selector {
  background-color: rgba(31, 41, 55, 0.95);
}

@media print {
  .language-selector {
    display: none;
  }
}
</style>

<i18n lang="json">
{
  "en": {
    "language": {
      "switch": "Switch Language"
    }
  },
  "de": {
    "language": {
      "switch": "Sprache wechseln"
    }
  }
}
</i18n>
