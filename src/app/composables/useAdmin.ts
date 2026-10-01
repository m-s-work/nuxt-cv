/**
 * Admin API client (`/api/admin/*`, see docs/REQUIREMENTS_ACCESS_AND_TENANCY.md §8).
 *
 * Two ways to sign in (docs/REQUIREMENTS_SAAS.md §1): the super-admin key, kept in sessionStorage only (per tab,
 * gone when the tab closes) and sent as `X-Admin-Key`; or a user's account session (`cv_session` cookie), sent
 * with `X-Requested-With: cv` and never with the key. Nothing here is used by the public CV pages.
 */
import { adminAuthHeaders, type AdminAuthMode, type BillingConfig } from '~/utils/account'

export const ADMIN_KEY_STORAGE = 'cv-admin-key'

export const REDACTION_FLAGS = [
  'hideCompanies',
  'hideTimeframeDays',
  'hideTimeframeMonths',
  'hidePhoto',
  'hideContactDetails',
  'hideBirthDate',
  'hideMedia'
] as const

export type RedactionFlag = typeof REDACTION_FLAGS[number]

export interface AccessPolicy {
  grants?: string[]
  flags?: Partial<Record<RedactionFlag, boolean>>
  hiddenFields?: string[]
  /** Pinned CV revision (git SHA or prefix); "" in invite overrides = current CV. */
  revision?: string
  /** Consent modal + visitor tracking switch; invite > profile > tenant (docs/VISITOR_SESSION_TRACKING.md). */
  tracking?: { enabled?: boolean, consent?: ConsentMode, consentNote?: string }
}

export interface AdminTenant {
  id: string
  name: string
  hosts: string[]
  defaultLocale: string
  publicProfile?: string
  profiles: string[]
  locales: string[]
  /** Profiles pinned to a CV revision in tenant.json. */
  pins: Record<string, string>
  /** "free", "pro" or "managed" (no owner). */
  plan?: string
}

// --- Accounts (docs/REQUIREMENTS_SAAS.md) ---------------------------------------------------------

export interface PlanInfo {
  name: 'free' | 'pro'
  proUntil?: string | null
  proForever?: boolean
  limits: { activeInvites?: number | null, assetBytes: number, hideCredit: boolean, heatmaps: boolean, customDomain: boolean }
}

/** GET /api/auth/me. Null fields are omitted by the API, so `tenantId` is missing until onboarding. */
export interface AccountUser {
  id: string
  email: string
  name?: string
  avatarUrl?: string | null
  tenantId?: string | null
  createdAt?: string
  logins?: string[]
  customDomain?: string | null
  /** E-mail the owner when an invite is opened for the first time. */
  notifyOnOpen?: boolean
  plan: PlanInfo
}

/** GET /api/admin/stats (super-admin). */
export interface PlatformStats {
  users: number
  withTenant: number
  pro: number
  blocked: number
  tenants: number
  activeInvites: number
  signupsPerDay: Array<{ day: string, count: number }>
  /** Completed payments in the period per currency (amount in minor units). */
  revenue: Array<{ currency: string, amount: number, count: number }>
  refunds: number
  unmatchedPayments: number
  days: number
}

export interface AccountInfo {
  user: AccountUser
  usage?: { activeInvites: number, assetBytes: number, hideCredit: boolean, hosts: string[] } | null
}

export interface Payment {
  id: string
  provider: string
  externalId?: string
  userId?: string | null
  email?: string | null
  pass?: string | null
  days: number
  amount: number
  currency?: string | null
  status: 'completed' | 'unmatched' | 'refunded' | 'chargeback' | 'manual' | string
  note?: string | null
  createdAt: string
}

export interface LinkedInImport {
  applied: boolean
  locale: string
  cv: unknown
  warnings: string[]
  filesRead: string[]
  counts: { experiences: number, studies: number, skills: number, languages: number, projects: number, certifications: number }
}

