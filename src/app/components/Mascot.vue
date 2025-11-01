<script setup lang="ts">
import type { MascotDesign } from '~/types/mascot'
import MascotRobot from '~/components/mascots/MascotRobot.vue'
import MascotOwl from '~/components/mascots/MascotOwl.vue'

const { t } = useI18n()
const { 
  config, 
  animationState, 
  scrollContext, 
  currentMessage,
  setAnimationState,
  handleScroll,
  showMessage
} = useMascot()

// Track if mouse is hovering over mascot
const isHovering = ref(false)
const hoverTimer = ref<NodeJS.Timeout | null>(null)

// Handle mouse enter - tickle effect
const onMouseEnter = () => {
  isHovering.value = true
  setAnimationState('hover')
  
  // Show a random message after a brief hover
  if (hoverTimer.value) clearTimeout(hoverTimer.value)
  hoverTimer.value = setTimeout(() => {
    if (isHovering.value && config.value.showMessages) {
      const messages = t('mascot.hoverMessages', { returnObjects: true }) as string[]
      const randomMessage = messages[Math.floor(Math.random() * messages.length)]
      showMessage(randomMessage, 2000)
    }
  }, 800)
}

// Handle mouse leave
const onMouseLeave = () => {
  isHovering.value = false
  if (hoverTimer.value) {
    clearTimeout(hoverTimer.value)
    hoverTimer.value = null
  }
  // Return to idle after a brief delay
  setTimeout(() => {
    if (!isHovering.value) {
      setAnimationState('idle')
    }
  }, 300)
}

// Handle mascot click - trigger greeting animation
const onMascotClick = () => {
  setAnimationState('greeting')
  if (config.value.showMessages) {
    const messages = t('mascot.greetingMessages', { returnObjects: true }) as string[]
    const randomMessage = messages[Math.floor(Math.random() * messages.length)]
    showMessage(randomMessage, 3000)
  }
  // Return to idle after animation
  setTimeout(() => {
    if (!isHovering.value) {
      setAnimationState('idle')
    }
  }, 3000)
}

// Position classes
const positionClass = computed(() => {
  switch (config.value.position) {
    case 'bottom-left':
      return 'bottom-4 left-4'
    case 'top-right':
      return 'top-4 right-4'
    case 'top-left':
      return 'top-4 left-4'
    default:
      return 'bottom-4 right-4'
  }
})

// Set up scroll listener
onMounted(() => {
  if (typeof window !== 'undefined') {
    window.addEventListener('scroll', handleScroll, { passive: true })
    
    // Show initial greeting message after a delay
    setTimeout(() => {
      if (config.value.showMessages) {
        const messages = t('mascot.welcomeMessages', { returnObjects: true }) as string[]
        showMessage(messages[0], 4000)
      }
    }, 2000)
  }
})

onUnmounted(() => {
  if (typeof window !== 'undefined') {
    window.removeEventListener('scroll', handleScroll)
  }
  if (hoverTimer.value) {
    clearTimeout(hoverTimer.value)
  }
})
</script>

<template>
  <div 
    v-if="config.enabled"
    class="mascot-container fixed z-50 no-print"
    :class="positionClass"
  >
    <!-- Mascot -->
    <div 
      class="mascot-wrapper cursor-pointer transition-transform duration-300 hover:scale-110"
      @mouseenter="onMouseEnter"
      @mouseleave="onMouseLeave"
      @click="onMascotClick"
      role="button"
      :aria-label="t('mascot.ariaLabel')"
      tabindex="0"
      @keydown.enter="onMascotClick"
      @keydown.space.prevent="onMascotClick"
    >
      <MascotOwl
        v-if="config.design === 'owl'"
        :theme="config.theme"
        :animation-state="animationState"
        :scroll-context="scrollContext"
        :size="120"
      />
      <MascotRobot
        v-else
        :theme="config.theme"
        :animation-state="animationState"
        :scroll-context="scrollContext"
        :size="120"
      />
    </div>
    
    <!-- Message bubble -->
    <Transition name="message-fade">
      <div 
        v-if="currentMessage && config.showMessages"
        class="message-bubble absolute bottom-full mb-4 right-0 bg-white dark:bg-gray-800 text-gray-800 dark:text-gray-200 px-4 py-2 rounded-lg shadow-lg max-w-xs whitespace-normal"
      >
        <div class="text-sm">{{ currentMessage }}</div>
        <!-- Speech bubble arrow -->
        <div class="absolute top-full right-8 w-0 h-0 border-l-8 border-l-transparent border-r-8 border-r-transparent border-t-8 border-t-white dark:border-t-gray-800"></div>
      </div>
    </Transition>
  </div>
