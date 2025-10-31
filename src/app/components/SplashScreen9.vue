<script setup lang="ts">
const { t } = useI18n()

defineProps<{
  onComplete?: () => void
}>()

onMounted(() => {
  const timer = setTimeout(() => {
    // Animation completes naturally via CSS
  }, 2800)
  
  onUnmounted(() => clearTimeout(timer))
})
</script>

<template>
  <div class="splash-screen splash-bars">
    <div class="splash-content">
      <!-- Animated bars -->
      <div class="bars-container">
        <div v-for="i in 7" :key="i" class="bar" :style="`--delay: ${i * 0.1}s`"></div>
      </div>
      
      <div class="splash-text">
        <h1 class="text-4xl md:text-6xl font-bold">{{ t('splash.title') }}</h1>
        <p class="text-xl md:text-2xl mt-4">{{ t('splash.subtitle') }}</p>
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

.splash-bars {
  background: linear-gradient(135deg, rgb(37 99 235) 0%, rgb(29 78 216) 100%);
  animation: fadeOut 2.8s ease-in-out forwards;
}

.splash-content {
  position: relative;
  text-align: center;
  color: white;
}

.bars-container {
  display: flex;
  gap: 12px;
  align-items: flex-end;
  justify-content: center;
  height: 150px;
  margin-bottom: 3rem;
}

.bar {
  width: 20px;
  background: white;
  border-radius: 4px 4px 0 0;
  animation: barBounce 1.2s ease-in-out infinite;
  animation-delay: var(--delay);
}

.splash-text h1 {
  animation: slideUp 0.8s ease-out 0.5s both;
}

.splash-text p {
  animation: slideUp 0.8s ease-out 0.7s both;
}

@keyframes barBounce {
  0%, 100% {
    height: 40px;
  }
  50% {
    height: 120px;
  }
}

@keyframes slideUp {
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
      "title": "Performance",
      "subtitle": "Driven Results"
    }
  },
  "de": {
    "splash": {
      "title": "Leistung",
      "subtitle": "Orientierte Ergebnisse"
    }
  }
}
</i18n>
