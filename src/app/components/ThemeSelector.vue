<script setup lang="ts">
const { t } = useI18n()
const colorMode = useColorMode()

const modes = [
  { value: 'light', icon: 'i-heroicons-sun', emoji: '☀️', label: 'light' },
  { value: 'dark', icon: 'i-heroicons-moon', emoji: '🌙', label: 'dark' },
] as const

const cycleColorMode = () => {
  const currentIndex = modes.findIndex(m => m.value === colorMode.preference)
  const nextIndex = (currentIndex + 1) % modes.length
  colorMode.preference = modes[nextIndex].value
}

const currentMode = computed(() => {
  return modes.find(m => m.value === colorMode.preference) || modes[0]
})

const triggerPrint = () => {
  window.print()
}
</script>

<template>
  <div class="theme-selector print:hidden">
    <div class="flex items-center gap-2">
      <!-- Color mode toggle -->
      <button
        @click="cycleColorMode"
        class="p-2 rounded-md bg-gray-200 dark:bg-gray-700 hover:bg-blue-600 hover:text-white dark:hover:bg-blue-600 transition-colors text-lg"
        :title="t('theme.toggle')"
        :aria-label="t('theme.toggle')"
      >
        <UIcon :name="currentMode.icon" class="w-5 h-5" />
        <!-- Fallback emoji when icon doesn't load -->
        <span class="icon-fallback">{{ currentMode.emoji }}</span>
      </button>
      
      <!-- Print mode trigger -->
      <button
        @click="triggerPrint"
        class="p-2 rounded-md bg-gray-200 dark:bg-gray-700 hover:bg-blue-600 hover:text-white dark:hover:bg-blue-600 transition-colors text-lg"
        :title="t('theme.print')"
        :aria-label="t('theme.print')"
      >
        <UIcon name="i-heroicons-printer" class="w-5 h-5" />
        <!-- Fallback emoji when icon doesn't load -->
        <span class="icon-fallback">🖨️</span>
      </button>
    </div>
  </div>
</template>

<style scoped>
.theme-selector {
  position: fixed;
  top: 1rem;
  left: 1rem;
  z-index: 50;
  background-color: rgba(255, 255, 255, 0.95);
  padding: 0.5rem;
  border-radius: 0.5rem;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
}

.dark .theme-selector {
  background-color: rgba(31, 41, 55, 0.95);
}

/* Hide emoji fallback when icon loads successfully */
button:has(.nuxt-icon:not(:empty)) .icon-fallback {
  display: none;
}

/* Show emoji only when icon doesn't load */
.icon-fallback {
  display: inline-block;
  line-height: 1;
}

/* Hide UIcon when it fails to load */
.nuxt-icon:empty {
  display: none;
}

@media print {
  .theme-selector {
    display: none;
  }
}
</style>

<i18n lang="json">
{
  "en": {
    "theme": {
      "toggle": "Toggle theme",
      "print": "Print / Export PDF"
    }
  },
  "de": {
    "theme": {
      "toggle": "Design wechseln",
      "print": "Drucken / PDF exportieren"
    }
  }
}
</i18n>
