<script setup lang="ts">
// Template builder ("Design" tab): pick the PDF template, a colour set and variables for the tenant or a
// profile, see a live PDF preview and save the choice into tenant.json (comments are kept).
// The form is generated from each template's variable schema (utils/printTemplates.ts).
import { errorMessage, type AdminTenant } from '~/composables/useAdmin'
import { printTemplates } from '~/utils/printTemplates'
import { resolveTemplateVars, type TemplateVarDef, type TemplateVarValue } from '~/utils/templateVars'
import { minimalVars, readTemplatesSelection, writeTemplatesSelection, type TemplateScope, type TemplatesSelection } from '~/utils/templateBuilder'

const props = defineProps<{ tenant: AdminTenant }>()
const emit = defineEmits<{ changed: [] }>()
const admin = useAdmin()

type TemplateName = keyof typeof printTemplates
const templateList = Object.values(printTemplates)

// --- Scope: tenant default or a profile -------------------------------------------------------
const scopeValue = ref('tenant')
const scope = computed<TemplateScope>(() =>
  scopeValue.value === 'tenant' ? { kind: 'tenant' } : { kind: 'profile', name: scopeValue.value.slice('profile:'.length) })
const scopeItems = computed(() => [
  { label: 'Tenant default (all profiles and invites)', value: 'tenant' },
  ...props.tenant.profiles.map(p => ({ label: `Profile: ${p}`, value: `profile:${p}` }))
])

// --- Current tenant.json and the stored choice ------------------------------------------------
const tenantJson = ref('')
const stored = ref<TemplatesSelection>({})
const loadError = ref('')

// --- Editor state ------------------------------------------------------------------------------
// '' = not set at this scope (inherits: profile → tenant → system default)
const template = ref<'' | TemplateName>('')
const preset = ref('')
const values = ref<Record<string, TemplateVarValue>>({})
let applyingStored = false

const schema = computed(() => (template.value ? printTemplates[template.value].vars : undefined))
const varEntries = computed(() => Object.entries(schema.value?.vars ?? {}) as [string, TemplateVarDef][])
const presets = computed(() => Object.entries(schema.value?.presets ?? {}))
const pdfVars = computed(() => minimalVars(schema.value, preset.value || undefined, values.value))
const selection = computed<TemplatesSelection>(() => ({
  ...(stored.value.html ? { html: stored.value.html } : {}),
  ...(template.value ? { pdf: template.value } : {}),
  ...(template.value && Object.keys(pdfVars.value).length ? { pdfVars: pdfVars.value } : {})
}))
const dirty = computed(() => JSON.stringify(selection.value) !== JSON.stringify(normalise(stored.value)))
const snippet = computed(() => JSON.stringify({ templates: selection.value }, null, 2))

function normalise(s: TemplatesSelection): TemplatesSelection {
  return {
    ...(s.html ? { html: s.html } : {}),
    ...(s.pdf ? { pdf: s.pdf } : {}),
    ...(s.pdfVars && Object.keys(s.pdfVars).length ? { pdfVars: s.pdfVars } : {})
  }
}

function applyStored() {
  applyingStored = true
  const pdf = stored.value.pdf
  template.value = pdf && pdf in printTemplates ? pdf as TemplateName : ''
  const vars = stored.value.pdfVars ?? {}
  preset.value = typeof vars.preset === 'string' ? vars.preset : ''
  values.value = resolveTemplateVars(schema.value, vars)
  nextTick(() => { applyingStored = false })
}

async function load() {
  loadError.value = ''
  try {
    tenantJson.value = await admin.readFile(props.tenant.id, 'tenant.json')
    stored.value = readTemplatesSelection(tenantJson.value, scope.value)
    applyStored()
  } catch (e) {
    loadError.value = errorMessage(e)
  }
}

watch(scopeValue, () => {
  try {
    stored.value = readTemplatesSelection(tenantJson.value, scope.value)
    applyStored()
  } catch (e) { loadError.value = errorMessage(e) }
})

// Switching template or colour set starts from its defaults.
watch(template, () => {
  if (applyingStored) return
  preset.value = ''
  values.value = resolveTemplateVars(schema.value, null)
})
watch(preset, () => {
  if (applyingStored) return
  values.value = resolveTemplateVars(schema.value, preset.value ? { preset: preset.value } : null)
})

function setValue(key: string, value: TemplateVarValue) {
  values.value = { ...values.value, [key]: value }
}
function setPaletteColor(key: string, index: number, color: string) {
  const palette = [...(values.value[key] as string[])]
  palette[index] = color
  setValue(key, palette)
}
const hexRegex = /^#(?:[0-9a-f]{3}|[0-9a-f]{6})$/i
function onHexInput(key: string, text: string) {
  if (hexRegex.test(text)) setValue(key, text)
}
// <input type="color"> only understands #rrggbb
function sixDigit(color: unknown): string {
  const c = typeof color === 'string' ? color : '#000000'
  return /^#[0-9a-f]{3}$/i.test(c) ? `#${[...c.slice(1)].map(x => x + x).join('')}` : c.slice(0, 7)
}