/** Super-admin view of a user (GET /api/admin/users). */
export interface AdminUser {
  id: string
  email: string
  name?: string
  tenantId?: string | null
  providers: string[]
  createdAt: string
  lastLoginAt?: string | null
  blockedAt?: string | null
  plan: 'free' | 'pro'
  proUntil?: string | null
  proForever?: boolean
  planNote?: string | null
  customDomain?: string | null
}

export interface CvRevision {
  sha: string
  message?: string
  committedAt?: string
  registeredAt: string
  /** The current CV differs from this revision. */
  outdated: boolean
  /** Tags/branches that were resolved to this commit. */
  refs?: string[]
  /** Files of the current CV that differ from this revision (same SHA-256 as GET …/hash). */
  changes?: Array<{ path: string, change: 'added' | 'removed' | 'modified' }>
}

export interface CvRevisions {
  current?: string
  /** The live CV files were changed after the current revision was registered. */
  modified: boolean
  revisions: CvRevision[]
  /** Git repo + folder reported by cv-sync.sh; without it, pruned revisions cannot be fetched again. */
  source?: { repo: string, path: string }
}

export type PinStatus = 'current' | 'outdated' | 'missing'

/** The stored revision a pin (full SHA, prefix or fetched tag) refers to. */
export function findRevision(pin: string, revisions: CvRevisions | null | undefined): CvRevision | undefined {
  return revisions?.revisions.find(r => r.refs?.includes(pin))
    ?? (/^[0-9a-f]{7,40}$/i.test(pin) ? revisions?.revisions.find(r => r.sha.startsWith(pin.toLowerCase())) : undefined)
}

/** Short summary of what changed in the current CV since a revision, e.g. "cv.en.json, assets/photo.jpg (new)". */
export function changeSummary(revision: CvRevision | undefined): string {
  const labels = { added: ' (new)', removed: ' (removed)', modified: '' }
  return (revision?.changes ?? []).map(c => c.path + labels[c.change]).join(', ')
}

/** Whether a pinned revision (full SHA, prefix or fetched tag) still matches the current CV. */
export function pinStatus(pin: string, revisions: CvRevisions | null | undefined): PinStatus {
  const revision = findRevision(pin, revisions)
  if (!revision) return 'missing'
  return revision.outdated ? 'outdated' : 'current'
}

/** Short form of a SHA; tags and branch names are shown as they are. */
export function shortSha(sha?: string | null): string {
  if (!sha) return ''
  return /^[0-9a-f]{40}$/i.test(sha) ? sha.slice(0, 7) : sha
}

export interface AdminInvite {
  id: string
  /** Plain code and link; missing for invites created before codes were stored. */
  code?: string
  link?: string
  tenant: string
  profile: string
  label: string
  overrides?: AccessPolicy
  createdAt: string
  expiresAt?: string
  revokedAt?: string
  maxUses?: number
  useCount: number
  lastUsedAt?: string
  /** View-once invite: grace window in minutes for the browser that opened it. */
  viewOnceMinutes?: number
  /** View-once invite that was opened: that browser's access ends then. */
  viewOnceUntil?: string
  parentId?: string
  source?: string
  /** Clicks on website links printed into this PDF (pdf-qr invites), most clicked first. */
  linkClicks?: Array<{ url: string, count: number, lastAt?: string }>
  /** Effective CV pin (the invite's own or its profile's); undefined = follows the current CV. */
  revision?: string
  pinnedBy?: 'invite' | 'profile'
}

export interface PdfOutcome {
  locale: string
  ok: boolean
  bytes?: number
  error?: string
}

export interface CreatedInvite {
  invite: AdminInvite
  code: string
  link: string
  pdf?: PdfOutcome[]
}

// --- Visitor tracking reports (docs/VISITOR_SESSION_TRACKING.md §8.2) -----------------------------

