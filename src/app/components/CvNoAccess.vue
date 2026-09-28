<script setup lang="ts">
// Shown on a tenant's own host when the API grants no access, or when the API is unreachable.
// Deliberately neutral: it does not reveal whose CV lives on this host.
const props = defineProps<{ error?: boolean }>()

const { t } = useI18n()
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

      <CvInviteForm v-if="!props.error" />
    </div>
  </div>
</template>

<i18n lang="json">
{
  "en": {
    "noAccess": {
      "title": "Invitation required",
      "text": "This CV is only available with a personal invitation. Please use the link you received or enter your invite code.",
      "errorTitle": "Something went wrong",
      "errorText": "The CV could not be loaded. Please try again later."
    }
  },
  "de": {
    "noAccess": {
      "title": "Einladung erforderlich",
      "text": "Dieser Lebenslauf ist nur mit einer persönlichen Einladung verfügbar. Bitte nutze den erhaltenen Link oder gib deinen Einladungscode ein.",
      "errorTitle": "Etwas ist schiefgelaufen",
      "errorText": "Der Lebenslauf konnte nicht geladen werden. Bitte versuche es später erneut."
    }
  }
}
</i18n>
