<script setup lang="ts">
const { showSplash, splashType, hideSplash } = useSplashScreen()

// Auto-hide splash after animation completes
onMounted(() => {
  // Maximum duration for any splash screen + buffer
  const timer = setTimeout(() => {
    hideSplash()
  }, 3500)
  
  onUnmounted(() => clearTimeout(timer))
})

const handleComplete = () => {
  hideSplash()
}
</script>

<template>
  <Teleport to="body">
    <Transition name="splash-fade">
      <div v-if="showSplash" class="splash-wrapper">
        <SplashScreen1 v-if="splashType === 1" :on-complete="handleComplete" />
        <SplashScreen2 v-else-if="splashType === 2" :on-complete="handleComplete" />
        <SplashScreen3 v-else-if="splashType === 3" :on-complete="handleComplete" />
        <SplashScreen4 v-else-if="splashType === 4" :on-complete="handleComplete" />
        <SplashScreen5 v-else-if="splashType === 5" :on-complete="handleComplete" />
        <SplashScreen6 v-else-if="splashType === 6" :on-complete="handleComplete" />
        <SplashScreen7 v-else-if="splashType === 7" :on-complete="handleComplete" />
        <SplashScreen8 v-else-if="splashType === 8" :on-complete="handleComplete" />
        <SplashScreen9 v-else-if="splashType === 9" :on-complete="handleComplete" />
        <SplashScreen10 v-else-if="splashType === 10" :on-complete="handleComplete" />
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped>
.splash-wrapper {
  position: fixed;
  top: 0;
  left: 0;
  width: 100%;
  height: 100%;
  z-index: 10000;
}

.splash-fade-enter-active {
  transition: opacity 0.3s ease-in;
}

.splash-fade-leave-active {
  transition: opacity 0.5s ease-out;
}

.splash-fade-enter-from,
.splash-fade-leave-to {
  opacity: 0;
}
</style>
