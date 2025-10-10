<script setup lang="ts">
const { t } = useI18n()

interface Props {
  experiences?: Array<{
    id: number
    company: string
    position: string
    period: string
    startDate: string
    endDate: string | null
    description: string
    technologies: string[]
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

</script>

<template>
  <section>
    <h2 class="text-3xl font-bold text-gray-900 dark:text-white print:text-black mb-6">
      {{ t('experiences.title') }}
    </h2>
    
    <div class="space-y-6">
      <CvBlock
        v-for="exp in props.experiences" 
        :key="exp.id"
        :id="exp.id"
        :title="exp.position"
        :subtitle="exp.company"
        :period="exp.period"
        :description="exp.description"
        :technologies="exp.technologies"
        :active-ids="activeIds"
        type="experience"
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
