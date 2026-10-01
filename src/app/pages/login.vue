<script setup lang="ts">
/**
 * Sign-in page (docs/REQUIREMENTS_SAAS.md §2): the configured OAuth providers and, if enabled, a magic link by e-mail.
 * Public but noindex; uses the admin page's Nuxt UI theme (own CSS chunk, R13.4) and has its own language switch.
 */
import '~/assets/css/admin.css'
import { loginErrorKey } from '~/utils/account'

const { t, locale } = useI18n({ useScope: 'local' })
const route = useRoute()
const localePath = useLocalePath()
const switchLocalePath = useSwitchLocalePath()
const apiBase = useRuntimeConfig().public.apiBase as string

useSeoMeta({ title: () => t('login.metaTitle'), robots: 'noindex, nofollow' })
useHead({ htmlAttrs: { lang: () => locale.value } })

/** After sign-in the API redirects here (a local path, S2.6). */
const RETURN_URL = '/admin'

const PROVIDERS: Record<string, { label: string, icon?: string }> = {
  google: { label: 'Google' },
  microsoft: { label: 'Microsoft' },
  github: { label: 'GitHub', icon: 'i-lucide-github' },
  linkedin: { label: 'LinkedIn', icon: 'i-lucide-linkedin' }
}

const providers = ref<string[]>([])
const magicLink = ref(false)
const loading = ref(true)
const unavailable = ref(false)

const errorKey = computed(() => loginErrorKey(typeof route.query.error === 'string' ? route.query.error : null))

const email = ref('')
const sending = ref(false)
const sent = ref(false)
const sendError = ref('')

function loginUrl(provider: string) {
  return `${apiBase}/auth/login/${encodeURIComponent(provider)}?returnUrl=${encodeURIComponent(RETURN_URL)}`
}

async function sendLink() {
  if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.value.trim())) {
    sendError.value = t('login.invalidEmail')
    return
  }
  sending.value = true
  sendError.value = ''
  try {
    await $fetch(`${apiBase}/auth/magic-link`, {
      method: 'POST',
      body: { email: email.value.trim(), returnUrl: RETURN_URL },
      headers: { 'X-Requested-With': 'cv' }
    })
    sent.value = true
  } catch (e) {
    const status = (e as { statusCode?: number }).statusCode
    sendError.value = status === 429 ? t('login.tooMany') : status === 400 ? t('login.invalidEmail') : t('login.sendFailed')
  } finally {
    sending.value = false
  }
}

onMounted(async () => {
  try {
    // Already signed in: straight to the dashboard (unless an error is shown).
    if (!errorKey.value) {
      const me = await $fetch(`${apiBase}/auth/me`, { credentials: 'same-origin' }).catch(() => null)
      if (me) return navigateTo(RETURN_URL)
    }
    const result = await $fetch<{ providers: string[], magicLink: boolean }>(`${apiBase}/auth/providers`)
    providers.value = result.providers.filter(p => p in PROVIDERS)
    magicLink.value = result.magicLink
  } catch {
    unavailable.value = true
  } finally {
    loading.value = false
  }
})
</script>

