<script setup lang="ts">
const { t } = useI18n()

interface Props {
  entries?: Array<{
    id: number
    title: string
    institution?: string
    period?: string
    startDate: string
    endDate: string | null
    description?: string
    showPeriod?: boolean
    images?: string[]
    icon?: string
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
      {{ t('otherExperiences.title') }}
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
