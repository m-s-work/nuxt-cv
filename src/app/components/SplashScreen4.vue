<script setup lang="ts">
const { t } = useI18n()

defineProps<{
  onComplete?: () => void
}>()

onMounted(() => {
  const timer = setTimeout(() => {
    // Animation completes naturally via CSS
  }, 3200)
  
  onUnmounted(() => clearTimeout(timer))
})
</script>

<template>
  <div class="splash-screen splash-wave">
    <svg class="wave-svg" viewBox="0 0 1200 600" xmlns="http://www.w3.org/2000/svg">
      <defs>
        <linearGradient id="waveGradient1" x1="0%" y1="0%" x2="100%" y2="100%">
          <stop offset="0%" style="stop-color:rgb(59 130 246);stop-opacity:0.8" />
          <stop offset="100%" style="stop-color:rgb(37 99 235);stop-opacity:0.8" />
        </linearGradient>
        <linearGradient id="waveGradient2" x1="0%" y1="0%" x2="100%" y2="100%">
          <stop offset="0%" style="stop-color:rgb(96 165 250);stop-opacity:0.6" />
          <stop offset="100%" style="stop-color:rgb(59 130 246);stop-opacity:0.6" />
        </linearGradient>
        <linearGradient id="waveGradient3" x1="0%" y1="0%" x2="100%" y2="100%">
          <stop offset="0%" style="stop-color:rgb(147 197 253);stop-opacity:0.4" />
          <stop offset="100%" style="stop-color:rgb(96 165 250);stop-opacity:0.4" />
        </linearGradient>
      </defs>
      
      <!-- Animated wave layers -->
      <path class="wave wave-1" fill="url(#waveGradient1)"
        d="M0,300 Q300,200 600,300 T1200,300 L1200,600 L0,600 Z" />
      <path class="wave wave-2" fill="url(#waveGradient2)"
        d="M0,350 Q300,250 600,350 T1200,350 L1200,600 L0,600 Z" />
      <path class="wave wave-3" fill="url(#waveGradient3)"
        d="M0,400 Q300,300 600,400 T1200,400 L1200,600 L0,600 Z" />
    </svg>
    
    <div class="splash-content">
      <div class="content-wrapper">
        <h1 class="text-5xl md:text-7xl font-bold mb-6">{{ t('splash.greeting') }}</h1>
        <div class="divider"></div>
        <p class="text-2xl md:text-3xl mt-6">{{ t('splash.subtitle') }}</p>
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

.splash-wave {
  background: linear-gradient(135deg, rgb(37 99 235) 0%, rgb(29 78 216) 100%);
  animation: fadeOut 3.2s ease-in-out forwards;
}

.wave-svg {
  position: absolute;
  bottom: 0;
  left: 0;
  width: 100%;
  height: 100%;
}

.wave {
  animation: waveMove 3s ease-in-out infinite;
}

.wave-1 {
  animation: waveMove 3s ease-in-out infinite;
}

.wave-2 {
  animation: waveMove 4s ease-in-out infinite reverse;
}

.wave-3 {
  animation: waveMove 5s ease-in-out infinite;
}

.splash-content {
  position: relative;
  z-index: 1;
  text-align: center;
  color: white;
  padding: 2rem;
}

.content-wrapper h1 {
  animation: slideInLeft 1s ease-out both;
}

.divider {
  width: 100px;
  height: 4px;
  background: linear-gradient(90deg, transparent, white, transparent);
  margin: 0 auto;
  animation: expandWidth 1s ease-out 0.5s both;
}

.content-wrapper p {
  animation: slideInRight 1s ease-out 0.8s both;
}

@keyframes waveMove {
  0%, 100% {
    transform: translateX(0) translateY(0);
  }
  50% {
    transform: translateX(-50px) translateY(-20px);
  }
}

@keyframes slideInLeft {
  from {
    opacity: 0;
    transform: translateX(-50px);
  }
  to {
    opacity: 1;
    transform: translateX(0);
  }
}

@keyframes slideInRight {
  from {
    opacity: 0;
    transform: translateX(50px);
  }
  to {
    opacity: 1;
    transform: translateX(0);
  }
}

@keyframes expandWidth {
  from {
    width: 0;
  }
  to {
    width: 100px;
  }
}

@keyframes fadeOut {
  0% {
    opacity: 1;
  }
  75% {
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
      "greeting": "Hello",
      "subtitle": "Exploring Professional Journey"
    }
  },
  "de": {
    "splash": {
      "greeting": "Hallo",
      "subtitle": "Erkunden Sie die berufliche Reise"
    }
  }
}
</i18n>
