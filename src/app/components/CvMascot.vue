<script setup lang="ts">
const { currentMessage, currentAnimation, isVisible } = useMascot()
const { t } = useI18n()

// Watch for animation changes to trigger CSS animations
const mascotRef = ref<HTMLDivElement | null>(null)

watch(currentAnimation, (newAnimation) => {
  if (mascotRef.value) {
    // Remove all animation classes
    mascotRef.value.classList.remove('wave', 'bounce', 'point-up', 'point-down', 'celebrate', 'think')
    // Add new animation class after a short delay
    setTimeout(() => {
      if (mascotRef.value && newAnimation) {
        mascotRef.value.classList.add(newAnimation)
      }
    }, 50)
  }
})
</script>

<template>
  <div
    v-if="isVisible"
    ref="mascotRef"
    class="mascot-container fixed z-50 print:hidden transition-all duration-500"
    :class="{ 'mascot-visible': isVisible }"
  >
    <!-- Speech Bubble -->
    <div
      v-if="currentMessage"
      class="speech-bubble absolute bottom-full mb-4 left-1/2 transform -translate-x-1/2 bg-white dark:bg-gray-800 rounded-lg shadow-lg p-3 min-w-48 max-w-64"
    >
      <p class="text-sm text-gray-800 dark:text-gray-200 text-center">
        {{ t(currentMessage) }}
      </p>
      <!-- Speech bubble tail -->
      <div class="absolute top-full left-1/2 transform -translate-x-1/2 w-0 h-0 border-l-8 border-r-8 border-t-8 border-transparent border-t-white dark:border-t-gray-800"></div>
    </div>

    <!-- SVG Mascot -->
    <svg
      class="mascot-svg w-20 h-20 cursor-pointer"
      viewBox="0 0 100 100"
      xmlns="http://www.w3.org/2000/svg"
    >
      <!-- Shadow -->
      <ellipse
        cx="50"
        cy="90"
        rx="20"
        ry="4"
        fill="rgba(0, 0, 0, 0.2)"
        class="mascot-shadow"
      />

      <!-- Body -->
      <ellipse
        cx="50"
        cy="60"
        rx="22"
        ry="28"
        fill="#3B82F6"
        class="mascot-body"
      />

      <!-- Head -->
      <circle
        cx="50"
        cy="30"
        r="18"
        fill="#3B82F6"
        class="mascot-head"
      />

      <!-- Eyes -->
      <g class="mascot-eyes">
        <!-- Left eye -->
        <circle cx="43" cy="28" r="4" fill="white" />
        <circle cx="44" cy="28" r="2" fill="#1F2937" class="mascot-pupil-left" />
        
        <!-- Right eye -->
        <circle cx="57" cy="28" r="4" fill="white" />
        <circle cx="58" cy="28" r="2" fill="#1F2937" class="mascot-pupil-right" />
      </g>

      <!-- Smile -->
      <path
        d="M 42 34 Q 50 38 58 34"
        stroke="#1F2937"
        stroke-width="2"
        fill="none"
        stroke-linecap="round"
        class="mascot-mouth"
      />

      <!-- Left Arm -->
      <path
        d="M 30 55 Q 20 60 22 68"
        stroke="#3B82F6"
        stroke-width="8"
        fill="none"
        stroke-linecap="round"
        class="mascot-arm-left"
      />

      <!-- Right Arm -->
      <path
        d="M 70 55 Q 80 60 78 68"
        stroke="#3B82F6"
        stroke-width="8"
        fill="none"
        stroke-linecap="round"
        class="mascot-arm-right"
      />

      <!-- Cheeks (appear during celebrate animation) -->
      <circle cx="35" cy="34" r="3" fill="#EF4444" opacity="0" class="mascot-cheek-left" />
      <circle cx="65" cy="34" r="3" fill="#EF4444" opacity="0" class="mascot-cheek-right" />
    </svg>
  </div>
</template>

<style scoped>
.mascot-container {
  bottom: 2rem;
  right: 2rem;
  transform: translateY(120%);
  opacity: 0;
}

.mascot-container.mascot-visible {
  transform: translateY(0);
  opacity: 1;
}

.mascot-svg {
  filter: drop-shadow(0 4px 6px rgba(0, 0, 0, 0.1));
  transition: transform 0.3s ease;
}

.mascot-svg:hover {
  transform: scale(1.1);
}

/* Speech bubble animation */
.speech-bubble {
  animation: fadeInBounce 0.4s ease-out;
}

@keyframes fadeInBounce {
  0% {
    opacity: 0;
    transform: translate(-50%, 10px);
  }
  50% {
    transform: translate(-50%, -5px);
  }
  100% {
    opacity: 1;
    transform: translate(-50%, 0);
  }
}

