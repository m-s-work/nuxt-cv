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

// Width calculation based on column count
const timelineWidth = computed(() => {
  return BASE_WIDTH + (timelineData.value.columnCount * ENTRY_COLUMN_WIDTH)
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

// Handle click on timeline entry to scroll to corresponding section
function handleEntryClick(entryId: number | string) {
  const elementId = entryId.toString().replace('exp-', 'experience-').replace('study-', 'study-')
  const element = document.getElementById(elementId)
  if (element) {
    element.scrollIntoView({ behavior: 'smooth', block: 'center' })
  }
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
          :x1="TIMELINE_AXIS_X"
          :y1="TOP_PADDING"
          :x2="TIMELINE_AXIS_X"
          :y2="timelineData.timelineHeight + TOP_PADDING"
          stroke="currentColor"
          stroke-width="2"
          class="text-gray-300 dark:text-gray-600 print:text-gray-400"
        />
        
        <g v-for="marker in yearMarkers" :key="marker.year">
          <circle
            :cx="TIMELINE_AXIS_X"
            :cy="marker.y"
            r="5"
            fill="currentColor"
            class="text-gray-400 dark:text-gray-500 print:text-gray-400"
          />
          <text
            :x="YEAR_LABEL_X"
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
        <g 
          v-for="entry in timelineData.entries" 
          :key="entry.id"
          @click="handleEntryClick(entry.id)"
          class="cursor-pointer"
        >
          <!-- Entry bar -->
          <rect
            :x="ENTRY_START_X + (entry.column * ENTRY_COLUMN_WIDTH)"
            :y="entry.startY"
            :width="ENTRY_WIDTH"
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
          
          <!-- Icon (briefcase for experience, graduation cap for study) -->
          <g v-if="entry.type === 'experience'">
            <!-- Briefcase icon -->
            <svg
              :x="ENTRY_START_X + (entry.column * ENTRY_COLUMN_WIDTH) + 7"
              :y="entry.startY + entry.height / 2 - 10"
              width="20"
              height="20"
              viewBox="0 0 24 24"
              fill="none"
              stroke="white"
              stroke-width="2"
              stroke-linecap="round"
              stroke-linejoin="round"
            >
              <rect x="2" y="7" width="20" height="14" rx="2" ry="2"></rect>
              <path d="M16 21V5a2 2 0 0 0-2-2h-4a2 2 0 0 0-2 2v16"></path>
            </svg>
          </g>
          
          <g v-else>
            <!-- Graduation cap icon -->
            <svg
              :x="ENTRY_START_X + (entry.column * ENTRY_COLUMN_WIDTH) + 7"
              :y="entry.startY + entry.height / 2 - 10"
              width="20"
              height="20"
              viewBox="0 0 24 24"
              fill="none"
              stroke="white"
              stroke-width="2"
              stroke-linecap="round"
              stroke-linejoin="round"
            >
              <path d="M22 10v6M2 10l10-5 10 5-10 5z"></path>
              <path d="M6 12v5c3 3 9 3 12 0v-5"></path>
            </svg>
          </g>
          
          <!-- Connection line to main timeline -->
          <line
            :x1="TIMELINE_AXIS_X"
            :y1="entry.startY + entry.height / 2"
            :x2="ENTRY_START_X + (entry.column * ENTRY_COLUMN_WIDTH)"
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
  
  .timeline-svg {
    max-width: 150px;
  }
}
</style>
