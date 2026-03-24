<script setup lang="ts">
const { t } = useI18n()
const { scrollToElementSafely } = useSafeScroll()
const route = useRoute()

useSeoMeta({
  title: t('cv.title'),
  description: t('cv.description')
})

const cvDataName = computed(() => {
  return route.query.cv as string || 'default'
})

const { data: cvData, refresh } = useFetch(() => `/data/${cvDataName.value}.json`, {
  default: () => ({
    experiences: [],
    studies: [],
    projects: [],
    otherEntries: []
  })
})

watch(cvDataName, () => {
  refresh()
})

const experiences = computed(() => cvData.value?.experiences || [])
const studies = computed(() => cvData.value?.studies || [])
const projects = computed(() => cvData.value?.projects || [])
const otherEntries = computed(() => cvData.value?.otherEntries || [])

// Track active entries based on scroll position
const activeEntryIds = ref<(number | string)[]>([])
const clickedEntryId = ref<number | string | null>(null) // Track clicked entry

// Refs for tracking elements in viewport
const experienceSectionRef = ref<HTMLElement | null>(null)
const studiesSectionRef = ref<HTMLElement | null>(null)
const projectsSectionRef = ref<HTMLElement | null>(null)
const otherEntriesSectionRef = ref<HTMLElement | null>(null)

// Handle hover events on experience/study/project cards
function handleCardMouseEnter(id: number | string, type: 'exp' | 'study' | 'project' | 'other') {
  const fullId = type === 'exp' ? `exp-${id}` : type === 'study' ? `study-${id}` : type === 'project' ? `project-${id}` : `other-${id}`
  clickedEntryId.value = null // Clear clicked state on first hover
  if (!activeEntryIds.value.includes(fullId)) {
    activeEntryIds.value = [fullId]
  }
}

function handleCardMouseLeave() {
  // Only clear if no clicked entry is active
  if (clickedEntryId.value === null) {
    activeEntryIds.value = []
  }
}

// Handle timeline hover events
function handleTimelineHover(entryId: number | string) {
  clickedEntryId.value = null // Clear clicked state on first hover
  activeEntryIds.value = [entryId]
}

function handleTimelineLeave() {
  // Only clear if no clicked entry is active
  if (clickedEntryId.value === null) {
    activeEntryIds.value = []
  }
}

// Handle timeline click - set as active until first hover
function handleTimelineClick(entryId: number | string) {
  clickedEntryId.value = entryId
  activeEntryIds.value = [entryId]
}

function setupEventListeners() {
  if (typeof window === 'undefined') return
  
  experiences.value.forEach(exp => {
    const element = document.getElementById(`experience-${exp.id}`)
    if (element) {
      element.addEventListener('mouseenter', () => handleCardMouseEnter(exp.id, 'exp'))
      element.addEventListener('mouseleave', handleCardMouseLeave)
    }
  })
  
  studies.value.forEach(study => {
    const element = document.getElementById(`study-${study.id}`)
    if (element) {
      element.addEventListener('mouseenter', () => handleCardMouseEnter(study.id, 'study'))
      element.addEventListener('mouseleave', handleCardMouseLeave)
    }
  })

  projects.value.forEach(project => {
    const element = document.getElementById(`project-${project.id}`)
    if (element) {
      element.addEventListener('mouseenter', () => handleCardMouseEnter(project.id, 'project'))
      element.addEventListener('mouseleave', handleCardMouseLeave)
    }
  })

  otherEntries.value.forEach(entry => {
    const element = document.getElementById(`other-${entry.id}`)
    if (element) {
      element.addEventListener('mouseenter', () => handleCardMouseEnter(entry.id, 'other'))
      element.addEventListener('mouseleave', handleCardMouseLeave)
    }
  })
}

watch([experiences, studies, projects, otherEntries], () => {
  nextTick(() => {
    setupEventListeners()
  })
}, { deep: true })

onMounted(() => {
  if (typeof window === 'undefined') return
  
  setupEventListeners()
  
  // Restore scroll position from URL hash AFTER splash screen is hidden
  const { onSplashHidden } = useSplashScreen()
  if (typeof window !== 'undefined' && window.location.hash) {
    onSplashHidden(() => {
      setTimeout(() => {
        const hash = window.location.hash.substring(1)
        const element = document.getElementById(hash)
        if (element) {
          scrollToElementSafely(hash, 'smooth')
          // Extract ID and activate it
          if (hash.startsWith('experience-')) {
            const id = parseInt(hash.replace('experience-', ''))
            if (!isNaN(id)) {
              handleCardMouseEnter(id, 'exp')
              clickedEntryId.value = `exp-${id}`
            }
          } else if (hash.startsWith('study-')) {
            const id = parseInt(hash.replace('study-', ''))
            if (!isNaN(id)) {
              handleCardMouseEnter(id, 'study')
              clickedEntryId.value = `study-${id}`
            }
          } else if (hash.startsWith('project-')) {
            const id = parseInt(hash.replace('project-', ''))
            if (!isNaN(id)) {
              handleCardMouseEnter(id, 'project')
              clickedEntryId.value = `project-${id}`
            }
          } else if (hash.startsWith('other-')) {
            const id = parseInt(hash.replace('other-', ''))
            if (!isNaN(id)) {
              handleCardMouseEnter(id, 'other')
              clickedEntryId.value = `other-${id}`
            }
          }
        }
      }, 500) // Delay to ensure page is fully loaded
    })
  }
})

