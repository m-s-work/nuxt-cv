<script setup lang="ts">
import { getMascotDesign } from '~/mascots'

const { state, config, handleTickle, initScrollListener, cleanupScrollListener, scrollDirection } = useMascot()
const { t } = useI18n()

const mascotRef = ref<HTMLDivElement | null>(null)
const showTextBubble = ref(false)
const textBubbleContent = ref<string | null>(null)
let textBubbleTimeout: NodeJS.Timeout | null = null

// Get current mascot design
const currentDesign = computed(() => {
  const design = getMascotDesign(config.value.design)
  if (!design) {
    console.warn(`Mascot design '${config.value.design}' not found, falling back to 'robot'`)
    return getMascotDesign('robot')
  }
  return design
})

// Render SVG for current state
const svgContent = computed(() => {
  return currentDesign.value?.render(state.value, config.value.theme) || ''
})

// Get animation class for current state
const animationClass = computed(() => {
  return currentDesign.value?.getAnimationClass(state.value) || ''
})

// Position class
const positionClass = computed(() => {
  const pos = config.value.position || 'bottom-right'
  return `mascot-position-${pos}`
})

// Watch state changes to show text bubbles
watch(state, (newState) => {
  // Clear existing timeout
  if (textBubbleTimeout) {
    clearTimeout(textBubbleTimeout)
    textBubbleTimeout = null
  }

  const text = currentDesign.value?.getText?.(newState, config.value.texts)
  if (text) {
    textBubbleContent.value = text
    showTextBubble.value = true
    textBubbleTimeout = setTimeout(() => {
      showTextBubble.value = false
      textBubbleTimeout = null
    }, 2500)
  } else {
    showTextBubble.value = false
    textBubbleContent.value = null
  }
})

// Handle mascot click/tap
const handleMascotClick = () => {
  handleTickle()
}

onMounted(() => {
  initScrollListener()
  
  // Greet user after a short delay
  setTimeout(() => {
    const { greet } = useMascot()
    greet()
  }, 1000)
})

onUnmounted(() => {
  cleanupScrollListener()
  
  // Clear text bubble timeout on unmount
  if (textBubbleTimeout) {
    clearTimeout(textBubbleTimeout)
    textBubbleTimeout = null
  }
})
</script>

<template>
  <div 
    v-if="config.enabled"
    ref="mascotRef"
    class="mascot-container no-print"
    :class="[positionClass, animationClass]"
    :style="{ width: `${config.size}px`, height: `${config.size}px` }"
    @click="handleMascotClick"
  >
    <!-- Text bubble -->
    <Transition name="bubble">
      <div v-if="showTextBubble" class="mascot-text-bubble">
        {{ textBubbleContent }}
      </div>
    </Transition>

    <!-- Mascot SVG -->
    <div 
      class="mascot-svg"
      :class="{ 'mascot-scrolling-up': scrollDirection === 'up', 'mascot-scrolling-down': scrollDirection === 'down' }"
      v-html="svgContent"
    />
  </div>
</template>

<style scoped>
.mascot-container {
  position: fixed;
  z-index: 1000;
  cursor: pointer;
  user-select: none;
  transition: transform 0.3s ease;
}

.mascot-container:hover {
  transform: scale(1.05);
}

.mascot-container:active {
  transform: scale(0.95);
}

/* Positioning */
.mascot-position-bottom-right {
  bottom: 20px;
  right: 20px;
}

.mascot-position-bottom-left {
  bottom: 20px;
  left: 20px;
}

.mascot-position-top-right {
  top: 20px;
  right: 20px;
}

.mascot-position-top-left {
  top: 20px;
  left: 20px;
}

/* Text bubble */
.mascot-text-bubble {
  position: absolute;
  bottom: 100%;
  left: 50%;
  transform: translateX(-50%);
  background: white;
  border: 2px solid #3b82f6;
  border-radius: 12px;
  padding: 8px 12px;
  margin-bottom: 10px;
  white-space: nowrap;
  font-size: 14px;
  font-weight: 500;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.15);
  color: #1f2937;
}

.mascot-text-bubble::after {
  content: '';
  position: absolute;
  top: 100%;
  left: 50%;
  transform: translateX(-50%);
  border: 8px solid transparent;
  border-top-color: #3b82f6;
}

/* Dark mode support */
:root.dark .mascot-text-bubble {
  background: #1f2937;
  border-color: #60a5fa;
  color: white;
}

:root.dark .mascot-text-bubble::after {
  border-top-color: #60a5fa;
}

/* Bubble transition */
.bubble-enter-active,
.bubble-leave-active {
  transition: all 0.3s ease;
}

.bubble-enter-from,
.bubble-leave-to {
  opacity: 0;
  transform: translateX(-50%) translateY(10px);
}

/* Mascot SVG container */
.mascot-svg {
  width: 100%;
  height: 100%;
}

/* Animation states */
.mascot-idle {
  animation: idle-float 3s ease-in-out infinite;
}

@keyframes idle-float {
  0%, 100% {
    transform: translateY(0);
  }
  50% {
    transform: translateY(-5px);
  }
}

.mascot-tickled {
  animation: tickle-shake 0.5s ease-in-out;
}

@keyframes tickle-shake {
  0%, 100% {
    transform: rotate(0deg);
  }
  25% {
    transform: rotate(-10deg) scale(1.1);
  }
  75% {
    transform: rotate(10deg) scale(1.1);
  }
}

.mascot-scrolling {
  animation: scroll-bounce 0.6s ease-in-out;
}

@keyframes scroll-bounce {
  0%, 100% {
    transform: translateY(0);
  }
  50% {
    transform: translateY(-10px);
  }
}

.mascot-scrolling-up .mascot-svg :deep(.antenna-light) {
  animation: antenna-blink 0.5s ease-in-out infinite;
}

.mascot-scrolling-down .mascot-svg :deep(.antenna-light) {
  animation: antenna-blink 0.3s ease-in-out infinite;
}

@keyframes antenna-blink {
  0%, 100% {
    opacity: 1;
  }
  50% {
    opacity: 0.3;
  }
}

.mascot-greeting {
  animation: greeting-wave 1s ease-in-out;
}

@keyframes greeting-wave {
  0%, 100% {
    transform: rotate(0deg);
  }
  25% {
    transform: rotate(-15deg);
  }
  75% {
    transform: rotate(15deg);
  }
}

.mascot-thinking {
  animation: thinking-bob 1.5s ease-in-out infinite;
}

@keyframes thinking-bob {
  0%, 100% {
    transform: translateY(0) rotate(0deg);
  }
  50% {
    transform: translateY(-3px) rotate(5deg);
  }
}

.mascot-celebrating {
  animation: celebrate-jump 0.6s ease-in-out 3;
}

@keyframes celebrate-jump {
  0%, 100% {
    transform: translateY(0) scale(1);
  }
  50% {
    transform: translateY(-20px) scale(1.2);
  }
}

/* Mobile responsiveness */
@media (max-width: 768px) {
  .mascot-container {
    width: 80px !important;
    height: 80px !important;
  }

  .mascot-text-bubble {
    font-size: 12px;
    padding: 6px 10px;
  }
}

/* Print: hide mascot */
@media print {
  .mascot-container {
    display: none !important;
  }
}
</style>

<i18n lang="json">
{
  "en": {
    "mascot": {
      "greeting": "Hello!",
      "tickled": "Hehe!",
      "celebrating": "Awesome!"
    }
  },
  "de": {
    "mascot": {
      "greeting": "Hallo!",
      "tickled": "Hihi!",
      "celebrating": "Super!"
    }
  }
}
</i18n>
