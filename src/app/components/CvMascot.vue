<script setup lang="ts">
const { currentMessage, currentAnimation, isVisible, currentPosition, triggerAnimation, currentIdleAnimation, isTransitioning } = useMascot()
const { t } = useI18n()

// Watch for animation changes to trigger CSS animations
const mascotRef = ref<HTMLDivElement | null>(null)
const isHovered = ref(false)
const giggleCount = ref(0)

watch(currentAnimation, (newAnimation) => {
  if (mascotRef.value) {
    // Remove all animation classes
    mascotRef.value.classList.remove('wave', 'bounce', 'point-up', 'point-down', 'celebrate', 'think', 'giggle', 'scratch-head', 'look-around', 'workout', 'color-shift')
    // Add new animation class after a short delay
    setTimeout(() => {
      if (mascotRef.value && newAnimation) {
        mascotRef.value.classList.add(newAnimation)
      }
    }, 50)
  }
})

// Watch idle animation changes
watch(currentIdleAnimation, (newIdle) => {
  if (mascotRef.value && newIdle) {
    mascotRef.value.classList.add(newIdle)
  }
})

// Compute position classes and transforms based on current position
const positionClasses = computed(() => {
  // Always use fixed bottom-right as base position
  return 'bottom-8 right-8'
})

// Compute CSS variables for smooth position transitions
const positionStyle = computed(() => {
  const viewportWidth = typeof window !== 'undefined' ? window.innerWidth : 1920
  const viewportHeight = typeof window !== 'undefined' ? window.innerHeight : 1080
  
  // Calculate offsets from bottom-right
  let translateX = 0
  let translateY = 0
  
  switch (currentPosition.value) {
    case 'bottom-right':
      translateX = 0
      translateY = 0
      break
    case 'bottom-left':
      translateX = -(viewportWidth - 200)
      translateY = 0
      break
    case 'top-right':
      translateX = 0
      translateY = -(viewportHeight - 200)
      break
    case 'top-left':
      translateX = -(viewportWidth - 200)
      translateY = -(viewportHeight - 200)
      break
  }
  
  return {
    '--mascot-x': `${translateX}px`,
    '--mascot-y': `${translateY}px`
  }
})

// Handle mouse hover - tickle effect
const handleMouseEnter = () => {
  isHovered.value = true
  giggleCount.value++
  
  // Trigger giggle animation
  if (mascotRef.value) {
    mascotRef.value.classList.add('giggle')
  }
}

const handleMouseLeave = () => {
  isHovered.value = false
  
  // Remove giggle animation
  if (mascotRef.value) {
    mascotRef.value.classList.remove('giggle')
  }
}

// Handle click - stronger reaction
const handleClick = () => {
  triggerAnimation('celebrate')
  
  // Add a temporary bounce
  if (mascotRef.value) {
    mascotRef.value.classList.add('tickled')
    setTimeout(() => {
      if (mascotRef.value) {
        mascotRef.value.classList.remove('tickled')
      }
    }, 600)
  }
}
</script>

<template>
  <div
    v-if="isVisible"
    ref="mascotRef"
    class="mascot-container fixed z-50 print:hidden"
    :class="[
      { 'mascot-visible': isVisible, 'transitioning': isTransitioning }, 
      positionClasses
    ]"
    :style="positionStyle"
  >
      <!-- Speech Bubble -->
      <div
        v-if="currentMessage"
        class="speech-bubble absolute bg-white dark:bg-gray-800 rounded-lg shadow-lg p-4 min-w-48 max-w-72"
        :class="currentPosition.includes('bottom') ? 'bottom-full mb-4' : 'top-full mt-4'"
      >
        <p class="text-base text-gray-800 dark:text-gray-200 text-center">
          {{ t(currentMessage) }}
        </p>
        <!-- Speech bubble tail -->
        <div 
          class="absolute left-1/2 transform -translate-x-1/2 w-0 h-0 border-l-8 border-r-8 border-transparent"
          :class="currentPosition.includes('bottom') ? 'top-full border-t-8 border-t-white dark:border-t-gray-800' : 'bottom-full border-b-8 border-b-white dark:border-b-gray-800'"
        ></div>
      </div>

      <!-- SVG Mascot -->
      <svg
        class="mascot-svg w-32 h-32 md:w-40 md:h-40 cursor-pointer"
        viewBox="0 0 100 100"
        xmlns="http://www.w3.org/2000/svg"
        @mouseenter="handleMouseEnter"
        @mouseleave="handleMouseLeave"
        @click="handleClick"
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
  --mascot-x: 0px;
  --mascot-y: 0px;
  transform: translate(var(--mascot-x), var(--mascot-y)) scale(0);
  opacity: 0;
  /* Smooth transition for transform */
  transition: transform 1.8s cubic-bezier(0.25, 0.46, 0.45, 0.94), opacity 0.8s ease-in-out;
  will-change: transform;
}

