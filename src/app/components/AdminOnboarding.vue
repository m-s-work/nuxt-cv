<script setup lang="ts">
/**
 * First sign-in (docs/REQUIREMENTS_SAAS.md §3): choose a handle, display name and CV language, create the tenant,
 * then pick a starting point: the empty starter CV, a LinkedIn data export or a CV JSON file.
 */
import { errorMessage, parseJsonc, type AccountUser } from '~/composables/useAdmin'
import { HANDLE_PATTERN, accountErrorText } from '~/utils/account'

const props = defineProps<{ user: AccountUser }>()
/** created: the tenant exists now; finish: go to the dashboard tab. */
const emit = defineEmits<{ created: [tenantId: string], finish: [tab: string] }>()
const admin = useAdmin()

const step = ref<'handle' | 'start'>(props.user.tenantId ? 'start' : 'handle')
const tenantId = ref(props.user.tenantId ?? '')

// --- Step 1: handle, name, language ----------------------------------------------------------

const suggested = (props.user.name || props.user.email.split('@')[0] || '')
  .normalize('NFKD').replace(/[̀-ͯ]/g, '').toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '').slice(0, 31)
const handle = ref(HANDLE_PATTERN.test(suggested) ? suggested : '')
const name = ref(props.user.name ?? '')
const locale = ref<'en' | 'de'>(import.meta.client && navigator.language?.toLowerCase().startsWith('de') ? 'de' : 'en')
const localeItems = [{ label: 'English', value: 'en' }, { label: 'Deutsch', value: 'de' }]

/** Availability: null = not checked yet, '' = available, otherwise the API's error code. */
const handleState = ref<string | null>(null)
const checkingHandle = ref(false)
let timer: ReturnType<typeof setTimeout> | undefined
let seq = 0

watch(handle, (value) => {
  const normalized = value.trim().toLowerCase()
  if (normalized !== value) { handle.value = normalized; return }
  clearTimeout(timer)
  handleState.value = null
  if (!normalized) return
  if (!HANDLE_PATTERN.test(normalized)) { handleState.value = 'invalid_handle'; return }
  checkingHandle.value = true
  const mine = ++seq
  timer = setTimeout(async () => {
    try {
      const result = await admin.checkHandle(normalized)
      if (mine === seq) handleState.value = result.error ?? ''
    } catch (e) {
      if (mine === seq) handleState.value = errorMessage(e)
    } finally {
      if (mine === seq) checkingHandle.value = false
    }
  }, 350)
}, { immediate: true })

const creating = ref(false)
const createError = ref('')

async function create() {
  if (handleState.value !== '' || creating.value) return
  creating.value = true
  createError.value = ''
  try {
    const result = await admin.createTenant({ handle: handle.value, locale: locale.value, name: name.value.trim() || undefined })
    tenantId.value = result.tenantId
    step.value = 'start'
    emit('created', result.tenantId)
  } catch (e) {
    const code = (e as { data?: { error?: string } }).data?.error
    createError.value = code ? accountErrorText(code) : errorMessage(e)
    if (code?.startsWith('handle_') || code === 'invalid_handle') handleState.value = code
  } finally {
    creating.value = false
  }
}

// --- Step 2: starting point --------------------------------------------------------------------

const choice = ref<'starter' | 'linkedin' | 'json' | null>(null)
const jsonError = ref('')
const jsonBusy = ref(false)
const jsonInput = ref<HTMLInputElement | null>(null)

async function uploadJson(event: Event) {
  const target = event.target as HTMLInputElement
  const file = target.files?.[0]
  target.value = ''
  if (!file) return
  jsonError.value = ''
  jsonBusy.value = true
  try {
    const text = await file.text()
    const parsed = parseJsonc(text)
    if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) throw new SyntaxError('The file must contain a JSON object.')
    await admin.writeFile(tenantId.value, `cv.${locale.value}.json`, text)
    emit('finish', 'preview')
  } catch (e) {
    jsonError.value = e instanceof SyntaxError ? `Invalid JSON: ${e.message}` : errorMessage(e)
  } finally {
    jsonBusy.value = false
  }
}

const cards = [
  { value: 'starter', icon: 'i-lucide-file-text', title: 'Start with the empty CV', text: 'A starter CV with your name. Fill it in the Files tab.' },
  { value: 'linkedin', icon: 'i-lucide-linkedin', title: 'Import from LinkedIn', text: 'Upload your LinkedIn data export (ZIP): positions, education, skills and more.' },
  { value: 'json', icon: 'i-lucide-file-json', title: 'Upload a CV JSON file', text: 'Already have a cv.<locale>.json, e.g. from another instance? Upload it.' }
] as const
</script>

