<script setup lang="ts">
const { t } = useI18n()

useSeoMeta({
  title: t('cv.title'),
  description: t('cv.description')
})

const scrollY = ref(0)
const windowHeight = ref(0)
const windowWidth = ref(0)
const profileImageTransition = ref({
  top: '50%',
  left: '50%',
  transform: 'translate(-50%, -50%)',
  width: '12rem',
  height: '14rem',
  opacity: 1
})

onMounted(() => {
  // Initialize window dimensions
  windowHeight.value = window.innerHeight
  windowWidth.value = window.innerWidth
  
  const handleScroll = () => {
    scrollY.value = window.scrollY
    
    // Calculate transition progress (0 at top, 1 at target position)
    const heroHeight = windowHeight.value
    const progress = Math.min(scrollY.value / heroHeight, 1)
    
    if (progress < 1) {
      // Animate from hero center to sidebar position
      // Hero center: 50% top, 50% left
      // Sidebar target (desktop): ~175px from left, ~150px from top
      const startTop = 50 // percentage
      const startLeft = 50 // percentage
      const endTop = 150 // pixels from top when in sidebar
      const endLeft = windowWidth.value >= 1024 ? 175 : 50 // pixels from left
      
      const currentTop = startTop - (progress * startTop) + (progress * (endTop / windowHeight.value * 100))
      const currentLeft = startLeft - (progress * (startLeft - (endLeft / windowWidth.value * 100)))
      
      // Scale from 12rem to 10rem (160px)
      const startWidth = 192 // 12rem in pixels
      const endWidth = 160 // 10rem in pixels
      const currentWidth = startWidth - (progress * (startWidth - endWidth))
      
      const startHeight = 224 // 14rem in pixels
      const endHeight = 192 // 12rem in pixels
      const currentHeight = startHeight - (progress * (startHeight - endHeight))
      
      profileImageTransition.value = {
        top: `${currentTop}%`,
        left: `${currentLeft}%`,
        transform: `translate(-50%, -50%)`,
        width: `${currentWidth}px`,
        height: `${currentHeight}px`,
        opacity: 1 - (progress * 0.3) // Slight fade
      }
    } else {
      // Hide the transitioning image, show the one in sidebar
      profileImageTransition.value = {
        ...profileImageTransition.value,
        opacity: 0
      }
    }
  }
  
  const handleResize = () => {
    windowHeight.value = window.innerHeight
    windowWidth.value = window.innerWidth
    handleScroll() // Recalculate on resize
  }
  
  window.addEventListener('scroll', handleScroll)
  window.addEventListener('resize', handleResize)
  handleScroll() // Initial call
  
  onUnmounted(() => {
    window.removeEventListener('scroll', handleScroll)
    window.removeEventListener('resize', handleResize)
  })
})
</script>

<template>
  <div class="min-h-screen bg-white dark:bg-gray-900 print:bg-white">
    <!-- Transitioning Profile Image -->
    <div 
      v-if="windowHeight > 0"
      class="profile-image-transition print:hidden"
      :style="{
        top: profileImageTransition.top,
        left: profileImageTransition.left,
        transform: profileImageTransition.transform,
        width: profileImageTransition.width,
        height: profileImageTransition.height,
        opacity: profileImageTransition.opacity,
        pointerEvents: scrollY >= windowHeight ? 'none' : 'auto'
      }"
    >
      <img 
        src="https://via.placeholder.com/200x250/4F46E5/FFFFFF?text=Photo" 
        alt="Profile"
        class="w-full h-full object-cover rounded-lg shadow-2xl border-4 border-white"
      />
    </div>
    
    <!-- Hero Section - Full page height (profile image hidden) -->
    <CvHero :hide-image="true" />
    
    <!-- Main Content with Sidebar Layout -->
    <div class="cv-container">
      <!-- Sidebar -->
      <aside 
        class="sidebar bg-gray-100 dark:bg-gray-800 print:bg-gray-50 lg:sticky lg:top-0 lg:h-screen lg:overflow-y-auto"
      >
        <div class="p-6 space-y-8">
          <!-- Profile with Picture (hidden until scroll completes) -->
          <div :style="{ opacity: windowHeight > 0 && scrollY >= windowHeight ? 1 : 0, transition: 'opacity 0.3s' }">
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
.profile-image-transition {
  position: fixed;
  z-index: 100;
  transition: all 0.1s ease-out;
}

.cv-container {
  display: grid;
  grid-template-columns: 1fr;
}

@media (min-width: 1024px) {
  .cv-container {
    grid-template-columns: 350px 1fr;
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