<template>
  <UApp>
    <div class="min-h-screen bg-gray-50 dark:bg-gray-950 text-gray-900 dark:text-gray-100 flex flex-col">
      <header class="max-w-md w-full mx-auto px-4 pt-6 flex items-center">
        <NuxtLink :to="localePath('/')" class="font-semibold flex items-center gap-2">
          <UIcon name="i-lucide-file-user" class="size-5 text-primary" />
          {{ t('login.brand') }}
        </NuxtLink>
        <nav class="ml-auto flex gap-1 text-sm" :aria-label="t('login.language')">
          <NuxtLink
            v-for="code in ['en', 'de']" :key="code" :to="{ path: switchLocalePath(code), query: route.query }"
            class="px-2 py-1 rounded" :class="locale === code ? 'bg-gray-200 dark:bg-gray-800 font-medium' : 'text-gray-500 hover:text-gray-900 dark:hover:text-gray-100'"
            :aria-current="locale === code ? 'true' : undefined"
          >
            {{ code.toUpperCase() }}
          </NuxtLink>
        </nav>
      </header>

      <main class="flex-1 max-w-md w-full mx-auto px-4 py-10">
        <div class="rounded-xl border border-gray-200 dark:border-gray-800 bg-white dark:bg-gray-900 p-6 sm:p-8 space-y-6 shadow-sm">
          <div class="space-y-1">
            <h1 class="text-2xl font-semibold">{{ t('login.title') }}</h1>
            <p class="text-sm text-gray-500">{{ t('login.subtitle') }}</p>
          </div>

          <UAlert v-if="errorKey" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="t(errorKey)" data-testid="login-error" />

          <div v-if="loading" class="text-sm text-gray-500 text-center py-6">{{ t('login.loading') }}</div>
          <UAlert v-else-if="unavailable" color="warning" variant="subtle" :description="t('login.unavailable')" />

          <template v-else>
            <div v-if="providers.length" class="space-y-2">
              <UButton
                v-for="p in providers" :key="p" :href="loginUrl(p)" external block size="lg" color="neutral" variant="outline"
                :data-testid="`login-${p}`"
              >
                <svg v-if="p === 'google'" viewBox="0 0 24 24" class="size-5" aria-hidden="true">
                  <path fill="#EA4335" d="M12 10.2v3.9h5.5c-.2 1.3-1.6 3.8-5.5 3.8-3.3 0-6-2.7-6-6.1s2.7-6.1 6-6.1c1.9 0 3.1.8 3.8 1.5l2.6-2.5C16.8 3.2 14.6 2.2 12 2.2 6.6 2.2 2.2 6.6 2.2 12s4.4 9.8 9.8 9.8c5.7 0 9.4-4 9.4-9.6 0-.6-.1-1.1-.2-1.6H12z" />
                </svg>
                <svg v-else-if="p === 'microsoft'" viewBox="0 0 24 24" class="size-5" aria-hidden="true">
                  <path fill="#F25022" d="M2 2h9.5v9.5H2z" /><path fill="#7FBA00" d="M12.5 2H22v9.5h-9.5z" />
                  <path fill="#00A4EF" d="M2 12.5h9.5V22H2z" /><path fill="#FFB900" d="M12.5 12.5H22V22h-9.5z" />
                </svg>
                <UIcon v-else-if="PROVIDERS[p]?.icon" :name="PROVIDERS[p]!.icon!" class="size-5" />
                <span>{{ t('login.continueWith', { provider: PROVIDERS[p]?.label ?? p }) }}</span>
              </UButton>
            </div>

            <div v-if="providers.length && magicLink" class="flex items-center gap-3 text-xs text-gray-400">
              <span class="h-px flex-1 bg-gray-200 dark:bg-gray-800" />{{ t('login.or') }}<span class="h-px flex-1 bg-gray-200 dark:bg-gray-800" />
            </div>

            <div v-if="magicLink">
              <div v-if="sent" class="text-center space-y-2 py-2" data-testid="magic-link-sent">
                <UIcon name="i-lucide-mail-check" class="size-10 text-primary" />
                <h2 class="font-semibold">{{ t('login.checkInbox') }}</h2>
                <p class="text-sm text-gray-500">{{ t('login.sentTo', { email }) }}</p>
                <UButton size="sm" color="neutral" variant="link" :label="t('login.otherAddress')" @click="sent = false" />
              </div>
              <form v-else class="space-y-2" @submit.prevent="sendLink">
                <label for="login-email" class="text-sm font-medium">{{ t('login.emailLabel') }}</label>
                <UInput
                  id="login-email" v-model="email" type="email" autocomplete="email" size="lg" class="w-full"
                  :placeholder="t('login.emailPlaceholder')" icon="i-lucide-mail"
                />
                <p v-if="sendError" class="text-sm text-red-600 dark:text-red-400">{{ sendError }}</p>
                <UButton type="submit" block size="lg" :loading="sending" :label="t('login.sendLink')" />
                <p class="text-xs text-gray-500">{{ t('login.magicHint') }}</p>
              </form>
            </div>

            <p v-if="!providers.length && !magicLink" class="text-sm text-gray-500">{{ t('login.noMethods') }}</p>
          </template>

          <p class="text-xs text-gray-500 border-t border-gray-200 dark:border-gray-800 pt-4">
            <i18n-t keypath="login.legal" tag="span">
              <template #terms>
                <NuxtLink :to="localePath('/legal/terms')" class="underline hover:text-gray-900 dark:hover:text-gray-100">{{ t('login.terms') }}</NuxtLink>
              </template>
              <template #privacy>
                <NuxtLink :to="localePath('/legal/privacy')" class="underline hover:text-gray-900 dark:hover:text-gray-100">{{ t('login.privacy') }}</NuxtLink>
              </template>
            </i18n-t>
          </p>
        </div>
      </main>
    </div>
  </UApp>
</template>