</template>

<style scoped>
.mascot-container {
  pointer-events: auto;
}

.mascot-wrapper {
  filter: drop-shadow(0 4px 6px rgba(0, 0, 0, 0.1));
}

.mascot-wrapper:hover {
  filter: drop-shadow(0 6px 8px rgba(0, 0, 0, 0.15));
}

.message-bubble {
  animation: bounce-in 0.3s ease-out;
}

@keyframes bounce-in {
  0% {
    opacity: 0;
    transform: translateY(10px) scale(0.9);
  }
  60% {
    opacity: 1;
    transform: translateY(-5px) scale(1.05);
  }
  100% {
    opacity: 1;
    transform: translateY(0) scale(1);
  }
}

.message-fade-enter-active,
.message-fade-leave-active {
  transition: opacity 0.3s ease, transform 0.3s ease;
}

.message-fade-enter-from,
.message-fade-leave-to {
  opacity: 0;
  transform: translateY(10px);
}

@media print {
  .no-print {
    display: none !important;
  }
}

@media (max-width: 640px) {
  .mascot-wrapper {
    transform: scale(0.8);
  }
  
  .message-bubble {
    max-width: 12rem;
    font-size: 0.75rem;
  }
}
</style>

<i18n lang="json">
{
  "en": {
    "mascot": {
      "ariaLabel": "Interactive mascot - click to interact",
      "welcomeMessages": [
        "Hi there! 👋 Welcome to my CV!",
        "Hello! I'm here to guide you through this CV.",
        "Welcome! Feel free to explore my experience."
      ],
      "hoverMessages": [
        "Hehe, that tickles! 😄",
        "Hello there! 👋",
        "Like what you see?",
        "Keep scrolling for more!",
        "Want to know more? Just scroll!",
        "Nice to meet you! 😊"
      ],
      "greetingMessages": [
        "Thanks for clicking! 🎉",
        "Great to have you here!",
        "Hope you enjoy reading!",
        "Let me know if you need anything!",
        "Feel free to scroll through!"
      ]
    }
  },
  "de": {
    "mascot": {
      "ariaLabel": "Interaktives Maskottchen - Klicken zum Interagieren",
      "welcomeMessages": [
        "Hallo! 👋 Willkommen zu meinem Lebenslauf!",
        "Hallo! Ich bin hier, um dich durch diesen Lebenslauf zu führen.",
        "Willkommen! Erkunde gerne meine Erfahrung."
      ],
      "hoverMessages": [
        "Hehe, das kitzelt! 😄",
        "Hallo! 👋",
        "Gefällt dir, was du siehst?",
        "Scrolle weiter für mehr!",
        "Möchtest du mehr wissen? Einfach scrollen!",
        "Schön, dich kennenzulernen! 😊"
      ],
      "greetingMessages": [
        "Danke fürs Klicken! 🎉",
        "Toll, dass du hier bist!",
        "Ich hoffe, es gefällt dir!",
        "Lass mich wissen, wenn du etwas brauchst!",
        "Scrolle gerne durch!"
      ]
    }
  }
}
</i18n>
