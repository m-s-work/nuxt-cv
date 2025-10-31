<script setup lang="ts">
const { t } = useI18n()

// Mascot states
const isHovered = ref(false)
const currentEmotion = ref<'idle' | 'happy' | 'excited'>('idle')
const blinkInterval = ref<NodeJS.Timeout | null>(null)
const shouldBlink = ref(false)

// Handle mouse events
const handleMouseEnter = () => {
  isHovered.value = true
  currentEmotion.value = 'excited'
}

const handleMouseLeave = () => {
  isHovered.value = false
  currentEmotion.value = 'idle'
}

// Blink animation logic
const startBlinking = () => {
  blinkInterval.value = setInterval(() => {
    shouldBlink.value = true
    setTimeout(() => {
      shouldBlink.value = false
    }, 200)
  }, 3000 + Math.random() * 2000) // Random blink every 3-5 seconds
}

onMounted(() => {
  startBlinking()
})

onUnmounted(() => {
  if (blinkInterval.value) {
    clearInterval(blinkInterval.value)
  }
})
</script>

<template>
  <div 
    class="mascot-container no-print"
    :class="{ 'is-hovered': isHovered }"
    @mouseenter="handleMouseEnter"
    @mouseleave="handleMouseLeave"
  >
    <svg 
      class="mascot-svg"
      viewBox="0 0 200 200" 
      xmlns="http://www.w3.org/2000/svg"
      :aria-label="t('mascot.aria')"
    >
      <!-- Body (circle with gradient) -->
      <defs>
        <linearGradient id="bodyGradient" x1="0%" y1="0%" x2="0%" y2="100%">
          <stop offset="0%" style="stop-color:#667eea;stop-opacity:1" />
          <stop offset="100%" style="stop-color:#764ba2;stop-opacity:1" />
        </linearGradient>
        <linearGradient id="bodyGradientHover" x1="0%" y1="0%" x2="0%" y2="100%">
          <stop offset="0%" style="stop-color:#f59e0b;stop-opacity:1" />
          <stop offset="100%" style="stop-color:#ef4444;stop-opacity:1" />
        </linearGradient>
      </defs>

      <!-- Shadow -->
      <ellipse 
        class="mascot-shadow"
        cx="100" 
        cy="185" 
        rx="35" 
        ry="8" 
        fill="rgba(0,0,0,0.2)"
      />

      <!-- Main body -->
      <circle 
        class="mascot-body"
        cx="100" 
        cy="100" 
        r="60" 
        :fill="isHovered ? 'url(#bodyGradientHover)' : 'url(#bodyGradient)'"
      />

      <!-- Arms -->
      <g class="mascot-arms">
        <!-- Left arm -->
        <ellipse 
          class="mascot-arm mascot-arm-left"
          cx="55" 
          cy="100" 
          rx="12" 
          ry="30" 
          :fill="isHovered ? '#f59e0b' : '#667eea'"
        />
        <!-- Right arm -->
        <ellipse 
          class="mascot-arm mascot-arm-right"
          cx="145" 
          cy="100" 
          rx="12" 
          ry="30" 
          :fill="isHovered ? '#f59e0b' : '#667eea'"
        />
      </g>

      <!-- Eyes -->
      <g class="mascot-eyes">
        <!-- Left eye outer -->
        <circle 
          class="mascot-eye-outer"
          cx="85" 
          cy="90" 
          r="12" 
          fill="white"
        />
        <!-- Right eye outer -->
        <circle 
          class="mascot-eye-outer"
          cx="115" 
          cy="90" 
          r="12" 
          fill="white"
        />
        
        <!-- Left pupil -->
        <circle 
          v-if="!shouldBlink"
          class="mascot-pupil mascot-pupil-left"
          cx="85" 
          cy="90" 
          r="5" 
          fill="#1e293b"
        />
        <!-- Right pupil -->
        <circle 
          v-if="!shouldBlink"
          class="mascot-pupil mascot-pupil-right"
          cx="115" 
          cy="90" 
          r="5" 
          fill="#1e293b"
        />

        <!-- Blink lines (shown when blinking) -->
        <line 
          v-if="shouldBlink"
          class="mascot-blink"
          x1="75" y1="90" x2="95" y2="90" 
          stroke="#1e293b" 
          stroke-width="2" 
          stroke-linecap="round"
        />
        <line 
          v-if="shouldBlink"
          class="mascot-blink"
          x1="105" y1="90" x2="125" y2="90" 
          stroke="#1e293b" 
          stroke-width="2" 
          stroke-linecap="round"
        />
      </g>

      <!-- Mouth -->
      <g class="mascot-mouth">
        <path 
          v-if="currentEmotion === 'idle'"
          d="M 85 115 Q 100 120 115 115" 
          fill="none" 
          stroke="white" 
          stroke-width="3" 
          stroke-linecap="round"
        />
        <path 
          v-else-if="currentEmotion === 'excited'"
          d="M 80 110 Q 100 130 120 110" 
          fill="none" 
          stroke="white" 
          stroke-width="3" 
          stroke-linecap="round"
        />
      </g>

      <!-- Sparkles when hovered -->
      <g v-if="isHovered" class="mascot-sparkles">
        <circle class="sparkle sparkle-1" cx="50" cy="50" r="3" fill="#fbbf24" />
        <circle class="sparkle sparkle-2" cx="150" cy="60" r="2" fill="#fbbf24" />
        <circle class="sparkle sparkle-3" cx="140" cy="140" r="2.5" fill="#fbbf24" />
        <circle class="sparkle sparkle-4" cx="60" cy="130" r="2" fill="#fbbf24" />
      </g>
    </svg>

    <!-- Optional tooltip -->
    <div class="mascot-tooltip" :class="{ 'show': isHovered }">
      {{ t('mascot.tooltip') }}
    </div>
  </div>
