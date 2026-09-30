/**
 * CV data loaded from the backend (`/api/cv`).
 *
 * The frontend contains no CV data: the API decides tenant and redaction from the hostname
 * and the invite cookie (see docs/REQUIREMENTS_ACCESS_AND_TENANCY.md). Fields may be missing
 * whenever the visitor's profile hides them, so everything except ids is optional.
 */

export interface CvProfile {
  name?: string
  title?: string
  photoUrl?: string
  photoUrlLarge?: string
  academicTitlePrefix?: string
  academicTitleSuffix?: string
}

export interface CvDetails {
  location?: string
  citizenship?: string
  email?: string
  phone?: string
  birthDate?: string
}

export interface CvIntroStat {
  label: string
  value: number
  max: number
  suffix: string
  icon: string
}

export interface CvIntro {
  text?: string
  yearsOfExperience?: number
  programmingSince?: number
  stats?: CvIntroStat[]
}

export interface CvDated {
  id: number
  startDate: string
  endDate: string | null
  period?: string
  icon?: string
}

export interface CvExperience extends CvDated {
  company?: string
  position?: string
  description?: string
  technologies?: string[]
  images?: string[]
  logos?: string[]
}

export interface CvStudy extends CvDated {
  institution?: string
  degree?: string
  focus?: string
  technologies?: string[]
}

export interface CvProject extends CvDated {
  name?: string
  type?: string
  client?: string
  description?: string
  technologies?: string[]
  screenshots?: string[]
  images?: string[]
  logos?: string[]
}

export interface CvOtherEntry extends CvDated {
  title?: string
  institution?: string
  description?: string
  showPeriod?: boolean
  images?: string[]
}

export interface CvData {
  profile?: CvProfile
  details?: CvDetails
  intro?: CvIntro
  skills?: { skilled?: string[], liked?: string[] }
  preferredTechs?: string[]
  languages?: Array<{ name: string, level: string, code: string }>
  drivingLicenses?: Array<{ type: string, description: string }>
  experiences?: CvExperience[]
  studies?: CvStudy[]
  projects?: CvProject[]
  otherEntries?: CvOtherEntry[]
}

export interface CvAccess {
  tenant: string
  profile: string
  viaInvite: boolean
  label?: string
  expiresAt?: string
}

export type CvStatus = 'idle' | 'loading' | 'ready' | 'no-access' | 'error'

/** "shared": host without own tenant (shows the showcase); "tenant": a tenant's own host. */
export type CvHostKind = 'shared' | 'tenant'

export interface CvFeatures {
  pdf: boolean
}

/** Links provided by the API (platform = shared site, for the "Created with" credit). */
export interface CvLinks {
  platform?: string | null
}

/** Template names resolved by the API (null = frontend default). */
export interface CvTemplates {
  pdf?: string | null
  html?: string | null
  /** Template variables chosen by the owner (colours, toggles, preset); validated by the template. */
  pdfVars?: Record<string, unknown> | null
}

/**
 * Consent modal info from the API (docs/VISITOR_SESSION_TRACKING.md §9.1). `required: false` = no modal and no
 * tracking (switched off for this tenant / profile / invite). `state` null = the visitor has not decided yet.
 */
export interface CvConsent {
  required: boolean
  /** modal: ask first; notice: no modal, a notice with opt-out; prior: consent given elsewhere (§9.2). */
  mode?: 'modal' | 'notice' | 'prior'
  /** Implied consent was recorded on this request (notice mode shows its notice once). */
  impliedNow?: boolean
  state?: 'accept' | 'decline' | null
  policyVersion?: string
  controller?: string
  contact?: string
  retention?: { identifiersMonths: number, eventsMonths: number, summaryMonths: number }
  /** DNT / GPC sent by the browser ("dnt,gpc"). */
  signals?: string | null
}

interface CvResponse {
  access: CvAccess
  cvVersion?: string
  cvSourceSha?: string
  consent?: CvConsent
  locale: string
  features?: CvFeatures
  templates?: CvTemplates
  links?: CvLinks
  cv: CvData
}

/** Query parameter carrying the invite code, e.g. https://cv.example.org/?c=abc */
export const INVITE_PARAM = 'c'

/** Owner heatmap view (admin iframe): no consent modal, no tracking, no splash screen. */
export function isHeatmapView(): boolean {
  return import.meta.client && new URLSearchParams(window.location.search).has('heatmap')
}

/** Owner preview in the admin's Edit tab (/?preview=1 in an iframe): CV data is posted in by the admin page. */
export function isAdminPreview(): boolean {
  return import.meta.client && new URLSearchParams(window.location.search).has('preview')
}

/** Message the admin page posts into the preview iframe (same origin only). */
export interface CvPreviewMessage {
  type: 'cv-preview'
  tenant: string
  locale: string
  cv: CvData
}

