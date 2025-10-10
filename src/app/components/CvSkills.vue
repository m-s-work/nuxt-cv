<script setup lang="ts">
const { t } = useI18n()

// Skilled and liked technologies
const skilledTechs = ref([
  'Vue.js', 'Nuxt', 'TypeScript', 'Node.js', 
  'Docker', 'Kubernetes', 'PostgreSQL', 'Redis'
])

const likedTechs = ref([
  'Python', 'Go', 'React', 'AWS', 'GraphQL'
])
</script>

<template>
  <div id="skills-section" class="space-y-6">
    <div>
      <h2 class="text-2xl font-bold text-gray-900 dark:text-white print:text-black mb-4">
        {{ t('skills.skilled') }}
      </h2>
      
      <div class="flex flex-wrap gap-2">
        <TechBadge 
          v-for="tech in skilledTechs" 
          :key="tech"
          :technology="tech"
        />
      </div>
    </div>

    <div>
      <h2 class="text-2xl font-bold text-gray-900 dark:text-white print:text-black mb-4">
        {{ t('skills.liked') }}
      </h2>
      
      <div class="flex flex-wrap gap-2">
        <TechBadge 
          v-for="tech in likedTechs" 
          :key="tech"
          :technology="tech"
        />
      </div>
    </div>
  </div>
</template>

<i18n lang="json">
{
  "en": {
    "skills": {
      "skilled": "Core Skills & Technologies",
      "liked": "Additional Skills & Interests"
    }
  },
  "de": {
    "skills": {
      "skilled": "Kernkompetenzen & Technologien",
      "liked": "Weitere Fähigkeiten & Interessen"
    }
  }
}
</i18n>
