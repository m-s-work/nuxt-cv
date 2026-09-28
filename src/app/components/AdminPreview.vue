<script setup lang="ts">
import { errorMessage, type AccessPolicy, type AdminTenant } from '~/composables/useAdmin'

const props = defineProps<{ tenant: AdminTenant }>()
const admin = useAdmin()

const profile = ref(props.tenant.publicProfile ?? props.tenant.profiles[0] ?? '')
const locale = ref(props.tenant.locales.includes(props.tenant.defaultLocale) ? props.tenant.defaultLocale : props.tenant.locales[0] ?? '')
const profiles = ref<Record<string, AccessPolicy>>({})
const result = ref<{ locale: string, cv: unknown } | null>(null)
const loading = ref(false)
const error = ref('')

const profileItems = computed(() => props.tenant.profiles.map(p => ({ label: p, value: p })))
const localeItems = computed(() => props.tenant.locales.map(l => ({ label: l, value: l })))
const json = computed(() => result.value ? JSON.stringify(result.value.cv, null, 2) : '')

async function load() {
  if (!profile.value) return
  loading.value = true
  error.value = ''
  try {
    result.value = await admin.preview(props.tenant.id, profile.value, locale.value || undefined)
  } catch (e) {
    result.value = null
    error.value = errorMessage(e)
  } finally {
    loading.value = false
  }
}

watch([profile, locale], load)
onMounted(async () => {
  load()
  try { profiles.value = await admin.profiles(props.tenant.id) } catch { /* definition is optional */ }
})
</script>

<template>
  <div class="space-y-4">
    <div class="flex gap-3 items-end flex-wrap">
      <label class="text-sm space-y-1">
        <span class="text-gray-500 block">Profile</span>
        <USelect v-model="profile" :items="profileItems" class="min-w-40" />
      </label>
      <label v-if="localeItems.length" class="text-sm space-y-1">
        <span class="text-gray-500 block">Locale</span>
        <USelect v-model="locale" :items="localeItems" class="min-w-24" />
      </label>
      <UButton icon="i-lucide-refresh-cw" color="neutral" variant="ghost" aria-label="Reload preview" :loading="loading" @click="load" />
    </div>

    <div class="grid gap-4 lg:grid-cols-[20rem_1fr]">
      <section class="rounded-lg border border-gray-200 dark:border-gray-800 bg-white dark:bg-gray-900 p-3 text-sm">
        <h3 class="font-semibold mb-2">Profile definition</h3>
        <pre class="text-xs whitespace-pre-wrap break-all">{{ JSON.stringify(profiles[profile] ?? {}, null, 2) }}</pre>
        <p v-if="tenant.publicProfile?.toLowerCase() === profile.toLowerCase()" class="mt-2 text-xs text-amber-600">
          This is the public profile: visible without invite on the tenant's own hosts. Flags it does not set
          are treated as <strong>hide</strong>; set a flag to <code>false</code> to show that data.
        </p>
      </section>
      <section class="rounded-lg border border-gray-200 dark:border-gray-800 bg-white dark:bg-gray-900 p-3 min-w-0">
        <h3 class="font-semibold text-sm mb-2">
          Redacted CV as delivered to this profile
          <span v-if="result" class="font-normal text-gray-500">({{ result.locale }})</span>
        </h3>
        <p v-if="error" class="text-sm text-red-600 dark:text-red-400">{{ error }}</p>
        <pre v-else class="text-xs overflow-auto max-h-[65vh] bg-gray-50 dark:bg-gray-950 rounded p-3" data-testid="preview-json">{{ json }}</pre>
      </section>
    </div>
  </div>
</template>
