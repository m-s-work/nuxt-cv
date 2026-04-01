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
    :class="['mascot-robot', `animation-${animation}`, { 'is-hovered': isHovered }]"
  >
    <!-- Shadow -->
    <ellipse
      cx="100"
      cy="185"
      rx="45"
      ry="6"
      fill="rgba(0, 0, 0, 0.2)"
      class="shadow"
    />

    <!-- Antenna -->
    <line
      x1="100"
      y1="40"
      x2="100"
      y2="20"
      stroke="#708090"
      stroke-width="3"
      class="antenna"
    />
    <circle
      cx="100"
      cy="15"
      r="6"
      fill="#FF4444"
      class="antenna-light"
    />

    <!-- Head -->
    <rect
      x="70"
      y="40"
      width="60"
      height="50"
      rx="5"
      fill="#4A90E2"
      stroke="#2E5C8A"
      stroke-width="2"
      class="head"
    />

    <!-- Eyes -->
    <rect
      x="80"
      y="55"
      width="15"
      height="20"
      rx="2"
      fill="#00FF88"
      class="eye-left"
    />
    <rect
      x="105"
      y="55"
      width="15"
      height="20"
      rx="2"
      fill="#00FF88"
      class="eye-right"
    />

    <!-- Eye pupils -->
    <circle cx="87.5" cy="65" r="3" fill="#003300" class="pupil" />
    <circle cx="112.5" cy="65" r="3" fill="#003300" class="pupil" />

    <!-- Mouth -->
    <rect
      x="85"
      y="80"
      width="30"
      height="5"
      rx="2"
      fill="#2E5C8A"
      class="mouth"
    />

    <!-- Body -->
    <rect
      x="60"
      y="95"
      width="80"
      height="70"
      rx="5"
      fill="#4A90E2"
      stroke="#2E5C8A"
      stroke-width="2"
      class="body"
    />

    <!-- Control panel -->
    <rect
      x="75"
      y="110"
      width="50"
      height="30"
      rx="3"
      fill="#2E5C8A"
      class="panel"
    />
    <circle cx="85" cy="120" r="4" fill="#FF4444" opacity="0.8" />
    <circle cx="100" cy="120" r="4" fill="#44FF44" opacity="0.8" />
    <circle cx="115" cy="120" r="4" fill="#4444FF" opacity="0.8" />
    <rect x="80" y="128" width="40" height="8" rx="2" fill="#708090" />

    <!-- Arms -->
    <g class="arm-left">
      <rect
        x="35"
        y="100"
        width="25"
        height="40"
        rx="3"
        fill="#4A90E2"
        stroke="#2E5C8A"
        stroke-width="2"
      />
      <rect x="35" y="140" width="25" height="15" rx="5" fill="#708090" />
    </g>

    <g class="arm-right">
      <rect
        x="140"
        y="100"
        width="25"
        height="40"
        rx="3"
        fill="#4A90E2"
        stroke="#2E5C8A"
        stroke-width="2"
      />
      <rect x="140" y="140" width="25" height="15" rx="5" fill="#708090" />
    </g>

    <!-- Legs -->
    <rect
      x="70"
      y="165"
      width="20"
      height="15"
      rx="3"
      fill="#708090"
      class="leg"
    />
    <rect
      x="110"
      y="165"
      width="20"
      height="15"
      rx="3"
      fill="#708090"
      class="leg"
    />

    <!-- Treads -->
    <rect x="65" y="178" width="30" height="8" rx="4" fill="#2E5C8A" />
    <rect x="105" y="178" width="30" height="8" rx="4" fill="#2E5C8A" />
  </svg>
</template>

<style scoped>
.mascot-robot {
  width: 100%;
  height: 100%;
}

/* Idle animation - subtle hover */
.mascot-robot.animation-idle {
  animation: robotFloat 3s ease-in-out infinite;
}

@keyframes robotFloat {
  0%, 100% {
    transform: translateY(0);
  }
  50% {
    transform: translateY(-5px);
  }
}

/* Antenna light blinks */
.antenna-light {
  animation: lightBlink 1.5s ease-in-out infinite;
}

@keyframes lightBlink {
  0%, 45%, 55%, 100% {
    opacity: 1;
  }
  50% {
    opacity: 0.3;
  }
}

/* Tickle animation - shake */
.mascot-robot.animation-tickle {
  animation: robotShake 0.3s ease-in-out infinite;
}

@keyframes robotShake {
  0%, 100% {
    transform: translateX(0) rotate(0deg);
  }
  25% {
    transform: translateX(-3px) rotate(-2deg);
  }
  75% {
    transform: translateX(3px) rotate(2deg);
  }
}

/* Wave animation - arm movement */
.mascot-robot.animation-wave .arm-right {
  transform-origin: 152px 100px;
  animation: armWave 0.5s ease-in-out 3;
}

@keyframes armWave {
  0%, 100% {
    transform: rotate(0deg);
  }
  50% {
    transform: rotate(-30deg);
  }
}

/* Celebrate animation - jump */
.mascot-robot.animation-celebrate {
  animation: robotJump 0.5s ease-in-out 3;
}

@keyframes robotJump {
  0%, 100% {
    transform: translateY(0) rotate(0deg);
  }
  25% {
    transform: translateY(-15px) rotate(-5deg);
  }
  75% {
    transform: translateY(-15px) rotate(5deg);
  }
}

.mascot-robot.animation-celebrate .antenna-light {
  animation: celebrateLight 0.2s ease-in-out infinite;
}

@keyframes celebrateLight {
  0%, 100% {
    fill: #FF4444;
  }
  50% {
    fill: #44FF44;
  }
}

/* Eye animations */
.eye-left,
.eye-right {
  animation: eyeScan 3s ease-in-out infinite;
}

@keyframes eyeScan {
  0%, 100% {
    opacity: 1;
  }
  50% {
    opacity: 0.7;
  }
}

/* Hover effects */
.is-hovered .eye-left,
.is-hovered .eye-right {
  animation: eyeExcited 0.3s ease-in-out infinite;
}

@keyframes eyeExcited {
  0%, 100% {
    transform: scaleX(1);
  }
  50% {
    transform: scaleX(1.2);
  }
}

.is-hovered .antenna-light {
  animation: excitedLight 0.2s ease-in-out infinite;
}

@keyframes excitedLight {
  0%, 100% {
    transform: scale(1);
  }
  50% {
    transform: scale(1.5);
  }
}
</style>
