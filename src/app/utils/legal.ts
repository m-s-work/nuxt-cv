// Operator details for the legal pages (/legal/imprint, /legal/privacy, /legal/terms), from the build-time
// variables NUXT_PUBLIC_LEGAL_* (nuxt.config.ts runtimeConfig.public). Pure functions, no Vue.

export interface LegalOperator {
  name: string
  /** Address lines (NUXT_PUBLIC_LEGAL_ADDRESS, lines separated by "|", a newline or a literal "\n"). */
  address: string[]
  email: string
  vatId: string
  country: string
  /** false when name, address or e-mail is missing: the pages then show a hint for the operator. */
  configured: boolean
}

// Country names the operator is likely to set, translated for the German pages.
const COUNTRY_DE: Record<string, string> = {
  austria: 'Österreich',
  germany: 'Deutschland',
  switzerland: 'Schweiz',
  liechtenstein: 'Liechtenstein',
  luxembourg: 'Luxemburg',
  italy: 'Italien',
  netherlands: 'Niederlande',
  belgium: 'Belgien',
  france: 'Frankreich'
}

export function splitAddress(address: string): string[] {
  return address.split(/\\n|\r?\n|\|/).map(line => line.trim()).filter(Boolean)
}

export function localizedCountry(country: string, locale: string): string {
  const trimmed = country.trim()
  if (locale === 'de') return COUNTRY_DE[trimmed.toLowerCase()] ?? trimmed
  return trimmed
}

export function legalOperator(config: Record<string, unknown>, locale: string): LegalOperator {
  const text = (key: string) => (typeof config[key] === 'string' ? (config[key] as string).trim() : '')
  const name = text('legalName')
  const address = splitAddress(text('legalAddress'))
  const email = text('legalEmail')
  return {
    name,
    address,
    email,
    vatId: text('legalVatId'),
    country: localizedCountry(text('legalCountry') || 'Austria', locale),
    configured: !!(name && address.length && email)
  }
}
