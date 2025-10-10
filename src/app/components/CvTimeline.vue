<script setup lang="ts">
import type { TimelineEntry, TimelineItem } from '~/composables/useTimeline'

const { scrollToElementSafely } = useSafeScroll()

interface Props {
  experiences: Array<TimelineItem & { position: string }>
  studies: Array<TimelineItem & { degree: string; endDate: string }>
  projects: Array<TimelineItem & { name: string }>
  otherEntries?: Array<TimelineItem & { title: string }>
  activeIds?: (number | string)[]
}

const props = withDefaults(defineProps<Props>(), {
  activeIds: () => [],
  projects: () => [],
  otherEntries: () => []
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
      label: exp.position,
      icon: exp.icon
    })
  })
  
  props.studies.forEach(study => {
    entries.push({
      id: `study-${study.id}`,
      type: 'study',
      startDate: study.startDate,
      endDate: study.endDate,
      label: study.degree,
      icon: study.icon
    })
  })
  
  props.projects.forEach(project => {
    entries.push({
      id: `project-${project.id}`,
      type: 'project',
      startDate: project.startDate,
      endDate: project.endDate,
      label: project.name,
      icon: project.icon
    })
  })
  
  props.otherEntries.forEach(entry => {
    entries.push({
      id: `other-${entry.id}`,
      type: 'other',
      startDate: entry.startDate,
      endDate: entry.endDate,
      label: entry.title,
      icon: entry.icon
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
function getEntryColor(type: 'experience' | 'study' | 'project' | 'other', active: boolean): string {
  if (active) {
    return type === 'experience' ? '#2563eb' : type === 'study' ? '#059669' : type === 'project' ? '#9333ea' : '#ea580c' // blue-600, green-600, purple-600, or orange-600
  }
  return type === 'experience' ? '#bfdbfe' : type === 'study' ? '#a7f3d0' : type === 'project' ? '#e9d5ff' : '#fed7aa' // blue-200, green-200, purple-200, or orange-200
}

// Get stroke color for entry type
function getStrokeColor(type: 'experience' | 'study' | 'project' | 'other'): string {
  return type === 'experience' ? '#1e40af' : type === 'study' ? '#047857' : type === 'project' ? '#7e22ce' : '#c2410c' // blue-800, green-700, purple-700, or orange-700
}

// Get icon SVG path for entry
function getIconSVG(entry: TimelineEntry & { column: number; startY: number; height: number }): string {
  // If custom icon is provided, use it; otherwise fall back to type-based defaults
  const iconType = entry.icon || entry.type
  
  switch (iconType) {
    case 'experience':
    case 'briefcase':
      // Briefcase icon
      return '<rect x="2" y="7" width="20" height="14" rx="2" ry="2"></rect><path d="M16 21V5a2 2 0 0 0-2-2h-4a2 2 0 0 0-2 2v16"></path>'
    
    case 'study':
    case 'graduation-cap':
      // Graduation cap icon
      return '<path d="M22 10v6M2 10l10-5 10 5-10 5z"></path><path d="M6 12v5c3 3 9 3 12 0v-5"></path>'
    
    case 'project':
    case 'code':
      // Code icon
      return '<polyline points="16 18 22 12 16 6"></polyline><polyline points="8 6 2 12 8 18"></polyline>'
    
    case 'shield':
    case 'military':
      // Shield icon for military service
      return '<path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"></path>'
    
    case 'helmet':
    case 'army-helmet':
      // Army helmet icon
      return '<path d="M12 2C6.48 2 2 6.48 2 12c0 2.21.72 4.25 1.94 5.9L12 22l8.06-4.1C21.28 16.25 22 14.21 22 12c0-5.52-4.48-10-10-10z"></path><ellipse cx="12" cy="12" rx="8" ry="4"></ellipse>'
    
    case 'award':
    case 'abitur':
      // Award/medal icon for Abitur
      return '<circle cx="12" cy="8" r="7"></circle><polyline points="8.21 13.89 7 23 12 20 17 23 15.79 13.88"></polyline>'
    
    case 'school':
    case 'book-open':
      // Book icon for school
      return '<path d="M2 3h6a4 4 0 0 1 4 4v14a3 3 0 0 0-3-3H2z"></path><path d="M22 3h-6a4 4 0 0 0-4 4v14a3 3 0 0 1 3-3h7z"></path>'
    
    case 'internship':
    case 'users':
      // Users icon for internships
      return '<path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"></path><circle cx="9" cy="7" r="4"></circle><path d="M23 21v-2a4 4 0 0 0-3-3.87"></path><path d="M16 3.13a4 4 0 0 1 0 7.75"></path>'
    
    case 'hammer':
    case 'construction':
      // Hammer icon for construction internships
      return '<path d="M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.77-3.77a6 6 0 0 1-7.94 7.94l-6.91 6.91a2.12 2.12 0 0 1-3-3l6.91-6.91a6 6 0 0 1 7.94-7.94l-3.76 3.76z"></path>'
    
    case 'briefcase-business':
    case 'self-employed':
    case 'business':
      // Briefcase with star icon for self-employed/business
      return '<rect x="2" y="7" width="20" height="14" rx="2" ry="2"></rect><path d="M16 21V5a2 2 0 0 0-2-2h-4a2 2 0 0 0-2 2v16"></path><path d="M12 11l1.5 3 3 .5-2.5 2 .5 3-2.5-1.5L9.5 19.5l.5-3-2.5-2 3-.5z"></path>'
    
    case 'lightbulb':
    case 'entrepreneur':
      // Lightbulb icon for entrepreneurship/innovation
      return '<path d="M9 18h6"></path><path d="M10 22h4"></path><path d="M15.09 14c.18-.98.65-1.74 1.41-2.5A4.65 4.65 0 0 0 18 8 6 6 0 0 0 6 8c0 1 .23 2.23 1.5 3.5A4.61 4.61 0 0 1 8.91 14"></path>'
    
    default:
      // Default to code icon
      return '<polyline points="16 18 22 12 16 6"></polyline><polyline points="8 6 2 12 8 18"></polyline>'
  }
}

// Handle click on timeline entry to scroll to corresponding section
function handleEntryClick(entryId: number | string) {
  const elementId = entryId.toString()
    .replace('exp-', 'experience-')
    .replace('study-', 'study-')
    .replace('project-', 'project-')
    .replace('other-', 'other-')
  const element = document.getElementById(elementId)
  if (element) {
    scrollToElementSafely(elementId, 'smooth')
  }
}

// Handle hover on timeline entry
const emit = defineEmits<{
  'entry-hover': [id: number | string]
  'entry-leave': []
  'entry-click': [id: number | string]
}>()

function handleTimelineHover(entryId: number | string) {
  emit('entry-hover', entryId)
}

function handleTimelineLeave() {
  emit('entry-leave')
}

function handleTimelineClick(entryId: number | string) {
  // Scroll to the corresponding section
  const elementId = entryId.toString().replace('exp-', 'experience-').replace('study-', 'study-')
  const element = document.getElementById(elementId)
  if (element) {
    // Update URL hash
    const hash = `#${elementId}`
    if (typeof window !== 'undefined') {
      window.history.pushState(null, '', hash)
    }
    scrollToElementSafely(elementId, 'smooth')
  }
  // Emit the click event
  emit('entry-click', entryId)
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
          @click="handleTimelineClick(entry.id)"
          @mouseenter="handleTimelineHover(entry.id)"
          @mouseleave="handleTimelineLeave"
          class="cursor-pointer"
        >
          <!-- Entry bar -->
          <rect
            :x="ENTRY_START_X + (entry.column * ENTRY_COLUMN_WIDTH)"
            :y="entry.startY"
            :width="ENTRY_WIDTH"
            :height="entry.height"
            :fill="getEntryColor(entry.type, isActive(entry.id))"
            :stroke="isActive(entry.id) ? getStrokeColor(entry.type) : 'none'"
            :stroke-width="isActive(entry.id) ? 3 : 0"
            rx="6"
            :class="{
              'transition-all duration-300': true,
              'opacity-100': isActive(entry.id),
              'opacity-50': !isActive(entry.id)
            }"
          />
          
          <!-- Icon - dynamic based on entry type or custom icon -->
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
            v-html="getIconSVG(entry)"
          >
          </svg>
          
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
