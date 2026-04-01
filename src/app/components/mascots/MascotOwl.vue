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
    :class="['mascot-owl', `animation-${animation}`, { 'is-hovered': isHovered }]"
  >
    <!-- Shadow -->
    <ellipse
      cx="100"
      cy="185"
      rx="35"
      ry="6"
      fill="rgba(0, 0, 0, 0.2)"
      class="shadow"
    />

    <!-- Body -->
    <ellipse
      cx="100"
      cy="120"
      rx="50"
      ry="60"
      fill="#8B4513"
      class="body"
    />

    <!-- Wings -->
    <g class="wing-left">
      <ellipse
        cx="55"
        cy="120"
        rx="25"
        ry="45"
        fill="#A0522D"
        transform="rotate(-20 55 120)"
      />
    </g>
    <g class="wing-right">
      <ellipse
        cx="145"
        cy="120"
        rx="25"
        ry="45"
        fill="#A0522D"
        transform="rotate(20 145 120)"
      />
    </g>

    <!-- Head -->
    <circle
      cx="100"
      cy="70"
      r="45"
      fill="#8B4513"
      class="head"
    />

    <!-- Ear tufts -->
    <path
      d="M 70 40 L 65 15 L 75 35 Z"
      fill="#654321"
      class="ear-tuft-left"
    />
    <path
      d="M 130 40 L 135 15 L 125 35 Z"
      fill="#654321"
      class="ear-tuft-right"
    />

    <!-- Eye patches -->
    <circle cx="80" cy="70" r="22" fill="#F5DEB3" class="eye-patch-left" />
    <circle cx="120" cy="70" r="22" fill="#F5DEB3" class="eye-patch-right" />

    <!-- Eyes -->
    <circle cx="80" cy="70" r="15" fill="#2D2D2D" class="eye-left" />
    <circle cx="120" cy="70" r="15" fill="#2D2D2D" class="eye-right" />

    <!-- Pupils -->
    <circle cx="82" cy="68" r="6" fill="black" class="pupil-left" />
    <circle cx="122" cy="68" r="6" fill="black" class="pupil-right" />

    <!-- Eye shine -->
    <circle cx="84" cy="66" r="3" fill="white" class="shine-left" />
    <circle cx="124" cy="66" r="3" fill="white" class="shine-right" />

    <!-- Beak -->
    <path
      d="M 100 75 L 90 90 L 110 90 Z"
      fill="#FFA500"
      class="beak"
    />

    <!-- Belly pattern -->
    <ellipse
      cx="100"
      cy="130"
      rx="30"
      ry="40"
      fill="#F5DEB3"
      opacity="0.6"
      class="belly"
    />

    <!-- Feet -->
    <g class="foot-left">
      <path
        d="M 75 165 L 70 180 M 75 165 L 75 180 M 75 165 L 80 180"
        stroke="#FFA500"
        stroke-width="3"
        fill="none"
        stroke-linecap="round"
      />
    </g>
    <g class="foot-right">
      <path
        d="M 125 165 L 120 180 M 125 165 L 125 180 M 125 165 L 130 180"
        stroke="#FFA500"
        stroke-width="3"
        fill="none"
        stroke-linecap="round"
      />
    </g>
  </svg>
</template>

<style scoped>
.mascot-owl {
  width: 100%;
  height: 100%;
}

/* Idle animation - gentle sway */
.mascot-owl.animation-idle {
  animation: owlSway 4s ease-in-out infinite;
}

@keyframes owlSway {
  0%, 100% {
    transform: rotate(0deg);
  }
  25% {
    transform: rotate(-3deg);
  }
  75% {
    transform: rotate(3deg);
  }
}

/* Wings slightly move during idle */
.mascot-owl.animation-idle .wing-left {
  transform-origin: 55px 120px;
  animation: wingFlutterLeft 3s ease-in-out infinite;
}

.mascot-owl.animation-idle .wing-right {
  transform-origin: 145px 120px;
  animation: wingFlutterRight 3s ease-in-out infinite;
}

@keyframes wingFlutterLeft {
  0%, 100% {
    transform: rotate(0deg);
  }
  50% {
    transform: rotate(-5deg);
  }
}

@keyframes wingFlutterRight {
  0%, 100% {
    transform: rotate(0deg);
  }
  50% {
    transform: rotate(5deg);
  }
}

/* Tickle animation - head tilt */
.mascot-owl.animation-tickle .head {
  transform-origin: 100px 70px;
  animation: headTilt 0.3s ease-in-out infinite;
}

@keyframes headTilt {
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

.mascot-owl.animation-tickle .ear-tuft-left,
.mascot-owl.animation-tickle .ear-tuft-right {
  animation: tuftsWiggle 0.2s ease-in-out infinite;
}

@keyframes tuftsWiggle {
  0%, 100% {
    transform: scale(1);
  }
  50% {
    transform: scale(1.2);
  }
}

/* Wave animation - wing flap */
.mascot-owl.animation-wave .wing-left {
  animation: wingWaveLeft 0.4s ease-in-out 4;
}

.mascot-owl.animation-wave .wing-right {
  animation: wingWaveRight 0.4s ease-in-out 4;
}

@keyframes wingWaveLeft {
  0%, 100% {
    transform: rotate(0deg);
  }
  50% {
    transform: rotate(-25deg);
  }
}

@keyframes wingWaveRight {
  0%, 100% {
    transform: rotate(0deg);
  }
  50% {
    transform: rotate(25deg);
  }
}

/* Celebrate animation - hop */
.mascot-owl.animation-celebrate {
  animation: owlHop 0.5s ease-in-out 4;
}

@keyframes owlHop {
  0%, 100% {
    transform: translateY(0);
  }
  50% {
    transform: translateY(-20px);
  }
}

.mascot-owl.animation-celebrate .wing-left,
.mascot-owl.animation-celebrate .wing-right {
  animation: wingCelebrate 0.25s ease-in-out infinite;
}

@keyframes wingCelebrate {
  0%, 100% {
    transform: scale(1);
  }
  50% {
    transform: scale(1.1);
  }
}

/* Eyes blink */
.eye-left,
.eye-right {
  animation: owlBlink 5s ease-in-out infinite;
}

@keyframes owlBlink {
  0%, 48%, 52%, 100% {
    transform: scaleY(1);
  }
  50% {
    transform: scaleY(0.1);
  }
}

/* Hover effects */
.is-hovered .eye-left,
.is-hovered .eye-right {
  animation: eyesWide 0.5s ease-in-out infinite;
}

@keyframes eyesWide {
  0%, 100% {
    transform: scale(1);
  }
  50% {
    transform: scale(1.2);
  }
}

.is-hovered .pupil-left,
.is-hovered .pupil-right {
  animation: pupilDance 0.5s ease-in-out infinite;
}

@keyframes pupilDance {
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
</style>
