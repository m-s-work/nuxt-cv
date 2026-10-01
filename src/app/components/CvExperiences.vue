<script setup lang="ts">
import type { TimelineItem } from '~/composables/useTimeline'

const { t } = useI18n()
const { toggleTech, isTechSelected, shouldShowItem } = useTechFilter()
const { navigateToSection } = useSafeScroll()

interface Props {
  experiences?: Array<TimelineItem & {
    company: string
    position: string
    period: string
    description: string
    technologies?: string[]
    images?: string[]
    url?: string
    urlLabel?: string
    urlHost?: string
  }>
  activeIds?: (number | string)[]
}

const props = withDefaults(defineProps<Props>(), {
  experiences: () => [],
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
      <a :href="`#experiences-section`" @click="navigateToSection($event, 'experiences-section')" class="section-heading-link">
        {{ t('experiences.title') }}
      </a>
    </h2>
    
    <div class="space-y-6">
      <CvBlock
        v-for="exp in filteredExperiences" 
        :key="exp.id"
        :id="exp.id"
        :title="exp.position"
        :url="exp.url"
        :url-label="exp.urlLabel"
        :url-host="exp.urlHost"
        :subtitle="exp.company"
        :period="exp.period"
        :description="exp.description"
        :technologies="exp.technologies"
        :images="exp.images"
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
