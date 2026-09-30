<script setup lang="ts">
/**
 * Edit tab of the admin page: graphical CV editor next to a preview of what a profile gets – as the web page,
 * as the redacted data or as the PDF. Unsaved edits are previewed right away (redacted by the API, not stored).
 */
import { errorMessage, shortSha, type AccessPolicy, type AdminTenant, type CvRevisions } from '~/composables/useAdmin'
import type { CvPreviewMessage, CvData } from '~/composables/useCv'
import { printTemplates } from '~/utils/printTemplates'

const props = defineProps<{ tenant: AdminTenant, revisions?: CvRevisions | null }>()
const emit = defineEmits<{ 'revisions-changed': [], 'dirty': [dirty: boolean] }>()
const admin = useAdmin()

// --- Selection shared by editor and preview ----------------------------------------------

const view = ref<'web' | 'data' | 'pdf'>('web')
const showEditor = ref(true)
const profile = ref(props.tenant.publicProfile ?? props.tenant.profiles[0] ?? '')
const locale = ref(props.tenant.locales.includes(props.tenant.defaultLocale) ? props.tenant.defaultLocale : props.tenant.locales[0] ?? props.tenant.defaultLocale)
// 'profile' = the profile's own pin (or current CV), 'current' = current CV, otherwise a revision SHA.
const revision = ref('profile')
const revisionQuery = computed(() => revision.value === 'profile' ? undefined : revision.value)

/** Unsaved editor draft (null = the saved file is shown). */
const draft = ref<Record<string, unknown> | null>(null)
watch(() => !!draft.value, dirty => emit('dirty', dirty))

// The editor edits one locale's file: switching locales discards its unsaved changes (after asking).
const localeModel = computed({
  get: () => locale.value,
  set: (value: string) => {
    if (draft.value && !confirm(`Discard unsaved changes to cv.${locale.value}.json?`)) return
    draft.value = null
    locale.value = value
  }
})

const viewItems = [
  { label: 'Web', value: 'web', icon: 'i-lucide-globe' },
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
const pin = computed(() => props.tenant.pins?.[profile.value])

// --- Web and data view: the redacted CV exactly as delivered --------------------------------

const profiles = ref<Record<string, AccessPolicy>>({})
const result = ref<{ locale?: string, revision?: string | null, cv: unknown, draft: boolean } | null>(null)
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
    const response = draft.value
      ? { ...await admin.previewDraft(props.tenant.id, profile.value, draft.value, locale.value), draft: true }
      : { ...await admin.preview(props.tenant.id, profile.value, locale.value || undefined, revisionQuery.value), draft: false }
    if (request !== dataRequest) return
    result.value = response
    postToFrame()
  } catch (e) {
    if (request !== dataRequest) return
    result.value = null
    error.value = errorMessage(e)
  } finally {
    if (request === dataRequest) loading.value = false
  }
}

// The web view is the real CV page in an iframe (/?preview=1); it asks for its data once it is ready.
const frame = ref<HTMLIFrameElement | null>(null)
const frameReady = ref(false)
const frameSrc = computed(() => `${locale.value === 'de' ? '/de' : ''}/?preview=1`)
watch([frameSrc, view], () => { frameReady.value = false })

function postToFrame() {
  const target = frame.value?.contentWindow
  if (!target || !frameReady.value || !result.value) return
  const message: CvPreviewMessage = {
    type: 'cv-preview',
    tenant: props.tenant.id,
    locale: result.value.locale ?? locale.value,
    cv: JSON.parse(JSON.stringify(result.value.cv)) as CvData
  }
  target.postMessage(message, window.location.origin)
}

function onMessage(event: MessageEvent) {
  if (event.origin !== window.location.origin || event.source !== frame.value?.contentWindow) return
  if (event.data?.type === 'cv-preview-ready') {
    frameReady.value = true
    postToFrame()
  }
}

