<script setup lang="ts">
import type { TimelineItem } from '~/composables/useTimeline'

const { t } = useI18n()
const { navigateToSection } = useSafeScroll()

interface Props {
  entries?: Array<TimelineItem & {
    title: string
    institution?: string
    period?: string
    description?: string
    showPeriod?: boolean
    images?: string[]
  }>
  activeIds?: (number | string)[]
}

const props = withDefaults(defineProps<Props>(), {
  entries: () => [],
  activeIds: () => []
})
</script>

<template>
  <section>
    <h2 class="text-3xl font-bold text-gray-900 dark:text-white print:text-black mb-6">
      <a :href="`#other-section`" @click="navigateToSection($event, 'other-section')" class="section-heading-link">
        {{ t('otherExperiences.title') }}
      </a>
    </h2>
    
    <div class="space-y-6">
      <CvBlock
        v-for="entry in props.entries" 
        :key="entry.id"
        :id="entry.id"
        :title="entry.title"
        :subtitle="entry.institution"
        :period="entry.period"
        :description="entry.description"
        :images="entry.images"
        :active-ids="activeIds"
        :icon="entry.icon"
        type="other"
        :show-period="entry.showPeriod !== false"
        :show-technologies="false"
      />
    </div>
  </section>
</template>

<i18n lang="json">
{
  "en": {
    "otherExperiences": {
      "title": "Other Experience & Education"
    }
  },
  "de": {
    "otherExperiences": {
      "title": "Sonstige Erfahrung & Ausbildung"
    }
  }
}
</i18n>
