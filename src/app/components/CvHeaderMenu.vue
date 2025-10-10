<script setup lang="ts">
const { t } = useI18n()
const { scrollToElementSafely } = useSafeScroll()

// Define navigation items
const navItems = computed(() => [
  { id: 'skills', label: t('menu.skills') },
  { id: 'experiences', label: t('menu.experiences') },
  { id: 'studies', label: t('menu.studies') },
  { id: 'projects', label: t('menu.projects') }
])

// Handle navigation click
function navigateToSection(sectionId: string) {
  scrollToElementSafely(sectionId, 'smooth')
}
</script>

<template>
  <nav class="header-menu print:hidden">
    <div class="flex items-center gap-3">
      <a
        v-for="item in navItems"
        :key="item.id"
        href="#"
        @click.prevent="navigateToSection(item.id)"
        class="nav-link text-sm font-medium text-gray-700 dark:text-gray-300 hover:text-blue-600 dark:hover:text-blue-400 transition-colors"
      >
        {{ item.label }}
      </a>
    </div>
  </nav>
</template>

<style scoped>
.header-menu {
  position: fixed;
  top: 5rem;
  right: 1rem;
  z-index: 40;
  background-color: rgba(255, 255, 255, 0.95);
  padding: 0.75rem 1.25rem;
  border-radius: 0.5rem;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
}

.dark .header-menu {
  background-color: rgba(31, 41, 55, 0.95);
}

.nav-link {
  cursor: pointer;
  white-space: nowrap;
}

@media print {
  .header-menu {
    display: none;
  }
}

/* Responsive adjustments */
@media (max-width: 768px) {
  .header-menu {
    top: 4.5rem;
    right: 0.5rem;
    padding: 0.5rem 0.75rem;
  }
  
  .header-menu .flex {
    flex-direction: column;
    gap: 0.5rem;
  }
  
  .nav-link {
    font-size: 0.75rem;
  }
}
</style>

<i18n lang="json">
{
  "en": {
    "menu": {
      "skills": "Skills",
      "experiences": "Experiences",
      "studies": "Education",
      "projects": "Projects"
    }
  },
  "de": {
    "menu": {
      "skills": "Fähigkeiten",
      "experiences": "Erfahrung",
      "studies": "Ausbildung",
      "projects": "Projekte"
    }
  }
}
</i18n>
