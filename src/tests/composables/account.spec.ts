import { describe, it, expect } from 'vitest'
import {
  accountErrorText, adminAuthHeaders, completeLink, endOfDayIso, formatMoney, loginErrorKey, passName, passPricing, proChanged,
  upgradeReason, upgradeText, usagePercent
} from '~/utils/account'

const DEFAULT_PASSES = [
  { id: 'week', days: 7, amount: 500, currency: 'EUR' },
  { id: 'month', days: 30, amount: 1700, currency: 'EUR' },
  { id: 'halfyear', days: 182, amount: 7800, currency: 'EUR' },
  { id: 'year', days: 365, amount: 10400, currency: 'EUR' }
]

describe('passPricing', () => {
  const pricing = passPricing(DEFAULT_PASSES)
  const byId = Object.fromEntries(pricing.map(p => [p.id, p]))

  it('computes the price per week in minor units', () => {
    expect(byId.week!.perWeek).toBe(500)
    expect(byId.month!.perWeek).toBeCloseTo(396.67, 2)
    expect(byId.halfyear!.perWeek).toBeCloseTo(300, 5)
    expect(byId.year!.perWeek).toBeCloseTo(199.45, 2)
  })

  it('shows the per-week prices of the requirements table', () => {
    expect(formatMoney(byId.week!.perWeek, 'EUR')).toBe('€5.00')
    expect(formatMoney(byId.month!.perWeek, 'EUR')).toBe('€3.97')
    expect(formatMoney(byId.halfyear!.perWeek, 'EUR')).toBe('€3.00')
    expect(formatMoney(byId.year!.perWeek, 'EUR')).toBe('€1.99')
  })

  it('computes the saving against the weekly pass', () => {
    expect(byId.week!.savingPercent).toBe(0)
    expect(byId.month!.savingPercent).toBe(21)
    expect(byId.halfyear!.savingPercent).toBe(40)
    expect(byId.year!.savingPercent).toBe(60)
  })

  it('highlights only the cheapest per week', () => {
    expect(pricing.filter(p => p.cheapest).map(p => p.id)).toEqual(['year'])
  })

  it('handles a single pass and no weekly pass', () => {
    expect(passPricing([{ id: 'only', days: 30, amount: 1000, currency: 'EUR' }])[0]).toMatchObject({ savingPercent: 0, cheapest: false })
    const noWeek = passPricing([{ id: 'm', days: 30, amount: 1700, currency: 'EUR' }, { id: 'y', days: 365, amount: 10400, currency: 'EUR' }])
    expect(noWeek[0]!.savingPercent).toBe(0)
    expect(noWeek[1]!.savingPercent).toBe(50)
    expect(passPricing([])).toEqual([])
    expect(passPricing([{ id: 'x', days: 0, amount: 1, currency: 'EUR' }])).toEqual([])
  })

  it('formats in the pass currency', () => {
    expect(formatMoney(10400, 'USD')).toBe('$104.00')
    expect(formatMoney(500, 'EUR', 'de')).toMatch(/5,00\s?€/)
  })

  it('names passes', () => {
    expect([7, 14, 30, 182, 365, 90].map(passName)).toEqual(['1 week', '2 weeks', '1 month', '6 months', '1 year', '90 days'])
  })
})

describe('completeLink', () => {
  it('completes relative invite links with the origin', () => {
    expect(completeLink('/cv?c=abc', 'https://cv.example.com')).toBe('https://cv.example.com/cv?c=abc')
    expect(completeLink('/cv?c=abc', 'https://cv.example.com/')).toBe('https://cv.example.com/cv?c=abc')
    expect(completeLink('cv?c=abc', 'http://localhost:3000')).toBe('http://localhost:3000/cv?c=abc')
  })

  it('keeps absolute links and empty values', () => {
    expect(completeLink('https://jane.cv.example.com/?c=abc', 'https://cv.example.com')).toBe('https://jane.cv.example.com/?c=abc')
    expect(completeLink(undefined, 'https://x')).toBe('')
    expect(completeLink('', 'https://x')).toBe('')
  })
})

