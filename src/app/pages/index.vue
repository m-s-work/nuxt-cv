<script setup lang="ts">
const { t } = useI18n()
const { scrollToElementSafely } = useSafeScroll()
const { isFiltering } = useTechFilter()

useSeoMeta({
  title: t('cv.title'),
  description: t('cv.description')
})

// Data for timeline - would eventually come from API
const experiences = ref([
  
  // self employed entry for testing overlapping periods
  {
    id: 5,
    company: 'Freelance Developer',
    position: 'Self-Employed',
    period: '2018 - Present',
    startDate: '2018-05-01',
    endDate: null,
    description: 'Providing freelance software development services to various clients',
    technologies: ['JavaScript', 'Python', 'Django', 'React'],
    icon: 'lightbulb'
  },

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
  },
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
    degree: 'Master of Arts in Love Studies',
    period: '2028 - 2028',
    startDate: '2028-09-01',
    endDate: '2028-06-30',
    focus: 'Fundamentals of Pleasure'
  }
])

const projects = ref([
  {
    id: 1,
    name: 'E-Commerce Platform',
    type: 'Web Application',
    description: 'Built a scalable e-commerce platform with microservices architecture, handling thousands of transactions daily',
    technologies: ['Vue.js', 'Node.js', 'PostgreSQL', 'Redis', 'Docker'],
    period: '2021 - 2023',
    startDate: '2021-03-01',
    endDate: '2023-06-30',
    screenshots: ['/images/projects/ecommerce-screenshot.jpg'],
    logos: ['/images/projects/ecommerce-logo.png']
  },
  {
    id: 2,
    name: 'Mobile Banking App',
    type: 'Mobile Application',
    description: 'Developed a secure mobile banking application with biometric authentication and real-time transaction processing',
    technologies: ['React Native', 'TypeScript', 'Firebase', 'REST API'],
    period: '2019 - 2021',
    startDate: '2019-06-01',
    endDate: '2021-02-28',
    screenshots: ['/images/projects/banking-app-1.jpg', '/images/projects/banking-app-2.jpg']
  },
  {
    id: 3,
    name: 'AI Content Generator',
    type: 'SaaS Platform',
    description: 'Created an AI-powered content generation tool using machine learning models for automated content creation',
    technologies: ['Python', 'TensorFlow', 'FastAPI', 'React', 'AWS'],
    period: '2022 - Present',
    startDate: '2022-01-01',
    endDate: null,
    images: ['/images/projects/ai-generator.jpg'],
    logos: ['/images/projects/ai-logo.svg']
  }
])

const otherEntries = ref([
  {
    id: 1,
    title: 'Mandatory Military Service',
    institution: 'Armed Forces',
    period: '2011 - 2012',
    startDate: '2011-07-01',
    endDate: '2012-06-30',
    description: 'Completed mandatory military service',
    showPeriod: true,
    icon: 'shield'
  },
  {
    id: 1,
    title: 'Mandatory Military Service',
    institution: 'Armed Forces',
    period: '2011 - 2012',
    startDate: '2011-07-01',
    endDate: '2012-06-30',
    description: 'Completed mandatory military service',
    showPeriod: true,
    icon: 'helmet'
  },
  {
    id: 2,
    title: 'Abitur',
    institution: 'High School',
    period: '2011',
    startDate: '2011-01-01',
    endDate: '2011-06-30',
    description: 'High school diploma (Abitur)',
    showPeriod: true,
    icon: 'award'
  },
  {
    id: 3,
    title: 'School',
    institution: 'Secondary School',
    period: '2005 - 2011',
    startDate: '2005-09-01',
    endDate: '2011-06-30',
    description: 'Secondary education',
    showPeriod: false,
    icon: 'book-open'
  },
  {
    id: 4,
    title: 'Summer Internship',
    institution: 'Tech Startup Inc.',
    period: 'Jul 2010',
    startDate: '2010-07-01',
    endDate: '2010-07-31',
    description: 'One-month internship in web development',
    showPeriod: true,
    icon: 'users'
  },
  {
    id: 5,
    title: 'Summer Internship',
    institution: 'Local Engineering Firm',
    period: 'Aug 2009 - Sep 2009',
    startDate: '2009-08-01',
    endDate: '2009-09-30',
    description: 'Two-month internship in software engineering',
    showPeriod: true,
    icon: 'hammer'
  }
])

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

onMounted(() => {
  if (typeof window === 'undefined') return
  
  // Add hover listeners to all experience cards
  experiences.value.forEach(exp => {
    const element = document.getElementById(`experience-${exp.id}`)
    if (element) {
      element.addEventListener('mouseenter', () => handleCardMouseEnter(exp.id, 'exp'))
      element.addEventListener('mouseleave', handleCardMouseLeave)
    }
  })
  
  // Add hover listeners to all study cards
  studies.value.forEach(study => {
    const element = document.getElementById(`study-${study.id}`)
    if (element) {
      element.addEventListener('mouseenter', () => handleCardMouseEnter(study.id, 'study'))
      element.addEventListener('mouseleave', handleCardMouseLeave)
    }
  })

  // Add hover listeners to all project cards
  projects.value.forEach(project => {
    const element = document.getElementById(`project-${project.id}`)
    if (element) {
      element.addEventListener('mouseenter', () => handleCardMouseEnter(project.id, 'project'))
      element.addEventListener('mouseleave', handleCardMouseLeave)
    }
  })

  // Add hover listeners to all other entry cards
  otherEntries.value.forEach(entry => {
    const element = document.getElementById(`other-${entry.id}`)
    if (element) {
      element.addEventListener('mouseenter', () => handleCardMouseEnter(entry.id, 'other'))
      element.addEventListener('mouseleave', handleCardMouseLeave)
    }
  })
  
  // Restore scroll position from URL hash on page load
  if (typeof window !== 'undefined' && window.location.hash) {
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
    <!-- Tech Filter Bar -->
    <TechFilterBar />
    
    <!-- Hero Section - Full page height -->
    <CvHero />
    
    <!-- Main Content with Sidebar Layout -->
    <div class="cv-container" :class="{ 'with-filter': isFiltering }">
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
            <CvSkills />
            
            <!-- Experiences Section -->
            <div ref="experienceSectionRef">
              <CvExperiences :experiences="experiences" :active-ids="activeEntryIds" />
            </div>

            <!-- Studies Section -->
            <div ref="studiesSectionRef">
              <CvStudies :studies="studies" :active-ids="activeEntryIds" />
            </div>

            <!-- Projects Section -->
            <div ref="projectsSectionRef">
              <CvProjects :projects="projects" :active-ids="activeEntryIds" />
            </div>

            <!-- Other Experiences Section -->
            <div ref="otherEntriesSectionRef">
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

/* Add padding when filter bar is active */
.cv-container.with-filter {
  padding-top: 80px;
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
    padding-top: 0 !important;
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
