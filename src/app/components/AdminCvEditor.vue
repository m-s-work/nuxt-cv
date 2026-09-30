<script setup lang="ts">
/**
 * Graphical editor for a tenant's `cv.<locale>.json` (admin → Edit tab): one collapsible section per block,
 * forms generated from utils/cvEditorSchema.ts. Emits the unsaved draft so the preview next to it can show it.
 */
import { errorMessage, parseJsonc } from '~/composables/useAdmin'
import { cvEditorSchema, emptyValue, unknownKeys } from '~/utils/cvEditorSchema'

const props = defineProps<{ tenantId: string, locale: string, defaultLocale?: string }>()
const emit = defineEmits<{
  /** Current draft while it differs from the saved file, null otherwise. */
  draft: [cv: Record<string, unknown> | null]
  saved: []
}>()
const admin = useAdmin()
provide('cv-editor-tenant', props.tenantId)

const path = computed(() => `cv.${props.locale}.json`)
const draft = ref<Record<string, unknown> | null>(null)
const original = ref('')
const loading = ref(false)
const saving = ref(false)
const error = ref('')
const saved = ref(false)
/** The file does not exist (yet) for this locale. */
const missing = ref(false)
const hadComments = ref(false)

const serialized = computed(() => draft.value ? JSON.stringify(draft.value) : '')
const dirty = computed(() => !!draft.value && serialized.value !== original.value)

async function load() {
  loading.value = true
  error.value = ''
  saved.value = false
  missing.value = false
  try {
    const text = await admin.readFile(props.tenantId, path.value)
    const raw = typeof text === 'string' ? text : JSON.stringify(text)
    hadComments.value = /^\s*\/\/|\/\*/m.test(raw)
    const parsed = parseJsonc(raw)
    if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) throw new Error(`${path.value} is not a JSON object`)
    draft.value = parsed as Record<string, unknown>
    original.value = JSON.stringify(parsed)
  } catch (e) {
    draft.value = null
    original.value = ''
    if ((e as { statusCode?: number }).statusCode === 404) missing.value = true
    else error.value = errorMessage(e)
  } finally {
    loading.value = false
  }
}

/** New file for this locale: a copy of the default locale's CV (to translate) or empty. */
async function create(copyDefault: boolean) {
  error.value = ''
  let cv: Record<string, unknown> = { profile: { name: '', title: '' }, experiences: [] }
  if (copyDefault && props.defaultLocale) {
    try {
      cv = parseJsonc(await admin.readFile(props.tenantId, `cv.${props.defaultLocale}.json`)) as Record<string, unknown>
    } catch (e) {
      error.value = errorMessage(e)
      return
    }
  }
  missing.value = false
  hadComments.value = false
  draft.value = cv
  original.value = ''
}

async function save() {
  if (!draft.value || !dirty.value) return
  if (hadComments.value && !confirm(`${path.value} contains comments. Saving from the visual editor removes them. Continue?`)) return
  saving.value = true
  error.value = ''
  try {
    const content = JSON.stringify(draft.value, null, 2) + '\n'
    await admin.writeFile(props.tenantId, path.value, content)
    original.value = serialized.value
    hadComments.value = false
    saved.value = true
    emit('saved')
  } catch (e) {
    error.value = errorMessage(e)
  } finally {
    saving.value = false
  }
}

function revert() {
  if (dirty.value && !confirm('Discard all unsaved changes?')) return
  load()
}

// --- Blocks ---------------------------------------------------------------------------------

const open = ref(new Set<string>(['profile']))
function toggle(key: string) {
  if (open.value.has(key)) open.value.delete(key)
  else open.value.add(key)
}
const has = (key: string) => draft.value?.[key] !== undefined && draft.value?.[key] !== null
const block = <T,>(key: string) => draft.value![key] as T

function addBlock(key: string) {
  const field = cvEditorSchema.find(f => f.key === key)!
  draft.value![key] = emptyValue(field)
  open.value.add(key)
}

function setBlock(key: string, value: unknown) {
  draft.value![key] = value
}

function removeBlock(key: string, label: string) {
  if (!confirm(`Remove the whole "${label}" block from ${path.value}?`)) return
  delete draft.value![key]
}

const count = (key: string) => {
  const value = draft.value?.[key]
  return Array.isArray(value) ? value.length : undefined
}
const others = computed(() => draft.value ? unknownKeys(draft.value, cvEditorSchema) : [])

watch(serialized, () => emit('draft', dirty.value ? JSON.parse(serialized.value) : null))