describe('adminAuthHeaders', () => {
  it('sends only the admin key in key mode', () => {
    expect(adminAuthHeaders('key', 'secret')).toEqual({ 'X-Admin-Key': 'secret' })
  })

  it('never sends the key in session mode, but the CSRF header', () => {
    expect(adminAuthHeaders('session', 'secret')).toEqual({ 'X-Requested-With': 'cv' })
    expect(adminAuthHeaders('session', '')).toEqual({ 'X-Requested-With': 'cv' })
  })

  it('falls back to the stored key, else the session (heatmap iframe)', () => {
    expect(adminAuthHeaders(null, 'secret')).toEqual({ 'X-Admin-Key': 'secret' })
    expect(adminAuthHeaders(undefined, '')).toEqual({ 'X-Requested-With': 'cv' })
  })
})

describe('error codes', () => {
  it('maps login errors to i18n keys', () => {
    expect(loginErrorKey('cancelled')).toBe('login.errors.cancelled')
    expect(loginErrorKey('account_exists')).toBe('login.errors.account_exists')
    expect(loginErrorKey('link_invalid')).toBe('login.errors.link_invalid')
    expect(loginErrorKey('something_new')).toBe('login.errors.provider_failed')
    expect(loginErrorKey(null)).toBeNull()
    expect(loginErrorKey('')).toBeNull()
  })

  it('describes account API errors', () => {
    expect(accountErrorText('handle_taken')).toMatch(/taken/)
    expect(accountErrorText('handle_reserved')).toMatch(/reserved/)
    expect(accountErrorText('invalid_domain')).toMatch(/valid domain/)
    expect(accountErrorText('domain_taken')).toMatch(/already used/)
    expect(accountErrorText('dns_mismatch')).toMatch(/DNS/)
    expect(accountErrorText('unknown_code')).toBe('unknown_code')
    expect(accountErrorText(undefined)).toBe('')
  })

  it('detects plan limits and quota errors', () => {
    const limit = upgradeReason({ statusCode: 402, data: { error: 'plan_limit', feature: 'activeInvites', limit: 3 } })
    expect(limit).toEqual({ feature: 'activeInvites', limit: 3 })
    expect(upgradeText(limit!)).toBe('Free plan: up to 3 active invites. Revoke one or get Pro.')
    const quota = upgradeReason({ statusCode: 413, data: { error: 'quota_exceeded', quota: 20 * 1024 * 1024 } })
    expect(quota).toEqual({ feature: 'storage', quota: 20 * 1024 * 1024 })
    expect(upgradeText(quota!)).toMatch(/20 MB/)
    expect(upgradeText(upgradeReason({ statusCode: 402, data: { error: 'plan_limit', feature: 'heatmaps' } })!)).toMatch(/Heatmaps/)
    expect(upgradeReason({ statusCode: 413 })).toBeNull()
    expect(upgradeReason({ statusCode: 400, data: { error: 'invalid_code' } })).toBeNull()
    expect(upgradeReason(new Error('x'))).toBeNull()
  })
})

describe('misc', () => {
  it('computes usage percentages', () => {
    expect(usagePercent(2, 3)).toBe(67)
    expect(usagePercent(5, 3)).toBe(100)
    expect(usagePercent(5, null)).toBe(0)
  })

  it('detects a changed Pro end', () => {
    expect(proChanged({ proUntil: null }, { proUntil: '2026-11-01T00:00:00Z' })).toBe(true)
    expect(proChanged({ proUntil: '2026-11-01T00:00:00Z' }, { proUntil: '2026-11-01T00:00:00Z' })).toBe(false)
    expect(proChanged({ proUntil: undefined }, { proUntil: null })).toBe(false)
    expect(proChanged({ proForever: false }, { proForever: true })).toBe(true)
  })

  it('turns a date input into the end of that day', () => {
    expect(endOfDayIso('2026-12-31')).toBe('2026-12-31T23:59:59Z')
    expect(endOfDayIso('')).toBeNull()
  })
})
