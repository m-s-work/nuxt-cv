<script setup lang="ts">
const props = defineProps<{
  animation: 'idle' | 'tickle' | 'wave' | 'celebrate'
  isHovered: boolean
}>()
</script>

<template>
  <svg
    viewBox="0 0 200 200"
    xmlns="http://www.w3.org/2000/svg"
    :class="['mascot-cat', `animation-${animation}`, { 'is-hovered': isHovered }]"
  >
    <!-- Shadow -->
    <ellipse
      cx="100"
      cy="180"
      rx="40"
      ry="8"
      fill="rgba(0, 0, 0, 0.2)"
      class="shadow"
    />

    <!-- Tail -->
    <path
      d="M 140 120 Q 170 110, 180 80 Q 185 60, 175 50"
      fill="none"
      stroke="#FF9500"
      stroke-width="12"
      stroke-linecap="round"
      class="tail"
    />

    <!-- Body -->
    <ellipse
      cx="100"
      cy="130"
      rx="45"
      ry="50"
      fill="#FF9500"
      class="body"
    />

    <!-- Head -->
    <circle
      cx="100"
      cy="80"
      r="40"
      fill="#FF9500"
      class="head"
    />

    <!-- Left ear -->
    <path
      d="M 70 60 L 60 30 L 80 50 Z"
      fill="#FF9500"
      class="ear-left"
    />
    <path
      d="M 70 55 L 65 40 L 77 50 Z"
      fill="#FFB340"
      class="ear-inner"
    />

    <!-- Right ear -->
    <path
      d="M 130 60 L 140 30 L 120 50 Z"
      fill="#FF9500"
      class="ear-right"
    />
    <path
      d="M 130 55 L 135 40 L 123 50 Z"
      fill="#FFB340"
      class="ear-inner"
    />

    <!-- Face details -->
    <!-- Left eye -->
    <ellipse
      cx="85"
      cy="75"
      rx="8"
      ry="12"
      fill="#2D2D2D"
      class="eye-left"
    />
    <circle
      cx="87"
      cy="72"
      r="3"
      fill="white"
      class="eye-shine"
    />

    <!-- Right eye -->
    <ellipse
      cx="115"
      cy="75"
      rx="8"
      ry="12"
      fill="#2D2D2D"
      class="eye-right"
    />
    <circle
      cx="117"
      cy="72"
      r="3"
      fill="white"
      class="eye-shine"
    />

    <!-- Nose -->
    <path
      d="M 100 85 L 95 90 L 105 90 Z"
      fill="#2D2D2D"
      class="nose"
    />

    <!-- Mouth -->
    <path
      d="M 100 90 Q 90 95, 85 92"
      fill="none"
      stroke="#2D2D2D"
      stroke-width="2"
      stroke-linecap="round"
      class="mouth-left"
    />
    <path
      d="M 100 90 Q 110 95, 115 92"
      fill="none"
      stroke="#2D2D2D"
      stroke-width="2"
      stroke-linecap="round"
      class="mouth-right"
    />

    <!-- Whiskers left -->
    <line x1="60" y1="80" x2="40" y2="75" stroke="#2D2D2D" stroke-width="1.5" class="whisker" />
    <line x1="60" y1="85" x2="40" y2="85" stroke="#2D2D2D" stroke-width="1.5" class="whisker" />
    <line x1="60" y1="90" x2="40" y2="95" stroke="#2D2D2D" stroke-width="1.5" class="whisker" />

    <!-- Whiskers right -->
    <line x1="140" y1="80" x2="160" y2="75" stroke="#2D2D2D" stroke-width="1.5" class="whisker" />
    <line x1="140" y1="85" x2="160" y2="85" stroke="#2D2D2D" stroke-width="1.5" class="whisker" />
    <line x1="140" y1="90" x2="160" y2="95" stroke="#2D2D2D" stroke-width="1.5" class="whisker" />

    <!-- Paws -->
    <ellipse cx="75" cy="165" rx="18" ry="15" fill="#FF9500" class="paw" />
    <ellipse cx="125" cy="165" rx="18" ry="15" fill="#FF9500" class="paw" />

    <!-- Belly -->
    <ellipse
      cx="100"
      cy="135"
      rx="25"
      ry="35"
      fill="#FFB340"
      opacity="0.6"
      class="belly"
    />
  </svg>
</template>

<style scoped>
.mascot-cat {
  width: 100%;
  height: 100%;
}

/* Idle animation - gentle breathing */
.mascot-cat.animation-idle {
  animation: catBreathe 3s ease-in-out infinite;
}

@keyframes catBreathe {
  0%, 100% {
    transform: scale(1) translateY(0);
  }
  50% {
    transform: scale(1.02) translateY(-2px);
  }
}

/* Tail wag animation for idle */
.mascot-cat.animation-idle .tail {
  transform-origin: 140px 120px;
  animation: tailWag 2s ease-in-out infinite;
}

@keyframes tailWag {
  0%, 100% {
    transform: rotate(0deg);
  }
  50% {
    transform: rotate(10deg);
  }
}

/* Tickle animation - playful wiggle */
.mascot-cat.animation-tickle {
  animation: catTickle 0.5s ease-in-out infinite;
}

@keyframes catTickle {
  0%, 100% {
    transform: rotate(0deg) scale(1);
  }
  25% {
    transform: rotate(-5deg) scale(1.05);
  }
  75% {
    transform: rotate(5deg) scale(1.05);
  }
}

.mascot-cat.animation-tickle .ear-left,
.mascot-cat.animation-tickle .ear-right {
  animation: earWiggle 0.3s ease-in-out infinite;
}

@keyframes earWiggle {
  0%, 100% {
    transform: rotate(0deg);
  }
  50% {
    transform: rotate(-10deg);
  }
}

/* Wave animation */
.mascot-cat.animation-wave .paw:first-of-type {
  transform-origin: 75px 165px;
  animation: pawWave 0.5s ease-in-out 3;
}

@keyframes pawWave {
  0%, 100% {
    transform: rotate(0deg) translateY(0);
  }
  50% {
    transform: rotate(-20deg) translateY(-10px);
  }
}

/* Celebrate animation - jump for joy */
.mascot-cat.animation-celebrate {
  animation: catJump 0.6s ease-in-out 3;
}

@keyframes catJump {
  0%, 100% {
    transform: translateY(0) scale(1);
  }
  50% {
    transform: translateY(-20px) scale(1.1);
  }
}

.mascot-cat.animation-celebrate .tail {
  animation: tailCelebrate 0.3s ease-in-out infinite;
}

@keyframes tailCelebrate {
  0%, 100% {
    transform: rotate(0deg);
  }
  50% {
    transform: rotate(30deg);
  }
}

/* Eyes blink randomly */
.eye-left,
.eye-right {
  animation: eyeBlink 4s ease-in-out infinite;
}

@keyframes eyeBlink {
  0%, 48%, 52%, 100% {
    transform: scaleY(1);
  }
  50% {
    transform: scaleY(0.1);
  }
}

/* Hover state effects */
.is-hovered .eye-left,
.is-hovered .eye-right {
  animation: eyeExcited 0.5s ease-in-out infinite;
}

@keyframes eyeExcited {
  0%, 100% {
    transform: scaleY(1);
  }
  50% {
    transform: scaleY(1.3);
  }
}

/* Shadow pulses with breathing */
.shadow {
  animation: shadowPulse 3s ease-in-out infinite;
}

@keyframes shadowPulse {
  0%, 100% {
    opacity: 0.2;
    transform: scale(1);
  }
  50% {
    opacity: 0.15;
    transform: scale(1.1);
  }
}
</style>
