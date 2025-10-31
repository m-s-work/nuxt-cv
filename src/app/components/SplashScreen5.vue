<script setup lang="ts">
const { t } = useI18n()

defineProps<{
  onComplete?: () => void
}>()

onMounted(() => {
  const timer = setTimeout(() => {
    // Animation completes naturally via CSS
  }, 2700)
  
  onUnmounted(() => clearTimeout(timer))
})
</script>

<template>
  <div class="splash-screen splash-ripple">
    <div class="splash-content">
      <!-- Ripple circles -->
      <div class="ripple-container">
        <div class="ripple ripple-1"></div>
        <div class="ripple ripple-2"></div>
        <div class="ripple ripple-3"></div>
      </div>
      
      <div class="splash-text">
        <h1 class="text-4xl md:text-6xl font-bold">{{ t('splash.title') }}</h1>
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

.splash-ripple {
  background: linear-gradient(135deg, rgb(37 99 235) 0%, rgb(29 78 216) 100%);
  animation: fadeOut 2.7s ease-in-out forwards;
}

.splash-content {
  position: relative;
  text-align: center;
  color: white;
}

.ripple-container {
  position: relative;
  width: 200px;
  height: 200px;
  margin: 0 auto 3rem;
}

.ripple {
  position: absolute;
  top: 50%;
  left: 50%;
  transform: translate(-50%, -50%);
  width: 100%;
  height: 100%;
  border: 2px solid rgba(255, 255, 255, 0.6);
  border-radius: 50%;
  animation: rippleEffect 2s ease-out infinite;
}

.ripple-2 {
  animation-delay: 0.5s;
}

.ripple-3 {
  animation-delay: 1s;
}

.splash-text h1 {
  animation: fadeInUp 0.8s ease-out 0.3s both;
}

@keyframes rippleEffect {
  0% {
    transform: translate(-50%, -50%) scale(0.1);
    opacity: 1;
  }
  100% {
    transform: translate(-50%, -50%) scale(1.5);
    opacity: 0;
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
      "title": "Professional Portfolio"
    }
  },
  "de": {
    "splash": {
      "title": "Professionelles Portfolio"
    }
  }
}
</i18n>
