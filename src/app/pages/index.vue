<script setup lang="ts">
const { t } = useI18n()

useSeoMeta({
  title: t('cv.title'),
  description: t('cv.description')
})

// Timeline data - these would eventually come from an API
const experiences = ref([
  {
    id: 1,
    company: 'Tech Company Inc.',
    position: 'Senior Software Architect',
    period: '2020 - Present',
    description: 'Leading architecture design and implementation for cloud-native applications',
    technologies: ['Nuxt', 'Vue.js', 'Node.js', 'Docker', 'Kubernetes']
  },
  {
    id: 2,
    company: 'Software Solutions Ltd.',
    position: 'Full Stack Developer',
    period: '2017 - 2020',
    description: 'Developed enterprise web applications and microservices',
    technologies: ['Vue.js', 'Express', 'PostgreSQL', 'Redis']
  }
])

const studies = ref([
  {
    id: 1,
    institution: 'Technical University',
    degree: 'Master of Science in Computer Science',
    period: '2015 - 2017',
    focus: 'Software Engineering & Distributed Systems'
  },
  {
    id: 2,
    institution: 'University of Technology',
    degree: 'Bachelor of Science in Computer Science',
    period: '2012 - 2015',
    focus: 'Computer Science Fundamentals'
  }
])

const projects = ref([
  {
    id: 1,
    name: 'E-Commerce Platform',
    type: 'Web Application',
    period: '2022 - 2023',
    description: 'Built a scalable e-commerce platform with microservices architecture',
    technologies: ['Vue.js', 'Node.js', 'MongoDB', 'Redis', 'Docker']
  },
  {
    id: 2,
    name: 'Mobile Banking App',
    type: 'Mobile Application',
    period: '2021 - 2022',
    description: 'Developed a secure mobile banking application',
    technologies: ['React Native', 'TypeScript', 'GraphQL', 'PostgreSQL']
  },
  {
    id: 3,
    name: 'Cloud Infrastructure',
    type: 'DevOps Project',
    period: '2020 - 2021',
    description: 'Designed and implemented cloud infrastructure',
    technologies: ['Kubernetes', 'Terraform', 'AWS', 'Jenkins']
  }
])
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
        <div class="main-with-timeline">
          <!-- Timeline (left side, only visible on large screens and print) -->
          <CvTimeline :experiences="experiences" :studies="studies" :projects="projects" />
          
          <!-- Content area -->
          <div class="p-6 lg:p-8 space-y-8 mx-auto max-w-4xl">
            <!-- Skills Section -->
            <CvSkills />
            
            <!-- Experiences Section -->
            <CvExperiences />

            <!-- Projects Section -->
            <CvProjects />

            <!-- Studies Section -->
            <CvStudies />

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

.main-with-timeline {
  display: grid;
  grid-template-columns: 1fr;
}

@media (min-width: 1024px) {
  .main-with-timeline {
    grid-template-columns: auto 1fr;
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
  
  .main-with-timeline {
    grid-template-columns: auto 1fr;
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
