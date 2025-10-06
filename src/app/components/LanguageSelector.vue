<script setup lang="ts">
const { locale, locales } = useI18n()
const switchLocalePath = useSwitchLocalePath()

const availableLocales = computed(() => {
  return locales.value.filter((i) => i.code !== locale.value)
})

const currentLocale = computed(() => {
  return locales.value.find((i) => i.code === locale.value)
})

// For better UX with only 2 languages, make entire selector clickable
const isTwoLanguages = computed(() => locales.value.length === 2)
const nextLocale = computed(() => availableLocales.value[0])
</script>

<template>
  <div class="language-selector print:hidden">
    <!-- Single clickable element when there are only 2 languages -->
    <NuxtLink
      v-if="isTwoLanguages && nextLocale"
      :to="switchLocalePath(nextLocale.code)"
      class="flex items-center gap-2 px-3 py-2 rounded-md bg-gray-200 dark:bg-gray-700 hover:bg-blue-600 hover:text-white dark:hover:bg-blue-600 transition-colors cursor-pointer"
    >
      <span class="text-sm font-medium">
        {{ currentLocale?.code.toUpperCase() }}
      </span>
      <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 7h12m0 0l-4-4m4 4l-4 4m0 6H4m0 0l4 4m-4-4l4-4"></path>
      </svg>
      <span class="text-sm font-medium">
        {{ nextLocale.code.toUpperCase() }}
      </span>
    </NuxtLink>
    
    <!-- Multiple language selector for 3+ languages -->
    <div v-else class="flex items-center gap-2">
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
  right: 1rem;
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
