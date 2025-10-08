export interface TimelineItem {
  id: number
  type: 'experience' | 'study'
  start: number
  end: number
  duration: number
  column: number
}

export interface ParsedPeriod {
  start: number
  end: number
  duration: number
}

/**
 * Parse a period string like "2020 - Present" or "2017 - 2020"
 */
export function parsePeriod(period: string): ParsedPeriod {
  const parts = period.split(' - ')
  const start = parseInt(parts[0].trim())
  const end = parts[1].trim() === 'Present' ? new Date().getFullYear() : parseInt(parts[1].trim())
  
  return {
    start,
    end,
    duration: end - start
  }
}

/**
 * Calculate column assignments for overlapping timeline items
 * Items that overlap need to be in different columns
 */
export function calculateColumns(items: Array<{ id: number; type: 'experience' | 'study'; start: number; end: number }>): TimelineItem[] {
  // Sort by start year, then by duration (longer first)
  const sorted = [...items].sort((a, b) => {
    if (a.start !== b.start) return a.start - b.start
    return (b.end - b.start) - (a.end - a.start)
  })

  const result: TimelineItem[] = []
  const columns: Array<{ end: number }> = []

  for (const item of sorted) {
    // Find the first column where this item can fit
    let column = 0
    while (column < columns.length && columns[column].end > item.start) {
      column++
    }

    // Add the item to this column
    if (column >= columns.length) {
      columns.push({ end: item.end })
    } else {
      columns[column].end = item.end
    }

    result.push({
      ...item,
      duration: item.end - item.start,
      column
    })
  }

  return result
}

/**
 * Get the year range for the timeline
 */
export function getYearRange(items: TimelineItem[]): { minYear: number; maxYear: number; totalYears: number } {
  if (items.length === 0) {
    const currentYear = new Date().getFullYear()
    return { minYear: currentYear - 10, maxYear: currentYear, totalYears: 10 }
  }

  const minYear = Math.min(...items.map(i => i.start))
  const maxYear = Math.max(...items.map(i => i.end))
  
  return {
    minYear,
    maxYear,
    totalYears: maxYear - minYear
  }
}
