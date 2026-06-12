<script setup lang="ts">
const { locale, locales } = useI18n()
const switchLocalePath = useSwitchLocalePath()
const colorMode = useColorMode()

// Switch language without triggering navigation
function switchLanguage(newLocale: string) {
  if (locale.value === newLocale) return
  
  // Get current route info to preserve query and hash
  const route = useRoute()
  
  // Get the path for the new locale
  const newPath = switchLocalePath(newLocale)
  
  // Build the new URL with query params and hash
  let newUrl = newPath
  const queryString = new URLSearchParams(route.query as Record<string, string>).toString()
  if (queryString) {
    newUrl += '?' + queryString
  }
  
  // Preserve hash from current location
  // window.location.hash includes the # symbol
  const currentHash = window.location.hash
  if (currentHash && !newUrl.endsWith(currentHash)) {
    newUrl += currentHash
  }
  
  // Update the URL without triggering navigation (push to history)
  window.history.pushState({}, '', newUrl)
  
  // Update the locale (this will update all i18n content)
  locale.value = newLocale
}

// Toggle color mode
function toggleColorMode() {
  colorMode.preference = colorMode.value === 'dark' ? 'light' : 'dark'
}
</script>

<template>
  <div class="language-selector print:hidden">
    <div class="controls-container">
      <!-- Dark/Light mode toggle -->
      <button
        @click="toggleColorMode"
        class="mode-btn"
        :aria-label="colorMode.value === 'dark' ? 'Switch to light mode' : 'Switch to dark mode'"
      >
        <span v-if="colorMode.value === 'dark'" class="icon">☀️</span>
        <span v-else class="icon">🌙</span>
      </button>
      
      <!-- Language buttons with active state -->
      <div class="language-toggle">
        <button
          v-for="loc in locales"
          :key="loc.code"
          @click="switchLanguage(loc.code)"
          :class="[
            'language-btn',
            locale === loc.code ? 'active' : ''
          ]"
          :aria-label="`Switch to ${loc.name}`"
          :aria-current="locale === loc.code ? 'true' : 'false'"
        >
          {{ loc.code.toUpperCase() }}
        </button>
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
}

.controls-container {
  display: flex;
  gap: 0.5rem;
  align-items: center;
}

.mode-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 2.75rem;
  height: 2.75rem;
  background-color: rgba(255, 255, 255, 0.95);
  backdrop-filter: blur(8px);
  border-radius: 0.75rem;
  border: 1px solid rgba(229, 231, 235, 0.8);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.1);
  cursor: pointer;
  transition: all 0.2s ease;
  font-size: 1.25rem;
  padding: 0;
}

.dark .mode-btn {
  background-color: rgba(31, 41, 55, 0.95);
  border-color: rgba(75, 85, 99, 0.8);
}

.mode-btn:hover {
  transform: scale(1.05);
  box-shadow: 0 6px 16px rgba(0, 0, 0, 0.15);
}

.mode-btn:focus {
  outline: 2px solid #3b82f6;
  outline-offset: 2px;
}

.icon {
  display: flex;
  align-items: center;
  justify-content: center;
}

.language-toggle {
  display: inline-flex;
  background-color: rgba(255, 255, 255, 0.95);
  backdrop-filter: blur(8px);
  border-radius: 0.75rem;
  padding: 0.25rem;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.1);
  border: 1px solid rgba(229, 231, 235, 0.8);
}

.dark .language-toggle {
  background-color: rgba(31, 41, 55, 0.95);
  border-color: rgba(75, 85, 99, 0.8);
}

.language-btn {
  padding: 0.5rem 1rem;
  font-size: 0.875rem;
  font-weight: 500;
  border-radius: 0.5rem;
  transition: all 0.2s ease;
  color: #6b7280;
  background-color: transparent;
  border: none;
  cursor: pointer;
  min-width: 3rem;
}

.dark .language-btn {
  color: #9ca3af;
}

.language-btn:hover:not(.active) {
  background-color: rgba(229, 231, 235, 0.5);
  color: #374151;
}

.dark .language-btn:hover:not(.active) {
  background-color: rgba(75, 85, 99, 0.5);
  color: #d1d5db;
}

.language-btn.active {
  background-color: #3b82f6;
  color: white;
  box-shadow: 0 2px 4px rgba(59, 130, 246, 0.4);
}

.dark .language-btn.active {
  background-color: #3b82f6;
  color: white;
}

.language-btn:focus {
  outline: 2px solid #3b82f6;
  outline-offset: 2px;
}

@media print {
  .language-selector {
    display: none;
  }
}
</style>
