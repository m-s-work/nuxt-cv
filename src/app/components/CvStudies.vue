<script setup lang="ts">
import type { TimelineItem } from '~/composables/useTimeline'

const { t } = useI18n()
const { toggleTech, isTechSelected, shouldShowItem } = useTechFilter()
const { navigateToSection } = useSafeScroll()

interface Props {
  studies?: Array<TimelineItem & {
    institution: string
    degree: string
    period: string
    endDate: string  // Override to make non-nullable for studies
    focus: string
    technologies?: string[]
  }>
  activeIds?: (number | string)[]
}

const props = withDefaults(defineProps<Props>(), {
  studies: () => [],
  activeIds: () => []
})

// Filter studies based on selected technologies
const filteredStudies = computed(() => {
  return props.studies.filter(study => {
    if (!study.technologies || study.technologies.length === 0) return true
    return shouldShowItem(study.technologies)
  })
})

</script>

<template>
  <section>
    <h2 class="text-3xl font-bold text-gray-900 dark:text-white print:text-black mb-6">
      <a :href="`#studies-section`" @click="navigateToSection($event, 'studies-section')" class="section-heading-link">
        {{ t('studies.title') }}
      </a>
    </h2>
    
    <div class="space-y-6">
      <CvBlock
        v-for="study in filteredStudies" 
        :key="study.id"
        :id="study.id"
        :title="study.degree"
        :subtitle="study.institution"
        :period="study.period"
        :description="`${t('studies.focus')}: ${study.focus}`"
        :technologies="study.technologies"
        :active-ids="activeIds"
        :icon="study.icon"
        type="study"
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
