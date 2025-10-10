export interface TimelineEntry {
  id: number | string
  type: 'experience' | 'study' | 'project' | 'other'
  startDate: string
  endDate: string | null
  label: string
  icon?: string
}

export interface TimelineColumn {
  entries: Array<TimelineEntry & { column: number; startY: number; height: number }>
}

/**
 * Base interface for timeline items
 * All items that appear on the timeline should extend this interface
 */
export interface TimelineItem {
  id: number
  startDate: string
  endDate: string | null
  icon?: string
}

export function useTimeline() {
  /**
   * Parse a date string (year or ISO date) to a Date object
   */
  function parseDate(dateStr: string | null): Date {
    if (!dateStr) return new Date() // Current date for null (ongoing)
    
    // If it's just a year, use January 1st
    if (/^\d{4}$/.test(dateStr)) {
      return new Date(`${dateStr}-01-01`)
    }
    
    return new Date(dateStr)
  }

  /**
   * Calculate the minimum and maximum years from entries
   */
  function getYearRange(entries: TimelineEntry[]): { minYear: number; maxYear: number } {
    if (entries.length === 0) return { minYear: new Date().getFullYear(), maxYear: new Date().getFullYear() }
    
    let minYear = Infinity
    let maxYear = -Infinity
    
    entries.forEach(entry => {
      const startYear = parseDate(entry.startDate).getFullYear()
      const endYear = parseDate(entry.endDate).getFullYear()
      
      minYear = Math.min(minYear, startYear)
      maxYear = Math.max(maxYear, endYear)
    })
    
    return { minYear, maxYear }
  }

  /**
   * Check if two time periods overlap
   */
  function periodsOverlap(
    start1: Date,
    end1: Date,
    start2: Date,
    end2: Date
  ): boolean {
    return start1 < end2 && start2 < end1
  }

  /**
   * Assign entries to columns to handle overlapping periods
   */
  function assignColumns(entries: TimelineEntry[]): Array<TimelineEntry & { column: number }> {
    const columns: Array<Array<TimelineEntry>> = []
    
    // Sort entries by start date
    const sortedEntries = [...entries].sort((a, b) => {
      return parseDate(a.startDate).getTime() - parseDate(b.startDate).getTime()
    })
    
    sortedEntries.forEach(entry => {
      const entryStart = parseDate(entry.startDate)
      const entryEnd = parseDate(entry.endDate)
      
      // Find the first column where this entry doesn't overlap with any existing entry
      let columnIndex = 0
      while (columnIndex < columns.length) {
        const hasOverlap = columns[columnIndex].some(existingEntry => {
          const existingStart = parseDate(existingEntry.startDate)
          const existingEnd = parseDate(existingEntry.endDate)
          return periodsOverlap(entryStart, entryEnd, existingStart, existingEnd)
        })
        
        if (!hasOverlap) break
        columnIndex++
      }
      
      // Create new column if needed
      if (columnIndex === columns.length) {
        columns.push([])
      }
      
      columns[columnIndex].push(entry)
    })
    
    // Flatten with column information
    return sortedEntries.map(entry => {
      const columnIndex = columns.findIndex(col => col.includes(entry))
      return { ...entry, column: columnIndex }
    })
  }

  /**
   * Calculate Y positions and heights for timeline entries (newest on top)
   */
  function calculatePositions(
    entries: Array<TimelineEntry & { column: number }>,
    timelineHeight: number,
    topPadding: number = 20,
    entryMargin: number = 4
  ): Array<TimelineEntry & { column: number; startY: number; height: number }> {
    const { minYear, maxYear } = getYearRange(entries)
    const yearRange = maxYear - minYear || 1
    
    return entries.map(entry => {
      const startYear = parseDate(entry.startDate).getFullYear()
      const endYear = parseDate(entry.endDate).getFullYear()
      const durationYears = endYear - startYear
      
      // Calculate start position as percentage of total range (reversed - newest on top)
      const startY = ((maxYear - endYear) / yearRange) * timelineHeight + topPadding + entryMargin
      
      // Calculate height based on duration (subtract margin for spacing)
      const height = (durationYears / yearRange) * timelineHeight - (entryMargin * 2)
      
      return {
        ...entry,
        startY: Math.round(startY),
        height: Math.max(Math.round(height), 20) // Minimum height of 20px
      }
    })
  }

  return {
    parseDate,
    getYearRange,
    assignColumns,
    calculatePositions
  }
}
