/**
 * Pure helpers for accounts, plans and billing (docs/REQUIREMENTS_SAAS.md). Used by the admin dashboard and
 * the login page only; nothing here touches CV data.
 */

import type { InjectionKey } from 'vue'

/** Provided by the admin page in user mode: switches to the Account tab (upgrade hints). */
export const OPEN_ACCOUNT_TAB: InjectionKey<() => void> = Symbol('open-account-tab')

/** How the admin page talks to the admin API: the super-admin key, or the signed-in user's session cookie. */
export type AdminAuthMode = 'key' | 'session'

/**
 * Headers for an admin API request (SaaS §1, S2.5). In session mode the admin key is never sent, but the CSRF
 * header is (the API requires it on state-changing cookie-authenticated requests). Without a mode (e.g. the
 * heatmap iframe), the key is used when one is stored, otherwise the session.
 */
export function adminAuthHeaders(mode: AdminAuthMode | null | undefined, key: string): Record<string, string> {
  if (mode !== 'session' && key) return { 'X-Admin-Key': key }
  return { 'X-Requested-With': 'cv' }
}

/** Invite links may be relative ("/cv?c=…") when the API has no shared base URL configured. */
export function completeLink(link: string | null | undefined, origin: string): string {
  if (!link) return ''
  if (/^[a-z][a-z0-9+.-]*:\/\//i.test(link)) return link
  const base = origin.replace(/\/+$/, '')
  return link.startsWith('/') ? `${base}${link}` : `${base}/${link}`
}

export interface PassConfig {
  id: string
  days: number
  /** Minor units (cents). */
  amount: number
  currency: string
  priceId?: string | null
}

export interface BillingConfig {
  provider: 'paddle' | null
  environment?: 'sandbox' | 'production' | null
  clientToken?: string | null
  freeMaxActiveInvites?: number
  passes: PassConfig[]
}

export interface PassPricing extends PassConfig {
  /** Price per week in minor units (not rounded). */
  perWeek: number
  /** Saving against the 7-day pass in whole percent; 0 for the weekly pass or when there is none. */
  savingPercent: number
  /** Lowest price per week of all passes. */
  cheapest: boolean
}

/** Price per week and saving vs. the weekly pass for each pass (§4.1). */
export function passPricing(passes: PassConfig[]): PassPricing[] {
  const valid = passes.filter(p => p.days > 0)
  const perWeek = (p: PassConfig) => p.amount / (p.days / 7)
  const weekly = valid.find(p => p.days === 7) ?? valid.slice().sort((a, b) => a.days - b.days)[0]
  const weeklyRate = weekly ? perWeek(weekly) : 0
  const lowest = Math.min(...valid.map(perWeek))
  return valid.map(p => {
    const rate = perWeek(p)
    const saving = weeklyRate > 0 && p !== weekly ? Math.round((1 - rate / weeklyRate) * 100) : 0
    return { ...p, perWeek: rate, savingPercent: Math.max(0, saving), cheapest: valid.length > 1 && rate === lowest }
  })
}

/** "€3.97" from minor units in the pass currency. */
export function formatMoney(minor: number, currency: string, locale = 'en'): string {
  try {
    return new Intl.NumberFormat(locale, { style: 'currency', currency: currency || 'EUR' }).format(minor / 100)
  } catch {
    return `${(minor / 100).toFixed(2)} ${currency}`
  }
}

/** "1 week", "1 month", "6 months", "1 year", otherwise "N days". */
export function passName(days: number): string {
  if (days === 7) return '1 week'
  if (days % 7 === 0 && days < 28) return `${days / 7} weeks`
  if (days >= 28 && days <= 31) return '1 month'
  if (days >= 180 && days <= 184) return '6 months'
  if (days >= 365 && days <= 366) return '1 year'
  return `${days} days`
}

/** Login page errors (`/login?error=…`, set by the API's sign-in endpoints) → i18n key. */
export const LOGIN_ERRORS = ['cancelled', 'provider_failed', 'account_exists', 'blocked', 'no_email', 'link_invalid'] as const
export type LoginError = typeof LOGIN_ERRORS[number]

export function loginErrorKey(code: string | null | undefined): string | null {
  if (!code) return null
  return (LOGIN_ERRORS as readonly string[]).includes(code) ? `login.errors.${code}` : 'login.errors.provider_failed'
}

/** Handle rule of the API (S3.1). */
export const HANDLE_PATTERN = /^[a-z0-9][a-z0-9-]{2,30}$/

const HANDLE_ERRORS: Record<string, string> = {
  invalid_handle: '3–31 characters: lowercase letters, digits and dashes; must start with a letter or digit.',
  handle_reserved: 'This handle is reserved.',
  handle_taken: 'This handle is already taken.',
  has_tenant: 'Your account already has a CV.'
}

const DOMAIN_ERRORS: Record<string, string> = {
  invalid_domain: 'This is not a valid domain name (e.g. cv.example.com).',
  domain_taken: 'This domain is already used by another CV or by the platform.',
  dns_mismatch: 'The domain does not point to this service yet. Set the DNS record below and try again (DNS changes can take a while).',
  no_tenant: 'Create your CV first.'
}

/** Readable text for an error code of the account API; falls back to the code itself. */
export function accountErrorText(code: string | null | undefined): string {
  if (!code) return ''
  return HANDLE_ERRORS[code] ?? DOMAIN_ERRORS[code] ?? ({
    confirmation_mismatch: 'The confirmation does not match your handle.',
    import_failed: 'The file could not be read as a LinkedIn data export.',
    file_missing: 'Choose a file first.',
    invalid_email: 'Please enter a valid e-mail address.',
    csrf: 'The request was blocked (missing security header). Reload the page.',
    quota_exceeded: 'Storage quota exceeded.'
  } as Record<string, string>)[code] ?? code
}

/** A plan limit (402) or quota error (413) of the API, ready for an upgrade hint. */
export interface UpgradeReason {
  feature: string
  limit?: number | null
  quota?: number | null
}

/** Detects `402 { error: "plan_limit" }` and `413 { error: "quota_exceeded" }` in a failed $fetch call. */
export function upgradeReason(error: unknown): UpgradeReason | null {
  const e = error as { statusCode?: number, status?: number, data?: { error?: string, feature?: string, limit?: number | null, quota?: number | null } }
  const status = e?.statusCode ?? e?.status
  if (status === 402 && (e.data?.error === 'plan_limit' || !e.data?.error)) return { feature: e.data?.feature ?? 'pro', limit: e.data?.limit ?? null }
  if (status === 413 && e.data?.error === 'quota_exceeded') return { feature: 'storage', quota: e.data?.quota ?? null }
  return null
}

function mb(bytes: number) {
  return `${Math.round(bytes / 1024 / 1024)} MB`
}

/** The text of an upgrade hint, e.g. "Free plan: up to 3 active invites. Revoke one or get Pro." */
export function upgradeText(reason: UpgradeReason): string {
  switch (reason.feature) {
    case 'activeInvites':
      return `Free plan: up to ${reason.limit ?? 3} active invites. Revoke one or get Pro.`
    case 'storage':
      return `Storage quota${reason.quota ? ` of ${mb(reason.quota)}` : ''} exceeded. Delete assets or get Pro for more space.`
    case 'heatmaps':
      return 'Heatmaps, attention per section and technology intent are part of Pro.'
    case 'hideCredit':
      return 'Removing the "Created with" credit from PDFs is part of Pro.'
    case 'customDomain':
      return 'An own domain is part of Pro.'
    default:
      return 'This feature is part of Pro.'
  }
}

/** Percentage 0–100 of used / limit (0 when there is no limit). */
export function usagePercent(used: number, limit: number | null | undefined): number {
  if (!limit || limit <= 0) return 0
  return Math.min(100, Math.round((used / limit) * 100))
}

/** Pro end changed (or Pro forever was set) since a checkout started: the payment has been applied. */
export function proChanged(before: { proUntil?: string | null, proForever?: boolean } | null | undefined,
  after: { proUntil?: string | null, proForever?: boolean } | null | undefined): boolean {
  if (!after) return false
  return (after.proUntil ?? null) !== (before?.proUntil ?? null) || !!after.proForever !== !!before?.proForever
}

/** Date input value (yyyy-mm-dd) → ISO timestamp at the end of that day (UTC). */
export function endOfDayIso(date: string): string | null {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(date)) return null
  return `${date}T23:59:59Z`
}
