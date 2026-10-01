import { describe, it, expect } from 'vitest'
import {
  DEFAULT_PASSES, basePass, formatMoney, normalizeBillingConfig, passPeriod, priceRows, savingPercent, weeklyPrice
} from '~/utils/pricing'

// Default passes and their weekly prices: docs/REQUIREMENTS_SAAS.md §4.1
describe('weeklyPrice', () => {
  it.each([
    [7, 500, 5],
    [30, 1700, 3.9667],
    [182, 7800, 3],
    [365, 10400, 1.9945]
  ])('%i days for %i cents → %f per week', (days, amount, expected) => {
    expect(weeklyPrice({ days, amount })).toBeCloseTo(expected, 3)
  })
})

describe('savingPercent', () => {
  const base = DEFAULT_PASSES[0]!
  it('compares against the weekly pass', () => {
    expect(DEFAULT_PASSES.map(p => savingPercent(p, base))).toEqual([0, 21, 40, 60])
  })
  it('is 0 for a more expensive pass, another currency or no base', () => {
    expect(savingPercent({ id: 'x', days: 7, amount: 900, currency: 'EUR' }, base)).toBe(0)
    expect(savingPercent({ id: 'x', days: 30, amount: 100, currency: 'USD' }, base)).toBe(0)
    expect(savingPercent(base, undefined)).toBe(0)
  })
})

describe('basePass', () => {
  it('prefers the 7-day pass', () => {
    expect(basePass(DEFAULT_PASSES)?.id).toBe('week')
  })
  it('falls back to the most expensive per week', () => {
    expect(basePass(DEFAULT_PASSES.slice(1))?.id).toBe('month')
  })
})

describe('passPeriod', () => {
  it.each([[7, 'week'], [30, 'month'], [31, 'month'], [182, 'halfYear'], [365, 'year'], [14, null]])('%i → %s', (days, period) => {
    expect(passPeriod(days)).toBe(period)
  })
})

describe('formatMoney', () => {
  it('is locale-aware', () => {
    expect(formatMoney(3.9667, 'EUR', 'en')).toBe('€3.97')
    expect(formatMoney(3.9667, 'EUR', 'de').replace(/\s/g, ' ')).toBe('3,97 €')
  })
  it('drops decimals of whole totals only when asked', () => {
    expect(formatMoney(17, 'EUR', 'en', { whole: true })).toBe('€17')
    expect(formatMoney(17, 'EUR', 'en')).toBe('€17.00')
    expect(formatMoney(17.5, 'EUR', 'en', { whole: true })).toBe('€17.50')
  })
  it('survives an invalid currency', () => {
    expect(formatMoney(5, 'XXXX', 'en')).toBe('5.00 XXXX')
  })
})

describe('priceRows', () => {
  it('shows the default passes as documented', () => {
    const rows = priceRows(DEFAULT_PASSES, 'en')
    expect(rows.map(r => [r.period, r.perWeekText, r.totalText, r.saving])).toEqual([
      ['week', '€5.00', '€5', 0],
      ['month', '€3.97', '€17', 21],
      ['halfYear', '€3.00', '€78', 40],
      ['year', '€1.99', '€104', 60]
    ])
  })
  it('sorts by length', () => {
    const rows = priceRows([...DEFAULT_PASSES].reverse(), 'en')
    expect(rows.map(r => r.days)).toEqual([7, 30, 182, 365])
  })
})

describe('normalizeBillingConfig', () => {
  it('keeps valid passes and the invite limit', () => {
    const config = normalizeBillingConfig({
      passes: [{ id: 'm', days: 30, amount: 1500, currency: 'usd', priceId: 'pri_1' }, { id: 'bad', days: 0, amount: 1, currency: 'EUR' }],
      freeMaxActiveInvites: 5
    })
    expect(config).toEqual({ passes: [{ id: 'm', days: 30, amount: 1500, currency: 'USD' }], freeMaxActiveInvites: 5 })
  })
  it.each([null, undefined, 'x', {}, { passes: [] }, { passes: 'x', freeMaxActiveInvites: -1 }])('falls back to the defaults for %j', (input) => {
    expect(normalizeBillingConfig(input)).toEqual({ passes: [...DEFAULT_PASSES], freeMaxActiveInvites: 3 })
  })
})
