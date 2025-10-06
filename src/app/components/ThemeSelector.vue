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
        class="p-2 rounded-md hover:bg-blue-600 hover:bg-opacity-20 transition-colors text-2xl"
        :title="t('theme.toggle')"
        :aria-label="t('theme.toggle')"
      >
        {{ currentMode.emoji }}
      </button>
      
      <!-- Print mode trigger -->
      <button
        @click="triggerPrint"
        class="p-2 rounded-md hover:bg-blue-600 hover:bg-opacity-20 transition-colors text-2xl"
        :title="t('theme.print')"
        :aria-label="t('theme.print')"
      >
        🖨️
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
