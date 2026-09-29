<script setup lang="ts">
const { setSplashType, hideSplash } = useSplashScreen()
const route = useRoute()

// Owner admin page (/admin, /de/admin): no splash screen, no CV chrome.
const isAdmin = computed(() => /^(\/[a-z]{2})?\/admin\/?$/.test(route.path))
if (isAdmin.value) hideSplash()

// PDF renderer mode (?print=1) and the owner's heatmap view (?heatmap=1): no splash screen.
if (import.meta.client && ['print', 'heatmap'].some(p => new URLSearchParams(window.location.search).has(p))) {
  hideSplash()
}

// Check URL parameter for splash screen type (for testing/demo)
onMounted(() => {
  if (typeof window !== 'undefined') {
    const urlParams = new URLSearchParams(window.location.search)
    const splashParam = urlParams.get('splash')
    const validTypes = ['1', '2', '3', '4', '5', '6', '7', '8', '9', '10']
    if (splashParam && validTypes.includes(splashParam)) {
      setSplashType(parseInt(splashParam) as 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10)
    }
  }
})
</script>

<template>
  <div>
    <NuxtPage v-if="isAdmin" />
    <template v-else>
      <SplashScreenManager />
      <LanguageSelector />
      <NuxtPage />
      <Lightbox />
    </template>
  </div>
</template>

<style>
/* Print Theme Styles */
@media print {
  @page {
    size: A4;
    margin: 16mm 15mm 18mm 15mm;
  }
  
  body {
    print-color-adjust: exact;
    -webkit-print-color-adjust: exact;
  }
  
  /* Hide elements that shouldn't be printed */
  .no-print {
    display: none !important;
  }
  
  h1, h2, h3 {
    page-break-after: avoid;
  }
}

/* Global dark mode support */
:root {
  color-scheme: light dark;
}
</style>

