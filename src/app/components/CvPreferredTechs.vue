<script setup lang="ts">
const { t } = useI18n()
const { toggleTech, isTechSelected } = useTechFilter()

// Developer's preferred technologies
const preferredTechs = ref([
  'C#', 'Vue.js', 'Nuxt', 'TypeScript', 'Node.js', 'Docker'
])
</script>

<template>
  <div class="space-y-4">
    <h2 class="text-2xl font-bold text-gray-900 dark:text-white print:text-black mb-4">
      {{ t('preferredTechs.title') }}
    </h2>
    
    <div class="flex flex-wrap gap-2">
      <TechBadge 
        v-for="tech in preferredTechs" 
        :key="tech"
        :technology="tech"
        clickable
        :selected="isTechSelected(tech)"
        @click="toggleTech(tech)"
      />
    </div>
  </div>
</template>

<i18n lang="json">
{
  "en": {
    "preferredTechs": {
      "title": "Preferred Technologies"
    }
  },
  "de": {
    "preferredTechs": {
      "title": "Bevorzugte Technologien"
    }
  }
}
</i18n>
