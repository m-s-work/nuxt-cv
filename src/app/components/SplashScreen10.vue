<script setup lang="ts">
const { t } = useI18n()

defineProps<{
  onComplete?: () => void
}>()

onMounted(() => {
  const timer = setTimeout(() => {
    // Animation completes naturally via CSS
  }, 3100)
  
  onUnmounted(() => clearTimeout(timer))
})
</script>

<template>
  <div class="splash-screen splash-grid">
    <div class="splash-content">
      <!-- Animated grid -->
      <div class="grid-container">
        <div v-for="i in 16" :key="i" class="grid-cell" :style="`--delay: ${i * 0.05}s`"></div>
      </div>
      
      <div class="splash-text">
        <h1 class="text-5xl md:text-7xl font-bold">{{ t('splash.title') }}</h1>
      </div>
    </div>
  </div>
</template>

<style scoped>
.splash-screen {
  position: fixed;
  top: 0;
  left: 0;
  width: 100vw;
  height: 100vh;
  z-index: 9999;
  display: flex;
  align-items: center;
  justify-content: center;
  overflow: hidden;
}

.splash-grid {
  background: linear-gradient(135deg, rgb(37 99 235) 0%, rgb(29 78 216) 100%);
  animation: fadeOut 3.1s ease-in-out forwards;
}

.splash-content {
  position: relative;
  text-align: center;
  color: white;
}

.grid-container {
  display: grid;
  grid-template-columns: repeat(4, 50px);
  grid-template-rows: repeat(4, 50px);
  gap: 15px;
  margin: 0 auto 3rem;
  width: fit-content;
}

.grid-cell {
  width: 50px;
  height: 50px;
  background: white;
  border-radius: 8px;
  animation: gridPulse 1.5s ease-in-out infinite;
  animation-delay: var(--delay);
}

.splash-text h1 {
  animation: fadeInUp 1s ease-out 0.8s both;
}

@keyframes gridPulse {
  0%, 100% {
    opacity: 0.2;
    transform: scale(0.8);
  }
  50% {
    opacity: 1;
    transform: scale(1);
  }
}

@keyframes fadeInUp {
  from {
    opacity: 0;
    transform: translateY(30px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

@keyframes fadeOut {
  0% {
    opacity: 1;
  }
  70% {
    opacity: 1;
  }
  100% {
    opacity: 0;
    pointer-events: none;
  }
}

@media print {
  .splash-screen {
    display: none !important;
  }
}
</style>

<i18n lang="json">
{
  "en": {
    "splash": {
      "title": "Building Solutions"
    }
  },
  "de": {
    "splash": {
      "title": "Lösungen Entwickeln"
    }
  }
}
</i18n>
