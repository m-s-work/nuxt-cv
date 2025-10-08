<script setup lang="ts">
const { t } = useI18n()

interface Props {
  projects?: Array<{
    id: number
    name: string
    type: string
    description: string
    technologies: string[]
    period: string
    startDate: string
    endDate: string | null
    screenshots?: string[]
    images?: string[]
    logos?: string[]
  }>
  activeIds?: (number | string)[]
}

const props = withDefaults(defineProps<Props>(), {
  projects: () => [
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
  ],
  activeIds: () => []
})

// Check if a project is active
function isActive(projectId: number): boolean {
  return props.activeIds.includes(`project-${projectId}`)
}

// Get all media (screenshots, images, logos) for a project
function getProjectMedia(project: Props['projects'][0]) {
  const media = []
  if (project.screenshots) media.push(...project.screenshots)
  if (project.images) media.push(...project.images)
  if (project.logos) media.push(...project.logos)
  return media
}

</script>

<template>
  <section>
    <h2 class="text-3xl font-bold text-gray-900 dark:text-white print:text-black mb-6">
      {{ t('projects.title') }}
    </h2>
    
    <div class="space-y-6">
      <UCard 
        v-for="project in props.projects" 
        :key="project.id" 
        :id="`project-${project.id}`"
        :class="{
          'print:!shadow-none print:!border print:!border-gray-300': true,
          'transition-all duration-300': true,
          'shadow-lg': isActive(project.id),
          'translate-x-2': isActive(project.id)
        }"
      >
        <template #header>
          <div class="flex justify-between items-start">
            <div class="flex-1">
              <h3 
                :class="{
                  'text-xl font-semibold print:text-black': true,
                  'text-blue-600 dark:text-blue-400': isActive(project.id),
                  'text-gray-900 dark:text-white': !isActive(project.id)
                }"
              >
                {{ project.name }}
              </h3>
              <p class="text-gray-600 dark:text-gray-400 print:text-gray-700">
                {{ project.type }}
              </p>
            </div>
            <UBadge color="primary" variant="subtle" class="print:!bg-gray-100 print:!text-black">
              {{ project.period }}
            </UBadge>
          </div>
        </template>

        <!-- Project Images/Screenshots/Logos -->
        <div v-if="getProjectMedia(project).length > 0" class="mb-4 flex flex-wrap gap-2">
          <img 
            v-for="(media, index) in getProjectMedia(project)" 
            :key="index"
            :src="media"
            :alt="`${project.name} screenshot ${index + 1}`"
            class="h-32 w-auto object-cover rounded border border-gray-200 dark:border-gray-700 print:border-gray-300"
            loading="lazy"
          />
        </div>

        <!-- Project Description -->
        <p class="text-gray-700 dark:text-gray-300 print:text-black mb-4">
          {{ project.description }}
        </p>

        <!-- Technology Badges -->
        <div class="flex flex-wrap gap-2">
          <UBadge 
            v-for="tech in project.technologies" 
            :key="tech"
            color="gray"
            variant="soft"
            class="print:!bg-gray-100 print:!text-black"
          >
            {{ tech }}
          </UBadge>
        </div>
      </UCard>
    </div>
  </section>
</template>

<i18n lang="json">
{
  "en": {
    "projects": {
      "title": "Projects"
    }
  },
  "de": {
    "projects": {
      "title": "Projekte"
    }
  }
}
</i18n>
