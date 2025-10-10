<script setup lang="ts">
const { t } = useI18n()
const { scrollToElementSafely } = useSafeScroll()

// Navigate to a section
function navigateTo(sectionId: string) {
  scrollToElementSafely(sectionId, 'smooth')
}
</script>

<template>
  <nav class="header-menu print:hidden">
    <ul class="menu-items">
      <li>
        <a 
          @click.prevent="navigateTo('skills-section')"
          href="#skills-section"
          class="menu-link"
        >
          {{ t('menu.skills') }}
        </a>
      </li>
      <li>
        <a 
          @click.prevent="navigateTo('experience-1')"
          href="#experience-1"
          class="menu-link"
        >
          {{ t('menu.experiences') }}
        </a>
      </li>
      <li>
        <a 
          @click.prevent="navigateTo('study-1')"
          href="#study-1"
          class="menu-link"
        >
          {{ t('menu.studies') }}
        </a>
      </li>
      <li>
        <a 
          @click.prevent="navigateTo('project-1')"
          href="#project-1"
          class="menu-link"
        >
          {{ t('menu.projects') }}
        </a>
      </li>
    </ul>
  </nav>
</template>

<style scoped>
.header-menu {
  position: fixed;
  top: 2rem;
  right: 2rem;
  z-index: 50;
}

.menu-items {
  display: flex;
  flex-direction: column;
  align-items: flex-end;
  gap: 0.75rem;
  list-style: none;
  padding: 0;
  margin: 0;
}

.menu-link {
  position: relative;
  font-size: 0.875rem;
  font-weight: 500;
  letter-spacing: 0.025em;
  color: rgb(107, 114, 128); /* gray-500 */
  text-decoration: none;
  padding: 0.5rem 1rem;
  transition: all 0.2s ease-in-out;
  cursor: pointer;
  text-transform: uppercase;
}

.menu-link::after {
  content: '';
  position: absolute;
  bottom: 0.25rem;
  right: 0;
  width: 0;
  height: 2px;
  background: linear-gradient(90deg, transparent, rgb(59, 130, 246)); /* blue-500 */
  transition: width 0.3s ease-in-out;
}

.menu-link:hover {
  color: rgb(59, 130, 246); /* blue-500 */
  transform: translateX(-4px);
}

.menu-link:hover::after {
  width: 100%;
  right: auto;
  left: 0;
  background: linear-gradient(90deg, rgb(59, 130, 246), transparent);
}

/* Dark mode */
:global(.dark) .menu-link {
  color: rgb(156, 163, 175); /* gray-400 */
}

:global(.dark) .menu-link:hover {
  color: rgb(96, 165, 250); /* blue-400 */
}

:global(.dark) .menu-link::after {
  background: linear-gradient(90deg, transparent, rgb(96, 165, 250)); /* blue-400 */
}

:global(.dark) .menu-link:hover::after {
  background: linear-gradient(90deg, rgb(96, 165, 250), transparent);
}

/* Mobile responsive */
@media (max-width: 768px) {
  .header-menu {
    top: 1rem;
    right: 1rem;
  }
  
  .menu-items {
    gap: 0.5rem;
  }
  
  .menu-link {
    font-size: 0.75rem;
    padding: 0.375rem 0.75rem;
  }
}

/* Hide on small mobile screens */
@media (max-width: 480px) {
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
      "experiences": "Erfahrungen",
      "studies": "Ausbildung",
      "projects": "Projekte"
    }
  }
}
</i18n>
