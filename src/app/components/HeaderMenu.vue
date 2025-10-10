<script setup lang="ts">
const { t } = useI18n()
const { scrollToElementSafely } = useSafeScroll()

const menuItems = [
  { id: 'skills', label: 'menu.skills' },
  { id: 'experiences', label: 'menu.experiences' },
  { id: 'studies', label: 'menu.studies' },
  { id: 'projects', label: 'menu.projects' },
  { id: 'other', label: 'menu.other' }
]

function navigateTo(sectionId: string) {
  scrollToElementSafely(`${sectionId}-section`)
}
</script>

<template>
  <nav class="header-menu print:hidden">
    <ul class="menu-list">
      <li v-for="item in menuItems" :key="item.id" class="menu-item">
        <a 
          @click.prevent="navigateTo(item.id)"
          class="menu-link"
          :href="`#${item.id}-section`"
        >
          {{ t(item.label) }}
        </a>
      </li>
    </ul>
  </nav>
</template>

<style scoped>
.header-menu {
  position: absolute;
  top: 2rem;
  right: 2rem;
  z-index: 50;
}

.menu-list {
  display: flex;
  gap: 0.5rem;
  list-style: none;
  margin: 0;
  padding: 0;
}

.menu-item {
  margin: 0;
}

.menu-link {
  color: white;
  text-decoration: none;
  padding: 0.5rem 1rem;
  display: block;
  font-size: 0.875rem;
  font-weight: 500;
  letter-spacing: 0.025em;
  transition: all 0.2s ease-in-out;
  border-radius: 0.25rem;
  position: relative;
  overflow: hidden;
}

.menu-link::before {
  content: '';
  position: absolute;
  bottom: 0;
  left: 50%;
  width: 0;
  height: 2px;
  background-color: white;
  transition: all 0.3s ease-in-out;
  transform: translateX(-50%);
}

.menu-link:hover {
  transform: translateY(-2px);
}

.menu-link:hover::before {
  width: 80%;
}

/* Responsive adjustments */
@media (max-width: 640px) {
  .header-menu {
    top: 1rem;
    right: 1rem;
  }
  
  .menu-list {
    flex-direction: column;
    gap: 0.25rem;
    align-items: flex-end;
  }
  
  .menu-link {
    font-size: 0.75rem;
    padding: 0.375rem 0.75rem;
  }
}

@media (min-width: 641px) and (max-width: 1024px) {
  .menu-link {
    font-size: 0.8125rem;
    padding: 0.4375rem 0.875rem;
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
      "projects": "Projects",
      "other": "Other"
    }
  },
  "de": {
    "menu": {
      "skills": "Fähigkeiten",
      "experiences": "Erfahrungen",
      "studies": "Studium",
      "projects": "Projekte",
      "other": "Sonstiges"
    }
  }
}
</i18n>
