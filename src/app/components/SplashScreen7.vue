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
  <div class="splash-screen splash-lines">
    <div class="splash-content">
      <!-- Animated lines -->
      <div class="lines-container">
        <div class="line line-1"></div>
        <div class="line line-2"></div>
        <div class="line line-3"></div>
        <div class="line line-4"></div>
        <div class="line line-5"></div>
      </div>
      
      <div class="splash-text">
        <h1 class="text-5xl md:text-7xl font-bold">{{ t('splash.name') }}</h1>
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

.splash-lines {
  background: linear-gradient(135deg, rgb(29 78 216) 0%, rgb(30 64 175) 100%);
  animation: fadeOut 3s ease-in-out forwards;
}

.splash-content {
  position: relative;
  text-align: center;
  color: white;
  width: 100%;
}

.lines-container {
  position: absolute;
  top: 0;
  left: 0;
  width: 100%;
  height: 100vh;
  z-index: 0;
}

.line {
  position: absolute;
  height: 2px;
  background: linear-gradient(90deg, transparent, rgba(255, 255, 255, 0.8), transparent);
  width: 0;
  left: 0;
  animation: lineExpand 1.5s ease-out forwards;
}

.line-1 {
  top: 20%;
  animation-delay: 0s;
}

.line-2 {
  top: 35%;
  animation-delay: 0.2s;
}

.line-3 {
  top: 50%;
  animation-delay: 0.4s;
}

.line-4 {
  top: 65%;
  animation-delay: 0.6s;
}

.line-5 {
  top: 80%;
  animation-delay: 0.8s;
}

.splash-text {
  position: relative;
  z-index: 1;
}

.splash-text h1 {
  animation: zoomIn 1s ease-out 1s both;
}

@keyframes lineExpand {
  0% {
    width: 0;
    opacity: 0;
  }
  50% {
    opacity: 1;
  }
  100% {
    width: 100%;
    opacity: 0;
  }
}

@keyframes zoomIn {
  from {
    opacity: 0;
    transform: scale(0.5);
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
      "name": "Max Mustermann"
    }
  },
  "de": {
    "splash": {
      "name": "Max Mustermann"
    }
  }
}
</i18n>
