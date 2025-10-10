import { describe, it, expect } from 'vitest'
import { useNameFormatter } from '~/composables/useNameFormatter'

describe('useNameFormatter', () => {
  const { formatFullName } = useNameFormatter()

  it('should format name with prepended titles only', () => {
    const result = formatFullName('Max Mustermann', ['Dr.'], undefined)
    expect(result).toBe('Dr. Max Mustermann')
  })

  it('should format name with appended titles only', () => {
    const result = formatFullName('Max Mustermann', undefined, ['PhD', 'MSc'])
    expect(result).toBe('Max Mustermann, PhD, MSc')
  })

  it('should format name with both prepended and appended titles', () => {
    const result = formatFullName('Max Mustermann', ['Dr.'], ['PhD', 'MSc'])
    expect(result).toBe('Dr. Max Mustermann, PhD, MSc')
  })

  it('should format name with multiple prepended titles', () => {
    const result = formatFullName('Max Mustermann', ['Dr.', 'Prof.'], ['PhD'])
    expect(result).toBe('Dr. Prof. Max Mustermann, PhD')
  })

  it('should return just the name when no titles are provided', () => {
    const result = formatFullName('Max Mustermann', undefined, undefined)
    expect(result).toBe('Max Mustermann')
  })

  it('should handle empty arrays', () => {
    const result = formatFullName('Max Mustermann', [], [])
    expect(result).toBe('Max Mustermann')
  })

  it('should format name with single appended title', () => {
    const result = formatFullName('Max Mustermann', undefined, ['PhD'])
    expect(result).toBe('Max Mustermann, PhD')
  })

  it('should format name with three appended titles', () => {
    const result = formatFullName('Max Mustermann', undefined, ['PhD', 'MSc', 'MBA'])
    expect(result).toBe('Max Mustermann, PhD, MSc, MBA')
  })
})
