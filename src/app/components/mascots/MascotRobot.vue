<script setup lang="ts">
import type { MascotProps } from '~/types/mascot'

const props = withDefaults(defineProps<MascotProps>(), {
  theme: 'default',
  animationState: 'idle',
  size: 120
})

// Determine colors based on theme
const colors = computed(() => {
  switch (props.theme) {
    case 'dark':
      return {
        primary: '#1e293b',
        secondary: '#475569',
        accent: '#3b82f6',
        eye: '#60a5fa',
        background: '#0f172a'
      }
    case 'colorful':
      return {
        primary: '#8b5cf6',
        secondary: '#a78bfa',
        accent: '#fbbf24',
        eye: '#34d399',
        background: '#c084fc'
      }
    default:
      return {
        primary: '#3b82f6',
        secondary: '#60a5fa',
        accent: '#fbbf24',
        eye: '#1e293b',
        background: '#dbeafe'
      }
  }
})

// Animation classes based on state
const animationClass = computed(() => {
  switch (props.animationState) {
    case 'hover':
      return 'mascot-hover'
    case 'scrolling':
      return 'mascot-scrolling'
    case 'greeting':
      return 'mascot-greeting'
    case 'thinking':
      return 'mascot-thinking'
    case 'celebrating':
      return 'mascot-celebrating'
    default:
      return 'mascot-idle'
  }
})

// Show wave animation when scrolling down past certain sections
const showWave = computed(() => {
  return props.animationState === 'greeting' || 
         (props.scrollContext?.section === 'experiences' && props.scrollContext?.direction === 'down')
})
</script>

<template>
  <svg 
    :width="size" 
    :height="size" 
    viewBox="0 0 120 120" 
    :class="animationClass"
    class="mascot-robot"
  >
    <!-- Robot body -->
    <g class="robot-body">
      <!-- Head -->
      <rect 
        x="35" 
        y="25" 
        width="50" 
        height="45" 
        rx="8" 
        :fill="colors.primary"
        class="robot-head"
      />
      
      <!-- Antenna -->
      <line 
        x1="60" 
        y1="25" 
        x2="60" 
        y2="15" 
        stroke="#94a3b8" 
        stroke-width="2" 
        class="antenna"
      />
      <circle 
        cx="60" 
        cy="13" 
        r="3" 
        :fill="colors.accent"
        class="antenna-light"
      />
      
      <!-- Eyes -->
      <circle 
        cx="48" 
        cy="42" 
        r="5" 
        :fill="colors.eye"
        class="eye eye-left"
      />
      <circle 
        cx="72" 
        cy="42" 
        r="5" 
        :fill="colors.eye"
        class="eye eye-right"
      />
      
      <!-- Eye highlights -->
      <circle 
        cx="50" 
        cy="40" 
        r="2" 
        fill="white"
        class="eye-highlight"
      />
      <circle 
        cx="74" 
        cy="40" 
        r="2" 
        fill="white"
        class="eye-highlight"
      />
      
      <!-- Mouth -->
      <path 
        d="M 45 55 Q 60 62 75 55" 
        stroke="#94a3b8" 
        stroke-width="2" 
        fill="none" 
        stroke-linecap="round"
        class="mouth"
      />
      
      <!-- Body/Torso -->
      <rect 
        x="40" 
        y="72" 
        width="40" 
        height="35" 
        rx="5" 
        :fill="colors.secondary"
        class="robot-torso"
      />
      
      <!-- Chest panel -->
      <rect 
        x="50" 
        y="80" 
        width="20" 
        height="20" 
        rx="2" 
        :fill="colors.background"
        opacity="0.5"
      />
      
      <!-- Chest light -->
      <circle 
        cx="60" 
        cy="90" 
        r="3" 
        :fill="colors.accent"
        class="chest-light"
      />
      
      <!-- Arms -->
      <rect 
        x="28" 
        y="75" 
        width="8" 
        height="25" 
        rx="4" 
        :fill="colors.primary"
        class="arm arm-left"
      />
      <rect 
        x="84" 
        y="75" 
        width="8" 
        height="25" 
        rx="4" 
        :fill="colors.primary"
        class="arm arm-right"
      />
      
      <!-- Hands -->
      <circle 
        cx="32" 
        cy="102" 
        r="4" 
        :fill="colors.secondary"
        class="hand hand-left"
      />
      <circle 
        cx="88" 
        cy="102" 
        r="4" 
        :fill="colors.secondary"
        class="hand hand-right"
      />
      
      <!-- Wave hand (shown conditionally) -->
      <g v-if="showWave" class="wave-hand">
        <circle 
          cx="88" 
          cy="85" 
          r="4" 
          :fill="colors.secondary"
        />
      </g>
    </g>
  </svg>
