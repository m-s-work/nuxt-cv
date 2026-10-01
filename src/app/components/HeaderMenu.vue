<script setup lang="ts">
const { t } = useI18n()
const { scrollToElementSafely } = useSafeScroll()

const { cv } = useCv()

// Only link sections the visitor's profile actually delivers.
const menuItems = computed(() => [
  { id: 'skills', label: 'menu.skills', visible: true },
  { id: 'experiences', label: 'menu.experiences', visible: !!cv.value?.experiences?.length },
  { id: 'studies', label: 'menu.studies', visible: !!cv.value?.studies?.length },
  { id: 'projects', label: 'menu.projects', visible: !!cv.value?.projects?.length },
  { id: 'other', label: 'menu.other', visible: !!cv.value?.otherEntries?.length }
].filter(item => item.visible))

function navigateTo(event: Event, sectionId: string) {
  event.preventDefault()
  const hash = `#${sectionId}-section`
  
  // Update URL hash to add to browser history
  window.history.pushState(null, '', hash)
  
  // Scroll to the section
  scrollToElementSafely(`${sectionId}-section`)
}
</script>

<template>
  <nav class="header-menu print:hidden">
    <ul class="menu-list">
      <li v-for="item in menuItems" :key="item.id" class="menu-item">
        <a 
          @click="navigateTo($event, item.id)"
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
  /* Left: the language switcher sits fixed in the top right corner. */
  position: absolute;
  top: 2rem;
  left: 2rem;
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
/* Phones: sections follow each other in one column and the menu would not fit next to the
   language switcher – hide it. */
@media (max-width: 640px) {
  .header-menu {
    display: none;
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
