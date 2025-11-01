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
        secondary: '#334155',
        accent: '#f59e0b',
        eye: '#fbbf24',
        feather: '#475569'
      }
    case 'colorful':
      return {
        primary: '#ec4899',
        secondary: '#f472b6',
        accent: '#a855f7',
        eye: '#fbbf24',
        feather: '#f9a8d4'
      }
    default:
      return {
        primary: '#92400e',
        secondary: '#b45309',
        accent: '#f59e0b',
        eye: '#fbbf24',
        feather: '#d97706'
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
</script>

<template>
  <svg 
    :width="size" 
    :height="size" 
    viewBox="0 0 120 120" 
    :class="animationClass"
    class="mascot-owl"
  >
    <!-- Owl body -->
    <g class="owl-body">
      <!-- Body -->
      <ellipse 
        cx="60" 
        cy="70" 
        rx="35" 
        ry="40" 
        :fill="colors.primary"
        class="body"
      />
      
      <!-- Wings -->
      <ellipse 
        cx="30" 
        cy="70" 
        rx="12" 
        ry="25" 
        :fill="colors.feather"
        class="wing wing-left"
      />
      <ellipse 
        cx="90" 
        cy="70" 
        rx="12" 
        ry="25" 
        :fill="colors.feather"
        class="wing wing-right"
      />
      
      <!-- Head -->
      <circle 
        cx="60" 
        cy="45" 
        r="28" 
        :fill="colors.primary"
        class="head"
      />
      
      <!-- Ear tufts -->
      <path 
        d="M 40 25 L 35 15 L 42 20 Z" 
        :fill="colors.feather"
        class="ear-tuft ear-left"
      />
      <path 
        d="M 80 25 L 85 15 L 78 20 Z" 
        :fill="colors.feather"
        class="ear-tuft ear-right"
      />
      
      <!-- Eye patches (white) -->
      <circle 
        cx="50" 
        cy="45" 
        r="12" 
        fill="white"
        class="eye-patch"
      />
      <circle 
        cx="70" 
        cy="45" 
        r="12" 
        fill="white"
        class="eye-patch"
      />
      
      <!-- Eyes -->
      <circle 
        cx="50" 
        cy="45" 
        r="8" 
        :fill="colors.eye"
        class="eye eye-left"
      />
      <circle 
        cx="70" 
        cy="45" 
        r="8" 
        :fill="colors.eye"
        class="eye eye-right"
      />
      
      <!-- Pupils -->
      <circle 
        cx="50" 
        cy="45" 
        r="4" 
        fill="#1e293b"
        class="pupil pupil-left"
      />
      <circle 
        cx="70" 
        cy="45" 
        r="4" 
        fill="#1e293b"
        class="pupil pupil-right"
      />
      
      <!-- Eye highlights -->
      <circle 
        cx="52" 
        cy="43" 
        r="2" 
        fill="white"
      />
      <circle 
        cx="72" 
        cy="43" 
        r="2" 
        fill="white"
      />
      
      <!-- Beak -->
      <path 
        d="M 60 50 L 55 58 L 60 56 L 65 58 Z" 
        :fill="colors.accent"
        class="beak"
      />
      
      <!-- Feet -->
      <g class="feet">
        <path 
          d="M 50 105 L 48 112 M 50 105 L 50 112 M 50 105 L 52 112" 
          stroke="#f59e0b" 
          stroke-width="2"
          stroke-linecap="round"
          class="foot foot-left"
        />
        <path 
          d="M 70 105 L 68 112 M 70 105 L 70 112 M 70 105 L 72 112" 
          stroke="#f59e0b" 
          stroke-width="2"
          stroke-linecap="round"
          class="foot foot-right"
        />
      </g>
      
      <!-- Chest feather pattern -->
      <ellipse 
        cx="60" 
        cy="75" 
        rx="15" 
        ry="18" 
        :fill="colors.secondary"
        opacity="0.6"
      />
    </g>
  </svg>
</template>

<style scoped>
.mascot-owl {
  transition: transform 0.3s ease;
}

/* Idle animation - gentle breathing and blinking */
.mascot-idle .body {
  animation: breathe-owl 3s ease-in-out infinite;
}

.mascot-idle .eye {
  animation: blink-owl 5s ease-in-out infinite;
}

.mascot-idle .pupil {
  animation: look-around-owl 6s ease-in-out infinite;
}

/* Hover animation - excited flapping */
.mascot-hover .wing-left {
  animation: flap-left 0.4s ease-in-out infinite;
  transform-origin: 30px 70px;
}

.mascot-hover .wing-right {
  animation: flap-right 0.4s ease-in-out infinite;
  transform-origin: 90px 70px;
}

.mascot-hover .eye {
  animation: eye-wide 0.5s ease-in-out;
}

/* Scrolling animation - head tracking */
.mascot-scrolling .head {
  animation: head-turn 1s ease-in-out;
}

.mascot-scrolling .pupil {
  animation: pupil-scroll 1s ease-in-out;
}

/* Greeting animation - friendly wave with wing */
.mascot-greeting .wing-right {
  animation: wing-wave 1s ease-in-out 3;
  transform-origin: 90px 70px;
}

.mascot-greeting .head {
  animation: head-nod 1s ease-in-out 3;
}

/* Thinking animation - head tilt */
.mascot-thinking .head {
  animation: head-tilt 1.5s ease-in-out infinite;
  transform-origin: 60px 45px;
}

.mascot-thinking .ear-tuft {
  animation: ear-twitch 1.5s ease-in-out infinite;
}

/* Celebrating animation - happy bounce */
.mascot-celebrating {
  animation: owl-bounce 0.5s ease-in-out 5;
}

.mascot-celebrating .wing-left {
  animation: celebrate-flap-left 0.3s ease-in-out infinite;
  transform-origin: 30px 70px;
}

.mascot-celebrating .wing-right {
  animation: celebrate-flap-right 0.3s ease-in-out infinite;
  transform-origin: 90px 70px;
}

/* Keyframe animations */
@keyframes breathe-owl {
  0%, 100% {
    transform: scaleY(1);
  }
  50% {
    transform: scaleY(1.03);
  }
}

@keyframes blink-owl {
  0%, 90%, 100% {
    transform: scaleY(1);
  }
  95% {
    transform: scaleY(0.2);
  }
}

@keyframes look-around-owl {
  0%, 100% {
    transform: translate(0, 0);
  }
  25% {
    transform: translate(-2px, -1px);
  }
  50% {
    transform: translate(0, 0);
  }
  75% {
    transform: translate(2px, -1px);
  }
}

@keyframes flap-left {
  0%, 100% {
    transform: rotate(0deg);
  }
  50% {
    transform: rotate(-20deg);
  }
}

@keyframes flap-right {
  0%, 100% {
    transform: rotate(0deg);
  }
  50% {
    transform: rotate(20deg);
  }
}

@keyframes eye-wide {
  0%, 100% {
    transform: scale(1);
  }
  50% {
    transform: scale(1.15);
  }
}

@keyframes head-turn {
  0%, 100% {
    transform: rotate(0deg);
  }
  25% {
    transform: rotate(-5deg);
  }
  75% {
    transform: rotate(5deg);
  }
}

@keyframes pupil-scroll {
  0%, 100% {
    transform: translate(0, 0);
  }
  50% {
    transform: translate(0, 3px);
  }
}

@keyframes wing-wave {
  0%, 100% {
    transform: rotate(0deg);
  }
  25% {
    transform: rotate(30deg);
  }
  75% {
    transform: rotate(-10deg);
  }
}

@keyframes head-nod {
  0%, 100% {
    transform: translateY(0);
  }
  50% {
    transform: translateY(3px);
  }
}

@keyframes head-tilt {
  0%, 100% {
    transform: rotate(0deg);
  }
  50% {
    transform: rotate(-15deg);
  }
}

@keyframes ear-twitch {
  0%, 100% {
    transform: translateY(0);
  }
  50% {
    transform: translateY(-2px);
  }
}

@keyframes owl-bounce {
  0%, 100% {
    transform: translateY(0);
  }
  50% {
    transform: translateY(-12px);
  }
}

@keyframes celebrate-flap-left {
  0%, 100% {
    transform: rotate(0deg);
  }
  50% {
    transform: rotate(-30deg);
  }
}

@keyframes celebrate-flap-right {
  0%, 100% {
    transform: rotate(0deg);
  }
  50% {
    transform: rotate(30deg);
  }
}

@media print {
  .mascot-owl {
    display: none;
  }
}
</style>
