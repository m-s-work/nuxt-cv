<script setup lang="ts">
const { t } = useI18n()

defineProps<{
  onComplete?: () => void
}>()

// Auto-complete after animation
onMounted(() => {
  const timer = setTimeout(() => {
    // Animation completes naturally via CSS
  }, 2500)
  
  onUnmounted(() => clearTimeout(timer))
})
</script>

<template>
  <div class="splash-screen splash-fade">
    <div class="splash-content">
      <div class="logo-container">
        <div class="logo-circle"></div>
        <div class="logo-text">
          <h1 class="text-4xl md:text-6xl font-bold">{{ t('splash.welcome') }}</h1>
          <p class="text-xl md:text-2xl mt-4">{{ t('splash.loading') }}</p>
        </div>
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

.splash-fade {
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  animation: fadeOut 2.5s ease-in-out forwards;
}

.splash-content {
  text-align: center;
  color: white;
  animation: contentFadeIn 1s ease-out;
}

.logo-container {
  position: relative;
}

.logo-circle {
  width: 120px;
  height: 120px;
  margin: 0 auto 2rem;
  border: 4px solid rgba(255, 255, 255, 0.3);
  border-radius: 50%;
  border-top-color: white;
  animation: spin 1s linear infinite;
}

.logo-text h1 {
  animation: slideUp 0.8s ease-out 0.3s both;
}

.logo-text p {
  animation: slideUp 0.8s ease-out 0.5s both;
  opacity: 0.9;
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

@keyframes contentFadeIn {
  from {
    opacity: 0;
    transform: scale(0.9);
  }
  to {
    opacity: 1;
    transform: scale(1);
  }
}

@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}

@keyframes slideUp {
  from {
    opacity: 0;
    transform: translateY(20px);
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
      "welcome": "Welcome",
      "loading": "Loading CV..."
    }
  },
  "de": {
    "splash": {
      "welcome": "Willkommen",
      "loading": "Lade Lebenslauf..."
    }
  }
}
</i18n>
