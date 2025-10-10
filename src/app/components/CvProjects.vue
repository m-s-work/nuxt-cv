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
    icon?: string
  }>
  activeIds?: (number | string)[]
}

const props = withDefaults(defineProps<Props>(), {
  projects: () => [],
  activeIds: () => []
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
      {{ t('projects.title') }}
    </h2>
    
    <div class="space-y-6">
      <CvBlock
        v-for="project in props.projects" 
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
