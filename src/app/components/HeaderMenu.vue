<script setup lang="ts">
const { t } = useI18n()
const { scrollToElementSafely } = useSafeScroll()

interface MenuItem {
  id: string
  label: string
  targetId: string
}

const menuItems = computed<MenuItem[]>(() => [
  { id: 'skills', label: t('menu.skills'), targetId: 'skills-section' },
  { id: 'experiences', label: t('menu.experiences'), targetId: 'experience-section' },
  { id: 'studies', label: t('menu.studies'), targetId: 'studies-section' },
  { id: 'projects', label: t('menu.projects'), targetId: 'projects-section' },
  { id: 'other', label: t('menu.other'), targetId: 'other-section' }
])

function navigateTo(targetId: string) {
  scrollToElementSafely(targetId, 'smooth')
}
</script>

<template>
  <nav class="header-menu print:hidden">
    <ul class="menu-list">
      <li 
        v-for="item in menuItems" 
        :key="item.id"
        class="menu-item"
      >
        <a 
          href="#"
          class="menu-link"
          @click.prevent="navigateTo(item.targetId)"
        >
          {{ item.label }}
        </a>
      </li>
    </ul>
  </nav>
</template>

<style scoped>
.header-menu {
  position: fixed;
  top: 1rem;
  right: 1rem;
  z-index: 40;
  pointer-events: none;
}

.menu-list {
  display: flex;
  flex-direction: column;
  align-items: flex-end;
  gap: 0.5rem;
  list-style: none;
  padding: 0;
  margin: 0;
}

.menu-item {
  pointer-events: auto;
}

.menu-link {
  display: inline-block;
  padding: 0.5rem 1rem;
  font-size: 0.875rem;
  font-weight: 500;
  color: rgb(55, 65, 81);
  text-decoration: none;
  transition: all 0.2s ease-in-out;
  position: relative;
}

.menu-link::after {
  content: '';
  position: absolute;
  bottom: 0.25rem;
  left: 0;
  right: 0;
  height: 2px;
  background: rgb(37, 99, 235);
  transform: scaleX(0);
  transform-origin: right;
  transition: transform 0.2s ease-in-out;
}

.menu-link:hover {
  color: rgb(37, 99, 235);
}

.menu-link:hover::after {
  transform: scaleX(1);
  transform-origin: left;
}

/* Dark mode */
.dark .menu-link {
  color: rgb(209, 213, 219);
}

.dark .menu-link:hover {
  color: rgb(96, 165, 250);
}

.dark .menu-link::after {
  background: rgb(96, 165, 250);
}

/* Responsive - hide on small screens to avoid overlap with language selector */
@media (max-width: 640px) {
  .header-menu {
    top: 4rem;
  }
  
  .menu-list {
    gap: 0.25rem;
  }
  
  .menu-link {
    font-size: 0.75rem;
    padding: 0.375rem 0.75rem;
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
      "experiences": "Experience",
      "studies": "Education",
      "projects": "Projects",
      "other": "Other"
    }
  },
  "de": {
    "menu": {
      "skills": "Fähigkeiten",
      "experiences": "Erfahrung",
      "studies": "Ausbildung",
      "projects": "Projekte",
      "other": "Sonstiges"
    }
  }
}
</i18n>
