<script setup lang="ts">
import { errorMessage, formatBytes, parseJsonc, type AdminFile } from '~/composables/useAdmin'

const props = defineProps<{ tenantId: string, isNew?: boolean }>()
const emit = defineEmits<{ changed: [] }>()
const admin = useAdmin()

const TENANT_TEMPLATE = `{
  "name": "",
  "hosts": [],
  "defaultLocale": "en",
  "publicProfile": null,
  "profiles": {
    "recruiter": { "grants": ["contact"], "flags": { "hideTimeframeDays": true, "hideBirthDate": true } },
    "full": { "grants": ["contact", "private"] }
  }
}
`
const CV_TEMPLATE = `{
  "profile": { "name": "", "title": "" },
  "experiences": []
}
`

const files = ref<AdminFile[]>([])
const loading = ref(false)
const error = ref('')

const jsonFiles = computed(() => files.value.filter(f => f.path.endsWith('.json')))
const assets = computed(() => files.value.filter(f => f.path.startsWith('assets/')))

async function load() {
  if (props.isNew) return
  loading.value = true
  error.value = ''
  try {
    files.value = await admin.files(props.tenantId)
  } catch (e) {
    error.value = errorMessage(e)
  } finally {
    loading.value = false
  }
}

// --- JSON editor --------------------------------------------------------------------------

const editing = ref<string | null>(null)
const content = ref('')
const original = ref('')
const saving = ref(false)
const editorError = ref('')
const saved = ref(false)

const dirty = computed(() => content.value !== original.value)
const parseError = computed(() => {
  if (!editing.value) return ''
  try {
    parseJsonc(content.value)
    return ''
  } catch (e) {
    return (e as Error).message
  }
})

async function open(path: string, template?: string) {
  if (dirty.value && !confirm('Discard unsaved changes?')) return
  clearPreview()
  editorError.value = ''
  saved.value = false
  editing.value = path
  if (template !== undefined) {
    content.value = original.value = ''
    content.value = template
    return
  }
  try {
    const text = await admin.readFile(props.tenantId, path)
    content.value = original.value = typeof text === 'string' ? text : JSON.stringify(text, null, 2)
  } catch (e) {
    editorError.value = errorMessage(e)
    content.value = original.value = ''
  }
}

function close() {
  if (dirty.value && !confirm('Discard unsaved changes?')) return
  editing.value = null
}

const hasComments = computed(() => /\/\/|\/\*/.test(content.value))

function format() {
  if (hasComments.value && !confirm('Formatting removes all comments. Continue?')) return
  try { content.value = JSON.stringify(parseJsonc(content.value), null, 2) + '\n' } catch { /* shown as parseError */ }
}

async function save() {
  if (!editing.value || parseError.value) return
  saving.value = true
  editorError.value = ''
  try {
    await admin.writeFile(props.tenantId, editing.value, content.value)
    original.value = content.value
    saved.value = true
    emit('changed')
    await load()
  } catch (e) {
    editorError.value = errorMessage(e)
  } finally {
    saving.value = false
  }
}

const newLocale = ref('')
function newCvFile() {
  const locale = newLocale.value.trim()
  if (!/^[a-z]{2}(-[A-Z]{2})?$/.test(locale)) {
    error.value = 'Locale must look like "en" or "de-AT".'
    return
  }
  error.value = ''
  newLocale.value = ''
  open(`cv.${locale}.json`, CV_TEMPLATE)
}

// --- Assets -------------------------------------------------------------------------------

const uploading = ref(false)
const fileInput = ref<HTMLInputElement | null>(null)

async function upload(event: Event) {
  const input = event.target as HTMLInputElement
  const selected = Array.from(input.files ?? [])
  if (!selected.length) return
  uploading.value = true
  error.value = ''
  try {
    for (const file of selected) {
      const name = file.name.replace(/[^A-Za-z0-9._-]/g, '-').replace(/^[^A-Za-z0-9]+/, '')
      await admin.writeFile(props.tenantId, `assets/${name}`, file)
    }
    emit('changed')
    await load()
  } catch (e) {
    error.value = errorMessage(e)
  } finally {
    uploading.value = false
    input.value = ''
  }
}