.mascot-container.mascot-visible {
  transform: translate(var(--mascot-x), var(--mascot-y)) scale(1);
  opacity: 1;
  /* Add floating animation when NOT transitioning */
  animation: float 3s ease-in-out infinite;
}

/* During transition, use flying animation instead of float */
.mascot-container.mascot-visible.transitioning {
  animation: fly 1.8s ease-in-out;
}

/* Floating idle animation */
@keyframes float {
  0%, 100% { 
    transform: translate(var(--mascot-x), calc(var(--mascot-y) + 0px)) scale(1); 
  }
  50% { 
    transform: translate(var(--mascot-x), calc(var(--mascot-y) - 10px)) scale(1); 
  }
}

/* Flying animation during transitions - adds movement to arms/body */
@keyframes fly {
  0%, 100% { 
    transform: translate(var(--mascot-x), var(--mascot-y)) scale(1);
  }
  25% { 
    transform: translate(var(--mascot-x), calc(var(--mascot-y) - 15px)) scale(1.05);
  }
  50% { 
    transform: translate(var(--mascot-x), var(--mascot-y)) scale(1);
  }
  75% { 
    transform: translate(var(--mascot-x), calc(var(--mascot-y) - 15px)) scale(1.05);
  }
}

/* Arm flapping during transition */
.mascot-container.transitioning .mascot-arm-left {
  animation: flapLeftArm 0.3s ease-in-out infinite;
  transform-origin: 30px 55px;
}

.mascot-container.transitioning .mascot-arm-right {
  animation: flapRightArm 0.3s ease-in-out infinite;
  transform-origin: 70px 55px;
}

@keyframes flapLeftArm {
  0%, 100% { transform: rotate(0deg); }
  50% { transform: rotate(-30deg); }
}

@keyframes flapRightArm {
  0%, 100% { transform: rotate(0deg); }
  50% { transform: rotate(30deg); }
}

