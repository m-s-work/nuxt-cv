<script setup lang="ts">
const { t } = useI18n()
const { isFiltering, selectedTechs, clearFilters } = useTechFilter()
</script>

<template>
  <div 
    v-if="isFiltering" 
    class="fixed top-0 left-0 right-0 z-50 bg-orange-500 dark:bg-orange-600 text-white shadow-lg transition-all duration-300 print:relative print:bg-orange-400 print:text-black print:shadow-none print:mb-4"
  >
    <div class="container mx-auto px-4 py-3">
      <div class="flex items-center justify-between flex-wrap gap-2">
        <div class="flex items-center gap-3 flex-wrap">
          <div class="flex items-center gap-2">
            <UIcon name="i-heroicons-funnel" class="w-5 h-5 print:hidden" />
            <span class="font-semibold">{{ t('filter.active') }}:</span>
          </div>
          <div class="flex flex-wrap gap-2">
            <UBadge 
              v-for="tech in selectedTechs" 
              :key="tech"
              color="white"
              variant="solid"
              size="md"
              class="px-3 py-1 print:!bg-gray-200 print:!text-black print:border print:border-gray-400"
            >
              {{ tech }}
            </UBadge>
          </div>
        </div>
        <button
          @click="clearFilters"
          class="text-sm underline hover:no-underline print:hidden"
        >
          {{ t('filter.clear') }}
        </button>
      </div>
    </div>
  </div>
</template>

<i18n lang="json">
{
  "en": {
    "filter": {
      "active": "Filtering by",
      "clear": "Clear Filter"
    }
  },
  "de": {
    "filter": {
      "active": "Filtern nach",
      "clear": "Filter löschen"
    }
  }
}
</i18n>
