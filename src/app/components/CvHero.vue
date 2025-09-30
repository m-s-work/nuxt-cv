<script setup lang="ts">
const { t } = useI18n()
const isScrolled = ref(false)

// Monitor scroll position
onMounted(() => {
  const handleScroll = () => {
    isScrolled.value = window.scrollY > 100
  }
  
  window.addEventListener('scroll', handleScroll)
  
  onUnmounted(() => {
    window.removeEventListener('scroll', handleScroll)
  })
})
</script>

<template>
  <div>
    <!-- Hero Section - Full width when at top -->
    <div 
      v-show="!isScrolled"
      class="hero-section min-h-[60vh] flex items-center justify-center bg-blue-600 dark:bg-blue-700 print:hidden transition-all duration-500"
    >
      <div class="text-center text-white px-4">
        <div class="mb-8">
          <img 
            src="https://via.placeholder.com/200x250/4F46E5/FFFFFF?text=Photo" 
            alt="Profile"
            class="w-48 h-56 object-cover rounded-lg shadow-2xl mx-auto border-4 border-white"
          />
        </div>
        <h1 class="text-5xl md:text-6xl font-bold mb-4">
          Max Mustermann
        </h1>
        <p class="text-2xl md:text-3xl mb-8 text-blue-100">
          Software Architect
        </p>
        <div class="animate-bounce">
          <svg class="w-8 h-8 mx-auto" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 14l-7 7m0 0l-7-7m7 7V3"></path>
          </svg>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
@keyframes bounce {
  0%, 100% {
    transform: translateY(0);
  }
  50% {
    transform: translateY(-10px);
  }
}

.animate-bounce {
  animation: bounce 2s infinite;
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
