import { printLabels, type PrintLocale } from '~/utils/printLabels'

/**
 * Data shared by all print/PDF templates (components/print/Print*.vue): the redacted CV prepared
 * for print (sorted sections, formatted dates, contact list), labels and the QR code.
 * Templates only decide the layout.
 */
export function usePrintData() {
  const { locale } = useI18n()
  const { cv, links } = useCv()
  const { getAssetPath } = useAssetPath()
  const { dataUrl: qrDataUrl, url: qrUrl } = useCvQrCode(400)

  /** Label lookup, e.g. label('contact.email') or label('years', { years: 25 }). */
  function label(path: string, params: Record<string, string | number> = {}): string {
    const messages = printLabels[(locale.value in printLabels ? locale.value : 'en') as PrintLocale]
    const value = path.split('.').reduce<unknown>((node, key) => (node as Record<string, unknown>)?.[key], messages)
    return typeof value === 'string'
      ? value.replace(/\{(\w+)\}/g, (_, key: string) => String(params[key] ?? ''))
      : path
  }

  const profile = computed(() => cv.value?.profile ?? {})
  const details = computed(() => cv.value?.details ?? {})
  const intro = computed(() => cv.value?.intro)
  const photo = computed(() => profile.value.photoUrlLarge ?? profile.value.photoUrl)

  const birthDate = computed(() => {
    const value = details.value.birthDate
    if (!value || !/^\d{4}-\d{2}-\d{2}$/.test(value)) return value
    return new Date(`${value}T00:00:00`).toLocaleDateString(locale.value === 'de' ? 'de-DE' : 'en-GB')
  })

  const contact = computed(() => ([
    { key: 'location', value: details.value.location },
    { key: 'email', value: details.value.email },
    { key: 'phone', value: details.value.phone },
    { key: 'citizenship', value: details.value.citizenship },
    { key: 'born', value: birthDate.value }
  ]).filter((item): item is { key: string, value: string } => !!item.value))

  // Clickable links in the PDF: the online version (same target as the QR code, incl. invite code)
  // and the platform for the "Created with …" credit. Displayed as bare host names.
  const host = (url?: string | null) => {
    try { return url ? new URL(url).host : '' } catch { return '' }
  }
  const onlineHost = computed(() => host(qrUrl.value))
  const platformUrl = computed(() => links.value?.platform ?? '')
  const platformHost = computed(() => host(platformUrl.value))

  /** Typographic dash between dates ("2017 – 2020"). */
  const period = (value?: string) => value?.replace(' - ', ' – ') ?? ''

  // Newest first, independent of the order in the JSON.
  const byStart = <T extends { startDate: string }>(items?: T[]) =>
    [...(items ?? [])].sort((a, b) => b.startDate.localeCompare(a.startDate))

  return {
    locale,
    label,
    getAssetPath,
    qrDataUrl,
    qrUrl,
    onlineHost,
    platformUrl,
    platformHost,
    profile,
    details,
    intro,
    photo,
    contact,
    period,
    skills: computed(() => cv.value?.skills?.skilled ?? []),
    liked: computed(() => cv.value?.skills?.liked ?? []),
    preferredTechs: computed(() => cv.value?.preferredTechs ?? []),
    languages: computed(() => cv.value?.languages ?? []),
    licenses: computed(() => cv.value?.drivingLicenses ?? []),
    experiences: computed(() => byStart(cv.value?.experiences)),
    studies: computed(() => byStart(cv.value?.studies)),
    projects: computed(() => byStart(cv.value?.projects)),
    otherEntries: computed(() => byStart(cv.value?.otherEntries))
  }
}
