// Price math for the public pricing (docs/REQUIREMENTS_SAAS.md §4.1). Pure functions, no Vue.
// Pro is sold as prepaid passes; the UI shows the price per week large and the total + saving small.

export interface BillingPass {
  id: string
  days: number
  /** Total price in minor units (cents). */
  amount: number
  currency: string
}

export interface BillingConfig {
  passes: BillingPass[]
  freeMaxActiveInvites: number
}

/** Shown when GET /api/billing/config fails (same as the API defaults). */
export const DEFAULT_PASSES: readonly BillingPass[] = Object.freeze([
  { id: 'week', days: 7, amount: 500, currency: 'EUR' },
  { id: 'month', days: 30, amount: 1700, currency: 'EUR' },
  { id: 'halfyear', days: 182, amount: 7800, currency: 'EUR' },
  { id: 'year', days: 365, amount: 10400, currency: 'EUR' }
])

export const DEFAULT_FREE_MAX_ACTIVE_INVITES = 3

export type PassPeriod = 'week' | 'month' | 'halfYear' | 'year'

/** Price per week in major units (e.g. 3.9667 for €17 / 30 days). */
export function weeklyPrice(pass: Pick<BillingPass, 'amount' | 'days'>): number {
  return pass.amount / 100 / (pass.days / 7)
}

/** The pass the savings are measured against: the 7-day pass, else the one with the highest weekly price. */
export function basePass(passes: readonly BillingPass[]): BillingPass | undefined {
  return passes.find(p => p.days === 7)
    ?? [...passes].sort((a, b) => weeklyPrice(b) - weeklyPrice(a))[0]
}

/** Saving in whole percent against the base pass' weekly price; 0 if it is not cheaper (or another currency). */
export function savingPercent(pass: BillingPass, base: BillingPass | undefined): number {
  if (!base || base.currency !== pass.currency) return 0
  const baseWeekly = weeklyPrice(base)
  if (baseWeekly <= 0) return 0
  const saving = Math.round((1 - weeklyPrice(pass) / baseWeekly) * 100)
  return saving > 0 ? saving : 0
}

/** Label of a pass by its length; null for unusual lengths (the UI then shows "N days"). */
export function passPeriod(days: number): PassPeriod | null {
  if (days === 7) return 'week'
  if (days >= 28 && days <= 31) return 'month'
  if (days >= 180 && days <= 184) return 'halfYear'
  if (days >= 364 && days <= 366) return 'year'
  return null
}

/** Formats an amount in major units, locale-aware. Whole totals are shown without decimals (€17, not €17.00). */
export function formatMoney(value: number, currency: string, locale: string, opts: { whole?: boolean } = {}): string {
  const whole = opts.whole && Number.isInteger(value)
  try {
    return new Intl.NumberFormat(locale, {
      style: 'currency',
      currency,
      minimumFractionDigits: whole ? 0 : 2,
      maximumFractionDigits: whole ? 0 : 2
    }).format(value)
  } catch {
    return `${value.toFixed(whole ? 0 : 2)} ${currency}`
  }
}

export interface PriceRow {
  id: string
  days: number
  period: PassPeriod | null
  perWeek: number
  perWeekText: string
  total: number
  totalText: string
  saving: number
}

/** Everything the pricing cards show, in display order (shortest pass first). */
export function priceRows(passes: readonly BillingPass[], locale: string): PriceRow[] {
  const base = basePass(passes)
  return [...passes].sort((a, b) => a.days - b.days).map(pass => {
    const perWeek = weeklyPrice(pass)
    const total = pass.amount / 100
    return {
      id: pass.id,
      days: pass.days,
      period: passPeriod(pass.days),
      perWeek,
      perWeekText: formatMoney(perWeek, pass.currency, locale),
      total,
      totalText: formatMoney(total, pass.currency, locale, { whole: true }),
      saving: pass === base ? 0 : savingPercent(pass, base)
    }
  })
}

/** Validates the API response; anything unusable falls back to the defaults. */
export function normalizeBillingConfig(response: unknown): BillingConfig {
  const data = (response && typeof response === 'object' ? response : {}) as Record<string, unknown>
  const passes = Array.isArray(data.passes)
    ? data.passes.filter((p): p is BillingPass => !!p && typeof p === 'object'
      && typeof p.id === 'string'
      && Number.isFinite(p.days) && p.days > 0
      && Number.isFinite(p.amount) && p.amount >= 0
      && typeof p.currency === 'string' && /^[A-Za-z]{3}$/.test(p.currency))
      .map(p => ({ id: p.id, days: p.days, amount: p.amount, currency: p.currency.toUpperCase() }))
    : []
  const max = data.freeMaxActiveInvites
  return {
    passes: passes.length ? passes : DEFAULT_PASSES.map(p => ({ ...p })),
    freeMaxActiveInvites: typeof max === 'number' && Number.isInteger(max) && max > 0 ? max : DEFAULT_FREE_MAX_ACTIVE_INVITES
  }
}