onUnmounted(() => {
  // Clean up event listeners
  experiences.value.forEach(exp => {
    const element = document.getElementById(`experience-${exp.id}`)
    if (element) {
      element.removeEventListener('mouseenter', () => handleCardMouseEnter(exp.id, 'exp'))
      element.removeEventListener('mouseleave', handleCardMouseLeave)
    }
  })
  
  studies.value.forEach(study => {
    const element = document.getElementById(`study-${study.id}`)
    if (element) {
      element.removeEventListener('mouseenter', () => handleCardMouseEnter(study.id, 'study'))
      element.removeEventListener('mouseleave', handleCardMouseLeave)
    }
  })

  projects.value.forEach(project => {
    const element = document.getElementById(`project-${project.id}`)
    if (element) {
      element.removeEventListener('mouseenter', () => handleCardMouseEnter(project.id, 'project'))
      element.removeEventListener('mouseleave', handleCardMouseLeave)
    }
  })

  otherEntries.value.forEach(entry => {
    const element = document.getElementById(`other-${entry.id}`)
    if (element) {
      element.removeEventListener('mouseenter', () => handleCardMouseEnter(entry.id, 'other'))
      element.removeEventListener('mouseleave', handleCardMouseLeave)
    }
  })
})

</script>

<template>
  <div class="min-h-screen bg-white dark:bg-gray-900 print:bg-white">
    <!-- Hero Section - Full page height -->
    <CvHero />
    
    <!-- Intro Section - Between hero and main content -->
    <CvIntro />
    
    <!-- Main Content with Sidebar Layout -->
    <div class="cv-container">
      <!-- Sidebar -->
      <aside 
        class="sidebar bg-gray-100 dark:bg-gray-800 print:bg-gray-50 lg:sticky lg:top-0 lg:self-start lg:min-h-screen lg:overflow-y-auto"
      >
        <div class="p-6 space-y-8">
          <!-- Profile with Picture (fades in on scroll) - Hidden on mobile -->
          <div class="sidebar-profile hidden lg:block print:block">
            <CvProfile />
          </div>
          
          <!-- Personal Details - Hidden on mobile -->
          <div class="hidden lg:block print:block">
            <CvDetails />
          </div>
          
          <!-- Languages - Hidden on mobile -->
          <div class="hidden lg:block print:block">
            <CvLanguages />
          </div>
          
          <!-- Preferred Technologies - Hidden on mobile -->
          <div class="hidden lg:block print:block">
            <CvPreferredTechs />
          </div>
          
          <!-- Driving Licenses - Hidden on mobile -->
          <div class="hidden lg:block print:block">
            <CvDrivingLicenses />
          </div>

          <!-- Spacer to push QR code to bottom on print -->
          <div class="flex-grow print:block hidden"></div>
          
          <!-- QR Code -->
          <div class="hidden lg:block print:block">
            <CvQrCode />
          </div>
        </div>
      </aside>
      
      <!-- Main Content -->
      <main class="main-content bg-white dark:bg-gray-900 print:bg-white">
        <div class="main-content-wrapper">
          <!-- Timeline (left side) -->
          <CvTimeline 
            :experiences="experiences"
            :studies="studies"
            :projects="projects"
            :other-entries="otherEntries"
            :active-ids="activeEntryIds"
            @entry-hover="handleTimelineHover"
            @entry-leave="handleTimelineLeave"
            @entry-click="handleTimelineClick"
          />
          
          <!-- Content (right side) -->
          <div class="content-area p-6 lg:p-8 space-y-8 mx-auto max-w-4xl">
            <!-- Skills Section -->
            <div id="skills-section">
              <CvSkills />
            </div>
            
            <!-- Experiences Section -->
            <div id="experiences-section" ref="experienceSectionRef">
              <CvExperiences :experiences="experiences" :active-ids="activeEntryIds" />
            </div>

            <!-- Studies Section -->
            <div id="studies-section" ref="studiesSectionRef">
              <CvStudies :studies="studies" :active-ids="activeEntryIds" />
            </div>

            <!-- Projects Section -->
            <div id="projects-section" ref="projectsSectionRef">
              <CvProjects :projects="projects" :active-ids="activeEntryIds" />
            </div>

            <!-- Other Experiences Section -->
            <div id="other-section" ref="otherEntriesSectionRef">
              <CvOtherExperiences :entries="otherEntries" :active-ids="activeEntryIds" />
            </div>

            <!-- Sidebar sections on mobile (shown at end) -->
            <div class="lg:hidden print:hidden mobile-sidebar-sections space-y-8 mt-12 pt-8 border-t border-gray-200 dark:border-gray-700">
              <!-- Personal Details -->
              <CvDetails />
              
              <!-- Languages -->
              <CvLanguages />
              
              <!-- Preferred Technologies -->
              <CvPreferredTechs />
              
              <!-- Driving Licenses -->
              <CvDrivingLicenses />
            </div>

            <!-- Footer -->
            <CvFooter />
          </div>
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
    min-height: 100vh;
  }
  
  .sidebar {
    height: auto !important;
    position: static !important;
    overflow: visible !important;
    min-height: 100vh;
  }
  
  .sidebar-content {
    display: flex !important;
    flex-direction: column;
    min-height: calc(100vh - 3rem); /* Account for padding */
  }
  
  .sidebar-profile {
    opacity: 1 !important;
    animation: none !important;
  }
}

/* Main content wrapper for timeline layout */
.main-content-wrapper {
  display: flex;
  gap: 1rem;
}

.content-area {
  flex: 1;
  min-width: 0; /* Prevent flex item from overflowing */
}

@media (max-width: 1279px) {
  .main-content-wrapper {
    display: block;
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
