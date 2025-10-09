<script setup lang="ts">
const { t } = useI18n()

// Languages with proficiency levels
const languages = ref([
  { name: 'German', level: 'Native', code: 'de' },
  { name: 'English', level: 'Fluent (C1)', code: 'en' },
  { name: 'French', level: 'Intermediate (B1)', code: 'fr' }
])

// Get level color
const getLevelColor = (level: string) => {
  if (level.includes('Native') || level.includes('C2')) return 'green'
  if (level.includes('C1') || level.includes('Fluent')) return 'blue'
  if (level.includes('B')) return 'yellow'
  return 'gray'
}
</script>

<template>
  <div class="space-y-4">
    <h2 class="text-2xl font-bold text-gray-900 dark:text-white print:text-black mb-4">
      {{ t('languages.title') }}
    </h2>
    
    <div class="space-y-3">
      <div 
        v-for="lang in languages" 
        :key="lang.code"
        class="language-row flex items-center justify-between"
      >
        <span class="text-sm font-medium text-gray-800 dark:text-gray-200 print:text-black">
          {{ lang.name }}
        </span>
        <UBadge 
          :color="getLevelColor(lang.level)" 
          variant="subtle"
          class="print:!bg-gray-100 print:!text-black"
        >
          {{ lang.level }}
        </UBadge>
      </div>
    </div>
  </div>
</template>

<style scoped>
.language-row {
  padding: 0.25rem;
  margin: -0.25rem;
  border-radius: 0.375rem;
  transition: all 0.2s ease-in-out;
}

.language-row:hover {
  background-color: rgba(0, 0, 0, 0.05);
  transform: translateX(4px);
}

:global(.dark) .language-row:hover {
  background-color: rgba(255, 255, 255, 0.05);
}

@media print {
  .language-row:hover {
    background-color: transparent;
    transform: none;
  }
}
</style>

<i18n lang="json">
{
  "en": {
    "languages": {
      "title": "Languages"
    }
  },
  "de": {
    "languages": {
      "title": "Sprachen"
    }
  }
}
</i18n>
