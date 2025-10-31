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
  <div class="splash-screen splash-particles">
    <div class="particles-container">
      <!-- Generate particle dots with CSS -->
      <div v-for="i in 50" :key="i" class="particle" :style="`--delay: ${i * 0.1}s; --x: ${Math.random() * 100}%; --y: ${Math.random() * 100}%`"></div>
    </div>
    
    <div class="splash-content">
      <div class="text-container">
        <h1 class="text-5xl md:text-7xl font-bold mb-4">{{ t('splash.name') }}</h1>
        <p class="text-2xl md:text-3xl">{{ t('splash.role') }}</p>
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

.splash-particles {
  background: linear-gradient(135deg, rgb(29 78 216) 0%, rgb(30 64 175) 100%);
  animation: fadeOut 2.8s ease-in-out forwards;
}

.particles-container {
  position: absolute;
  top: 0;
  left: 0;
  width: 100%;
  height: 100%;
}

.particle {
  position: absolute;
  width: 4px;
  height: 4px;
  background: white;
  border-radius: 50%;
  left: var(--x);
  top: var(--y);
  opacity: 0;
  animation: particleFloat 3s ease-in-out var(--delay) infinite;
}

.splash-content {
  position: relative;
  z-index: 1;
  text-align: center;
  color: white;
}

.text-container h1 {
  animation: glowPulse 2s ease-in-out infinite, fadeInScale 1s ease-out both;
  text-shadow: 0 0 20px rgba(255, 255, 255, 0.5);
}

.text-container p {
  animation: fadeInScale 1s ease-out 0.3s both;
  opacity: 0.9;
}

@keyframes particleFloat {
  0% {
    opacity: 0;
    transform: translateY(0) scale(0);
  }
  20% {
    opacity: 1;
    transform: translateY(-20px) scale(1);
  }
  80% {
    opacity: 1;
    transform: translateY(-40px) scale(1);
  }
  100% {
    opacity: 0;
    transform: translateY(-60px) scale(0);
  }
}

@keyframes glowPulse {
  0%, 100% {
    text-shadow: 0 0 20px rgba(255, 255, 255, 0.5);
  }
  50% {
    text-shadow: 0 0 40px rgba(255, 255, 255, 0.8), 0 0 60px rgba(255, 255, 255, 0.4);
  }
}

@keyframes fadeInScale {
  from {
    opacity: 0;
    transform: scale(0.8);
  }
  to {
    opacity: 1;
    transform: scale(1);
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
      "name": "Professional CV",
      "role": "Software Architect"
    }
  },
  "de": {
    "splash": {
      "name": "Professioneller Lebenslauf",
      "role": "Software-Architekt"
    }
  }
}
</i18n>
