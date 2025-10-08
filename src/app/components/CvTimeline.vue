<script setup lang="ts">
import type { TimelineEntry } from '~/composables/useTimeline'

interface Props {
  experiences: Array<{
    id: number
    position: string
    startDate: string
    endDate: string | null
  }>
  studies: Array<{
    id: number
    degree: string
    startDate: string
    endDate: string
  }>
  activeIds?: (number | string)[]
}

const props = withDefaults(defineProps<Props>(), {
  activeIds: () => []
})

const { parseDate, getYearRange, assignColumns, calculatePositions } = useTimeline()

// Convert experiences and studies to timeline entries
const timelineEntries = computed<TimelineEntry[]>(() => {
  const entries: TimelineEntry[] = []
  
  props.experiences.forEach(exp => {
    entries.push({
      id: `exp-${exp.id}`,
      type: 'experience',
      startDate: exp.startDate,
      endDate: exp.endDate,
      label: exp.position
    })
  })
  
  props.studies.forEach(study => {
    entries.push({
      id: `study-${study.id}`,
      type: 'study',
      startDate: study.startDate,
      endDate: study.endDate,
      label: study.degree
    })
  })
  
  return entries
})

// Calculate timeline layout
const timelineData = computed(() => {
  const entries = timelineEntries.value
  if (entries.length === 0) return { entries: [], yearRange: { minYear: 0, maxYear: 0 }, columnCount: 0 }
  
  const yearRange = getYearRange(entries)
  const withColumns = assignColumns(entries)
  const columnCount = Math.max(...withColumns.map(e => e.column)) + 1
  const timelineHeight = 600 // Base height, adjust based on year range
  const positioned = calculatePositions(withColumns, timelineHeight)
  
  return {
    entries: positioned,
    yearRange,
    columnCount,
    timelineHeight
  }
})

// Calculate year markers
const yearMarkers = computed(() => {
  const { minYear, maxYear } = timelineData.value.yearRange
  const years: Array<{ year: number; y: number }> = []
  const yearRange = maxYear - minYear || 1
  
  for (let year = minYear; year <= maxYear; year++) {
    const y = ((year - minYear) / yearRange) * timelineData.value.timelineHeight
    years.push({ year, y })
  }
  
  return years
})

// Width calculation based on column count
const timelineWidth = computed(() => {
  const columnWidth = 40
  const baseWidth = 60 // Space for years
  return baseWidth + (timelineData.value.columnCount * columnWidth)
})

// Check if entry is active
function isActive(entryId: number | string): boolean {
  return props.activeIds.includes(entryId)
}

// Get color for entry type
function getEntryColor(type: 'experience' | 'study', active: boolean): string {
  if (active) {
    return type === 'experience' ? '#3b82f6' : '#10b981' // blue-500 or green-500
  }
  return type === 'experience' ? '#93c5fd' : '#6ee7b7' // blue-300 or green-300
}
</script>

<template>
  <div class="timeline-container hidden xl:block print:block">
    <svg 
      :width="timelineWidth" 
      :height="timelineData.timelineHeight + 40"
      class="timeline-svg"
      xmlns="http://www.w3.org/2000/svg"
    >
      <!-- Year markers and labels -->
      <g class="year-markers">
        <line
          x1="50"
          y1="0"
          x2="50"
          :y2="timelineData.timelineHeight"
          stroke="currentColor"
          stroke-width="2"
          class="text-gray-400 dark:text-gray-600 print:text-gray-400"
        />
        
        <g v-for="marker in yearMarkers" :key="marker.year">
          <circle
            cx="50"
            :cy="marker.y"
            r="4"
            fill="currentColor"
            class="text-gray-400 dark:text-gray-600 print:text-gray-400"
          />
          <text
            x="10"
            :y="marker.y + 5"
            class="text-xs fill-gray-600 dark:fill-gray-400 print:fill-gray-600"
            font-family="system-ui, -apple-system, sans-serif"
          >
            {{ marker.year }}
          </text>
        </g>
      </g>

      <!-- Timeline entries -->
      <g class="timeline-entries">
        <g v-for="entry in timelineData.entries" :key="entry.id">
          <!-- Entry bar -->
          <rect
            :x="60 + (entry.column * 40)"
            :y="entry.startY"
            width="30"
            :height="entry.height"
            :fill="getEntryColor(entry.type, isActive(entry.id))"
            :stroke="isActive(entry.id) ? '#1e40af' : 'none'"
            :stroke-width="isActive(entry.id) ? 2 : 0"
            rx="4"
            :class="{
              'transition-all duration-300': true,
              'opacity-100': isActive(entry.id),
              'opacity-60': !isActive(entry.id)
            }"
          />
          
          <!-- Connection line to main timeline -->
          <line
            :x1="50"
            :y1="entry.startY + entry.height / 2"
            :x2="60 + (entry.column * 40)"
            :y2="entry.startY + entry.height / 2"
            stroke="currentColor"
            stroke-width="1"
            stroke-dasharray="2,2"
            class="text-gray-300 dark:text-gray-700 print:text-gray-300"
            :class="{ 'opacity-100': isActive(entry.id), 'opacity-40': !isActive(entry.id) }"
          />
        </g>
      </g>
    </svg>
  </div>
</template>

<style scoped>
.timeline-container {
  position: sticky;
  top: 20px;
  align-self: start;
  padding: 1rem 0;
}

.timeline-svg {
  display: block;
}

/* Hide on smaller screens */
@media (max-width: 1279px) {
  .timeline-container {
    display: none;
  }
}

/* Show in print */
@media print {
  .timeline-container {
    display: block !important;
    position: static;
  }
}
</style>
