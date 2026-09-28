<script setup lang="ts">
import { errorMessage, shortSha, type AccessPolicy, type AdminTenant, type CvRevisions } from '~/composables/useAdmin'
import { printTemplates } from '~/utils/printTemplates'

const props = defineProps<{ tenant: AdminTenant, revisions?: CvRevisions | null }>()
const admin = useAdmin()

// Shared selection for both views.
const view = ref<'data' | 'pdf'>('data')
const profile = ref(props.tenant.publicProfile ?? props.tenant.profiles[0] ?? '')
const locale = ref(props.tenant.locales.includes(props.tenant.defaultLocale) ? props.tenant.defaultLocale : props.tenant.locales[0] ?? '')
// 'profile' = the profile's own pin (or current CV), 'current' = current CV, otherwise a revision SHA.
const revision = ref('profile')
const revisionQuery = computed(() => revision.value === 'profile' ? undefined : revision.value)

const viewItems = [
  { label: 'Data', value: 'data', icon: 'i-lucide-braces' },
  { label: 'PDF', value: 'pdf', icon: 'i-lucide-file-text' }
]
const profileItems = computed(() => props.tenant.profiles.map(p => ({ label: p, value: p })))
const localeItems = computed(() => props.tenant.locales.map(l => ({ label: l, value: l })))
const revisionItems = computed(() => [
  { label: 'As the profile sees it', value: 'profile' },
  { label: 'Current CV', value: 'current' },
  ...(props.revisions?.revisions ?? []).map(r => ({ label: `${shortSha(r.sha)}${r.message ? ` · ${r.message}` : ''}`, value: r.sha }))
])

// --- Data view: the redacted JSON exactly as delivered ------------------------------------

const profiles = ref<Record<string, AccessPolicy>>({})
const result = ref<{ locale: string, revision?: string, cv: unknown } | null>(null)
const loading = ref(false)
const error = ref('')
const json = computed(() => result.value ? JSON.stringify(result.value.cv, null, 2) : '')

let dataRequest = 0

async function loadData() {
  if (!profile.value) return
  const request = ++dataRequest
  loading.value = true
  error.value = ''
  try {
    const response = await admin.preview(props.tenant.id, profile.value, locale.value || undefined, revisionQuery.value)
    if (request === dataRequest) result.value = response
  } catch (e) {
    if (request !== dataRequest) return
    result.value = null
    error.value = errorMessage(e)
  } finally {
    if (request === dataRequest) loading.value = false
  }
}

// --- PDF view: rendered by the PDF container in any template -------------------------------

type TemplateName = keyof typeof printTemplates
// 'default' = the template the profile would get (tenant/profile setting).
const template = ref<'default' | TemplateName>('default')
const preset = ref('default')
const templateItems = [
  { label: 'Profile default', value: 'default' },
  ...Object.values(printTemplates).map(t => ({ label: t.title, value: t.name, description: t.description }))
]
const presetItems = computed(() => {
  const presets = template.value === 'default' ? undefined : printTemplates[template.value].vars?.presets
  return presets
    ? [{ label: 'Template default', value: 'default' }, ...Object.entries(presets).map(([value, p]) => ({ label: p.label, value }))]
    : []
})
watch(template, () => { preset.value = 'default' })

const pdfUrl = ref<string | null>(null)
const pdfLoading = ref(false)
const pdfError = ref('')

function setPdfUrl(url: string | null) {
  if (pdfUrl.value) URL.revokeObjectURL(pdfUrl.value)
  pdfUrl.value = url
}

// Renders take seconds and pickers can change meanwhile: only the latest request may update the view.
let pdfRequest = 0

