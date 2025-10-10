<script setup lang="ts">
const { t } = useI18n()
const { scrollToElementSafely } = useSafeScroll()

// Menu items with their corresponding section IDs
const menuItems = [
  { id: 'skills-section', label: 'menu.skills' },
  { id: 'experiences-section', label: 'menu.experiences' },
  { id: 'studies-section', label: 'menu.studies' },
  { id: 'projects-section', label: 'menu.projects' }
]

// Scroll to a section
function scrollToSection(sectionId: string) {
  const element = document.getElementById(sectionId)
  if (element) {
    scrollToElementSafely(sectionId, 'smooth')
  }
}
</script>

<template>
  <nav class="header-menu print:hidden">
    <div class="menu-container">
      <div class="menu-items">
        <button
          v-for="item in menuItems"
          :key="item.id"
          @click="scrollToSection(item.id)"
          class="menu-item"
        >
          {{ t(item.label) }}
        </button>
      </div>
    </div>
  </nav>
</template>

<style scoped>
.header-menu {
  position: fixed;
  top: 4rem;
  left: 0;
  right: 0;
  z-index: 40;
  background-color: rgba(255, 255, 255, 0.95);
  backdrop-filter: blur(10px);
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
}

.dark .header-menu {
  background-color: rgba(31, 41, 55, 0.95);
}

.menu-container {
  max-width: 1280px;
  margin: 0 auto;
  padding: 0.75rem 1rem;
}

.menu-items {
  display: flex;
  gap: 0.5rem;
  justify-content: center;
  flex-wrap: wrap;
}

.menu-item {
  padding: 0.5rem 1rem;
  border-radius: 0.375rem;
  font-size: 0.875rem;
  font-weight: 500;
  color: rgb(55 65 81);
  background-color: transparent;
  border: none;
  cursor: pointer;
  transition: all 0.2s ease;
}

.dark .menu-item {
  color: rgb(209 213 219);
}

.menu-item:hover {
  background-color: rgb(59 130 246);
  color: white;
}

.dark .menu-item:hover {
  background-color: rgb(59 130 246);
  color: white;
}

/* Responsive adjustments */
@media (max-width: 640px) {
  .header-menu {
    top: 3.5rem;
  }
  
  .menu-items {
    gap: 0.25rem;
  }
  
  .menu-item {
    padding: 0.375rem 0.75rem;
    font-size: 0.8125rem;
  }
}

@media print {
  .header-menu {
    display: none;
  }
}
</style>

<i18n lang="json">
{
  "en": {
    "menu": {
      "skills": "Skills",
      "experiences": "Experiences",
      "studies": "Studies",
      "projects": "Projects"
    }
  },
  "de": {
    "menu": {
      "skills": "Fähigkeiten",
      "experiences": "Berufserfahrung",
      "studies": "Studium",
      "projects": "Projekte"
    }
  }
}
</i18n>
