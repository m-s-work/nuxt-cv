<script setup lang="ts">
import type { TimelineItem } from '~/composables/useTimeline'

const { t } = useI18n()
const { toggleTech, isTechSelected, shouldShowItem } = useTechFilter()

interface Props {
  experiences?: Array<TimelineItem & {
    company: string
    position: string
    period: string
    description: string
    technologies?: string[]
  }>
  activeIds?: (number | string)[]
}

const props = withDefaults(defineProps<Props>(), {
  experiences: () => [
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
      id: 2,
      company: 'Software Solutions Ltd.',
      position: 'Full Stack Developer',
      period: '2017 - 2020',
      startDate: '2017-03-01',
      endDate: '2019-12-31',
      description: 'Developed enterprise web applications and microservices',
      technologies: ['Vue.js', 'Express', 'PostgreSQL', 'Redis']
    }
  ],
  activeIds: () => []
})

// Filter experiences based on selected technologies
const filteredExperiences = computed(() => {
  return props.experiences.filter(exp => {
    if (!exp.technologies || exp.technologies.length === 0) return true
    return shouldShowItem(exp.technologies)
  })
})

</script>

<template>
  <section>
    <h2 class="text-3xl font-bold text-gray-900 dark:text-white print:text-black mb-6">
      {{ t('experiences.title') }}
    </h2>
    
    <div class="space-y-6">
      <CvBlock
        v-for="exp in filteredExperiences" 
        :key="exp.id"
        :id="exp.id"
        :title="exp.position"
        :subtitle="exp.company"
        :period="exp.period"
        :description="exp.description"
        :technologies="exp.technologies"
        :active-ids="activeIds"
        :icon="exp.icon"
        type="experience"
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
    "experiences": {
      "title": "Professional Experience"
    }
  },
  "de": {
    "experiences": {
      "title": "Berufserfahrung"
    }
  }
}
</i18n>
