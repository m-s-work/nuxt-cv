<script setup lang="ts">
import type { TimelineItem } from '~/composables/useTimeline'

const { t } = useI18n()
const { toggleTech, isTechSelected, shouldShowItem } = useTechFilter()

interface Props {
  projects?: Array<TimelineItem & {
    name: string
    type: string
    description: string
    technologies: string[]
    period: string
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

// Filter projects based on selected technologies
const filteredProjects = computed(() => {
  return props.projects.filter(project => {
    if (!project.technologies || project.technologies.length === 0) return true
    return shouldShowItem(project.technologies)
  })
})

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
      <a :href="`#projects-section`" class="section-heading-link">
        {{ t('projects.title') }}
      </a>
    </h2>
    
    <div class="space-y-6">
      <CvBlock
        v-for="project in filteredProjects" 
        :key="project.id"
        :id="project.id"
        :title="project.name"
        :subtitle="project.type"
        :period="project.period"
        :description="project.description"
        :technologies="project.technologies"
        :images="getProjectMedia(project)"
        :active-ids="activeIds"
        :icon="project.icon"
        type="project"
        :tech-clickable="true"
        :tech-selected="(tech) => isTechSelected(tech)"
        @tech-click="toggleTech"
      />
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
