import { legalOperator } from '~/utils/legal'

/** Operator details for the legal pages, from NUXT_PUBLIC_LEGAL_* (build time), localized country name. */
export function useLegalOperator() {
  const { locale } = useI18n()
  const config = useRuntimeConfig().public as Record<string, unknown>
  return computed(() => legalOperator(config, locale.value))
}
