<script setup lang="ts">
// Invite code entry, used on the no-access page and the showcase.
const { t } = useI18n()
const { redeem, inviteRejected, hostKind } = useCv()
const localePath = useLocalePath()
const router = useRouter()

const code = ref('')
const submitting = ref(false)

async function submit() {
  if (!code.value.trim() || submitting.value) return
  submitting.value = true
  if (await redeem(code.value)) {
    // Full load so the whole page initializes with the new access. On the shared host the CV lives at /cv
    // ("/" is the showcase there).
    if (hostKind.value === 'shared') window.location.assign(router.resolve(localePath('/cv')).href)
    else window.location.reload()
    return
  }
  submitting.value = false
}
</script>

<template>
  <form class="space-y-3" @submit.prevent="submit">
    <label for="invite-code" class="sr-only">{{ t('invite.codeLabel') }}</label>
    <input
      id="invite-code"
      v-model="code"
      type="text"
      autocomplete="off"
      autocapitalize="off"
      spellcheck="false"
      :placeholder="t('invite.codeLabel')"
      class="w-full rounded-lg border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-800 px-4 py-2 text-gray-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-blue-500"
    >
    <p v-if="inviteRejected" class="text-sm text-red-600 dark:text-red-400" role="alert">
      {{ t('invite.invalid') }}
    </p>
    <button
      type="submit"
      :disabled="submitting || !code.trim()"
      class="w-full rounded-lg bg-blue-600 px-4 py-2 font-medium text-white hover:bg-blue-700 disabled:opacity-50"
    >
      {{ t('invite.submit') }}
    </button>
  </form>
</template>

<i18n lang="json">
{
  "en": {
    "invite": {
      "codeLabel": "Invite code",
      "submit": "Open CV",
      "invalid": "This invite code is not valid."
    }
  },
  "de": {
    "invite": {
      "codeLabel": "Einladungscode",
      "submit": "Lebenslauf öffnen",
      "invalid": "Dieser Einladungscode ist nicht gültig."
    }
  }
}
</i18n>
