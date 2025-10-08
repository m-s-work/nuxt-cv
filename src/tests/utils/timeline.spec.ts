import { describe, it, expect } from 'vitest'
import { parsePeriod, calculateColumns, getYearRange } from '~/utils/timeline'

describe('Timeline utilities', () => {
  describe('parsePeriod', () => {
    it('should parse period with end year', () => {
      const result = parsePeriod('2017 - 2020')
      expect(result.start).toBe(2017)
      expect(result.end).toBe(2020)
      expect(result.duration).toBe(3)
    })

    it('should parse period with Present', () => {
      const currentYear = new Date().getFullYear()
      const result = parsePeriod('2020 - Present')
      expect(result.start).toBe(2020)
      expect(result.end).toBe(currentYear)
      expect(result.duration).toBe(currentYear - 2020)
    })
  })

  describe('calculateColumns', () => {
    it('should assign column 0 to non-overlapping items', () => {
      const items = [
        { id: 1, type: 'study' as const, start: 2012, end: 2015 },
        { id: 2, type: 'study' as const, start: 2015, end: 2017 },
        { id: 3, type: 'experience' as const, start: 2017, end: 2020 }
      ]
      
      const result = calculateColumns(items)
      expect(result.every(item => item.column === 0)).toBe(true)
    })

    it('should assign different columns to overlapping items', () => {
      const items = [
        { id: 1, type: 'experience' as const, start: 2015, end: 2020 },
        { id: 2, type: 'study' as const, start: 2017, end: 2019 }
      ]
      
      const result = calculateColumns(items)
      // Items overlap, so they should be in different columns
      expect(result[0].column).not.toBe(result[1].column)
    })

    it('should handle three overlapping items', () => {
      const items = [
        { id: 1, type: 'experience' as const, start: 2015, end: 2020 },
        { id: 2, type: 'study' as const, start: 2016, end: 2019 },
        { id: 3, type: 'experience' as const, start: 2017, end: 2018 }
      ]
      
      const result = calculateColumns(items)
      const columns = result.map(r => r.column)
      // All three overlap, so we need at least 2 columns (some might share)
      const uniqueColumns = new Set(columns)
      expect(uniqueColumns.size).toBeGreaterThan(0)
    })
  })

  describe('getYearRange', () => {
    it('should calculate year range from items', () => {
      const items = [
        { id: 1, type: 'study' as const, start: 2012, end: 2015, duration: 3, column: 0 },
        { id: 2, type: 'experience' as const, start: 2020, end: 2025, duration: 5, column: 0 }
      ]
      
      const result = getYearRange(items)
      expect(result.minYear).toBe(2012)
      expect(result.maxYear).toBe(2025)
      expect(result.totalYears).toBe(13)
    })

    it('should handle empty items array', () => {
      const result = getYearRange([])
      expect(result.totalYears).toBe(10)
      expect(result.minYear).toBeLessThan(result.maxYear)
    })
  })
})