/* Wave animation */
.mascot-container.wave .mascot-arm-right {
  animation: waveArm 0.6s ease-in-out 3;
  transform-origin: 70px 55px;
}

@keyframes waveArm {
  0%, 100% { transform: rotate(0deg); }
  25% { transform: rotate(-20deg); }
  75% { transform: rotate(20deg); }
}

/* Bounce animation */
.mascot-container.bounce .mascot-svg {
  animation: bounceUp 0.6s ease-in-out 3;
}

@keyframes bounceUp {
  0%, 100% { transform: translateY(0); }
  50% { transform: translateY(-15px); }
}

.mascot-container.bounce .mascot-shadow {
  animation: shadowScale 0.6s ease-in-out 3;
}

@keyframes shadowScale {
  0%, 100% { transform: scale(1); opacity: 0.2; }
  50% { transform: scale(0.7); opacity: 0.1; }
}

/* Point up animation */
.mascot-container.point-up .mascot-arm-right {
  animation: pointUp 0.5s ease-out forwards;
  transform-origin: 70px 55px;
}

@keyframes pointUp {
  0% { transform: rotate(0deg); }
  100% { transform: rotate(-120deg) translateX(-5px); }
}

/* Point down animation */
.mascot-container.point-down .mascot-arm-right {
  animation: pointDown 0.5s ease-out forwards;
  transform-origin: 70px 55px;
}

@keyframes pointDown {
  0% { transform: rotate(0deg); }
  100% { transform: rotate(60deg); }
}

/* Celebrate animation */
.mascot-container.celebrate {
  animation: celebrateBounce 0.5s ease-in-out 3;
}

@keyframes celebrateBounce {
  0%, 100% { transform: translateY(0) rotate(0deg); }
  25% { transform: translateY(-10px) rotate(-5deg); }
  75% { transform: translateY(-10px) rotate(5deg); }
}

.mascot-container.celebrate .mascot-arm-left {
  animation: celebrateLeftArm 0.5s ease-in-out 3;
  transform-origin: 30px 55px;
}

.mascot-container.celebrate .mascot-arm-right {
  animation: celebrateRightArm 0.5s ease-in-out 3;
  transform-origin: 70px 55px;
}

@keyframes celebrateLeftArm {
  0%, 100% { transform: rotate(0deg); }
  50% { transform: rotate(-30deg); }
}

@keyframes celebrateRightArm {
  0%, 100% { transform: rotate(0deg); }
  50% { transform: rotate(30deg); }
}

.mascot-container.celebrate .mascot-cheek-left,
.mascot-container.celebrate .mascot-cheek-right {
  animation: fadeInOut 0.5s ease-in-out 3;
}

@keyframes fadeInOut {
  0%, 100% { opacity: 0; }
  50% { opacity: 0.8; }
}

/* Think animation */
.mascot-container.think .mascot-head {
  animation: thinkNod 1s ease-in-out infinite;
  transform-origin: 50px 30px;
}

@keyframes thinkNod {
  0%, 100% { transform: rotate(0deg); }
  25% { transform: rotate(-3deg); }
  75% { transform: rotate(3deg); }
}

.mascot-container.think .mascot-pupil-left {
  animation: lookAround 2s ease-in-out infinite;
}

.mascot-container.think .mascot-pupil-right {
  animation: lookAround 2s ease-in-out infinite;
}

@keyframes lookAround {
  0%, 100% { transform: translate(0, 0); }
  25% { transform: translate(1px, -1px); }
  50% { transform: translate(-1px, 0); }
  75% { transform: translate(0, 1px); }
}

/* Mobile positioning */
@media (max-width: 768px) {
  .mascot-container {
    bottom: 1rem;
    right: 1rem;
  }

  .mascot-svg {
    width: 64px;
    height: 64px;
  }

  .speech-bubble {
    min-width: 160px;
    max-width: 12rem;
  }
}
</style>

<i18n lang="json">
{
  "en": {
    "mascot": {
      "welcome": "Welcome! Let me guide you through this CV 👋",
      "scrollDown": "Scroll down to learn more!",
      "checkExperience": "Check out the work experience!",
      "seeProjects": "Amazing projects below!",
      "keepGoing": "Keep scrolling to see more!",
      "almostThere": "You're almost at the end!",
      "goodbye": "Thanks for viewing! 🎉"
    }
  },
  "de": {
    "mascot": {
      "welcome": "Willkommen! Ich führe dich durch diesen Lebenslauf 👋",
      "scrollDown": "Scrolle nach unten, um mehr zu erfahren!",
      "checkExperience": "Schau dir die Berufserfahrung an!",
      "seeProjects": "Tolle Projekte weiter unten!",
      "keepGoing": "Scrolle weiter, um mehr zu sehen!",
      "almostThere": "Du bist fast am Ende!",
      "goodbye": "Danke fürs Anschauen! 🎉"
    }
  }
}
</i18n>