export interface ConsentCounts {
  accept: number
  decline: number
  withdraw: number
  declineBySource: Record<string, number>
}

export interface AnalyticsGroup {
  groupKey: string
  inviteId?: string
  label: string
  profile?: string
  source?: string
  parentId?: string
  revoked: boolean
  visitors: number
  persons: number
  sessions: number
  visits: number
  activeMs: number
  visibleMs: number
  firstVisit?: string
  lastVisit?: string
  consent: ConsentCounts
  score: number
  /** Sections this group saw / sections any visitor of the tenant saw (coverage). */
  sectionsSeen: number
  sectionsKnown: number
}

/** Optional period of the analytics reports: sessions started in [from, to). ISO date-times. */
export interface AnalyticsPeriod { from?: string, to?: string }

export interface AnalyticsBreakdown { key: string, visitors: number, sessions: number }

export interface AnalyticsOverview {
  from?: string
  to?: string
  totals: {
    visitors: number, persons: number, sessions: number, visits: number, groups: number,
    activeMs: number, visibleMs: number, avgActiveMs: number, avgVisibleMs: number
  }
  consent: ConsentCounts
  perDay: Array<{ date: string, sessions: number, visitors: number, activeMs: number }>
  devices: AnalyticsBreakdown[]
  browsers: AnalyticsBreakdown[]
  os: AnalyticsBreakdown[]
  countries: AnalyticsBreakdown[]
}

export interface AnalyticsSession {
  id: string
  visitorId: string
  visitId: string
  previousSessionId?: string
  startedAt: string
  lastSeenAt: string
  endedAt?: string
  endReason?: string
  openMs: number
  visibleMs: number
  activeMs: number
  maxScroll: number
  locale?: string
  breakpoint?: string
  viewportW?: number
  viewportH?: number
  referrer?: string
  localHour?: number
  signals?: string
  ip?: string
  ipTruncated: boolean
  ipCountry?: string
  ipCity?: string
  asOrg?: string
  fp?: string
  appSha?: string
  cvSourceSha?: string
  cvVersion?: string
  /** CV version the client reported when it differs from cvVersion (heat data is stored under cvVersion). */
  clientCvVersion?: string
  versionMismatch: boolean
}

export interface ScoreParts { time: number, coverage: number, returns: number, detail: number, intent: number, spread: number }

export interface AnalyticsGroupDetail {
  groupKey: string
  score: { total: number, parts: ScoreParts }
  coverage?: { seen: number, known: number }
  consent: ConsentCounts
  visitors: Array<{
    id: string, personId: string, personReason: string, device?: string, browser?: string, os?: string, language?: string,
    firstSeen: string, lastSeen: string, sessions: number, visits: number, activeMs: number, visibleMs?: number
  }>
  /** All sessions of the period; `sessions` lists the most recent ones. */
  sessionsTotal?: number
  sessions: AnalyticsSession[]
  anchors: Array<{
    anchor: string, label?: string, visibleMs: number, hoverMs: number, clicks: number, views: number, sessions: number,
    readingRatio?: number, reading?: 'skimmed' | 'scanned' | 'read'
  }>
  techIntent: Array<{ tech: string, count: number }>
  actions: Record<string, number>
  networks: Array<{ org?: string, city?: string, country?: string, sessions: number, visitors: number }>
  versions: Array<{ appSha?: string, cvSourceSha?: string, cvVersion?: string, sessions: number, firstSeen: string, lastSeen: string }>
}

export interface AnalyticsSessionDetail {
  session: AnalyticsSession
  ips: Array<{ ip: string, firstSeen: string, lastSeen: string }>
  linked: Array<{ id: string, startedAt: string, endReason?: string, cvVersion?: string, appSha?: string }>
  anchors: Array<{ anchor: string, label?: string, visibleMs: number, hoverMs: number, clicks: number, views: number }>
  events: Array<{ t: number, type: string, anchor?: string, label?: string, payload?: Record<string, unknown> }>
}