// Device width of the web view; wider layouts are scaled down to fit the column.
const width = ref<'fit' | 390 | 768 | 1280>('fit')
const widthItems = [
  { label: 'Fit', value: 'fit', icon: 'i-lucide-maximize-2' },
  { label: 'Phone', value: 390, icon: 'i-lucide-smartphone' },
  { label: 'Tablet', value: 768, icon: 'i-lucide-tablet' },
  { label: 'Desktop', value: 1280, icon: 'i-lucide-monitor' }
]
const frameBox = ref<HTMLElement | null>(null)
const boxWidth = ref(0)
let observer: ResizeObserver | undefined
watch(frameBox, (box) => {
  observer?.disconnect()
  if (!box) return
  observer = new ResizeObserver(([entry]) => { boxWidth.value = entry?.contentRect.width ?? 0 })
  observer.observe(box)
})
const scale = computed(() => width.value === 'fit' || !boxWidth.value ? 1 : Math.min(1, boxWidth.value / width.value))
const frameStyle = computed(() => width.value === 'fit'
  ? { width: '100%', height: '100%' }
  : { width: `${width.value}px`, height: `${100 / scale.value}%`, transform: `scale(${scale.value})`, transformOrigin: 'top left' })

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
  if (view.value === 'pdf') renderPdf()
  else loadData()
}

function onSaved() {
  draft.value = null
  emit('revisions-changed')
  load()
}

watch([profile, locale, revision, view], load)
// Template changes re-render only in the PDF view (rendering takes a few seconds).
watch([template, preset], () => { if (view.value === 'pdf') renderPdf() })
// Edits: the web / data view follows while typing (the PDF only shows saved files).
let draftTimer: ReturnType<typeof setTimeout> | undefined
watch(draft, () => {
  clearTimeout(draftTimer)
  if (view.value !== 'pdf') draftTimer = setTimeout(loadData, 350)
})

onMounted(async () => {
  window.addEventListener('message', onMessage)
  load()
  try { profiles.value = await admin.profiles(props.tenant.id) } catch { /* definition is optional */ }
})
onBeforeUnmount(() => {
  window.removeEventListener('message', onMessage)
  observer?.disconnect()
  clearTimeout(draftTimer)
  setPdfUrl(null)
})
</script>

