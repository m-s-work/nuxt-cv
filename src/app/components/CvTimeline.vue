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

// Track scroll position and the single active item
const scrollY = ref(0)
const activeItemId = ref<string | null>(null)

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

// Calculate Y position based on year (reversed - newest at top)
function getYPosition(year: number): number {
  const { minYear, maxYear, totalYears } = yearRange.value
  if (totalYears === 0) return 0
  
  // Reverse: newer years at top
  const progress = (maxYear - year) / totalYears
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

// Check if item is the active one
function isItemActive(item: TimelineItem): boolean {
  return activeItemId.value === `${item.type}-${item.id}`
}

// Set up intersection observer for tracking active items
onMounted(() => {
  const handleScroll = () => {
    scrollY.value = window.scrollY
  }
  
  window.addEventListener('scroll', handleScroll)
  
  // Set up intersection observer to track which item is most visible
  const observer = new IntersectionObserver(
    (entries) => {
      // Find the entry with the highest intersection ratio
      let mostVisible = entries.reduce((max, entry) => {
        return entry.intersectionRatio > (max?.intersectionRatio || 0) ? entry : max
      }, null as IntersectionObserverEntry | null)
      
      if (mostVisible && mostVisible.isIntersecting && mostVisible.intersectionRatio > 0.3) {
        const el = mostVisible.target as HTMLElement
        const itemId = el.dataset.itemId
        if (itemId) {
          activeItemId.value = itemId
        }
      } else if (!entries.some(e => e.isIntersecting)) {
        // No items visible
        activeItemId.value = null
      }
    },
    {
      threshold: [0, 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1.0],
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
      <!-- Define arrow marker -->
      <defs>
        <marker
          id="arrowhead"
          markerWidth="8"
          markerHeight="8"
          refX="7"
          refY="4"
          orient="auto"
        >
          <polygon
            points="0 0, 8 4, 0 8"
            class="fill-gray-400 dark:fill-gray-500 print:fill-gray-500"
          />
        </marker>
        <marker
          id="arrowhead-active"
          markerWidth="8"
          markerHeight="8"
          refX="7"
          refY="4"
          orient="auto"
        >
          <polygon
            points="0 0, 8 4, 0 8"
            class="fill-gray-600 dark:fill-gray-400 print:fill-gray-600"
          />
        </marker>
      </defs>
      
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
        <!-- Item bar - always gray, only active items get color -->
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
              : 'fill-gray-300 dark:fill-gray-600 print:fill-gray-400'
          ]"
          rx="3"
        />
        
        <!-- Arrow line to center axis - with proper distance and arrow marker -->
        <line 
          :x1="getXPosition(item.column) + 20" 
          :y1="getYPosition(item.start) + getItemHeight(item.duration) / 2" 
          :x2="svgWidth / 2 - 8" 
          :y2="getYPosition(item.start) + getItemHeight(item.duration) / 2" 
          :class="[
            'transition-all duration-300',
            isItemActive(item) 
              ? 'stroke-gray-600 dark:stroke-gray-400 print:stroke-gray-600' 
              : 'stroke-gray-300 dark:stroke-gray-700 print:stroke-gray-300 opacity-30'
          ]"
          stroke-width="1.5"
          :marker-end="isItemActive(item) ? 'url(#arrowhead-active)' : 'url(#arrowhead)'"
        />
        
        <!-- Dot/Bubble on the right side - only visible when active, with spacing -->
        <circle 
          v-if="isItemActive(item)"
          :cx="getXPosition(item.column) + 35" 
          :cy="getYPosition(item.start) + getItemHeight(item.duration) / 2" 
          r="8" 
          :class="[
            'transition-all duration-300',
            item.type === 'experience' 
              ? 'fill-primary-600 dark:fill-primary-500 print:fill-primary-700' 
              : 'fill-blue-600 dark:fill-blue-500 print:fill-blue-700'
          ]"
        />
        
        <!-- Inner dot for bubble effect - only visible when active, with spacing -->
        <circle 
          v-if="isItemActive(item)"
          :cx="getXPosition(item.column) + 35" 
          :cy="getYPosition(item.start) + getItemHeight(item.duration) / 2" 
          r="4" 
          class="fill-white dark:fill-gray-900 print:fill-white transition-all duration-300"
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