export type HeatmapType = 'move' | 'click' | 'attention'

export interface HeatmapFacet {
  breakpoint: string
  appSha: string
  cvVersion: string
  /** Sum of all cursor and click cells. */
  weight: number
  move?: number
  click?: number
  /** Section dwell of sessions with this layout and version (touch devices too). */
  attentionMs?: number
  /** Whether the CV snapshot of cvVersion exists, i.e. the heatmap can be rendered. */
  snapshot?: boolean
}

export interface HeatmapData {
  type: HeatmapType
  facets: HeatmapFacet[]
  cells?: Array<{ anchor: string, x: number, y: number, w: number }>
  anchors?: Array<{ anchor: string, weight: number }>
}

export interface TrackingSettings {
  privacy?: { controller?: string, contact?: string }
  tenantEnabled: boolean
  honorBrowserSignals: boolean
  retention: { identifiersMonths: number, eventsMonths: number, summaryMonths: number, heatMonths: number }
  geo: boolean
  /** "service" (internal geo container), "files" (local databases) or null. */
  geoSource?: 'service' | 'files' | null
  geoStatus?: { status?: string, error?: string, databases?: { city?: string | null, asn?: string | null } } | null
  profiles: Record<string, boolean | null>
  consentMode: ConsentMode
  profileModes: Record<string, ConsentMode>
  policyVersion: string
}

export interface ConsentStats {
  total: ConsentCounts
  byPolicyVersion: Array<{ policyVersion: string, first: string, last: string, counts: ConsentCounts, acceptRate?: number }>
  withSignals: ConsentCounts
}

/** Readable name of an anchor without a CV label: "section:preferredTechs" → "Preferred techs (section)". */
export function anchorName(anchor: string, label?: string | null): string {
  if (label) return label
  const [kind, key = ''] = anchor.split(':', 2)
  const words = key.replace(/([a-z])([A-Z])/g, '$1 $2').toLowerCase()
  const name = words.charAt(0).toUpperCase() + words.slice(1)
  return kind === 'section' ? `${name} (section)` : kind === 'tech' ? `${key} (technology)` : kind === 'contact' ? `Contact: ${key}` : anchor
}

/** "2 h 5 min", "3 min 20 s", "40 s". */
export function formatDuration(ms: number): string {
  const s = Math.round(ms / 1000)
  if (s < 60) return `${s} s`
  const m = Math.floor(s / 60)
  if (m < 60) return `${m} min${s % 60 ? ` ${s % 60} s` : ''}`
  return `${Math.floor(m / 60)} h${m % 60 ? ` ${m % 60} min` : ''}`
}

export interface AdminFile {
  path: string
  size: number
  modifiedAt: string
}

export type InviteStatus = 'active' | 'revoked' | 'expired' | 'exhausted' | 'viewing' | 'viewed'

/**
 * Mirrors the API's check: revoked > expired > view-once used (viewing within the grace window, viewed after)
 * > exhausted (maxUses only blocks new redemptions).
 */
export function inviteStatus(invite: AdminInvite, now: Date = new Date()): InviteStatus {
  if (invite.revokedAt) return 'revoked'
  if (invite.expiresAt && new Date(invite.expiresAt) <= now) return 'expired'
  if (invite.viewOnceUntil) return new Date(invite.viewOnceUntil) > now ? 'viewing' : 'viewed'
  if (invite.maxUses != null && invite.useCount >= invite.maxUses) return 'exhausted'
  return 'active'
}

/** Choices for "view once": grace window in minutes for the browser that opened the link; 0 = off. */
export const VIEW_ONCE_CHOICES = [0, 10, 30, 60, 240, 1440]

export function viewOnceLabel(minutes: number): string {
  if (!minutes) return 'Off'
  if (minutes % 1440 === 0) return `View once, ${minutes / 1440} day${minutes === 1440 ? '' : 's'}`
  if (minutes % 60 === 0) return `View once, ${minutes / 60} h`
  return `View once, ${minutes} min`
}