/**
 * Replaces "/api/assets/<file>" URLs with object URLs of files loaded by `load` (admin heatmap view and preview).
 * `urls` caches object URLs across calls (the preview re-renders on every edit).
 */
export async function inlineAssets<T>(data: T, load: (path: string) => Promise<Blob>, urls = new Map<string, string>()): Promise<T> {
  const json = JSON.stringify(data)
  const files = [...new Set(json.match(/\/api\/assets\/[A-Za-z0-9._-]+/g) ?? [])]
  await Promise.all(files.filter(file => !urls.has(file)).map(async (file) => {
    try { urls.set(file, URL.createObjectURL(await load(`assets/${file.slice('/api/assets/'.length)}`))) } catch { /* missing asset */ }
  }))
  return JSON.parse(json.replace(/\/api\/assets\/[A-Za-z0-9._-]+/g, m => urls.get(m) ?? m)) as T
}

/**
 * Formats a period from ISO dates of any precision ("2020", "2020-03", "2020-03-15").
 * Year precision renders as "2020", finer precision as "03/2020".
 */
export function formatPeriod(startDate: string | null | undefined, endDate: string | null | undefined, presentLabel: string): string {
  const format = (date: string) => {
    const match = /^(\d{4})(?:-(\d{2}))?/.exec(date)
    if (!match) return date
    return match[2] ? `${match[2]}/${match[1]}` : match[1]!
  }
  const start = startDate ? format(startDate) : ''
  const end = endDate ? format(endDate) : presentLabel
  return start === end ? start : `${start} - ${end}`
}

/** Adds a formatted `period` to every dated item that has none (the API drops periods when dates are reduced). */
export function withPeriods(cv: CvData, presentLabel: string): CvData {
  const fill = <T extends CvDated>(items?: T[]) =>
    items?.map(item => ({ ...item, period: item.period ?? formatPeriod(item.startDate, item.endDate, presentLabel) }))
  return {
    ...cv,
    experiences: fill(cv.experiences),
    studies: fill(cv.studies),
    projects: fill(cv.projects),
    otherEntries: fill(cv.otherEntries)
  }
}

