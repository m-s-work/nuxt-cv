<script setup lang="ts">
import { parsePeriod, calculateColumns, getYearRange, type TimelineItem } from '~/utils/timeline'

interface Experience {
  id: number
  company: string
  position: string
  period: string
  description: string
  technologies: string[]
}

interface Study {
  id: number
  institution: string
  degree: string
  period: string
  focus: string
}

const props = defineProps<{
  experiences: Experience[]
  studies: Study[]
}>()

// Track scroll position and active items
const scrollY = ref(0)
const activeItems = ref<Set<string>>(new Set())

// Parse timeline data
const timelineItems = computed(() => {
  const items: Array<{ id: number; type: 'experience' | 'study'; start: number; end: number }> = []
  
  props.experiences.forEach(exp => {
    const parsed = parsePeriod(exp.period)
    items.push({
      id: exp.id,
      type: 'experience',
      start: parsed.start,
      end: parsed.end
    })
  })
  
  props.studies.forEach(study => {
    const parsed = parsePeriod(study.period)
    items.push({
      id: study.id,
      type: 'study',
      start: parsed.start,
      end: parsed.end
    })
  })
  
  return calculateColumns(items)
})

const yearRange = computed(() => getYearRange(timelineItems.value))

const maxColumns = computed(() => {
  return Math.max(...timelineItems.value.map(item => item.column), 0) + 1
})

// Calculate SVG dimensions
const svgWidth = computed(() => {
  // Base width + width per column
  return 60 + (maxColumns.value - 1) * 40
})

const svgHeight = 800 // Fixed height, will scroll with content

// Calculate Y position based on year
function getYPosition(year: number): number {
  const { minYear, totalYears } = yearRange.value
  if (totalYears === 0) return 0
  
  const progress = (year - minYear) / totalYears
  return progress * (svgHeight - 100) + 50 // 50px padding top/bottom
}

// Calculate item height based on duration
function getItemHeight(duration: number): number {
  const { totalYears } = yearRange.value
  if (totalYears === 0) return 0
  
  return (duration / totalYears) * (svgHeight - 100)
}

// Calculate X position based on column
function getXPosition(column: number): number {
  return 30 + column * 40
}

// Check if item is in viewport/active
function isItemActive(item: TimelineItem): boolean {
  return activeItems.value.has(`${item.type}-${item.id}`)
}

// Set up intersection observer for tracking active items
onMounted(() => {
  const handleScroll = () => {
    scrollY.value = window.scrollY
  }
  
  window.addEventListener('scroll', handleScroll)
  
  // Set up intersection observer to track which items are visible
  const observer = new IntersectionObserver(
    (entries) => {
      entries.forEach(entry => {
        const el = entry.target as HTMLElement
        const itemId = el.dataset.itemId
        if (itemId) {
          if (entry.isIntersecting) {
            activeItems.value.add(itemId)
          } else {
            activeItems.value.delete(itemId)
          }
        }
      })
    },
    {
      threshold: 0.3,
      rootMargin: '-20% 0px -20% 0px'
    }
  )
  
  // Observe all experience and study cards
  nextTick(() => {
    document.querySelectorAll('[data-item-id]').forEach(el => {
      observer.observe(el)
    })
  })
  
  onUnmounted(() => {
    window.removeEventListener('scroll', handleScroll)
    observer.disconnect()
  })
})

// Generate year markers
const yearMarkers = computed(() => {
  const { minYear, maxYear } = yearRange.value
  const markers = []
  
  // Show every 2 years for cleaner look
  for (let year = minYear; year <= maxYear; year += 2) {
    markers.push({
      year,
      y: getYPosition(year)
    })
  }
  
  return markers
})
</script>

<template>
  <div class="timeline-container hidden lg:block print:block">
    <svg 
      :width="svgWidth" 
      :height="svgHeight" 
      class="timeline-svg"
      xmlns="http://www.w3.org/2000/svg"
    >
      <!-- Year axis line -->
      <line 
        :x1="svgWidth / 2" 
        y1="40" 
        :x2="svgWidth / 2" 
        :y2="svgHeight - 40" 
        stroke="currentColor" 
        stroke-width="2" 
        class="text-gray-300 dark:text-gray-600 print:text-gray-400"
      />
      
      <!-- Year markers -->
      <g v-for="marker in yearMarkers" :key="marker.year">
        <circle 
          :cx="svgWidth / 2" 
          :cy="marker.y" 
          r="4" 
          class="text-gray-400 dark:text-gray-500 print:text-gray-500"
          fill="currentColor"
        />
        <text 
          :x="svgWidth / 2 - 35" 
          :y="marker.y + 5" 
          class="text-xs text-gray-600 dark:text-gray-400 print:text-gray-700"
          fill="currentColor"
        >
          {{ marker.year }}
        </text>
      </g>
      
      <!-- Timeline items -->
      <g v-for="item in timelineItems" :key="`${item.type}-${item.id}`">
        <!-- Item bar -->
        <rect 
          :x="getXPosition(item.column)" 
          :y="getYPosition(item.start)" 
          width="20" 
          :height="getItemHeight(item.duration)"
          :class="[
            'transition-all duration-300',
            isItemActive(item) 
              ? item.type === 'experience' 
                ? 'fill-primary-600 dark:fill-primary-500 print:fill-primary-700' 
                : 'fill-blue-600 dark:fill-blue-500 print:fill-blue-700'
              : 'fill-gray-400 dark:fill-gray-600 print:fill-gray-400 opacity-50'
          ]"
          rx="3"
        />
        
        <!-- Connector line to center axis -->
        <line 
          :x1="getXPosition(item.column) + 20" 
          :y1="getYPosition(item.start) + getItemHeight(item.duration) / 2" 
          :x2="svgWidth / 2" 
          :y2="getYPosition(item.start) + getItemHeight(item.duration) / 2" 
          :class="[
            'transition-all duration-300',
            isItemActive(item) 
              ? 'stroke-gray-400 dark:stroke-gray-500 print:stroke-gray-500' 
              : 'stroke-gray-300 dark:stroke-gray-700 print:stroke-gray-300 opacity-30'
          ]"
          stroke-width="1.5"
          stroke-dasharray="3,3"
        />
      </g>
    </svg>
  </div>
</template>

<style scoped>
.timeline-container {
  position: sticky;
  top: 0;
  height: 100vh;
  align-items: flex-start;
  padding-top: 2rem;
}

.timeline-svg {
  max-height: 100%;
  overflow: visible;
}

@media (min-width: 1024px) {
  .timeline-container {
    display: flex;
  }
}

@media print {
  .timeline-container {
    display: flex;
    position: static;
    height: auto;
    padding-top: 1rem;
  }
  
  .timeline-svg {
    max-height: none;
  }
}
</style>