/** Select items for "view once", keeping a non-standard current value (set via the API) selectable. */
export function viewOnceItems(current = 0) {
  const values = VIEW_ONCE_CHOICES.includes(current) ? VIEW_ONCE_CHOICES : [...VIEW_ONCE_CHOICES, current].sort((a, b) => a - b)
  return values.map(value => ({ label: viewOnceLabel(value), value }))
}

/** Splits a comma/newline separated list, trimming and dropping empty entries. */
export function splitList(text: string): string[] {
  return text.split(/[,\n]/).map(s => s.trim()).filter(Boolean)
}

/** Tri-state flag choice in the invite form: inherit from the profile, force on, force off. */
export type FlagChoice = 'inherit' | 'on' | 'off'

export interface OverridesForm {
  flags: Partial<Record<RedactionFlag, FlagChoice>>
  hiddenFields: string
  /** Empty = keep the profile's grants; otherwise replaces them. */
  grants: string
  replaceGrants: boolean
  /** Consent modal / tracking: inherit from profile and tenant, a consent mode, or off. */
  tracking?: 'inherit' | ConsentMode | 'off'
  /** Where / when consent was given elsewhere (mode "prior"). */
  consentNote?: string
}

/** How consent is obtained (docs/VISITOR_SESSION_TRACKING.md §9.2). */
export type ConsentMode = 'modal' | 'notice' | 'prior'

/** Builds the invite `overrides` object, or undefined if nothing is overridden. */
export function buildOverrides(form: OverridesForm): AccessPolicy | undefined {
  const overrides: AccessPolicy = {}
  const flags: AccessPolicy['flags'] = {}
  for (const flag of REDACTION_FLAGS) {
    const choice = form.flags[flag]
    if (choice === 'on') flags[flag] = true
    else if (choice === 'off') flags[flag] = false
  }
  if (Object.keys(flags).length) overrides.flags = flags
  const hidden = splitList(form.hiddenFields)
  if (hidden.length) overrides.hiddenFields = hidden
  if (form.replaceGrants) overrides.grants = splitList(form.grants)
  if (form.tracking === 'off') overrides.tracking = { enabled: false }
  else if (form.tracking && form.tracking !== 'inherit') {
    overrides.tracking = { enabled: true, consent: form.tracking }
    if (form.tracking === 'prior' && form.consentNote?.trim()) overrides.tracking.consentNote = form.consentNote.trim()
  }
  return Object.keys(overrides).length ? overrides : undefined
}

/** Orders invites so derived QR invites follow directly after their parent. */
export function groupInvites(invites: AdminInvite[]): Array<AdminInvite & { depth: number }> {
  const children = new Map<string, AdminInvite[]>()
  const roots: AdminInvite[] = []
  const ids = new Set(invites.map(i => i.id))
  for (const invite of invites) {
    if (invite.parentId && ids.has(invite.parentId)) {
      const list = children.get(invite.parentId) ?? []
      list.push(invite)
      children.set(invite.parentId, list)
    } else {
      roots.push(invite)
    }
  }
  return roots.flatMap(root => [
    { ...root, depth: 0 },
    ...(children.get(root.id) ?? []).map(child => ({ ...child, depth: 1 }))
  ])
}

export function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / 1024 / 1024).toFixed(1)} MB`
}

/**
 * Parses JSON the way the API reads tenant files: `//` and `/* *\/` comments and trailing commas
 * are allowed. Throws a SyntaxError for invalid input.
 */