const preview = ref<{ path: string, url: string, type: string } | null>(null)

function clearPreview() {
  if (preview.value) URL.revokeObjectURL(preview.value.url)
  preview.value = null
}

async function view(path: string) {
  if (dirty.value && !confirm('Discard unsaved changes?')) return
  editing.value = null
  content.value = original.value = ''
  editorError.value = ''
  // Loaded as a blob, because a plain link could not send the admin key header.
  try {
    const blob = await admin.readBlob(props.tenantId, path)
    clearPreview()
    preview.value = { path, url: URL.createObjectURL(blob), type: blob.type }
  } catch (e) {
    clearPreview()
    error.value = errorMessage(e)
  }
}

onBeforeUnmount(clearPreview)

async function remove(path: string) {
  if (!confirm(`Delete ${path}? This cannot be undone.`)) return
  try {
    await admin.deleteFile(props.tenantId, path)
    if (editing.value === path) editing.value = null
    if (preview.value?.path === path) clearPreview()
    emit('changed')
    await load()
  } catch (e) {
    error.value = errorMessage(e)
  }
}

function formatDate(value: string) {
  return new Date(value).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' })
}

onMounted(() => {
  if (props.isNew) open('tenant.json', TENANT_TEMPLATE)
  else load()
})
</script>

<template>
  <div class="grid gap-6 lg:grid-cols-[18rem_1fr]">
    <aside class="space-y-4">
      <section class="rounded-lg border border-gray-200 dark:border-gray-800 bg-white dark:bg-gray-900 p-3">
        <h3 class="font-semibold text-sm mb-2">Configuration &amp; CV</h3>
        <ul class="space-y-1 text-sm">
          <li v-for="f in jsonFiles" :key="f.path">
            <button
              type="button"
              class="w-full text-left rounded px-2 py-1 hover:bg-gray-100 dark:hover:bg-gray-800"
              :class="{ 'bg-primary/10 text-primary': editing === f.path }"
              @click="open(f.path)"
            >
              <code>{{ f.path }}</code>
              <span class="block text-xs text-gray-500">{{ formatBytes(f.size) }} · {{ formatDate(f.modifiedAt) }}</span>
            </button>
          </li>
          <li v-if="isNew" class="text-gray-500 px-2">New tenant – no files yet.</li>
        </ul>
        <form v-if="!isNew" class="flex gap-2 mt-3" @submit.prevent="newCvFile">
          <UInput v-model="newLocale" size="xs" placeholder="locale, e.g. de" class="flex-1" aria-label="New CV locale" />
          <UButton type="submit" size="xs" icon="i-lucide-plus" label="CV" color="neutral" variant="outline" />
        </form>
      </section>

      <section v-if="!isNew" class="rounded-lg border border-gray-200 dark:border-gray-800 bg-white dark:bg-gray-900 p-3">
        <div class="flex items-center mb-2">
          <h3 class="font-semibold text-sm">Assets</h3>
          <UButton
            size="xs" icon="i-lucide-upload" label="Upload" color="neutral" variant="outline" class="ml-auto"
            :loading="uploading" @click="fileInput?.click()"
          />
          <input ref="fileInput" type="file" multiple class="hidden" @change="upload">
        </div>
        <p v-if="!assets.length" class="text-sm text-gray-500">No assets.</p>
        <ul class="space-y-1 text-sm">
          <li v-for="f in assets" :key="f.path" class="flex items-center gap-1">
            <button type="button" class="flex-1 min-w-0 text-left rounded px-2 py-1 hover:bg-gray-100 dark:hover:bg-gray-800"
              :class="{ 'bg-primary/10 text-primary': preview?.path === f.path }"
              @click="view(f.path)">
              <code class="block truncate">{{ f.path.slice(7) }}</code>
              <span class="block text-xs text-gray-500">{{ formatBytes(f.size) }}</span>
            </button>
            <UButton size="xs" icon="i-lucide-trash-2" color="error" variant="ghost" :aria-label="`Delete ${f.path}`" @click="remove(f.path)" />
          </li>
        </ul>
        <p class="text-xs text-gray-500 mt-2">Reference assets in the CV as <code>/api/assets/&lt;file&gt;</code>.</p>
      </section>
      <p v-if="error" class="text-sm text-red-600 dark:text-red-400">{{ error }}</p>
    </aside>

    <section class="rounded-lg border border-gray-200 dark:border-gray-800 bg-white dark:bg-gray-900 p-3 min-w-0">
      <template v-if="preview">
        <div class="flex items-center gap-2 mb-2 flex-wrap">
          <code class="font-semibold">{{ preview.path }}</code>
          <div class="ml-auto flex gap-2">
            <UButton size="sm" color="neutral" variant="ghost" label="Close" @click="clearPreview" />
            <UButton size="sm" color="error" variant="ghost" icon="i-lucide-trash-2" label="Delete" @click="remove(preview.path)" />
          </div>
        </div>
        <div class="flex justify-center rounded border border-gray-200 dark:border-gray-800 bg-gray-50 dark:bg-gray-950 p-3">
          <img v-if="preview.type.startsWith('image/')" :src="preview.url" :alt="preview.path" class="max-w-full max-h-[65vh] object-contain">
          <iframe v-else-if="preview.type === 'application/pdf'" :src="preview.url" class="w-full h-[65vh]" :title="preview.path" />
          <a v-else :href="preview.url" :download="preview.path.split('/').pop()" class="text-primary underline p-6">Download {{ preview.path }}</a>
        </div>
      </template>
      <div v-else-if="!editing" class="text-sm text-gray-500 p-6 text-center">
        {{ loading ? 'Loading…' : 'Select a file to edit.' }}
      </div>
      <template v-else>
        <div class="flex items-center gap-2 mb-2 flex-wrap">
          <code class="font-semibold">{{ editing }}</code>
          <UBadge v-if="dirty" label="unsaved" color="warning" variant="subtle" size="sm" />
          <UBadge v-else-if="saved" label="saved" color="success" variant="subtle" size="sm" />
          <div class="ml-auto flex gap-2">
            <UButton size="sm" color="neutral" variant="ghost" label="Format" :disabled="!!parseError" @click="format" />
            <UButton size="sm" color="neutral" variant="ghost" label="Close" @click="close" />
            <UButton
              v-if="!isNew && editing !== 'tenant.json' && original"
              size="sm" color="error" variant="ghost" icon="i-lucide-trash-2" label="Delete" @click="remove(editing)"
            />
            <UButton size="sm" icon="i-lucide-save" label="Save" :loading="saving" :disabled="!dirty || !!parseError" @click="save" />
          </div>
        </div>
        <textarea
          v-model="content"
          spellcheck="false"
          aria-label="File content"
          class="w-full h-[65vh] font-mono text-xs leading-5 p-3 rounded border bg-gray-50 dark:bg-gray-950 border-gray-200 dark:border-gray-800 focus:outline-none focus:ring-2 focus:ring-primary"
          @keydown.ctrl.s.prevent="save"
          @keydown.meta.s.prevent="save"
        />
        <p v-if="parseError" class="text-sm text-red-600 dark:text-red-400 mt-1">Invalid JSON: {{ parseError }}</p>
        <p v-if="editorError" class="text-sm text-red-600 dark:text-red-400 mt-1">{{ editorError }}</p>
        <p class="text-xs text-gray-500 mt-1">
          Changes are live for visitors within seconds. Cached PDFs become obsolete and are re-rendered on the next download.
        </p>
      </template>
    </section>
  </div>
</template>