function resetToDefaults() {
  preset.value = ''
  values.value = resolveTemplateVars(schema.value, null)
}

// --- Live preview --------------------------------------------------------------------------------
const previewProfile = ref(props.tenant.publicProfile ?? props.tenant.profiles[0] ?? '')
const previewLocale = ref(props.tenant.locales.includes(props.tenant.defaultLocale) ? props.tenant.defaultLocale : props.tenant.locales[0] ?? '')
watch(scope, (s) => { if (s.kind === 'profile') previewProfile.value = s.name })

const profileItems = computed(() => props.tenant.profiles.map(p => ({ label: p, value: p })))
const localeItems = computed(() => props.tenant.locales.map(l => ({ label: l, value: l })))

const pdfUrl = ref<string | null>(null)
const pdfLoading = ref(false)
const pdfError = ref('')
let pdfRequest = 0
let debounce: ReturnType<typeof setTimeout> | undefined

function setPdfUrl(url: string | null) {
  if (pdfUrl.value) URL.revokeObjectURL(pdfUrl.value)
  pdfUrl.value = url
}

async function renderPreview() {
  if (!previewProfile.value) return
  const request = ++pdfRequest
  pdfLoading.value = true
  pdfError.value = ''
  try {
    const blob = await admin.pdfPreview(props.tenant.id, {
      profile: previewProfile.value,
      locale: previewLocale.value || undefined,
      template: template.value || undefined,
      vars: Object.keys(pdfVars.value).length ? JSON.stringify(pdfVars.value) : undefined
    })
    if (request === pdfRequest) setPdfUrl(URL.createObjectURL(blob))
  } catch (e) {
    if (request !== pdfRequest) return
    setPdfUrl(null)
    const status = (e as { statusCode?: number }).statusCode
    pdfError.value = status === 404 ? 'No PDF: the PDF renderer is not configured, or there is no CV for this selection.' : errorMessage(e)
  } finally {
    if (request === pdfRequest) pdfLoading.value = false
  }
}

watch([template, () => JSON.stringify(pdfVars.value), previewProfile, previewLocale], () => {
  clearTimeout(debounce)
  debounce = setTimeout(renderPreview, 700)
})

// --- Save / copy ---------------------------------------------------------------------------------
const saving = ref(false)
const message = ref('')

async function save() {
  saving.value = true
  message.value = ''
  try {
    const text = writeTemplatesSelection(tenantJson.value, scope.value, selection.value)
    await admin.writeFile(props.tenant.id, 'tenant.json', text)
    tenantJson.value = text
    stored.value = readTemplatesSelection(text, scope.value)
    message.value = 'Saved to tenant.json. New and stale PDFs use the new design.'
    emit('changed')
  } catch (e) {
    message.value = `Not saved: ${errorMessage(e)}`
  } finally {
    saving.value = false
  }
}

async function copySnippet() {
  await navigator.clipboard.writeText(snippet.value)
  message.value = 'JSON copied – paste it into tenant.json in your CV repository.'
}

onMounted(async () => {
  await load()
  renderPreview()
})
onBeforeUnmount(() => {
  clearTimeout(debounce)
  setPdfUrl(null)
})
</script>