<i18n lang="json">
{
  "en": {
    "login": {
      "metaTitle": "Sign in",
      "brand": "CV",
      "language": "Language",
      "title": "Sign in",
      "subtitle": "Create your CV or continue where you left off. New here? Signing in creates your free account.",
      "loading": "Loading…",
      "unavailable": "Sign-in is not available right now. Please try again later.",
      "continueWith": "Continue with {provider}",
      "or": "or",
      "emailLabel": "E-mail address",
      "emailPlaceholder": "you{'@'}example.com",
      "sendLink": "Send me a sign-in link",
      "magicHint": "We e-mail you a link that signs you in. It is valid for 15 minutes and can be used once.",
      "checkInbox": "Check your inbox",
      "sentTo": "If {email} can sign in, a link is on its way. Open it on this device.",
      "otherAddress": "Use another address",
      "invalidEmail": "Please enter a valid e-mail address.",
      "tooMany": "Too many attempts. Please wait a minute and try again.",
      "sendFailed": "The link could not be sent. Please try again.",
      "noMethods": "No sign-in method is configured on this server.",
      "legal": "By signing in you agree to the {terms} and acknowledge the {privacy}.",
      "terms": "terms of service",
      "privacy": "privacy policy",
      "errors": {
        "cancelled": "Sign-in was cancelled.",
        "provider_failed": "Sign-in with this provider failed. Please try again or use another method.",
        "account_exists": "An account with this e-mail address already exists. Sign in with the method you used first.",
        "blocked": "This account is blocked. Please contact the operator.",
        "no_email": "The provider did not share an e-mail address. Please allow access to your e-mail address or use another method.",
        "link_invalid": "This sign-in link is invalid, expired or was already used. Request a new one."
      }
    }
  },
  "de": {
    "login": {
      "metaTitle": "Anmelden",
      "brand": "CV",
      "language": "Sprache",
      "title": "Anmelden",
      "subtitle": "Erstellen Sie Ihren Lebenslauf oder machen Sie dort weiter, wo Sie aufgehört haben. Neu hier? Mit der Anmeldung entsteht Ihr kostenloses Konto.",
      "loading": "Wird geladen…",
      "unavailable": "Die Anmeldung ist gerade nicht verfügbar. Bitte versuchen Sie es später erneut.",
      "continueWith": "Weiter mit {provider}",
      "or": "oder",
      "emailLabel": "E-Mail-Adresse",
      "emailPlaceholder": "sie{'@'}beispiel.de",
      "sendLink": "Anmeldelink senden",
      "magicHint": "Wir senden Ihnen einen Link, der Sie anmeldet. Er ist 15 Minuten gültig und nur einmal verwendbar.",
      "checkInbox": "Sehen Sie in Ihr Postfach",
      "sentTo": "Falls sich {email} anmelden kann, ist ein Link unterwegs. Öffnen Sie ihn auf diesem Gerät.",
      "otherAddress": "Andere Adresse verwenden",
      "invalidEmail": "Bitte geben Sie eine gültige E-Mail-Adresse ein.",
      "tooMany": "Zu viele Versuche. Bitte warten Sie eine Minute und versuchen Sie es erneut.",
      "sendFailed": "Der Link konnte nicht gesendet werden. Bitte versuchen Sie es erneut.",
      "noMethods": "Auf diesem Server ist keine Anmeldemethode eingerichtet.",
      "legal": "Mit der Anmeldung akzeptieren Sie die {terms} und nehmen die {privacy} zur Kenntnis.",
      "terms": "Nutzungsbedingungen",
      "privacy": "Datenschutzerklärung",
      "errors": {
        "cancelled": "Die Anmeldung wurde abgebrochen.",
        "provider_failed": "Die Anmeldung über diesen Anbieter ist fehlgeschlagen. Bitte versuchen Sie es erneut oder wählen Sie eine andere Methode.",
        "account_exists": "Es gibt bereits ein Konto mit dieser E-Mail-Adresse. Melden Sie sich mit der Methode an, die Sie zuerst verwendet haben.",
        "blocked": "Dieses Konto ist gesperrt. Bitte wenden Sie sich an den Betreiber.",
        "no_email": "Der Anbieter hat keine E-Mail-Adresse übermittelt. Bitte erlauben Sie den Zugriff auf Ihre E-Mail-Adresse oder wählen Sie eine andere Methode.",
        "link_invalid": "Dieser Anmeldelink ist ungültig, abgelaufen oder wurde bereits verwendet. Fordern Sie einen neuen an."
      }
    }
  }
}
</i18n>
