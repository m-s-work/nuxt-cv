<script setup lang="ts">
const { t } = useI18n()

defineProps<{
  onComplete?: () => void
}>()

onMounted(() => {
  const timer = setTimeout(() => {
    // Animation completes naturally via CSS
  }, 2900)
  
  onUnmounted(() => clearTimeout(timer))
})
</script>

<template>
  <div class="splash-screen splash-hexagon">
    <div class="splash-content">
      <svg class="hexagon-svg" viewBox="0 0 200 200" xmlns="http://www.w3.org/2000/svg">
        <!-- Hexagon shape -->
        <polygon class="hex hex-1" points="100,10 170,50 170,130 100,170 30,130 30,50" 
          fill="none" stroke="#fff" stroke-width="2"/>
        <polygon class="hex hex-2" points="100,25 155,57.5 155,122.5 100,155 45,122.5 45,57.5" 
          fill="none" stroke="#fff" stroke-width="2"/>
        <polygon class="hex hex-3" points="100,40 140,65 140,115 100,140 60,115 60,65" 
          fill="none" stroke="#fff" stroke-width="2"/>
      </svg>
      
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

.splash-hexagon {
  background: linear-gradient(135deg, rgb(37 99 235) 0%, rgb(29 78 216) 100%);
  animation: fadeOut 2.9s ease-in-out forwards;
}

.splash-content {
  text-align: center;
  color: white;
}

.hexagon-svg {
  width: 200px;
  height: 200px;
  margin: 0 auto 2rem;
}

.hex {
  animation: hexRotate 3s ease-in-out infinite;
  transform-origin: center;
}

.hex-1 {
  animation: hexRotate 3s ease-in-out infinite;
}

.hex-2 {
  animation: hexRotate 3s ease-in-out infinite reverse;
  animation-delay: 0.3s;
}

.hex-3 {
  animation: hexPulse 2s ease-in-out infinite;
}

.splash-text h1 {
  animation: slideInLeft 0.8s ease-out 0.4s both;
}

.splash-text p {
  animation: slideInRight 0.8s ease-out 0.6s both;
}

@keyframes hexRotate {
  0%, 100% {
    transform: rotate(0deg);
  }
  50% {
    transform: rotate(60deg);
  }
}

@keyframes hexPulse {
  0%, 100% {
    opacity: 0.4;
    transform: scale(1);
  }
  50% {
    opacity: 1;
    transform: scale(1.1);
  }
}

@keyframes slideInLeft {
  from {
    opacity: 0;
    transform: translateX(-30px);
  }
  to {
    opacity: 1;
    transform: translateX(0);
  }
}

@keyframes slideInRight {
  from {
    opacity: 0;
    transform: translateX(30px);
  }
  to {
    opacity: 1;
    transform: translateX(0);
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
      "title": "Expertise",
      "subtitle": "& Experience"
    }
  },
  "de": {
    "splash": {
      "title": "Expertise",
      "subtitle": "& Erfahrung"
    }
  }
}
</i18n>