</template>

<style scoped>
.mascot-robot {
  transition: transform 0.3s ease;
}

/* Idle animation - gentle breathing */
.mascot-idle .robot-torso {
  animation: breathe 3s ease-in-out infinite;
}

.mascot-idle .antenna-light {
  animation: blink-light 2s ease-in-out infinite;
}

.mascot-idle .eye {
  animation: blink-eye 4s ease-in-out infinite;
}

/* Hover animation - excited bounce */
.mascot-hover {
  animation: bounce 0.5s ease-in-out 3;
}

.mascot-hover .eye {
  animation: eye-excited 0.5s ease-in-out infinite;
}

.mascot-hover .mouth {
  animation: smile 0.5s ease-in-out infinite;
}

/* Scrolling animation - looking around */
.mascot-scrolling .robot-head {
  animation: look-around 0.8s ease-in-out;
}

.mascot-scrolling .eye {
  animation: eye-follow 0.8s ease-in-out;
}

/* Greeting animation - waving */
.mascot-greeting .arm-right {
  animation: wave 1s ease-in-out 3;
  transform-origin: 88px 75px;
}

.mascot-greeting .hand-right {
  animation: wave-hand 1s ease-in-out 3;
}

/* Thinking animation - pondering */
.mascot-thinking .antenna {
  animation: antenna-think 1s ease-in-out infinite;
}

.mascot-thinking .antenna-light {
  animation: think-light 0.5s ease-in-out infinite;
}

/* Celebrating animation - jumping for joy */
.mascot-celebrating {
  animation: celebrate-jump 0.6s ease-in-out 4;
}

.mascot-celebrating .antenna-light {
  animation: celebrate-light 0.3s ease-in-out infinite;
}

.mascot-celebrating .chest-light {
  animation: celebrate-light 0.3s ease-in-out infinite;
}

/* Keyframe animations */
@keyframes breathe {
  0%, 100% {
    transform: scaleY(1);
  }
  50% {
    transform: scaleY(1.05);
  }
}

@keyframes blink-light {
  0%, 90%, 100% {
    opacity: 1;
  }
  95% {
    opacity: 0.3;
  }
}

@keyframes blink-eye {
  0%, 90%, 100% {
    transform: scaleY(1);
  }
  95% {
    transform: scaleY(0.1);
  }
}

@keyframes bounce {
  0%, 100% {
    transform: translateY(0);
  }
  50% {
    transform: translateY(-10px);
  }
}

@keyframes eye-excited {
  0%, 100% {
    transform: scale(1);
  }
  50% {
    transform: scale(1.2);
  }
}

@keyframes smile {
  0%, 100% {
    transform: translateY(0);
  }
  50% {
    transform: translateY(-2px);
  }
}

@keyframes look-around {
  0%, 100% {
    transform: translateX(0);
  }
  25% {
    transform: translateX(-3px);
  }
  75% {
    transform: translateX(3px);
  }
}

@keyframes eye-follow {
  0%, 100% {
    transform: translateX(0);
  }
  25% {
    transform: translateX(-2px);
  }
  75% {
    transform: translateX(2px);
  }
}

@keyframes wave {
  0%, 100% {
    transform: rotate(0deg);
  }
  25% {
    transform: rotate(-25deg);
  }
  75% {
    transform: rotate(25deg);
  }
}

@keyframes wave-hand {
  0%, 100% {
    transform: translate(0, 0);
  }
  25% {
    transform: translate(-3px, -5px);
  }
  75% {
    transform: translate(3px, -5px);
  }
}

@keyframes antenna-think {
  0%, 100% {
    transform: rotate(0deg);
    transform-origin: 60px 25px;
  }
  50% {
    transform: rotate(10deg);
    transform-origin: 60px 25px;
  }
}

@keyframes think-light {
  0%, 100% {
    opacity: 1;
    transform: scale(1);
  }
  50% {
    opacity: 0.5;
    transform: scale(1.3);
  }
}

@keyframes celebrate-jump {
  0%, 100% {
    transform: translateY(0);
  }
  50% {
    transform: translateY(-15px);
  }
}

@keyframes celebrate-light {
  0%, 100% {
    opacity: 1;
  }
  50% {
    opacity: 0.3;
  }
}

@media print {
  .mascot-robot {
    display: none;
  }
}
</style>
