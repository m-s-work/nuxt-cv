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
  projects: () => [],
  activeIds: () => []
})

// Check if a project is active
function isActive(projectId: number): boolean {
  return props.activeIds.includes(`project-${projectId}`)
}

// Handle heading click to update URL hash
function handleHeadingClick(elementId: string) {
  if (typeof window !== 'undefined') {
    window.history.pushState(null, '', `#${elementId}`)
  }
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
  <section id="projects-section">
    <h2 class="text-3xl font-bold text-gray-900 dark:text-white print:text-black mb-6">
      {{ t('projects.title') }}
    </h2>
    
    <div class="space-y-6">
      <UCard 
        v-for="project in props.projects" 
        :key="project.id" 
        :id="`project-${project.id}`"
        :class="{
          'print:!shadow-none': true,
          'transition-all duration-300': true,
          'translate-x-2': isActive(project.id)
        }"
      >
        <template #header>
          <div class="flex justify-between items-start">
            <div class="flex-1">
              <h3 
                @click="handleHeadingClick(`project-${project.id}`)"
                :class="{
                  'text-xl font-semibold print:text-black cursor-pointer hover:underline': true,
                  'text-purple-600 dark:text-purple-400': isActive(project.id),
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
          <TechBadge 
            v-for="tech in project.technologies" 
            :key="tech"
            :technology="tech"
          />
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
