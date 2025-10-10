<script setup lang="ts">
const { t } = useI18n()
const { isFiltering, selectedTechs, clearFilters } = useTechFilter()
</script>

<template>
  <div 
    v-if="isFiltering" 
    class="print:hidden fixed top-0 left-0 right-0 z-50 bg-orange-500 dark:bg-orange-600 text-white shadow-lg transition-all duration-300"
  >
    <div class="container mx-auto px-4 py-3">
      <div class="flex items-center justify-between flex-wrap gap-2">
        <div class="flex items-center gap-3 flex-wrap">
          <div class="flex items-center gap-2">
            <UIcon name="i-heroicons-funnel" class="w-5 h-5" />
            <span class="font-semibold">{{ t('filter.active') }}:</span>
          </div>
          <div class="flex flex-wrap gap-2">
            <UBadge 
              v-for="tech in selectedTechs" 
              :key="tech"
              color="white"
              variant="solid"
              size="md"
              class="px-3 py-1"
            >
              {{ tech }}
            </UBadge>
          </div>
        </div>
        <UButton 
          color="white" 
          variant="outline"
          size="sm"
          @click="clearFilters"
          class="hover:bg-white hover:text-orange-600 transition-colors"
        >
          {{ t('filter.clear') }}
        </UButton>
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
