<script setup lang="ts">
const { t } = useI18n()
const { selectedTechs, isFiltering, clearFilters } = useTechFilter()
</script>

<template>
  <div 
    v-if="isFiltering"
    class="tech-filter-bar fixed top-0 left-0 right-0 z-50 bg-yellow-50 dark:bg-yellow-900/20 border-b border-yellow-200 dark:border-yellow-800 shadow-md print:relative print:bg-yellow-50 print:mb-4"
  >
    <div class="max-w-7xl mx-auto px-4 py-3 flex items-center justify-between gap-4">
      <div class="flex items-center gap-3 flex-1 min-w-0">
        <UIcon 
          name="i-heroicons-funnel" 
          class="text-yellow-700 dark:text-yellow-400 print:text-yellow-700 flex-shrink-0 w-5 h-5" 
        />
        <div class="flex-1 min-w-0">
          <p class="text-sm font-semibold text-yellow-800 dark:text-yellow-300 print:text-yellow-800">
            {{ t('filter.active') }}
          </p>
          <!-- Tech badges for screen/non-print -->
          <div class="flex flex-wrap gap-2 mt-1 print:hidden">
            <TechBadge 
              v-for="tech in selectedTechs" 
              :key="tech"
              :technology="tech"
              :selected="true"
            />
          </div>
          <!-- Plain text for print -->
          <p class="hidden print:block text-sm text-yellow-700 mt-1">
            {{ selectedTechs.join(', ') }}
          </p>
        </div>
      </div>
      <button
        @click="clearFilters"
        class="text-sm font-medium text-yellow-700 dark:text-yellow-400 hover:text-yellow-900 dark:hover:text-yellow-200 print:text-yellow-700 flex-shrink-0 underline hover:no-underline"
      >
        {{ t('filter.clear') }}
      </button>
    </div>
  </div>
</template>

<i18n lang="json">
{
  "en": {
    "filter": {
      "active": "Filtering by technologies:",
      "clear": "Clear filters"
    }
  },
  "de": {
    "filter": {
      "active": "Filterung nach Technologien:",
      "clear": "Filter löschen"
    }
  }
}
</i18n>