async function renderPdf() {
  if (!profile.value) return
  const request = ++pdfRequest
  pdfLoading.value = true
  pdfError.value = ''
  try {
    const blob = await admin.pdfPreview(props.tenant.id, {
      profile: profile.value,
      locale: locale.value || undefined,
      template: template.value === 'default' ? undefined : template.value,
      vars: preset.value === 'default' ? undefined : JSON.stringify({ preset: preset.value }),
      revision: revisionQuery.value
    })
    if (request !== pdfRequest) return
    setPdfUrl(URL.createObjectURL(blob))
  } catch (e) {
    if (request !== pdfRequest) return
    setPdfUrl(null)
    const status = (e as { statusCode?: number, data?: unknown }).statusCode
    pdfError.value = status === 404
      ? 'No PDF: the PDF renderer is not configured, or there is no CV for this selection.'
      : errorMessage(e)
  } finally {
    if (request === pdfRequest) pdfLoading.value = false
  }
}

function load() {
  if (view.value === 'data') loadData()
  else renderPdf()
}

watch([profile, locale, revision, view], load)
// Template changes re-render only in the PDF view (rendering takes a few seconds).
watch([template, preset], () => { if (view.value === 'pdf') renderPdf() })
onMounted(async () => {
  load()
  try { profiles.value = await admin.profiles(props.tenant.id) } catch { /* definition is optional */ }
})
onBeforeUnmount(() => setPdfUrl(null))
</script>

<template>
  <div class="space-y-4">
    <div class="flex gap-3 items-end flex-wrap">
      <UTabs v-model="view" :items="viewItems" :content="false" size="sm" class="w-44" />
      <label class="text-sm space-y-1">
        <span class="text-gray-500 block">Profile</span>
        <USelect v-model="profile" :items="profileItems" class="min-w-40" aria-label="Preview profile" />
      </label>
      <label v-if="localeItems.length" class="text-sm space-y-1">
        <span class="text-gray-500 block">Locale</span>
        <USelect v-model="locale" :items="localeItems" class="min-w-24" aria-label="Preview locale" />
      </label>
      <label class="text-sm space-y-1">
        <span class="text-gray-500 block">CV version</span>
        <USelect v-model="revision" :items="revisionItems" class="min-w-56" aria-label="Preview CV version" />
      </label>
      <template v-if="view === 'pdf'">
        <label class="text-sm space-y-1">
          <span class="text-gray-500 block">Template</span>
          <USelect v-model="template" :items="templateItems" class="min-w-40" aria-label="PDF template" />
        </label>
        <label v-if="presetItems.length" class="text-sm space-y-1">
          <span class="text-gray-500 block">Colour set</span>
          <USelect v-model="preset" :items="presetItems" class="min-w-36" aria-label="PDF colour set" />
        </label>
      </template>
      <UButton
        icon="i-lucide-refresh-cw" color="neutral" variant="ghost" :aria-label="view === 'pdf' ? 'Render PDF again' : 'Reload preview'"
        :loading="view === 'pdf' ? pdfLoading : loading" @click="load"
      />
    </div>

    <div v-if="view === 'data'" class="grid gap-4 lg:grid-cols-[20rem_1fr]">
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
          <span v-if="result" class="font-normal text-gray-500">({{ result.locale }}{{ result.revision ? `, version ${shortSha(result.revision)}` : ', current CV' }})</span>
        </h3>
        <p v-if="error" class="text-sm text-red-600 dark:text-red-400">{{ error }}</p>
        <pre v-else class="text-xs overflow-auto max-h-[65vh] bg-gray-50 dark:bg-gray-950 rounded p-3" data-testid="preview-json">{{ json }}</pre>
      </section>
    </div>

    <section v-else class="rounded-lg border border-gray-200 dark:border-gray-800 bg-white dark:bg-gray-900 p-3">
      <p v-if="pdfLoading" class="text-sm text-gray-500 mb-2">Rendering PDF, this can take a few seconds…</p>
      <p v-if="pdfError" class="text-sm text-red-600 dark:text-red-400" data-testid="pdf-preview-error">{{ pdfError }}</p>
      <iframe
        v-else-if="pdfUrl" :src="pdfUrl" title="PDF preview" class="w-full h-[75vh] rounded border border-gray-200 dark:border-gray-800"
        data-testid="pdf-preview"
      />
      <p class="text-xs text-gray-500 mt-2">Preview only: not cached, not sent to anyone. Invites get their PDF via their own settings.</p>
    </section>
  </div>
</template>
