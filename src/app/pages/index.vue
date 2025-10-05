<script setup lang="ts">
const { t } = useI18n()

useSeoMeta({
  title: t('cv.title'),
  description: t('cv.description')
})

const scrollProgress = ref(0)

onMounted(() => {
  const handleScroll = () => {
    // Calculate scroll progress (0 to 1) based on viewport height
    const maxScroll = window.innerHeight
    const currentScroll = window.scrollY
    scrollProgress.value = Math.min(currentScroll / maxScroll, 1)
  }
  
  window.addEventListener('scroll', handleScroll)
  handleScroll() // Initial call
  
  onUnmounted(() => {
    window.removeEventListener('scroll', handleScroll)
  })
})
</script>

<template>
  <div class="cv-page">
    <!-- Hero Section - Full page height initially -->
    <CvHero :scroll-progress="scrollProgress" />
    
    <!-- Main Content with Sidebar Layout -->
    <div class="cv-container" :style="{ marginTop: scrollProgress < 1 ? '100vh' : '0' }">
      <!-- Sidebar - where hero transitions to -->
      <aside 
        class="sidebar bg-gray-100 dark:bg-gray-800 print:bg-gray-50 lg:sticky lg:top-0 lg:h-screen lg:overflow-y-auto"
      >
        <div class="p-6 space-y-8">
          <!-- Profile with Picture (hidden until hero transitions) -->
          <div :style="{ opacity: scrollProgress }">
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
        <div class="p-6 lg:p-8 space-y-8">
          <!-- Skills Section -->
          <CvSkills />
          
          <!-- Experiences Section -->
          <CvExperiences />

          <!-- Studies Section -->
          <CvStudies />
        </div>
      </main>
    </div>
  </div>
</template>

<style scoped>
.cv-page {
  min-height: 100vh;
  background: white;
}

.dark .cv-page {
  background: rgb(17 24 39);
}

.cv-container {
  display: grid;
  grid-template-columns: 1fr;
  transition: margin-top 0.3s ease-out;
}

@media (min-width: 1024px) {
  .cv-container {
    grid-template-columns: 350px 1fr;
  }
}

@media print {
  .cv-container {
    grid-template-columns: 300px 1fr;
    margin-top: 0 !important;
  }
  
  .sidebar {
    height: auto !important;
    position: static !important;
    overflow: visible !important;
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