<template>
  <div class="space-y-4">
    <div class="flex gap-3 items-end flex-wrap">
      <UButton
        :icon="showEditor ? 'i-lucide-panel-left-close' : 'i-lucide-panel-left-open'" color="neutral" variant="outline"
        :label="showEditor ? 'Hide editor' : 'Show editor'" @click="showEditor = !showEditor"
      />
      <label class="text-sm space-y-1">
        <span class="text-gray-500 block">Profile</span>
        <USelect v-model="profile" :items="profileItems" class="min-w-40" aria-label="Preview profile" />
      </label>
      <label v-if="localeItems.length" class="text-sm space-y-1">
        <span class="text-gray-500 block">Locale</span>
        <USelect v-model="localeModel" :items="localeItems" class="min-w-24" aria-label="Locale" />
      </label>
      <label class="text-sm space-y-1">
        <span class="text-gray-500 block">CV version</span>
        <USelect v-model="revision" :items="revisionItems" class="min-w-56" aria-label="Preview CV version" :disabled="!!draft && view !== 'pdf'" />
      </label>
    </div>
    <p v-if="pin && revision === 'profile' && showEditor" class="text-xs text-amber-600">
      Profile <strong>{{ profile }}</strong> is pinned to version {{ shortSha(pin) }}: saved edits reach it only after re-pinning.
      Choose “Current CV” to preview the saved file.
    </p>

    <div class="grid gap-4" :class="{ 'lg:grid-cols-2': showEditor }">
      <AdminCvEditor
        v-show="showEditor" :key="`${tenant.id}-${locale}`" :tenant-id="tenant.id" :locale="locale" :default-locale="tenant.defaultLocale"
        @draft="draft = $event" @saved="onSaved"
      />

      <section class="rounded-lg border border-gray-200 dark:border-gray-800 bg-white dark:bg-gray-900 p-3 min-w-0 space-y-3">
        <div class="flex gap-2 items-center flex-wrap">
          <UTabs v-model="view" :items="viewItems" :content="false" size="sm" class="w-60" />
          <UTabs v-if="view === 'web'" v-model="width" :items="widthItems" :content="false" size="xs" variant="link" aria-label="Device width" />
          <template v-if="view === 'pdf'">
            <USelect v-model="template" :items="templateItems" size="sm" class="min-w-36" aria-label="PDF template" />
            <USelect v-if="presetItems.length" v-model="preset" :items="presetItems" size="sm" class="min-w-32" aria-label="PDF colour set" />
          </template>
          <UButton
            icon="i-lucide-refresh-cw" color="neutral" variant="ghost" class="ml-auto" :aria-label="view === 'pdf' ? 'Render PDF again' : 'Reload preview'"
            :loading="view === 'pdf' ? pdfLoading : loading" @click="load"
          />
        </div>
        <p v-if="draft && view !== 'pdf'" class="text-xs text-amber-600" data-testid="preview-draft-note">
          Showing your unsaved changes as <strong>{{ profile }}</strong> sees them.
        </p>
        <p v-if="draft && view === 'pdf'" class="text-xs text-amber-600">The PDF shows the saved CV: save to see your changes.</p>

        <!-- Web: the CV page itself -->
        <template v-if="view === 'web'">
          <p v-if="error" class="text-sm text-red-600 dark:text-red-400">{{ error }}</p>
          <div ref="frameBox" class="h-[75vh] overflow-hidden rounded border border-gray-200 dark:border-gray-800 bg-gray-100 dark:bg-gray-950">
            <iframe
              ref="frame" :key="frameSrc" :src="frameSrc" :style="frameStyle" title="Web preview" class="block mx-auto bg-white"
              data-testid="web-preview"
            />
          </div>
        </template>

        <!-- Data: redacted JSON and the profile definition -->
        <template v-else-if="view === 'data'">
          <details class="text-sm rounded border border-gray-200 dark:border-gray-800 p-2">
            <summary class="cursor-pointer font-semibold">Profile definition</summary>
            <pre class="text-xs whitespace-pre-wrap break-all mt-2">{{ JSON.stringify(profiles[profile] ?? {}, null, 2) }}</pre>
            <p v-if="tenant.publicProfile?.toLowerCase() === profile.toLowerCase()" class="mt-2 text-xs text-amber-600">
              This is the public profile: visible without invite on the tenant's own hosts. Flags it does not set
              are treated as <strong>hide</strong>; set a flag to <code>false</code> to show that data.
            </p>
          </details>
          <h3 class="font-semibold text-sm">
            Redacted CV as delivered to this profile
            <span v-if="result" class="font-normal text-gray-500">
              ({{ result.locale ?? locale }}{{ result.draft ? ', unsaved draft' : result.revision ? `, version ${shortSha(result.revision)}` : ', current CV' }})
            </span>
          </h3>
          <p v-if="error" class="text-sm text-red-600 dark:text-red-400">{{ error }}</p>
          <pre v-else class="text-xs overflow-auto max-h-[65vh] bg-gray-50 dark:bg-gray-950 rounded p-3" data-testid="preview-json">{{ json }}</pre>
        </template>

        <!-- PDF -->
        <template v-else>
          <p v-if="pdfLoading" class="text-sm text-gray-500">Rendering PDF, this can take a few seconds…</p>
          <p v-if="pdfError" class="text-sm text-red-600 dark:text-red-400" data-testid="pdf-preview-error">{{ pdfError }}</p>
          <iframe
            v-else-if="pdfUrl" :src="pdfUrl" title="PDF preview" class="w-full h-[75vh] rounded border border-gray-200 dark:border-gray-800"
            data-testid="pdf-preview"
          />
          <p class="text-xs text-gray-500">Preview only: not cached, not sent to anyone. Invites get their PDF via their own settings.</p>
        </template>
      </section>
    </div>
  </div>
</template>
