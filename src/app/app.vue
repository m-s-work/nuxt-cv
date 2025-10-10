<template>
  <div>
    <LanguageSelector />
    <CvHeaderMenu />
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

