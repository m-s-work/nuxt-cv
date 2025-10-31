<script setup lang="ts">
const { t } = useI18n()

defineProps<{
  onComplete?: () => void
}>()

onMounted(() => {
  const timer = setTimeout(() => {
    // Animation completes naturally via CSS
  }, 3000)
  
  onUnmounted(() => clearTimeout(timer))
})
</script>

<template>
  <div class="splash-screen splash-geometric">
    <div class="splash-content">
      <svg class="geometric-animation" viewBox="0 0 400 400" xmlns="http://www.w3.org/2000/svg">
        <!-- Animated geometric shapes -->
        <circle class="shape shape-1" cx="200" cy="200" r="80" fill="none" stroke="#fff" stroke-width="2"/>
        <rect class="shape shape-2" x="150" y="150" width="100" height="100" fill="none" stroke="#fff" stroke-width="2"/>
        <polygon class="shape shape-3" points="200,120 240,180 160,180" fill="none" stroke="#fff" stroke-width="2"/>
        <circle class="shape shape-4" cx="200" cy="200" r="120" fill="none" stroke="#fff" stroke-width="1" opacity="0.5"/>
      </svg>
      
      <div class="splash-text">
        <h1 class="text-3xl md:text-5xl font-bold">{{ t('splash.title') }}</h1>
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

.splash-geometric {
  background: linear-gradient(135deg, rgb(37 99 235) 0%, rgb(29 78 216) 100%);
  animation: fadeOut 3s ease-in-out forwards;
}

.splash-content {
  text-align: center;
  color: white;
}

.geometric-animation {
  width: 300px;
  height: 300px;
  margin: 0 auto 2rem;
}

.shape {
  animation: rotate 3s ease-in-out infinite;
  transform-origin: center;
}

.shape-1 {
  animation: rotate 2s linear infinite, pulse 2s ease-in-out infinite;
}

.shape-2 {
  animation: rotate 3s linear infinite reverse;
}

.shape-3 {
  animation: rotate 2.5s linear infinite, scale-pulse 2s ease-in-out infinite;
}

.shape-4 {
  animation: rotate 4s linear infinite reverse, fade-pulse 3s ease-in-out infinite;
}

.splash-text h1 {
  animation: fadeInUp 1s ease-out 0.5s both;
}

@keyframes rotate {
  to {
    transform: rotate(360deg);
  }
}

@keyframes pulse {
  0%, 100% {
    stroke-opacity: 1;
  }
  50% {
    stroke-opacity: 0.3;
  }
}

@keyframes scale-pulse {
  0%, 100% {
    transform: scale(1);
  }
  50% {
    transform: scale(1.1);
  }
}

@keyframes fade-pulse {
  0%, 100% {
    opacity: 0.5;
  }
  50% {
    opacity: 0.2;
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
      "title": "Curriculum Vitae"
    }
  },
  "de": {
    "splash": {
      "title": "Lebenslauf"
    }
  }
}
</i18n>
