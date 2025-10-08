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
  if (entries.length === 0) return { entries: [], yearRange: { minYear: 0, maxYear: 0 }, columnCount: 0, timelineHeight: 0 }
  
  const yearRange = getYearRange(entries)
  const withColumns = assignColumns(entries)
  const columnCount = Math.max(...withColumns.map(e => e.column)) + 1
  const timelineHeight = 600 // Base height, adjust based on year range
  const positioned = calculatePositions(withColumns, timelineHeight, TOP_PADDING)
  
  return {
    entries: positioned,
    yearRange,
    columnCount,
    timelineHeight
  }
})

// Calculate year markers (newest year on top, descending)
const yearMarkers = computed(() => {
  const { minYear, maxYear } = timelineData.value.yearRange
  const years: Array<{ year: number; y: number }> = []
  const yearRange = maxYear - minYear || 1
  
  for (let year = maxYear; year >= minYear; year--) {
    const y = ((maxYear - year) / yearRange) * timelineData.value.timelineHeight + TOP_PADDING
    years.push({ year, y })
  }
  
  return years
})

// Timeline dimensions and positioning constants
const TIMELINE_AXIS_X = 70 // X position of main timeline axis
const YEAR_LABEL_X = 10 // X position for year labels
const ENTRY_COLUMN_WIDTH = 45 // Width allocated per column for entries
const ENTRY_OFFSET = 15 // Gap between timeline axis and first entry
const ENTRY_START_X = TIMELINE_AXIS_X + ENTRY_OFFSET // X position where entries start
const ENTRY_WIDTH = 35 // Width of each entry bar
const BASE_WIDTH = 80 // Minimum width for year labels and axis
const TOP_PADDING = 20 // Padding at top to prevent year label cutoff
const BOTTOM_PADDING = 20 // Padding at bottom for visual balance

// Print-specific slimmer dimensions
const PRINT_TIMELINE_AXIS_X = 40 // Reduced X position for print
const PRINT_YEAR_LABEL_X = 5 // Reduced X position for year labels in print
const PRINT_ENTRY_COLUMN_WIDTH = 25 // Reduced width per column for print
const PRINT_ENTRY_OFFSET = 8 // Reduced gap for print
const PRINT_ENTRY_START_X = PRINT_TIMELINE_AXIS_X + PRINT_ENTRY_OFFSET
const PRINT_ENTRY_WIDTH = 20 // Reduced entry bar width for print
const PRINT_BASE_WIDTH = 45 // Reduced minimum width for print

// Detect if we're in print mode using media query
const isPrintMode = ref(false)

// Setup print mode detection after component is mounted
onMounted(() => {
  if (typeof window !== 'undefined') {
    const printMediaQuery = window.matchMedia('print')
    isPrintMode.value = printMediaQuery.matches
    
    const handlePrintChange = (e: MediaQueryListEvent) => {
      isPrintMode.value = e.matches
    }
    
    printMediaQuery.addEventListener('change', handlePrintChange)
    
    // Cleanup on unmount
    onBeforeUnmount(() => {
      printMediaQuery.removeEventListener('change', handlePrintChange)
    })
  }
})

// Width calculation based on column count
const timelineWidth = computed(() => {
  if (isPrintMode.value) {
    return PRINT_BASE_WIDTH + (timelineData.value.columnCount * PRINT_ENTRY_COLUMN_WIDTH)
  }
  return BASE_WIDTH + (timelineData.value.columnCount * ENTRY_COLUMN_WIDTH)
})

// Get dimension values based on print mode
const dimensionValues = computed(() => {
  if (isPrintMode.value) {
    return {
      axisX: PRINT_TIMELINE_AXIS_X,
      yearLabelX: PRINT_YEAR_LABEL_X,
      entryStartX: PRINT_ENTRY_START_X,
      entryColumnWidth: PRINT_ENTRY_COLUMN_WIDTH,
      entryWidth: PRINT_ENTRY_WIDTH
    }
  }
  return {
    axisX: TIMELINE_AXIS_X,
    yearLabelX: YEAR_LABEL_X,
    entryStartX: ENTRY_START_X,
    entryColumnWidth: ENTRY_COLUMN_WIDTH,
    entryWidth: ENTRY_WIDTH
  }
})

// Check if entry is active
function isActive(entryId: number | string): boolean {
  return props.activeIds.includes(entryId)
}

// Get color for entry type
function getEntryColor(type: 'experience' | 'study', active: boolean): string {
  if (active) {
    return type === 'experience' ? '#2563eb' : '#059669' // blue-600 or green-600
  }
  return type === 'experience' ? '#bfdbfe' : '#a7f3d0' // blue-200 or green-200
}
</script>

<template>
  <div class="timeline-container hidden xl:block print:block">
    <svg 
      :width="timelineWidth" 
      :height="timelineData.timelineHeight + TOP_PADDING + BOTTOM_PADDING"
      class="timeline-svg"
      xmlns="http://www.w3.org/2000/svg"
    >
      <!-- Year markers and labels -->
      <g class="year-markers">
        <line
          :x1="dimensionValues.axisX"
          :y1="TOP_PADDING"
          :x2="dimensionValues.axisX"
          :y2="timelineData.timelineHeight + TOP_PADDING"
          stroke="currentColor"
          stroke-width="2"
          class="text-gray-300 dark:text-gray-600 print:text-gray-400"
        />
        
        <g v-for="marker in yearMarkers" :key="marker.year">
          <circle
            :cx="dimensionValues.axisX"
            :cy="marker.y"
            r="5"
            fill="currentColor"
            class="text-gray-400 dark:text-gray-500 print:text-gray-400"
          />
          <text
            :x="dimensionValues.yearLabelX"
            :y="marker.y + 5"
            class="text-sm fill-gray-700 dark:fill-gray-300 print:fill-gray-700 font-medium"
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
            :x="dimensionValues.entryStartX + (entry.column * dimensionValues.entryColumnWidth)"
            :y="entry.startY"
            :width="dimensionValues.entryWidth"
            :height="entry.height"
            :fill="getEntryColor(entry.type, isActive(entry.id))"
            :stroke="isActive(entry.id) ? '#1e40af' : 'none'"
            :stroke-width="isActive(entry.id) ? 3 : 0"
            rx="6"
            :class="{
              'transition-all duration-300': true,
              'opacity-100': isActive(entry.id),
              'opacity-50': !isActive(entry.id)
            }"
          />
          
          <!-- Connection line to main timeline -->
          <line
            :x1="dimensionValues.axisX"
            :y1="entry.startY + entry.height / 2"
            :x2="dimensionValues.entryStartX + (entry.column * dimensionValues.entryColumnWidth)"
            :y2="entry.startY + entry.height / 2"
            stroke="currentColor"
            stroke-width="1"
            stroke-dasharray="3,3"
            class="text-gray-300 dark:text-gray-600 print:text-gray-300"
            :class="{ 'opacity-100': isActive(entry.id), 'opacity-30': !isActive(entry.id) }"
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
  margin-right: 1rem;
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
    page-break-inside: avoid;
    margin-right: 0.5rem;
  }
}
</style>
