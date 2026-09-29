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

interface CvResponse {
  access: CvAccess
  locale: string
  features?: CvFeatures
  templates?: CvTemplates
  links?: CvLinks
  cv: CvData
}

/** Query parameter carrying the invite code, e.g. https://cv.example.org/?c=abc */
export const INVITE_PARAM = 'c'

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
   * Redeems an invite code from the URL (?c=...), removes it from the address bar
   * so it does not end up in bookmarks/history/screenshots, then loads the CV.
   */
  async function init(locale: string) {
    if (import.meta.client) {
      const url = new URL(window.location.href)
      const code = url.searchParams.get(INVITE_PARAM)
      if (code) {
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
    else if (loadedLocale.value !== locale) await load(locale)
  }

  return {
    cv,
    access,
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
