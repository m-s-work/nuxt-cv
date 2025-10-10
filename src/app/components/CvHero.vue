<script setup lang="ts">
const { t } = useI18n()

// Profile information with academic titles
const profile = ref({
  name: 'Max Mustermann',
  title: 'Software Architect',
  prependedTitles: ['Dr.'],
  appendedTitles: ['PhD', 'MSc']
})

// Compute full name with titles
const fullName = computed(() => {
  const prepended = profile.value.prependedTitles?.join(' ') || ''
  const appended = profile.value.appendedTitles?.join(', ') || ''
  
  let result = profile.value.name
  if (prepended) {
    result = `${prepended} ${result}`
  }
  if (appended) {
    result = `${result}, ${appended}`
  }
  return result
})

// Function to scroll down past the hero section
function scrollToContent() {
  // Find the cv-container (main content) and scroll to it
  const mainContent = document.querySelector('.cv-container')
  if (mainContent) {
    mainContent.scrollIntoView({ behavior: 'smooth', block: 'start' })
  } else {
    // Fallback: scroll down by 100vh if element not found
    window.scrollTo({
      top: window.innerHeight,
      behavior: 'smooth'
    })
  }
}
</script>

<template>
  <div class="hero-section">
    <div class="hero-content">
      <div class="mb-8">
        <img 
          src="/images/profile-medium.jpg"
          srcset="/images/profile-small.jpg 160w, /images/profile-medium.jpg 192w, /images/profile-large.jpg 320w, /images/profile-xlarge.jpg 384w"
          sizes="(max-width: 640px) 160px, 192px"
          alt="Profile"
          class="profile-img"
        />
      </div>
      <h1 class="hero-title">
        {{ fullName }}
      </h1>
      <p class="hero-subtitle">
        {{ profile.title }}
      </p>
      <div class="scroll-indicator cursor-pointer" @click="scrollToContent">
        <svg class="w-12 h-12 mx-auto" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 14l-7 7m0 0l-7-7m7 7V3"></path>
        </svg>
      </div>
    </div>
  </div>
</template>

<style scoped>
.hero-section {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background: linear-gradient(135deg, rgb(37 99 235) 0%, rgb(29 78 216) 100%);
}

@media (prefers-color-scheme: dark) {
  .hero-section {
    background: linear-gradient(135deg, rgb(29 78 216) 0%, rgb(30 64 175) 100%);
  }
}

.hero-content {
  text-align: center;
  color: white;
  padding: 0 1rem;
}

.profile-img {
  width: 12rem;
  height: 14rem;
  object-fit: cover;
  border-radius: 0.5rem;
  box-shadow: 0 25px 50px -12px rgba(0, 0, 0, 0.25);
  margin: 0 auto;
  border: 4px solid white;
}

.hero-title {
  font-size: 3.75rem;
  font-weight: bold;
  margin-bottom: 1rem;
}

@media (min-width: 768px) {
  .hero-title {
    font-size: 4.5rem;
  }
}

.hero-subtitle {
  font-size: 1.5rem;
  margin-bottom: 2rem;
  color: rgb(191 219 254);
}

@media (min-width: 768px) {
  .hero-subtitle {
    font-size: 1.875rem;
  }
}

@keyframes bounce {
  0%, 100% {
    transform: translateY(0);
  }
  50% {
    transform: translateY(-10px);
  }
}

.scroll-indicator {
  animation: bounce 2s infinite;
}

@media print {
  .hero-section {
    display: none;
  }
}
</style>

<i18n lang="json">
{
  "en": {
    "hero": {
      "title": "Hero Section"
    }
  },
  "de": {
    "hero": {
      "title": "Hero-Bereich"
    }
  }
}
</i18n>