export function useCv() {
  const cv = useState<CvData | null>('cv-data', () => null)
  const access = useState<CvAccess | null>('cv-access', () => null)
  const status = useState<CvStatus>('cv-status', () => 'idle')
  const inviteRejected = useState<boolean>('cv-invite-rejected', () => false)
  const loadedLocale = useState<string | null>('cv-locale', () => null)
  const hostKind = useState<CvHostKind>('cv-host-kind', () => 'tenant')
  const features = useState<CvFeatures>('cv-features', () => ({ pdf: false }))
  const templates = useState<CvTemplates>('cv-templates', () => ({}))
  const links = useState<CvLinks>('cv-links', () => ({}))
  const consent = useState<CvConsent>('cv-consent', () => ({ required: false }))
  const versions = useState<{ cvVersion?: string, cvSourceSha?: string }>('cv-versions', () => ({}))
  /** The visitor arrived with an invite link (?c=…) in this tab. */
  const arrivedViaLink = useState<boolean>('cv-arrived-via-link', () => false)

  const apiBase = useRuntimeConfig().public.apiBase as string

  // Kept here (not in component i18n blocks) because periods are computed when the data is loaded.
  function presentLabel(locale: string) {
    return locale === 'de' ? 'heute' : 'Present'
  }

  async function load(locale: string) {
    status.value = 'loading'
    loadedLocale.value = locale
    try {
      const response = await $fetch<CvResponse>(`${apiBase}/cv`, {
        query: { locale },
        credentials: 'include'
      })
      cv.value = withPeriods(response.cv, presentLabel(locale))
      access.value = response.access
      features.value = response.features ?? { pdf: false }
      templates.value = response.templates ?? {}
      links.value = response.links ?? {}
      consent.value = response.consent ?? { required: false }
      versions.value = { cvVersion: response.cvVersion, cvSourceSha: response.cvSourceSha }
      status.value = 'ready'
    } catch (error: unknown) {
      cv.value = null
      access.value = null
      const fetchError = error as { statusCode?: number, data?: { host?: CvHostKind } }
      status.value = fetchError?.statusCode === 403 ? 'no-access' : 'error'
      hostKind.value = fetchError?.data?.host === 'shared' ? 'shared' : 'tenant'
    }
  }

  /** The API draws the favicon of the visitor's tenant; after redeeming an invite it may be a different one. */
  function refreshFavicon() {
    if (!import.meta.client) return
    const link = document.querySelector<HTMLLinkElement>('link[rel="icon"][type="image/svg+xml"]')
    if (link) link.href = `${apiBase}/favicon.svg?v=${Date.now()}`
  }

  /** Redeems an invite code. Returns false if the code is not valid. */
  async function redeem(code: string): Promise<boolean> {
    try {
      await $fetch(`${apiBase}/access/redeem`, {
        method: 'POST',
        body: { code: code.trim() },
        credentials: 'include'
      })
      inviteRejected.value = false
      refreshFavicon()
      return true
    } catch {
      inviteRejected.value = true
      return false
    }
  }

  /**
   * Heatmap view for the owner (/?heatmap=1&tenant=…&cv=…, opened by the admin page in an iframe): renders the
   * stored CV snapshot of that version instead of calling /api/cv (docs/VISITOR_SESSION_TRACKING.md R6.8, R6.12).
   * Uses the admin key of this browser tab; assets are loaded through the admin API.
   */
  async function initHeatmap(params: URLSearchParams) {
    status.value = 'loading'
    const admin = useAdmin()
    const tenant = params.get('tenant') ?? ''
    try {
      const snapshot = await admin.cvSnapshot(tenant, params.get('cv') ?? '')
      const data = await inlineAssets(snapshot.cv as CvData, path => admin.readBlob(tenant, path))
      cv.value = withPeriods(data, presentLabel(snapshot.locale))
      access.value = { tenant, profile: 'heatmap', viaInvite: false }
      consent.value = { required: false }
      features.value = { pdf: false }
      status.value = 'ready'
    } catch {
      status.value = 'error'
    }
  }

  /**
   * Owner preview (/?preview=1, iframe in the admin's Edit tab): shows the CV the admin page posts in – already
   * redacted by the API for the chosen profile, possibly an unsaved draft. No tracking, no consent, no PDF button.
   */
  function initPreview() {
    status.value = 'loading'
    const admin = useAdmin()
    const urls = new Map<string, string>()
    let latest = 0
    window.addEventListener('message', async (event: MessageEvent<CvPreviewMessage>) => {
      if (event.origin !== window.location.origin || event.source !== window.parent || event.data?.type !== 'cv-preview') return
      const { tenant, locale, cv: data } = event.data
      const request = ++latest
      const inlined = await inlineAssets(data, path => admin.readBlob(tenant, path), urls)
      if (request !== latest) return
      cv.value = withPeriods(inlined, presentLabel(locale))
      access.value = { tenant, profile: 'preview', viaInvite: false }
      consent.value = { required: false }
      features.value = { pdf: false }
      status.value = 'ready'
    })
    window.parent.postMessage({ type: 'cv-preview-ready' }, window.location.origin)
  }

  /**
   * Redeems an invite code from the URL (?c=...), removes it from the address bar
   * so it does not end up in bookmarks/history/screenshots, then loads the CV.
   */
  async function init(locale: string) {
    if (import.meta.client) {
      const url = new URL(window.location.href)
      if (url.searchParams.has('heatmap')) return initHeatmap(url.searchParams)
      if (url.searchParams.has('preview')) return initPreview()
      const code = url.searchParams.get(INVITE_PARAM)
      if (code) {
        arrivedViaLink.value = true
        await redeem(code)
        url.searchParams.delete(INVITE_PARAM)
        window.history.replaceState(window.history.state, '', url.pathname + url.search + url.hash)
      }
    }
    await load(locale)
  }

  /** Initializes once, afterwards reloads only when the locale changed. */
  async function ensure(locale: string) {
    if (status.value === 'idle') await init(locale)
    else if (loadedLocale.value !== locale && !isHeatmapView() && !isAdminPreview()) await load(locale)
  }

  /** Records the visitor's choice in the consent modal (§9.1). */
  async function decideConsent(choice: 'accept' | 'decline', source: 'modal' | 'footer' = 'modal') {
    try {
      await $fetch(`${apiBase}/consent`, {
        method: 'POST',
        body: { choice, source, policyVersion: consent.value.policyVersion },
        credentials: 'include'
      })
      consent.value = { ...consent.value, state: choice }
    } catch {
      // Text changed meanwhile (409) or network error: the CV stays usable, the modal is shown again on next load.
      consent.value = { ...consent.value, state: 'decline' }
    }
  }

  /** Withdraws an accepted consent (footer "Privacy"): stops tracking and forgets the browser id. */
  async function withdrawConsent() {
    try {
      await $fetch(`${apiBase}/consent`, { method: 'DELETE', credentials: 'include' })
    } catch { /* ignored: the tracker is stopped either way */ }
    consent.value = { ...consent.value, state: 'decline' }
  }

  return {
    cv,
    access,
    consent,
    versions,
    arrivedViaLink,
    decideConsent,
    withdrawConsent,
    status,
    hostKind,
    features,
    templates,
    links,
    inviteRejected,
    init,
    load,
    ensure,
    redeem
  }
}
