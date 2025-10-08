<script setup lang="ts">
const { t } = useI18n()

useSeoMeta({
  title: t('cv.title'),
  description: t('cv.description')
})
</script>

<template>
  <div class="min-h-screen bg-white dark:bg-gray-900 print:bg-white">
    <!-- Hero Section - Full page height -->
    <CvHero />
    
    <!-- Main Content with Sidebar Layout -->
    <div class="cv-container">
      <!-- Sidebar -->
      <aside 
        class="sidebar bg-gray-100 dark:bg-gray-800 print:bg-gray-50 lg:sticky lg:top-0 lg:self-start lg:min-h-screen lg:overflow-y-auto"
      >
        <div class="p-6 space-y-8">
          <!-- Profile with Picture (fades in on scroll) -->
          <div class="sidebar-profile">
            <CvProfile />
          </div>
          
          <!-- Personal Details -->
          <CvDetails />
          
          <!-- Languages -->
          <CvLanguages />
          
          <!-- Driving Licenses -->
          <CvDrivingLicenses />
        </div>
      </aside>
      
      <!-- Main Content -->
      <main class="main-content bg-white dark:bg-gray-900 print:bg-white">
        <div class="p-6 lg:p-8 space-y-8 mx-auto max-w-4xl">
          <!-- Skills Section -->
          <CvSkills />
          
          <!-- Experiences Section -->
          <CvExperiences />

          <!-- Studies Section -->
          <CvStudies />

          <!-- Footer -->
          <CvFooter />
        </div>
      </main>
    </div>
  </div>
</template>

<style scoped>
.cv-container {
  display: grid;
  grid-template-columns: 1fr;
}

@media (min-width: 1024px) {
  .cv-container {
    grid-template-columns: 350px 1fr;
  }
}

/* CSS-based fade-in animation for sidebar profile */
.sidebar-profile {
  opacity: 0;
  animation: fadeInOnScroll 0.5s ease-out 0.3s forwards;
}

@keyframes fadeInOnScroll {
  from {
    opacity: 0;
    transform: translateY(-10px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

@media print {
  .cv-container {
    grid-template-columns: 300px 1fr;
  }
  
  .sidebar {
    height: auto !important;
    position: static !important;
    overflow: visible !important;
  }
  
  .sidebar-profile {
    opacity: 1 !important;
    animation: none !important;
  }
}
</style>

<i18n lang="json">
{
  "en": {
    "cv": {
      "title": "Software Architect CV",
      "subtitle": "Professional Experience & Education",
      "description": "Curriculum Vitae of a Software Architect"
    }
  },
  "de": {
    "cv": {
      "title": "Lebenslauf Software-Architekt",
      "subtitle": "Berufserfahrung & Ausbildung",
      "description": "Lebenslauf eines Software-Architekten"
    }
  }
}
</i18n>
