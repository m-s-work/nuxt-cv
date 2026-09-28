<script setup lang="ts">
// Shown when the API grants no access (no/invalid invite and no public profile) or is unreachable.
// Deliberately neutral: it does not reveal whose CV lives on this host.
const props = defineProps<{ error?: boolean }>()

const { t } = useI18n()
const { redeem, inviteRejected } = useCv()

const code = ref('')
const submitting = ref(false)

async function submit() {
  if (!code.value.trim() || submitting.value) return
  submitting.value = true
  if (await redeem(code.value)) {
    // Full reload so the whole page initializes with the new access.
    window.location.reload()
    return
  }
  submitting.value = false
}
</script>

<template>
  <div class="min-h-screen flex items-center justify-center bg-gray-50 dark:bg-gray-900 p-4">
    <div class="w-full max-w-sm space-y-6 text-center">
      <svg class="w-12 h-12 mx-auto text-gray-400 dark:text-gray-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z" />
      </svg>

      <div class="space-y-2">
        <h1 class="text-2xl font-bold text-gray-900 dark:text-white">
          {{ props.error ? t('noAccess.errorTitle') : t('noAccess.title') }}
        </h1>
        <p class="text-sm text-gray-600 dark:text-gray-400">
          {{ props.error ? t('noAccess.errorText') : t('noAccess.text') }}
        </p>
      </div>

      <form v-if="!props.error" class="space-y-3" @submit.prevent="submit">
        <label for="invite-code" class="sr-only">{{ t('noAccess.codeLabel') }}</label>
        <input
          id="invite-code"
          v-model="code"
          type="text"
          autocomplete="off"
          autocapitalize="off"
          spellcheck="false"
          :placeholder="t('noAccess.codeLabel')"
          class="w-full rounded-lg border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-800 px-4 py-2 text-gray-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-blue-500"
        >
        <p v-if="inviteRejected" class="text-sm text-red-600 dark:text-red-400" role="alert">
          {{ t('noAccess.invalid') }}
        </p>
        <button
          type="submit"
          :disabled="submitting || !code.trim()"
          class="w-full rounded-lg bg-blue-600 px-4 py-2 font-medium text-white hover:bg-blue-700 disabled:opacity-50"
        >
          {{ t('noAccess.submit') }}
        </button>
      </form>
    </div>
  </div>
</template>

<i18n lang="json">
{
  "en": {
    "noAccess": {
      "title": "Invitation required",
      "text": "This CV is only available with a personal invitation. Please use the link you received or enter your invite code.",
      "codeLabel": "Invite code",
      "submit": "Open CV",
      "invalid": "This invite code is not valid.",
      "errorTitle": "Something went wrong",
      "errorText": "The CV could not be loaded. Please try again later."
    }
  },
  "de": {
    "noAccess": {
      "title": "Einladung erforderlich",
      "text": "Dieser Lebenslauf ist nur mit einer persönlichen Einladung verfügbar. Bitte nutze den erhaltenen Link oder gib deinen Einladungscode ein.",
      "codeLabel": "Einladungscode",
      "submit": "Lebenslauf öffnen",
      "invalid": "Dieser Einladungscode ist nicht gültig.",
      "errorTitle": "Etwas ist schiefgelaufen",
      "errorText": "Der Lebenslauf konnte nicht geladen werden. Bitte versuche es später erneut."
    }
  }
}
</i18n>
