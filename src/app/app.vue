<script setup lang="ts">
const { setSplashType } = useSplashScreen()

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
    <SplashScreenManager />
    <LanguageSelector />
    <NuxtPage />
  </div>
</template>

<style>
/* Print Theme Styles */
@media print {
  @page {
    size: A4;
    margin: 1cm;
  }
  
  body {
    print-color-adjust: exact;
    -webkit-print-color-adjust: exact;
  }
  
  /* Hide elements that shouldn't be printed */
  .no-print {
    display: none !important;
  }
  
  /* Ensure content fits on page */
  * {
    page-break-inside: avoid;
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

