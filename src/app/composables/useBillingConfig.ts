import { normalizeBillingConfig, type BillingConfig } from '~/utils/pricing'

/**
 * Public prices for the showcase and /pricing: GET /api/billing/config (docs/REQUIREMENTS_SAAS.md §5.1).
 * Starts with the default passes, so the page never waits for or breaks on the API; `loaded` turns true once
 * the request finished (successfully or not). Loaded once per page load.
 */
export function useBillingConfig() {
  const config = useState<BillingConfig>('billing-config', () => normalizeBillingConfig(null))
  const loaded = useState<boolean>('billing-config-loaded', () => false)
  const started = useState<boolean>('billing-config-started', () => false)
  const apiBase = useRuntimeConfig().public.apiBase as string

  if (import.meta.client && !started.value) {
    started.value = true
    $fetch<unknown>(`${apiBase}/billing/config`)
      .then(response => { config.value = normalizeBillingConfig(response) })
      .catch(() => { /* keep the defaults */ })
      .finally(() => { loaded.value = true })
  }

  return { config: readonly(config), loaded: readonly(loaded) }
}
