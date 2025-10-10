<script setup lang="ts">
const { t } = useI18n()
const { scrollToElementSafely } = useSafeScroll()

// Navigation items
const navItems = computed(() => [
  { id: 'skills', label: t('navigation.skills'), sectionId: 'skills-section' },
  { id: 'experiences', label: t('navigation.experiences'), sectionId: 'experience-section' },
  { id: 'studies', label: t('navigation.studies'), sectionId: 'studies-section' },
  { id: 'projects', label: t('navigation.projects'), sectionId: 'projects-section' }
])

// Handle navigation click
function navigateToSection(sectionId: string) {
  scrollToElementSafely(sectionId, 'smooth')
}
</script>

<template>
  <nav class="cv-navigation print:hidden">
    <ul class="nav-list">
      <li v-for="item in navItems" :key="item.id" class="nav-item">
        <a
          href="#"
          class="nav-link"
          @click.prevent="navigateToSection(item.sectionId)"
        >
          {{ item.label }}
        </a>
      </li>
    </ul>
  </nav>
</template>

<style scoped>
.cv-navigation {
  position: fixed;
  top: 50%;
  right: 2rem;
  transform: translateY(-50%);
  z-index: 40;
  background-color: rgba(255, 255, 255, 0.95);
  padding: 1rem;
  border-radius: 0.75rem;
  box-shadow: 0 10px 25px rgba(0, 0, 0, 0.1);
  backdrop-filter: blur(10px);
}

.dark .cv-navigation {
  background-color: rgba(31, 41, 55, 0.95);
}

.nav-list {
  list-style: none;
  padding: 0;
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.nav-item {
  margin: 0;
}

.nav-link {
  display: block;
  padding: 0.5rem 1rem;
  color: rgb(55, 65, 81);
  text-decoration: none;
  font-size: 0.875rem;
  font-weight: 500;
  border-radius: 0.5rem;
  transition: all 0.2s ease;
  white-space: nowrap;
}

.dark .nav-link {
  color: rgb(209, 213, 219);
}

.nav-link:hover {
  background-color: rgb(59, 130, 246);
  color: white;
  transform: translateX(-4px);
}

/* Hide on smaller screens to avoid obstruction */
@media (max-width: 1023px) {
  .cv-navigation {
    display: none;
  }
}

/* Hide on print */
@media print {
  .cv-navigation {
    display: none !important;
  }
}
</style>

<i18n lang="json">
{
  "en": {
    "navigation": {
      "skills": "Skills",
      "experiences": "Experiences",
      "studies": "Studies",
      "projects": "Projects"
    }
  },
  "de": {
    "navigation": {
      "skills": "Fähigkeiten",
      "experiences": "Erfahrungen",
      "studies": "Studium",
      "projects": "Projekte"
    }
  }
}
</i18n>
