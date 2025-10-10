<script setup lang="ts">
const { t } = useI18n()
const { scrollToElementSafely } = useSafeScroll()

// Navigation items with their target sections
const navItems = computed(() => [
  { label: t('nav.skills'), target: 'skills-section' },
  { label: t('nav.experiences'), target: 'experiences-section' },
  { label: t('nav.studies'), target: 'studies-section' },
  { label: t('nav.projects'), target: 'projects-section' }
])

// Scroll to section
function scrollToSection(target: string) {
  scrollToElementSafely(target, 'smooth')
}
</script>

<template>
  <nav class="cv-navigation-menu print:hidden">
    <ul class="nav-list">
      <li v-for="item in navItems" :key="item.target" class="nav-item">
        <button 
          @click="scrollToSection(item.target)"
          class="nav-link"
        >
          {{ item.label }}
        </button>
      </li>
    </ul>
  </nav>
</template>

<style scoped>
.cv-navigation-menu {
  position: fixed;
  top: 1rem;
  right: 10rem;
  z-index: 40;
  background-color: rgba(255, 255, 255, 0.95);
  backdrop-filter: blur(8px);
  border-radius: 0.5rem;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.1);
  padding: 0.75rem;
}

.dark .cv-navigation-menu {
  background-color: rgba(31, 41, 55, 0.95);
}

.nav-list {
  display: flex;
  gap: 0.5rem;
  list-style: none;
  margin: 0;
  padding: 0;
}

.nav-item {
  margin: 0;
}

.nav-link {
  display: block;
  padding: 0.5rem 1rem;
  font-size: 0.875rem;
  font-weight: 500;
  color: rgb(55, 65, 81);
  background-color: transparent;
  border: none;
  border-radius: 0.375rem;
  cursor: pointer;
  transition: all 0.2s ease;
  white-space: nowrap;
}

.dark .nav-link {
  color: rgb(229, 231, 235);
}

.nav-link:hover {
  background-color: rgb(59, 130, 246);
  color: white;
}

.dark .nav-link:hover {
  background-color: rgb(59, 130, 246);
  color: white;
}

.nav-link:focus {
  outline: 2px solid rgb(59, 130, 246);
  outline-offset: 2px;
}

/* Make menu responsive on smaller screens */
@media (max-width: 640px) {
  .cv-navigation-menu {
    top: auto;
    bottom: 1rem;
    right: 1rem;
    left: 1rem;
  }
  
  .nav-list {
    justify-content: space-between;
  }
  
  .nav-link {
    padding: 0.5rem 0.75rem;
    font-size: 0.75rem;
  }
}

@media print {
  .cv-navigation-menu {
    display: none;
  }
}
</style>

<i18n lang="json">
{
  "en": {
    "nav": {
      "skills": "Skills",
      "experiences": "Experience",
      "studies": "Education",
      "projects": "Projects"
    }
  },
  "de": {
    "nav": {
      "skills": "Fähigkeiten",
      "experiences": "Erfahrung",
      "studies": "Ausbildung",
      "projects": "Projekte"
    }
  }
}
</i18n>
