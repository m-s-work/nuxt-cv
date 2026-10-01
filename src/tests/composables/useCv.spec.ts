import { describe, it, expect } from 'vitest'
import { formatPeriod, inlineAssets, withPeriods } from '~/composables/useCv'

describe('formatPeriod', () => {
  it('formats year precision', () => {
    expect(formatPeriod('2017', '2020', 'Present')).toBe('2017 - 2020')
  })

  it('formats month and day precision as MM/YYYY', () => {
    expect(formatPeriod('2020-03', '2021-07-31', 'Present')).toBe('03/2020 - 07/2021')
  })

  it('uses the present label for ongoing entries', () => {
    expect(formatPeriod('2020', null, 'heute')).toBe('2020 - heute')
  })

  it('collapses identical start and end', () => {
    expect(formatPeriod('2011', '2011', 'Present')).toBe('2011')
  })
})

describe('withPeriods', () => {
  it('keeps hand-written periods and fills missing ones', () => {
    const cv = withPeriods({
      experiences: [
        { id: 1, startDate: '2020', endDate: null },
        { id: 2, startDate: '2018-01-01', endDate: '2019-12-31', period: 'Two years' }
      ]
    }, 'Present')

    expect(cv.experiences?.[0]?.period).toBe('2020 - Present')
    expect(cv.experiences?.[1]?.period).toBe('Two years')
    expect(cv.studies).toBeUndefined()
  })
})

describe('inlineAssets', () => {
  it('replaces asset URLs and reuses cached object URLs', async () => {
    const loaded: string[] = []
    const load = async (path: string) => { loaded.push(path); return new Blob(['x']) }
    const urls = new Map<string, string>()
    const first = await inlineAssets({ photo: '/api/assets/a.jpg', other: 'keep' }, load, urls)
    expect(first.photo).toMatch(/^blob:/)
    expect(first.other).toBe('keep')
    const second = await inlineAssets({ photo: '/api/assets/a.jpg' }, load, urls)
    expect(second.photo).toBe(first.photo)
    expect(loaded).toEqual(['assets/a.jpg'])
  })
})
