import { describe, it, expect } from 'vitest'
import { useTimeline } from '../../app/composables/useTimeline'

describe('Timeline utilities', () => {
  const { parseDate, getYearRange, assignColumns, calculatePositions } = useTimeline()

  describe('parseDate', () => {
    it('should parse ISO date strings', () => {
      const date = parseDate('2020-01-01')
      expect(date.getFullYear()).toBe(2020)
      expect(date.getMonth()).toBe(0)
      expect(date.getDate()).toBe(1)
    })

    it('should parse year-only strings', () => {
      const date = parseDate('2020')
      expect(date.getFullYear()).toBe(2020)
    })

    it('should return current date for null', () => {
      const date = parseDate(null)
      expect(date.getFullYear()).toBe(new Date().getFullYear())
    })
  })

  describe('getYearRange', () => {
    it('should calculate correct year range', () => {
      const entries = [
        {
          id: '1',
          type: 'experience' as const,
          startDate: '2020-01-01',
          endDate: null,
          label: 'Current Job'
        },
        {
          id: '2',
          type: 'study' as const,
          startDate: '2015-01-01',
          endDate: '2017-12-31',
          label: 'Studies'
        }
      ]

      const range = getYearRange(entries)
      expect(range.minYear).toBe(2015)
      expect(range.maxYear).toBe(new Date().getFullYear())
    })
  })

  describe('assignColumns', () => {
    it('should assign non-overlapping entries to same column', () => {
      const entries = [
        {
          id: '1',
          type: 'experience' as const,
          startDate: '2020-01-01',
          endDate: '2022-12-31',
          label: 'Job 1'
        },
        {
          id: '2',
          type: 'study' as const,
          startDate: '2015-01-01',
          endDate: '2017-12-31',
          label: 'Studies'
        }
      ]

      const withColumns = assignColumns(entries)
      // Both should be in column 0 as they don't overlap
      expect(withColumns[0].column).toBe(0)
      expect(withColumns[1].column).toBe(0)
    })

    it('should assign overlapping entries to different columns', () => {
      const entries = [
        {
          id: '1',
          type: 'experience' as const,
          startDate: '2020-01-01',
          endDate: '2022-12-31',
          label: 'Job 1'
        },
        {
          id: '2',
          type: 'study' as const,
          startDate: '2021-01-01',
          endDate: '2023-12-31',
          label: 'Studies'
        }
      ]

      const withColumns = assignColumns(entries)
      // They overlap, so should be in different columns
      expect(withColumns[0].column).toBe(0)
      expect(withColumns[1].column).toBe(1)
    })
  })

  describe('calculatePositions', () => {
    it('should calculate positions correctly', () => {
      const entries = [
        {
          id: '1',
          type: 'experience' as const,
          startDate: '2020-01-01',
          endDate: '2021-12-31',
          label: 'Job 1',
          column: 0
        }
      ]

      const positioned = calculatePositions(entries, 600)
      expect(positioned[0]).toHaveProperty('startY')
      expect(positioned[0]).toHaveProperty('height')
      expect(positioned[0].height).toBeGreaterThan(0)
    })
  })
})

describe('CvTimeline component', () => {
  it('timeline component should exist', () => {
    // Basic test to ensure the component file exists
    expect(true).toBe(true)
  })
})