.speech-bubble {
  position: absolute;
  left: 50%;
  transform: translateX(-50%);
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

.mascot-svg {
  filter: drop-shadow(0 4px 6px rgba(0, 0, 0, 0.1));
  transition: transform 0.3s ease;
}

.mascot-svg:hover {
  transform: scale(1.15) rotate(2deg);
}

/* Giggle animation on hover */
.mascot-container.giggle {
  animation: giggle 0.5s ease-in-out infinite, float 3s ease-in-out infinite;
}

@keyframes giggle {
  0%, 100% { transform: translateY(0) rotate(0deg); }
  25% { transform: translateY(-5px) rotate(-3deg); }
  75% { transform: translateY(-5px) rotate(3deg); }
}

.mascot-container.giggle .mascot-body,
.mascot-container.giggle .mascot-head {
  animation: wiggle 0.3s ease-in-out infinite;
}

@keyframes wiggle {
  0%, 100% { transform: translateX(0); }
  50% { transform: translateX(2px); }
}

.mascot-container.giggle .mascot-cheek-left,
.mascot-container.giggle .mascot-cheek-right {
  opacity: 0.6 !important;
}

/* Tickled animation on click */
.mascot-container.tickled {
  animation: tickleBounce 0.6s ease-out !important;
}

@keyframes tickleBounce {
  0% { transform: translateY(0) scale(1) rotate(0deg); }
  20% { transform: translateY(-30px) scale(1.15) rotate(-10deg); }
  40% { transform: translateY(-20px) scale(1.1) rotate(10deg); }
  60% { transform: translateY(-25px) scale(1.12) rotate(-5deg); }
  80% { transform: translateY(-15px) scale(1.05) rotate(5deg); }
  100% { transform: translateY(0) scale(1) rotate(0deg); }
}

.mascot-container.tickled .mascot-mouth {
  d: path("M 40 32 Q 50 42 60 32");
}

.mascot-container.tickled .mascot-cheek-left,
.mascot-container.tickled .mascot-cheek-right {
  opacity: 1 !important;
  animation: blush 0.6s ease-out;
}

@keyframes blush {
  0%, 100% { opacity: 0; }
  50% { opacity: 1; }
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

/* New Idle Animations */

/* Scratch head animation */
.mascot-container.scratch-head .mascot-arm-right {
  animation: scratchHead 2s ease-in-out;
  transform-origin: 70px 55px;
}

@keyframes scratchHead {
  0%, 100% { transform: rotate(0deg); }
  20% { transform: rotate(-100deg) translateX(-15px) translateY(-20px); }
  40% { transform: rotate(-100deg) translateX(-12px) translateY(-20px); }
  60% { transform: rotate(-100deg) translateX(-15px) translateY(-20px); }
  80% { transform: rotate(-100deg) translateX(-12px) translateY(-20px); }
}

.mascot-container.scratch-head .mascot-head {
  animation: headTilt 2s ease-in-out;
}

@keyframes headTilt {
  0%, 100% { transform: rotate(0deg); }
  20%, 80% { transform: rotate(-10deg); }
}

/* Look around animation - only eyes, no head rotation */
.mascot-container.look-around .mascot-pupil-left,
.mascot-container.look-around .mascot-pupil-right {
  animation: eyesLookAround 3s ease-in-out;
}

@keyframes eyesLookAround {
  0%, 100% { transform: translate(0, 0); }
  15% { transform: translate(2px, 0); }
  30% { transform: translate(-2px, 0); }
  45% { transform: translate(0, -2px); }
  60% { transform: translate(0, 2px); }
  75% { transform: translate(1px, 1px); }
  90% { transform: translate(-1px, -1px); }
}

/* Workout animation (jumping jacks) */
.mascot-container.workout {
  animation: workoutBounce 1.5s ease-in-out 3;
}

@keyframes workoutBounce {
  0%, 100% { transform: translateY(0) scaleY(1); }
  50% { transform: translateY(-20px) scaleY(1.1); }
}

.mascot-container.workout .mascot-arm-left {
  animation: leftArmWorkout 1.5s ease-in-out 3;
  transform-origin: 30px 55px;
}

@keyframes leftArmWorkout {
  0%, 100% { transform: rotate(0deg); }
  50% { transform: rotate(-80deg); }
}

.mascot-container.workout .mascot-arm-right {
  animation: rightArmWorkout 1.5s ease-in-out 3;
  transform-origin: 70px 55px;
}

@keyframes rightArmWorkout {
  0%, 100% { transform: rotate(0deg); }
  50% { transform: rotate(80deg); }
}

/* Color shift animation */
.mascot-container.color-shift .mascot-body,
.mascot-container.color-shift .mascot-head,
.mascot-container.color-shift .mascot-arm-left,
.mascot-container.color-shift .mascot-arm-right {
  animation: colorCycle 4s ease-in-out;
}

@keyframes colorCycle {
  0%, 100% { fill: #3B82F6; stroke: #3B82F6; }
  25% { fill: #8B5CF6; stroke: #8B5CF6; }
  50% { fill: #EC4899; stroke: #EC4899; }
  75% { fill: #10B981; stroke: #10B981; }
}

/* Mobile adjustments */
@media (max-width: 768px) {
  .mascot-svg {
    width: 80px !important;
    height: 80px !important;
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