<template>
  <div class="grid gap-4 lg:grid-cols-[24rem_1fr]" data-testid="template-builder">
    <!-- Settings -->
    <section class="space-y-5 rounded-lg border border-gray-200 dark:border-gray-800 bg-white dark:bg-gray-900 p-4 text-sm">
      <p v-if="loadError" class="text-red-600 dark:text-red-400">{{ loadError }}</p>

      <label class="block space-y-1">
        <span class="text-gray-500 block">Applies to</span>
        <USelect v-model="scopeValue" :items="scopeItems" class="w-full" aria-label="Scope" />
      </label>

      <div class="space-y-2">
        <span class="text-gray-500 block">PDF template</span>
        <div class="grid gap-2">
          <button
            type="button"
            class="text-left rounded-md border px-3 py-2"
            :class="template === '' ? 'border-primary ring-1 ring-primary' : 'border-gray-200 dark:border-gray-700'"
            @click="template = ''"
          >
            <span class="font-medium">Not set here</span>
            <span class="block text-xs text-gray-500">{{ scope.kind === 'tenant' ? 'System default (Editorial)' : 'Uses the tenant default' }}</span>
          </button>
          <button
            v-for="t in templateList" :key="t.name" type="button"
            class="text-left rounded-md border px-3 py-2"
            :class="template === t.name ? 'border-primary ring-1 ring-primary' : 'border-gray-200 dark:border-gray-700'"
            :data-testid="`template-${t.name}`"
            @click="template = t.name"
          >
            <span class="font-medium">{{ t.title }}</span>
            <span class="block text-xs text-gray-500">{{ t.description }}</span>
          </button>
        </div>
      </div>

      <div v-if="presets.length" class="space-y-2">
        <span class="text-gray-500 block">Colour set</span>
        <div class="flex flex-wrap gap-2">
          <UButton size="xs" :variant="preset === '' ? 'solid' : 'outline'" color="neutral" label="Default" @click="preset = ''" />
          <UButton
            v-for="[name, p] in presets" :key="name" size="xs" color="neutral"
            :variant="preset === name ? 'solid' : 'outline'" :data-testid="`preset-${name}`" @click="preset = name"
          >
            <span class="inline-flex gap-0.5 mr-1">
              <span v-for="c in ['sidebar', 'band', 'accent']" :key="c" class="size-2.5 rounded-full border border-black/10" :style="{ background: String(p.values[c] ?? '') }" />
            </span>
            {{ p.label }}
          </UButton>
        </div>
      </div>

      <div v-if="varEntries.length" class="space-y-3">
        <div class="flex items-center justify-between">
          <span class="text-gray-500">Variables</span>
          <UButton size="xs" variant="ghost" color="neutral" label="Reset to defaults" @click="resetToDefaults" />
        </div>

        <div v-for="[key, def] in varEntries" :key="key" class="space-y-1">
          <span class="block text-xs font-medium">{{ def.label }}</span>

          <div v-if="def.type === 'color'" class="flex items-center gap-2 flex-wrap">
            <input
              type="color" class="h-8 w-10 rounded border border-gray-300 dark:border-gray-700 bg-transparent"
              :value="sixDigit(values[key])" :aria-label="def.label" :data-testid="`var-${key}`"
              @input="setValue(key, ($event.target as HTMLInputElement).value)"
            >
            <UInput :model-value="String(values[key] ?? '')" size="xs" class="w-24 font-mono" :aria-label="`${def.label} (hex)`" @update:model-value="onHexInput(key, String($event))" />
            <button
              v-for="s in def.suggestions ?? []" :key="s" type="button" class="size-5 rounded-full border border-black/10"
              :style="{ background: s }" :title="s" :aria-label="`${def.label}: ${s}`" @click="setValue(key, s)"
            />
          </div>

          <USwitch v-else-if="def.type === 'boolean'" :model-value="values[key] === true" :aria-label="def.label" :data-testid="`var-${key}`" @update:model-value="setValue(key, !!$event)" />

          <div v-else-if="def.type === 'palette'" class="flex gap-1.5">
            <input
              v-for="(color, i) in (values[key] as string[])" :key="i" type="color"
              class="h-7 w-8 rounded border border-gray-300 dark:border-gray-700 bg-transparent"
              :value="sixDigit(color)" :aria-label="`${def.label} ${i + 1}`"
              @input="setPaletteColor(key, i, ($event.target as HTMLInputElement).value)"
            >
          </div>

          <USelect
            v-else-if="def.type === 'enum'" :model-value="String(values[key])" size="xs" class="w-40"
            :items="def.options.map(o => ({ label: o, value: o }))" :aria-label="def.label"
            @update:model-value="setValue(key, String($event))"
          />
        </div>
      </div>
      <p v-else-if="template" class="text-xs text-gray-500">This template has no variables.</p>

      <div class="space-y-2 border-t border-gray-200 dark:border-gray-800 pt-4">
        <div class="flex gap-2 flex-wrap">
          <UButton icon="i-lucide-save" label="Save to tenant.json" :disabled="!dirty || !!loadError" :loading="saving" data-testid="save-templates" @click="save" />
          <UButton icon="i-lucide-copy" label="Copy JSON" color="neutral" variant="outline" @click="copySnippet" />
          <UButton v-if="dirty" label="Discard" color="neutral" variant="ghost" @click="applyStored" />
        </div>
        <p v-if="message" class="text-xs" data-testid="templates-message">{{ message }}</p>
        <pre class="text-xs bg-gray-50 dark:bg-gray-950 rounded p-2 overflow-auto" data-testid="templates-snippet">{{ snippet }}</pre>
        <p class="text-xs text-gray-500">
          If this tenant's files come from a Git repository (<code>tools/cv-sync.sh</code>), put this JSON into
          <code>tenant.json</code> there – the next sync overwrites changes saved here.
        </p>
      </div>
    </section>

    <!-- Live preview -->
    <section class="rounded-lg border border-gray-200 dark:border-gray-800 bg-white dark:bg-gray-900 p-3 min-w-0 space-y-2">
      <div class="flex gap-3 items-end flex-wrap text-sm">
        <label class="space-y-1">
          <span class="text-gray-500 block">Preview as profile</span>
          <USelect v-model="previewProfile" :items="profileItems" class="min-w-36" aria-label="Preview profile" />
        </label>
        <label v-if="localeItems.length" class="space-y-1">
          <span class="text-gray-500 block">Locale</span>
          <USelect v-model="previewLocale" :items="localeItems" class="min-w-20" aria-label="Preview locale" />
        </label>
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="ghost" aria-label="Render again" :loading="pdfLoading" @click="renderPreview" />
        <span v-if="pdfLoading" class="text-gray-500">Rendering…</span>
      </div>
      <p v-if="pdfError" class="text-sm text-red-600 dark:text-red-400">{{ pdfError }}</p>
      <iframe v-else-if="pdfUrl" :src="pdfUrl" title="PDF preview" class="w-full h-[78vh] rounded border border-gray-200 dark:border-gray-800" data-testid="template-preview" />
    </section>
  </div>
</template>
