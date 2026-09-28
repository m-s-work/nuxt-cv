/**
 * Admin API client (`/api/admin/*`, see docs/REQUIREMENTS_ACCESS_AND_TENANCY.md §8).
 *
 * The admin key is kept in sessionStorage only (per tab, gone when the tab closes) and sent as
 * `X-Admin-Key`. Nothing here is used by the public CV pages.
 */

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
  parentId?: string
  source?: string
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

export interface AdminFile {
  path: string
  size: number
  modifiedAt: string
}

export type InviteStatus = 'active' | 'revoked' | 'expired' | 'exhausted'

/** Mirrors the API's check: revoked > expired > exhausted (maxUses only blocks new redemptions). */
export function inviteStatus(invite: AdminInvite, now: Date = new Date()): InviteStatus {
  if (invite.revokedAt) return 'revoked'
  if (invite.expiresAt && new Date(invite.expiresAt) <= now) return 'expired'
  if (invite.maxUses != null && invite.useCount >= invite.maxUses) return 'exhausted'
  return 'active'
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
}

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
  const apiBase = useRuntimeConfig().public.apiBase as string

  function setKey(value: string) {
    key.value = value
    try {
      if (value) sessionStorage.setItem(ADMIN_KEY_STORAGE, value)
      else sessionStorage.removeItem(ADMIN_KEY_STORAGE)
    } catch { /* storage unavailable: key stays in memory */ }
  }

  function request<T>(path: string, options: Parameters<typeof $fetch>[1] = {}): Promise<T> {
    return $fetch<T>(`${apiBase}/admin${path}`, {
      ...options,
      headers: { ...(options.headers as Record<string, string> | undefined), 'X-Admin-Key': key.value }
    } as Parameters<typeof $fetch>[1]) as Promise<T>
  }

  const t = (tenant: string) => `/tenants/${encodeURIComponent(tenant)}`

  return {
    key,
    setKey,
    tenants: () => request<AdminTenant[]>('/tenants'),
    profiles: (tenant: string) => request<Record<string, AccessPolicy>>(`${t(tenant)}/profiles`),
    invites: (tenant: string) => request<AdminInvite[]>(`${t(tenant)}/invites`),
    createInvite: (tenant: string, body: {
      profile: string
      label?: string
      expiresAt?: string
      maxUses?: number
      overrides?: AccessPolicy
    }) => request<CreatedInvite>(`${t(tenant)}/invites`, { method: 'POST', body }),
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
    /** PDF of a profile in any template (not cached); revision as for preview. Needs the PDF renderer. */
    pdfPreview: (tenant: string, query: { profile: string, locale?: string, template?: string, vars?: string, revision?: string }) =>
      request<Blob>(`${t(tenant)}/pdf-preview`, { query, responseType: 'blob' }),
    revisions: (tenant: string) => request<CvRevisions>(`${t(tenant)}/revisions`),
    /** Fetches a revision (SHA, tag or branch) from the tenant's git repo again. */
    fetchRevision: (tenant: string, ref: string) =>
      request<{ sha: string }>(`${t(tenant)}/revisions/fetch`, { method: 'POST', body: { ref } }),
    /** revision: SHA = pin, "" = current CV (ignores a profile pin), null = follow the profile. */
    pinInvite: (tenant: string, id: string, revision: string | null) =>
      request<AdminInvite>(`${t(tenant)}/invites/${id}/revision`, { method: 'PUT', body: { revision } })
  }
}
