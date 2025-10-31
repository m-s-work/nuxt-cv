<script setup lang="ts">
const { t } = useI18n()

defineProps<{
  onComplete?: () => void
}>()

onMounted(() => {
  const timer = setTimeout(() => {
    // Animation completes naturally via CSS
  }, 2600)
  
  onUnmounted(() => clearTimeout(timer))
})
</script>

<template>
  <div class="splash-screen splash-orbit">
    <div class="splash-content">
      <!-- Orbiting elements -->
      <div class="orbit-container">
        <div class="orbit orbit-1">
          <div class="planet"></div>
        </div>
        <div class="orbit orbit-2">
          <div class="planet"></div>
        </div>
        <div class="orbit orbit-3">
          <div class="planet"></div>
        </div>
        <div class="center-dot"></div>
      </div>
      
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

.splash-orbit {
  background: linear-gradient(135deg, rgb(37 99 235) 0%, rgb(29 78 216) 100%);
  animation: fadeOut 2.6s ease-in-out forwards;
}

.splash-content {
  text-align: center;
  color: white;
}

.orbit-container {
  position: relative;
  width: 250px;
  height: 250px;
  margin: 0 auto 2rem;
}

.orbit {
  position: absolute;
  top: 50%;
  left: 50%;
  border: 1px solid rgba(255, 255, 255, 0.3);
  border-radius: 50%;
  transform: translate(-50%, -50%);
}

.orbit-1 {
  width: 80px;
  height: 80px;
  animation: orbitRotate 2s linear infinite;
}

.orbit-2 {
  width: 140px;
  height: 140px;
  animation: orbitRotate 3s linear infinite reverse;
}

.orbit-3 {
  width: 200px;
  height: 200px;
  animation: orbitRotate 4s linear infinite;
}

.planet {
  position: absolute;
  width: 10px;
  height: 10px;
  background: white;
  border-radius: 50%;
  top: 0;
  left: 50%;
  transform: translateX(-50%);
  box-shadow: 0 0 10px rgba(255, 255, 255, 0.8);
}

.center-dot {
  position: absolute;
  top: 50%;
  left: 50%;
  transform: translate(-50%, -50%);
  width: 20px;
  height: 20px;
  background: white;
  border-radius: 50%;
  box-shadow: 0 0 20px rgba(255, 255, 255, 0.9);
  animation: pulse 2s ease-in-out infinite;
}

.splash-text h1 {
  animation: fadeInScale 1s ease-out 0.5s both;
}

@keyframes orbitRotate {
  to {
    transform: translate(-50%, -50%) rotate(360deg);
  }
}

@keyframes pulse {
  0%, 100% {
    transform: translate(-50%, -50%) scale(1);
    opacity: 1;
  }
  50% {
    transform: translate(-50%, -50%) scale(1.2);
    opacity: 0.7;
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
      "title": "Innovation in Motion"
    }
  },
  "de": {
    "splash": {
      "title": "Innovation in Bewegung"
    }
  }
}
</i18n>
