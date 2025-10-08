<script setup lang="ts">
const { t } = useI18n()

interface Props {
  studies?: Array<{
    id: number
    institution: string
    degree: string
    period: string
    startDate: string
    endDate: string
    focus: string
  }>
  activeIds?: (number | string)[]
}

const props = withDefaults(defineProps<Props>(), {
  studies: () => [
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
    }
  ],
  activeIds: () => []
})

// Check if a study is active
function isActive(studyId: number): boolean {
  return props.activeIds.includes(`study-${studyId}`)
}

// Handle heading click to update URL hash
function handleHeadingClick(elementId: string) {
  if (typeof window !== 'undefined') {
    window.history.pushState(null, '', `#${elementId}`)
  }
}

</script>

<template>
  <section>
    <h2 class="text-3xl font-bold text-gray-900 dark:text-white print:text-black mb-6">
      {{ t('studies.title') }}
    </h2>
    
    <div class="space-y-6">
      <UCard 
        v-for="study in props.studies" 
        :key="study.id" 
        :id="`study-${study.id}`"
        :class="{
          'print:!shadow-none print:!border print:!border-gray-300': true,
          'transition-all duration-300': true,
          'shadow-lg': isActive(study.id),
          'translate-x-2': isActive(study.id)
        }"
      >
        <template #header>
          <div class="flex justify-between items-start">
            <div>
              <h3 
                @click="handleHeadingClick(`study-${study.id}`)"
                :class="{
                  'text-xl font-semibold print:text-black cursor-pointer hover:underline': true,
                  'text-green-600 dark:text-green-400': isActive(study.id),
                  'text-gray-900 dark:text-white': !isActive(study.id)
                }"
              >
                {{ study.degree }}
              </h3>
              <p class="text-gray-600 dark:text-gray-400 print:text-gray-700">
                {{ study.institution }}
              </p>
            </div>
            <UBadge color="primary" variant="subtle" class="print:!bg-gray-100 print:!text-black">
              {{ study.period }}
            </UBadge>
          </div>
        </template>

        <p class="text-gray-700 dark:text-gray-300 print:text-black">
          {{ t('studies.focus') }}: {{ study.focus }}
        </p>
      </UCard>
    </div>
  </section>
</template>

<i18n lang="json">
{
  "en": {
    "studies": {
      "title": "Education",
      "focus": "Focus"
    }
  },
  "de": {
    "studies": {
      "title": "Ausbildung",
      "focus": "Schwerpunkt"
    }
  }
}
</i18n>
