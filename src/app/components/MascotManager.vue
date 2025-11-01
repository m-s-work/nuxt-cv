<script setup lang="ts">
import MascotCat from './mascots/MascotCat.vue'
import MascotRobot from './mascots/MascotRobot.vue'
import MascotOwl from './mascots/MascotOwl.vue'

const { t } = useI18n()
const mascot = useMascot()

// Track scroll position
const updateScroll = () => {
  if (typeof window === 'undefined') return
  
  const scrollTop = window.scrollY
  const docHeight = document.documentElement.scrollHeight - window.innerHeight
  const scrollPercent = docHeight > 0 ? (scrollTop / docHeight) * 100 : 0
  
  mascot.updateScrollProgress(scrollPercent)
}

// Set up scroll listener
onMounted(() => {
  if (typeof window === 'undefined') return
  
  // Check URL parameter for mascot design
  const urlParams = new URLSearchParams(window.location.search)
  const mascotParam = urlParams.get('mascot')
  const validDesigns = ['cat', 'robot', 'owl']
  if (mascotParam && validDesigns.includes(mascotParam)) {
    mascot.setDesign(mascotParam as 'cat' | 'robot' | 'owl')
  }
  
  // Position parameter
  const positionParam = urlParams.get('mascotPosition')
  const validPositions = ['bottom-right', 'bottom-left', 'top-right', 'top-left']
  if (positionParam && validPositions.includes(positionParam)) {
    mascot.setPosition(positionParam as any)
  }
  
  window.addEventListener('scroll', updateScroll, { passive: true })
  updateScroll()
  
  // Wave on mount after a delay
  setTimeout(() => {
    mascot.wave()
  }, 2000)
})

onUnmounted(() => {
  if (typeof window !== 'undefined') {
    window.removeEventListener('scroll', updateScroll)
  }
})

// Position classes
const positionClasses = computed(() => {
  const pos = mascot.position.value
  const classes: string[] = ['mascot-container']
  
  if (pos.includes('bottom')) {
    classes.push('bottom-position')
  } else {
    classes.push('top-position')
  }
  
  if (pos.includes('right')) {
    classes.push('right-position')
  } else {
    classes.push('left-position')
  }
  
  return classes.join(' ')
})

// Handle mouse events
const handleMouseEnter = () => {
  mascot.setHovered(true)
}

const handleMouseLeave = () => {
  mascot.setHovered(false)
}
</script>

<template>
  <Teleport to="body">
    <Transition name="mascot-fade">
      <div
        v-if="mascot.isVisible.value"
        :class="positionClasses"
        @mouseenter="handleMouseEnter"
        @mouseleave="handleMouseLeave"
      >
        <div class="mascot-wrapper no-print">
          <!-- Cat mascot -->
          <MascotCat
            v-if="mascot.design.value === 'cat'"
            :animation="mascot.currentAnimation.value"
            :is-hovered="mascot.isHovered.value"
          />
          
          <!-- Robot mascot -->
          <MascotRobot
            v-else-if="mascot.design.value === 'robot'"
            :animation="mascot.currentAnimation.value"
            :is-hovered="mascot.isHovered.value"
          />
          
          <!-- Owl mascot -->
          <MascotOwl
            v-else-if="mascot.design.value === 'owl'"
            :animation="mascot.currentAnimation.value"
            :is-hovered="mascot.isHovered.value"
          />
        </div>
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped>
.mascot-container {
  position: fixed;
  z-index: 9000;
  pointer-events: auto;
}

.bottom-position {
  bottom: 20px;
}

.top-position {
  top: 20px;
}

.right-position {
  right: 20px;
}

.left-position {
  left: 20px;
}

.mascot-wrapper {
  width: 120px;
  height: 120px;
  cursor: pointer;
  transition: transform 0.3s ease;
}

.mascot-wrapper:hover {
  transform: scale(1.05);
}

.mascot-placeholder {
  width: 100%;
  height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 80px;
  background: rgba(255, 255, 255, 0.9);
  border-radius: 50%;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.1);
}

.mascot-fade-enter-active,
.mascot-fade-leave-active {
  transition: opacity 0.5s ease, transform 0.5s ease;
}

.mascot-fade-enter-from,
.mascot-fade-leave-to {
  opacity: 0;
  transform: scale(0.5);
}

/* Responsive adjustments */
@media (max-width: 768px) {
  .mascot-wrapper {
    width: 80px;
    height: 80px;
  }
  
  .bottom-position {
    bottom: 10px;
  }
  
  .top-position {
    top: 10px;
  }
  
  .right-position {
    right: 10px;
  }
  
  .left-position {
    left: 10px;
  }
  
  .mascot-placeholder {
    font-size: 50px;
  }
}

@media print {
  .mascot-container,
  .no-print {
    display: none !important;
  }
}
</style>
