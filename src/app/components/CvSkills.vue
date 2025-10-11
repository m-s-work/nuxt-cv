<script setup lang="ts">
const { t } = useI18n()
const { toggleTech, isTechSelected } = useTechFilter()
const { scrollToElementSafely, navigateToSection } = useSafeScroll()

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
  <div class="space-y-6">
    <div>
      <h2 class="text-2xl font-bold text-gray-900 dark:text-white print:text-black mb-4">
        <a :href="`#skills-section`" @click="navigateToSection($event, 'skills-section')" class="section-heading-link">
          {{ t('skills.skilled') }}
        </a>
      </h2>
      
      <div class="flex flex-wrap gap-2">
        <TechBadge 
          v-for="tech in skilledTechs" 
          :key="tech"
          :technology="tech"
          :clickable="true"
          :selected="isTechSelected(tech)"
          @click="toggleTech"
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
          :clickable="true"
          :selected="isTechSelected(tech)"
          @click="toggleTech"
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