// Leaving the page with unsaved changes.
function beforeUnload(event: BeforeUnloadEvent) {
  if (dirty.value) event.preventDefault()
}
onMounted(() => {
  load()
  window.addEventListener('beforeunload', beforeUnload)
})
onBeforeUnmount(() => window.removeEventListener('beforeunload', beforeUnload))

defineExpose({ dirty })
</script>

<template>
  <section class="rounded-lg border border-gray-200 dark:border-gray-800 bg-white dark:bg-gray-900 p-3 min-w-0" data-testid="cv-editor">
    <div class="flex items-center gap-2 mb-3 flex-wrap">
      <UIcon name="i-lucide-pencil" class="text-gray-500" />
      <code class="font-semibold text-sm">{{ path }}</code>
      <UBadge v-if="dirty" label="unsaved" color="warning" variant="subtle" size="sm" />
      <UBadge v-else-if="saved" label="saved" color="success" variant="subtle" size="sm" />
      <div class="ml-auto flex gap-2">
        <UButton size="sm" color="neutral" variant="ghost" icon="i-lucide-undo-2" label="Revert" :disabled="!dirty" @click="revert" />
        <UButton size="sm" icon="i-lucide-save" label="Save" :loading="saving" :disabled="!dirty" data-testid="cv-editor-save" @click="save" />
      </div>
    </div>

    <p v-if="loading" class="text-sm text-gray-500">Loading…</p>
    <p v-if="error" class="text-sm text-red-600 dark:text-red-400 mb-2">{{ error }}</p>

    <div v-if="missing" class="text-sm text-gray-500 space-y-2 p-4 text-center">
      <p>There is no <code>{{ path }}</code> yet; visitors with this locale get the default locale's CV.</p>
      <div class="flex gap-2 justify-center">
        <UButton v-if="defaultLocale && defaultLocale !== locale" size="sm" icon="i-lucide-copy" :label="`Start from cv.${defaultLocale}.json`" @click="create(true)" />
        <UButton size="sm" color="neutral" variant="outline" icon="i-lucide-file-plus" label="Start empty" @click="create(false)" />
      </div>
    </div>

    <div v-else-if="draft" class="space-y-2 max-h-[75vh] overflow-y-auto pr-1">
      <p v-if="hadComments" class="text-xs text-amber-600">This file contains comments; saving from here removes them (the Files tab keeps them).</p>
      <div
        v-for="b in cvEditorSchema" :key="b.key"
        class="rounded-md border border-gray-200 dark:border-gray-800"
        :data-testid="`cv-block-${b.key}`"
      >
        <div class="flex items-center gap-2 px-3 py-2">
          <button type="button" class="flex-1 flex items-center gap-2 text-left text-sm font-semibold" :aria-expanded="open.has(b.key)" @click="toggle(b.key)">
            <UIcon :name="open.has(b.key) ? 'i-lucide-chevron-down' : 'i-lucide-chevron-right'" class="text-gray-500" />
            <UIcon v-if="b.icon" :name="b.icon" class="text-primary" />
            {{ b.label }}
            <span v-if="count(b.key) !== undefined" class="text-xs font-normal text-gray-500">({{ count(b.key) }})</span>
            <span v-if="!has(b.key)" class="text-xs font-normal text-gray-500">– not in this CV</span>
          </button>
          <UButton v-if="has(b.key)" size="xs" color="error" variant="ghost" icon="i-lucide-trash-2" :aria-label="`Remove block ${b.label}`" @click="removeBlock(b.key, b.label)" />
        </div>
        <div v-if="open.has(b.key)" class="px-3 pb-3 pt-1 border-t border-gray-200 dark:border-gray-800">
          <UButton v-if="!has(b.key)" size="xs" color="neutral" variant="outline" icon="i-lucide-plus" :label="`Add ${b.label}`" @click="addBlock(b.key)" />
          <AdminCvFields v-else-if="b.type === 'object'" :fields="b.fields ?? []" :value="block<Record<string, unknown>>(b.key)" :field-requires="b.fieldRequires" />
          <AdminCvList v-else-if="b.type === 'list'" :field="b" :items="block<Array<Record<string, unknown>>>(b.key)" />
          <UInputTags
            v-else-if="b.type === 'tags'" :model-value="block<string[]>(b.key)" placeholder="Add…" class="w-full"
            @update:model-value="setBlock(b.key, $event)"
          />
        </div>
      </div>
      <p v-if="others.length" class="text-xs text-gray-500">
        Also contains <code>{{ others.join(', ') }}</code> – kept as is, edit them in the Files tab.
      </p>
    </div>
  </section>
</template>