export function parseJsonc(text: string): unknown {
  let out = ''
  let inString = false
  for (let i = 0; i < text.length; i++) {
    const c = text[i]!
    if (inString) {
      out += c
      if (c === '\\') out += text[++i] ?? ''
      else if (c === '"') inString = false
    } else if (c === '"') {
      inString = true
      out += c
    } else if (c === '/' && text[i + 1] === '/') {
      while (i < text.length && text[i] !== '\n') i++
      out += '\n'
    } else if (c === '/' && text[i + 1] === '*') {
      const end = text.indexOf('*/', i + 2)
      if (end < 0) throw new SyntaxError('Unterminated comment')
      i = end + 1
      out += ' '
    } else {
      out += c
    }
  }
  // Then trailing commas before } or ] (again outside strings).
  return JSON.parse(removeTrailingCommas(out))
}

function removeTrailingCommas(text: string): string {
  let out = ''
  let inString = false
  for (let i = 0; i < text.length; i++) {
    const c = text[i]!
    if (inString) {
      out += c
      if (c === '\\') out += text[++i] ?? ''
      else if (c === '"') inString = false
    } else if (c === '"') {
      inString = true
      out += c
    } else if (c === ',' && /^\s*[}\]]/.test(text.slice(i + 1))) {
      // drop it
    } else {
      out += c
    }
  }
  return out
}

/** Readable message from a failed $fetch call. */
export interface FaviconCatalogue {
  defaults: { symbol: string, color: string, background: string }
  /** Named colours → hex */
  colors: Record<string, string>
  symbols: Array<{ name: string, glyph: string, svg: string }>
}

export function errorMessage(error: unknown): string {
  const e = error as { statusCode?: number, data?: { error?: string, detail?: string }, message?: string }
  if (e?.data?.error) return e.data.detail ? `${e.data.error}: ${e.data.detail}` : e.data.error
  if (e?.statusCode) return `HTTP ${e.statusCode}`
  return e?.message ?? String(error)
}

function readStoredKey(): string {
  if (!import.meta.client) return ''
  try { return sessionStorage.getItem(ADMIN_KEY_STORAGE) ?? '' } catch { return '' }
}

