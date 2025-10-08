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

// Check if an experience is active
function isActive(expId: number): boolean {
  return props.activeIds.includes(`exp-${expId}`)
}

</script>

<template>
  <section>
    <h2 class="text-3xl font-bold text-gray-900 dark:text-white print:text-black mb-6">
      {{ t('experiences.title') }}
    </h2>
    
    <div class="space-y-6">
      <UCard 
        v-for="exp in props.experiences" 
        :key="exp.id" 
        :id="`experience-${exp.id}`"
        :class="{
          'print:!shadow-none print:!border print:!border-gray-300': true,
          'transition-all duration-300': true,
          'shadow-lg': isActive(exp.id),
          'translate-x-2': isActive(exp.id)
        }"
      >
        <template #header>
          <div class="flex justify-between items-start">
            <div>
              <h3 
                :class="{
                  'text-xl font-semibold print:text-black': true,
                  'text-blue-600 dark:text-blue-400': isActive(exp.id),
                  'text-gray-900 dark:text-white': !isActive(exp.id)
                }"
              >
                {{ exp.position }}
              </h3>
              <p class="text-gray-600 dark:text-gray-400 print:text-gray-700">
                {{ exp.company }}
              </p>
            </div>
            <UBadge color="primary" variant="subtle" class="print:!bg-gray-100 print:!text-black">
              {{ exp.period }}
            </UBadge>
          </div>
        </template>

        <p class="text-gray-700 dark:text-gray-300 print:text-black mb-4">
          {{ exp.description }}
        </p>

        <div class="flex flex-wrap gap-2">
          <UBadge 
            v-for="tech in exp.technologies" 
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
