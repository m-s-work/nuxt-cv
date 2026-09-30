import { describe, it, expect } from 'vitest'
import { cvEditorSchema, moveItem, newListItem, setField, toLines, unknownKeys } from '~/utils/cvEditorSchema'

const field = (key: string) => cvEditorSchema.find(f => f.key === key)!

describe('cv editor schema', () => {
  it('describes every block of the CV data', () => {
    expect(cvEditorSchema.map(f => f.key)).toEqual([
      'profile', 'details', 'intro', 'skills', 'preferredTechs', 'languages', 'drivingLicenses',
      'experiences', 'studies', 'projects', 'otherEntries'
    ])
  })

  it('titles list items from their content', () => {
    expect(field('experiences').itemTitle!({ position: 'Architect', company: 'ACME', startDate: '2020-01-01', endDate: null }))
      .toBe('Architect · ACME · 2020–today')
    expect(field('languages').itemTitle!({ name: 'German', level: 'Native' })).toBe('German · Native')
  })
})

describe('newListItem', () => {
  it('gives dated entries the next free id and an ongoing period', () => {
    const item = newListItem(field('experiences'), [{ id: 3 }, { id: 7 }])
    expect(item.id).toBe(8)
    expect(item.endDate).toBeNull()
    expect(item.position).toBe('')
  })

  it('creates plain items without id', () => {
    expect(newListItem(field('languages'), [])).toEqual({ name: '', level: '', code: '' })
  })
})

describe('setField', () => {
  it('removes cleared optional keys', () => {
    const target: Record<string, unknown> = { a: 'x', b: ['y'], c: 1 }
    setField(target, 'a', '')
    setField(target, 'b', [])
    setField(target, 'c', null)
    expect(target).toEqual({})
  })

  it('keeps an empty end date as null (ongoing)', () => {
    const target: Record<string, unknown> = { endDate: '2020' }
    setField(target, 'endDate', '')
    expect(target).toEqual({ endDate: null })
  })
})

describe('helpers', () => {
  it('moves items and ignores moves past the ends', () => {
    const items = ['a', 'b', 'c']
    moveItem(items, 0, 1)
    expect(items).toEqual(['b', 'a', 'c'])
    moveItem(items, 2, 1)
    expect(items).toEqual(['b', 'a', 'c'])
  })

  it('reports keys the schema does not know', () => {
    expect(unknownKeys({ name: 'x', custom: 1, id: 2 }, field('languages').fields!, ['id'])).toEqual(['custom'])
  })

  it('splits lines', () => {
    expect(toLines(' a \n\n b\n')).toEqual(['a', 'b'])
  })
})
