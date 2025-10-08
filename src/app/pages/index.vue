<script setup lang="ts">
const { t } = useI18n()

useSeoMeta({
  title: t('cv.title'),
  description: t('cv.description')
})

// Data for timeline - would eventually come from API
const experiences = ref([
  {
    id: 1,
    company: 'Tech Company Inc.',
    position: 'Senior Software Architect',
    period: '2020 - Present',
    startDate: '2020-01-01',
    endDate: null,
    description: 'Leading architecture design and implementation for cloud-native applications',
    technologies: ['Nuxt', 'Vue.js', 'Node.js', 'Docker', 'Kubernetes']
  },
  {
    id: 4,
    company: 'Software Solutions Ltd.',
    position: 'Full Stack Developer',
    period: '2017 - 2020',
    startDate: '2017-03-01',
    endDate: '2019-12-31',
    description: 'Developed enterprise web applications and microservices',
    technologies: ['Vue.js', 'Express', 'PostgreSQL', 'Redis']
  },
  
  {
    id: 2,
    company: 'Peter Enis KG',
    position: 'Rubber Tester',
    period: '2019 - 2023',
    startDate: '2019-01-01',
    endDate: '2023-12-31',
    description: 'Did extensive testing of rubber materials for quality assurance',
    technologies: ['Rubber.js', 'MongoDB']
  },
  {
    id: 3,
    company: 'Auto GmbH',
    position: 'Car Mechanic',
    period: '2015 - 2018',
    startDate: '2015-06-01',
    endDate: '2018-11-30',
    description: 'Performed maintenance and repairs on various car models',
    technologies: ['AutoCAD', 'Diagnostic Tools']
  }
])

const studies = ref([
  {
    id: 1,
    institution: 'Technical University',
    degree: 'Master of Science in Computer Science',
    period: '2015 - 2017',
    startDate: '2015-09-01',
    endDate: '2017-06-30',
    focus: 'Software Engineering & Distributed Systems'
  },
  {
    id: 2,
    institution: 'University of Technology',
    degree: 'Bachelor of Science in Computer Science',
    period: '2012 - 2015',
    startDate: '2012-09-01',
    endDate: '2015-06-30',
    focus: 'Computer Science Fundamentals'
  },
  {
    id: 3,
    institution: 'University of Kamasutra',
    degree: 'Bachelor of Science in Computer Science',
    period: '2028 - 2028',
    startDate: '2028-09-01',
    endDate: '2028-06-30',
    focus: 'Fundamentals of Pleasure'
  }
])

// Track active entries based on scroll position
const activeEntryIds = ref<(number | string)[]>([])

// Refs for tracking elements in viewport
const experienceSectionRef = ref<HTMLElement | null>(null)
const studiesSectionRef = ref<HTMLElement | null>(null)

onMounted(() => {
  if (typeof window === 'undefined') return
  
  const updateActiveEntries = () => {
    const active: (number | string)[] = []
    const scrollY = window.scrollY
    const viewportHeight = window.innerHeight
    const viewportCenter = scrollY + viewportHeight / 2
    
    // Check each experience card
    experiences.value.forEach(exp => {
      const element = document.getElementById(`experience-${exp.id}`)
      if (element) {
        const rect = element.getBoundingClientRect()
        const elementTop = scrollY + rect.top
        const elementBottom = elementTop + rect.height
        
        // Check if element is in viewport center region
        if (elementTop <= viewportCenter && elementBottom >= viewportCenter) {
          active.push(`exp-${exp.id}`)
        }
      }
    })
    
    // Check each study card
    studies.value.forEach(study => {
      const element = document.getElementById(`study-${study.id}`)
      if (element) {
        const rect = element.getBoundingClientRect()
        const elementTop = scrollY + rect.top
        const elementBottom = elementTop + rect.height
        
        // Check if element is in viewport center region
        if (elementTop <= viewportCenter && elementBottom >= viewportCenter) {
          active.push(`study-${study.id}`)
        }
      }
    })
    
    activeEntryIds.value = active
  }
  
  // Update on scroll
  window.addEventListener('scroll', updateActiveEntries, { passive: true })
  
  // Initial update
  setTimeout(updateActiveEntries, 100)
  
  onUnmounted(() => {
    window.removeEventListener('scroll', updateActiveEntries)
  })
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
        <div class="main-content-wrapper">
          <!-- Timeline (left side) -->
          <CvTimeline 
            :experiences="experiences"
            :studies="studies"
            :active-ids="activeEntryIds"
          />
          
          <!-- Content (right side) -->
          <div class="content-area p-6 lg:p-8 space-y-8 mx-auto max-w-4xl">
            <!-- Skills Section -->
            <CvSkills />
            
            <!-- Experiences Section -->
            <div ref="experienceSectionRef">
              <CvExperiences :experiences="experiences" />
            </div>

            <!-- Studies Section -->
            <div ref="studiesSectionRef">
              <CvStudies :studies="studies" />
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