<template>
  <div class="max-w-2xl mx-auto mt-6 space-y-6" data-testid="onboarding">
    <div class="space-y-1">
      <p class="text-sm text-primary font-medium">Step {{ step === 'handle' ? 1 : 2 }} of 2</p>
      <h2 class="text-2xl font-semibold">{{ step === 'handle' ? 'Welcome! Let’s create your CV' : 'How do you want to start?' }}</h2>
      <p class="text-sm text-gray-500">
        {{ step === 'handle'
          ? 'Choose a handle: it names your CV and cannot be changed later.'
          : `Your CV "${tenantId}" is ready. Pick a starting point – you can change everything later.` }}
      </p>
    </div>

    <form v-if="step === 'handle'" class="rounded-lg border border-gray-200 dark:border-gray-800 bg-white dark:bg-gray-900 p-5 space-y-4" @submit.prevent="create">
      <UFormField label="Handle" :hint="`${handle.length}/31`" required>
        <UInput v-model="handle" placeholder="jane-doe" class="w-full" autocomplete="off" spellcheck="false" data-testid="handle-input" :loading="checkingHandle">
          <template #trailing>
            <UIcon v-if="handleState === ''" name="i-lucide-circle-check" class="text-green-600 size-5" />
            <UIcon v-else-if="handleState" name="i-lucide-circle-alert" class="text-red-600 size-5" />
          </template>
        </UInput>
        <template #help>
          <span v-if="handleState === ''" class="text-green-700 dark:text-green-400" data-testid="handle-available">“{{ handle }}” is available.</span>
          <span v-else-if="handleState" class="text-red-600 dark:text-red-400" data-testid="handle-error">{{ accountErrorText(handleState) }}</span>
          <span v-else>Lowercase letters, digits and dashes, 3–31 characters.</span>
        </template>
      </UFormField>
      <UFormField label="Display name" help="Shown on your CV; you can change it later.">
        <UInput v-model="name" placeholder="Jane Doe" class="w-full" autocomplete="name" />
      </UFormField>
      <UFormField label="CV language" help="The language of your first CV. You can add more languages later.">
        <USelect v-model="locale" :items="localeItems" class="w-48" />
      </UFormField>
      <p v-if="createError" class="text-sm text-red-600 dark:text-red-400">{{ createError }}</p>
      <UButton type="submit" label="Create my CV" icon="i-lucide-arrow-right" trailing :loading="creating" :disabled="handleState !== '' || checkingHandle" />
    </form>

    <template v-else>
      <div class="grid gap-3 sm:grid-cols-3">
        <button
          v-for="card in cards" :key="card.value" type="button"
          class="text-left rounded-lg border bg-white dark:bg-gray-900 p-4 space-y-2 transition hover:border-primary"
          :class="choice === card.value ? 'border-primary ring-2 ring-primary/30' : 'border-gray-200 dark:border-gray-800'"
          :data-testid="`start-${card.value}`"
          @click="choice = card.value"
        >
          <UIcon :name="card.icon" class="size-6 text-primary" />
          <div class="font-medium">{{ card.title }}</div>
          <p class="text-xs text-gray-500">{{ card.text }}</p>
        </button>
      </div>

      <div v-if="choice" class="rounded-lg border border-gray-200 dark:border-gray-800 bg-white dark:bg-gray-900 p-5">
        <div v-if="choice === 'starter'" class="space-y-3 text-sm">
          <p>Your starter CV <code>cv.{{ locale }}.json</code> contains your name. Edit it in the <b>Files</b> tab, check it in <b>Preview</b>, and share it with invites.</p>
          <UButton label="Go to my dashboard" icon="i-lucide-arrow-right" trailing @click="emit('finish', 'files')" />
        </div>
        <AdminLinkedInImport v-else-if="choice === 'linkedin'" :locale="locale" replaces @applied="emit('finish', 'preview')" />
        <div v-else class="space-y-3 text-sm">
          <p>Upload a CV file in this app's JSON format. It is saved as <code>cv.{{ locale }}.json</code> and replaces the starter CV.</p>
          <UButton icon="i-lucide-upload" label="Choose JSON file…" color="neutral" variant="outline" :loading="jsonBusy" @click="jsonInput?.click()" />
          <input ref="jsonInput" type="file" accept=".json,application/json" class="hidden" @change="uploadJson">
          <p v-if="jsonError" class="text-red-600 dark:text-red-400">{{ jsonError }}</p>
        </div>
      </div>
      <div class="text-right">
        <UButton color="neutral" variant="link" label="Skip for now" @click="emit('finish', 'invites')" />
      </div>
    </template>
  </div>
</template>