</template>

<style scoped>
.mascot-container {
  position: fixed;
  bottom: 2rem;
  right: 2rem;
  width: 120px;
  height: 120px;
  cursor: pointer;
  z-index: 100;
  transition: transform 0.3s ease;
}

.mascot-container:hover {
  transform: scale(1.1);
}

.mascot-svg {
  width: 100%;
  height: 100%;
  filter: drop-shadow(0 4px 6px rgba(0, 0, 0, 0.1));
}

/* Body animation - gentle floating */
.mascot-body {
  animation: float 3s ease-in-out infinite;
  transition: fill 0.3s ease;
}

@keyframes float {
  0%, 100% {
    transform: translateY(0);
  }
  50% {
    transform: translateY(-8px);
  }
}

/* Shadow animation - synced with float */
.mascot-shadow {
  animation: shadowPulse 3s ease-in-out infinite;
}

@keyframes shadowPulse {
  0%, 100% {
    opacity: 0.2;
    transform: scale(1);
  }
  50% {
    opacity: 0.15;
    transform: scale(0.9);
  }
}

/* Arms animation - gentle wave */
.mascot-arm {
  animation: idle-wave 4s ease-in-out infinite;
  transform-origin: center;
  transition: fill 0.3s ease;
}

.mascot-arm-left {
  animation-delay: 0s;
}

.mascot-arm-right {
  animation-delay: 2s;
}

@keyframes idle-wave {
  0%, 100% {
    transform: rotate(0deg);
  }
  50% {
    transform: rotate(-10deg);
  }
}

/* Excited arm wave on hover */
.is-hovered .mascot-arm {
  animation: excited-wave 0.5s ease-in-out infinite;
}

@keyframes excited-wave {
  0%, 100% {
    transform: rotate(0deg);
  }
  25% {
    transform: rotate(-20deg);
  }
  75% {
    transform: rotate(20deg);
  }
}

/* Eye animations */
.mascot-pupil {
  animation: look-around 5s ease-in-out infinite;
}

.mascot-pupil-left {
  animation-delay: 0s;
}

.mascot-pupil-right {
  animation-delay: 0.1s;
}

@keyframes look-around {
  0%, 100% {
    transform: translate(0, 0);
  }
  25% {
    transform: translate(2px, -1px);
  }
  50% {
    transform: translate(-2px, 1px);
  }
  75% {
    transform: translate(1px, 2px);
  }
}

/* Hover effect - pupils follow cursor feeling */
.is-hovered .mascot-pupil {
  animation: none;
  transform: translate(2px, 2px);
}

/* Sparkles animation */
.sparkle {
  animation: sparkle-twinkle 1s ease-in-out infinite;
  transform-origin: center;
}

.sparkle-1 {
  animation-delay: 0s;
}

.sparkle-2 {
  animation-delay: 0.2s;
}

.sparkle-3 {
  animation-delay: 0.4s;
}

.sparkle-4 {
  animation-delay: 0.6s;
}

@keyframes sparkle-twinkle {
  0%, 100% {
    opacity: 0;
    transform: scale(0);
  }
  50% {
    opacity: 1;
    transform: scale(1);
  }
}

/* Tooltip */
.mascot-tooltip {
  position: absolute;
  bottom: 100%;
  left: 50%;
  transform: translateX(-50%) translateY(-10px);
  background: rgba(0, 0, 0, 0.8);
  color: white;
  padding: 0.5rem 1rem;
  border-radius: 0.5rem;
  white-space: nowrap;
  opacity: 0;
  pointer-events: none;
  transition: opacity 0.3s ease, transform 0.3s ease;
  font-size: 0.875rem;
  margin-bottom: 0.5rem;
}

.mascot-tooltip::after {
  content: '';
  position: absolute;
  top: 100%;
  left: 50%;
  transform: translateX(-50%);
  border: 6px solid transparent;
  border-top-color: rgba(0, 0, 0, 0.8);
}

.mascot-tooltip.show {
  opacity: 1;
  transform: translateX(-50%) translateY(0);
}

/* Hide on mobile devices (optional) */
@media (max-width: 768px) {
  .mascot-container {
    width: 80px;
    height: 80px;
    bottom: 1rem;
    right: 1rem;
  }
}

/* Print styles - hide mascot */
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
      "aria": "Friendly CV mascot companion",
      "tooltip": "Hi! I'm your CV guide 👋"
    }
  },
  "de": {
    "mascot": {
      "aria": "Freundliches CV-Maskottchen",
      "tooltip": "Hallo! Ich bin dein CV-Begleiter 👋"
    }
  }
}
</i18n>