export function useAdmin() {
  const key = useState<string>('admin-key', readStoredKey)
  /** null until the page decided (no stored key and GET /auth/me not answered yet). */
  const mode = useState<AdminAuthMode | null>('admin-mode', () => null)
  const apiBase = useRuntimeConfig().public.apiBase as string

  function setKey(value: string) {
    key.value = value
    try {
      if (value) sessionStorage.setItem(ADMIN_KEY_STORAGE, value)
      else sessionStorage.removeItem(ADMIN_KEY_STORAGE)
    } catch { /* storage unavailable: key stays in memory */ }
  }

  function api<T>(path: string, options: Parameters<typeof $fetch>[1] = {}, auth: Record<string, string> = { 'X-Requested-With': 'cv' }): Promise<T> {
    return $fetch<T>(`${apiBase}${path}`, {
      credentials: 'same-origin',
      ...options,
      headers: { ...(options.headers as Record<string, string> | undefined), ...auth }
    } as Parameters<typeof $fetch>[1]) as Promise<T>
  }

  function request<T>(path: string, options: Parameters<typeof $fetch>[1] = {}): Promise<T> {
    return api<T>(`/admin${path}`, options, adminAuthHeaders(mode.value, key.value))
  }

  const t = (tenant: string) => `/tenants/${encodeURIComponent(tenant)}`

  return {
    key,
    setKey,
    mode,
    apiBase,
    // Account session (SaaS §2, §3, §5, §8); always cookie + CSRF header, never the admin key.
    me: () => api<AccountUser>('/auth/me'),
    logout: () => api<void>('/auth/logout', { method: 'POST' }),
    account: () => api<AccountInfo>('/account'),
    updateAccount: (body: { name?: string, hideCredit?: boolean, notifyOnOpen?: boolean }) => api<void>('/account', { method: 'PATCH', body }),
    checkHandle: (handle: string) => api<{ handle: string, error?: string | null }>(`/account/handle/${encodeURIComponent(handle)}`),
    createTenant: (body: { handle: string, locale: string, name?: string }) =>
      api<{ tenantId: string }>('/account/tenant', { method: 'POST', body }),
    importLinkedIn: (file: File, locale: string, apply: boolean) => {
      const form = new FormData()
      form.append('file', file)
      return api<LinkedInImport>('/account/import/linkedin', { method: 'POST', body: form, query: { locale, apply: apply || undefined } })
    },
    setDomain: (domain: string) => api<{ domain: string }>('/account/domain', { method: 'PUT', body: { domain } }),
    removeDomain: () => api<void>('/account/domain', { method: 'DELETE' }),
    payments: () => api<Payment[]>('/account/payments'),
    exportUrl: () => `${apiBase}/account/export`,
    revokeSessions: () => api<void>('/account/sessions/revoke', { method: 'POST' }),
    deleteAccount: (confirm: string) => api<void>('/account', { method: 'DELETE', body: { confirm } }),
    billingConfig: () => api<BillingConfig>('/billing/config'),
    // Super-admin user management (SaaS §6).
    users: (q?: string) => request<AdminUser[]>('/users', { query: { q: q || undefined } }),
    user: (id: string) => request<{ user: AdminUser, payments: Payment[] }>(`/users/${id}`),
    setPlan: (id: string, body: { proUntil?: string | null, addDays?: number | null, proForever: boolean, note?: string }) =>
      request<AdminUser>(`/users/${id}/plan`, { method: 'PUT', body }),
    blockUser: (id: string, blocked: boolean) => request<void>(`/users/${id}/${blocked ? 'block' : 'unblock'}`, { method: 'POST' }),
    deleteUser: (id: string) => request<void>(`/users/${id}`, { method: 'DELETE' }),
    allPayments: () => request<Payment[]>('/payments'),
    stats: (days = 30) => request<PlatformStats>('/stats', { query: { days } }),
    tenants: () => request<AdminTenant[]>('/tenants'),
    profiles: (tenant: string) => request<Record<string, AccessPolicy>>(`${t(tenant)}/profiles`),
    invites: (tenant: string) => request<AdminInvite[]>(`${t(tenant)}/invites`),
    createInvite: (tenant: string, body: {
      profile: string
      label?: string
      expiresAt?: string
      maxUses?: number
      viewOnceMinutes?: number
      overrides?: AccessPolicy
    }) => request<CreatedInvite>(`${t(tenant)}/invites`, { method: 'POST', body }),
    /** Replaces label, expiry, max. redemptions and view once (null = none / unlimited / off). */
    updateInvite: (tenant: string, id: string, body: {
      label: string
      expiresAt: string | null
      maxUses: number | null
      viewOnceMinutes: number | null
    }) => request<AdminInvite>(`${t(tenant)}/invites/${id}/settings`, { method: 'PUT', body }),
    /** Makes a used-up code redeemable again (use count and view-once state reset). */
    rearmInvite: (tenant: string, id: string) =>
      request<AdminInvite>(`${t(tenant)}/invites/${id}/rearm`, { method: 'POST' }),
    revokeInvite: (tenant: string, id: string) =>
      request<AdminInvite>(`${t(tenant)}/invites/${id}`, { method: 'DELETE' }),
    renderPdf: (tenant: string, id: string) =>
      request<{ pdf: PdfOutcome[] }>(`${t(tenant)}/invites/${id}/pdf`, { method: 'POST' }),
    files: (tenant: string) => request<AdminFile[]>(`${t(tenant)}/files`),
    readFile: (tenant: string, path: string) =>
      request<string>(`${t(tenant)}/files/${path}`, { responseType: 'text' }),
    readBlob: (tenant: string, path: string) =>
      request<Blob>(`${t(tenant)}/files/${path}`, { responseType: 'blob' }),
    writeFile: (tenant: string, path: string, body: string | Blob) =>
      request<void>(`${t(tenant)}/files/${path}`, { method: 'PUT', body }),
    deleteFile: (tenant: string, path: string) =>
      request<void>(`${t(tenant)}/files/${path}`, { method: 'DELETE' }),
    preview: (tenant: string, profile: string, locale?: string, revision?: string) =>
      request<{ locale: string, revision?: string, cv: unknown }>(`${t(tenant)}/preview`, { query: { profile, locale, revision } }),
    /** Unsaved CV draft redacted for a profile (nothing is stored; pins are ignored). */
    previewDraft: (tenant: string, profile: string, cv: unknown, locale?: string) =>
      request<{ locale?: string, revision?: string | null, cv: unknown }>(`${t(tenant)}/preview`, { method: 'POST', body: { profile, locale, cv } }),
    /** PDF of a profile in any template (not cached); revision as for preview. Needs the PDF renderer. */
    pdfPreview: (tenant: string, query: { profile: string, locale?: string, template?: string, vars?: string, revision?: string }) =>
      request<Blob>(`${t(tenant)}/pdf-preview`, { query, responseType: 'blob' }),
    /** Favicon catalogue: every symbol drawn in the given colours (named or hex; defaults if not set). */
    faviconCatalogue: (color?: string, background?: string) =>
      request<FaviconCatalogue>('/favicon', { query: { color: color || undefined, background: background || undefined } }),
    revisions: (tenant: string) => request<CvRevisions>(`${t(tenant)}/revisions`),
    /** Fetches a revision (SHA, tag or branch) from the tenant's git repo again. */
    fetchRevision: (tenant: string, ref: string) =>
      request<{ sha: string }>(`${t(tenant)}/revisions/fetch`, { method: 'POST', body: { ref } }),
    /** revision: SHA = pin, "" = current CV (ignores a profile pin), null = follow the profile. */
    pinInvite: (tenant: string, id: string, revision: string | null) =>
      request<AdminInvite>(`${t(tenant)}/invites/${id}/revision`, { method: 'PUT', body: { revision } }),
    analyticsGroups: (tenant: string, period: AnalyticsPeriod = {}) =>
      request<AnalyticsGroup[]>(`${t(tenant)}/analytics/groups`, { query: { ...period } }),
    analyticsGroup: (tenant: string, group: string, period: AnalyticsPeriod = {}, limit?: number) =>
      request<AnalyticsGroupDetail>(`${t(tenant)}/analytics/groups/${encodeURIComponent(group)}`, { query: { ...period, limit } }),
    /** Tenant-wide totals, sessions per day and breakdowns; tz = new Date().getTimezoneOffset() for the day buckets. */
    analyticsOverview: (tenant: string, period: AnalyticsPeriod = {}, tz = new Date().getTimezoneOffset()) =>
      request<AnalyticsOverview>(`${t(tenant)}/analytics/overview`, { query: { ...period, tz } }),
    analyticsSession: (tenant: string, id: string) =>
      request<AnalyticsSessionDetail>(`${t(tenant)}/analytics/sessions/${encodeURIComponent(id)}`),
    heatmap: (tenant: string, query: { group?: string, bp?: string, appSha?: string, cvVersion?: string, type: HeatmapType }) =>
      request<HeatmapData>(`${t(tenant)}/analytics/heatmap`, { query }),
    cvSnapshot: (tenant: string, cvVersion: string) =>
      request<{ cvVersion: string, locale: string, cvSourceSha?: string, cv: unknown }>(`${t(tenant)}/analytics/cv-snapshots/${cvVersion}`),
    trackingSettings: (tenant: string) => request<TrackingSettings>(`${t(tenant)}/analytics/settings`),
    consentStats: (tenant: string) => request<ConsentStats>(`${t(tenant)}/analytics/consent`),
    eraseVisitor: (tenant: string, visitorId: string) =>
      request<void>(`${t(tenant)}/analytics/visitors/${visitorId}`, { method: 'DELETE' })
  }
}
