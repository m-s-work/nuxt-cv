<script setup lang="ts">
// Favicon picker ("Design" tab): symbol, symbol colour and background of the tenant's browser-tab icon,
// with a live preview drawn by the API (so it matches /api/favicon.svg) and saved into tenant.json.
import { errorMessage, type AdminTenant, type FaviconCatalogue } from '~/composables/useAdmin'
import { readFavicon, svgDataUrl, writeFavicon, type FaviconConfig } from '~/utils/faviconConfig'

const props = defineProps<{ tenant: AdminTenant }>()
const emit = defineEmits<{ changed: [] }>()
const admin = useAdmin()

const tenantJson = ref('')
const stored = ref<FaviconConfig>({})
const loadError = ref('')

// '' = not set (the API default)
const symbol = ref('')
const color = ref('')
const background = ref('')

const catalogue = ref<FaviconCatalogue | null>(null)
const colorNames = computed(() => Object.entries(catalogue.value?.colors ?? {}))

const hexRegex = /^#(?:[0-9a-f]{3}|[0-9a-f]{6})$/i
const isValidColor = (value: string) => !value || hexRegex.test(value) || value in (catalogue.value?.colors ?? {})

const current = computed<FaviconConfig>(() => ({
  ...(symbol.value ? { symbol: symbol.value } : {}),
  ...(color.value ? { color: color.value } : {}),
  ...(background.value ? { background: background.value } : {})
}))
const dirty = computed(() => JSON.stringify(current.value) !== JSON.stringify(stored.value))
const snippet = computed(() => JSON.stringify(Object.keys(current.value).length ? { favicon: current.value } : {}, null, 2))

const activeSymbol = computed(() => symbol.value || catalogue.value?.defaults.symbol || '')
const defaultGlyph = computed(() => catalogue.value?.symbols.find(s => s.name === catalogue.value?.defaults.symbol)?.glyph)
const previewSvg = computed(() => catalogue.value?.symbols.find(s => s.name === activeSymbol.value)?.svg)

function applyStored() {
  symbol.value = stored.value.symbol ?? ''
  color.value = stored.value.color ?? ''
  background.value = stored.value.background ?? ''
}

async function load() {
  loadError.value = ''
  try {
    tenantJson.value = await admin.readFile(props.tenant.id, 'tenant.json')
    stored.value = readFavicon(tenantJson.value)
    applyStored()
  } catch (e) {
    loadError.value = errorMessage(e)
  }
}

// Previews come from the API; refetch when the colours change (invalid input keeps the last preview).
let catalogueRequest = 0
let debounce: ReturnType<typeof setTimeout> | undefined
async function loadCatalogue() {
  const request = ++catalogueRequest
  try {
    const result = await admin.faviconCatalogue(color.value, background.value)
    if (request === catalogueRequest) catalogue.value = result
  } catch (e) {
    if (request === catalogueRequest) loadError.value = errorMessage(e)
  }
}
watch([color, background], ([c, b]) => {
  if (!isValidColor(c) || !isValidColor(b)) return
  clearTimeout(debounce)
  debounce = setTimeout(loadCatalogue, 150)
})

/** Hex of a named or hex colour (for <input type="color">, which only understands #rrggbb). */
function hexOf(value: string, fallback: string): string {
  const hex = catalogue.value?.colors[value] ?? (hexRegex.test(value) ? value : catalogue.value?.colors[fallback] ?? '#000000')
  return hex.length === 4 ? `#${[...hex.slice(1)].map(x => x + x).join('')}` : hex
}

function resetToDefault() {
  symbol.value = ''
  color.value = ''
  background.value = ''
}

const saving = ref(false)
const message = ref('')

async function save() {
  saving.value = true
  message.value = ''
  try {
    const text = writeFavicon(tenantJson.value, current.value)
    await admin.writeFile(props.tenant.id, 'tenant.json', text)
    tenantJson.value = text
    stored.value = readFavicon(text)
    message.value = 'Saved to tenant.json. Visitors see the new icon on their next page load.'
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

onMounted(() => Promise.all([load(), loadCatalogue()]))
onBeforeUnmount(() => clearTimeout(debounce))
</script>

<template>
  <section class="rounded-lg border border-gray-200 dark:border-gray-800 bg-white dark:bg-gray-900 p-4 text-sm space-y-4" data-testid="favicon-picker">
    <div class="flex items-start justify-between gap-4 flex-wrap">
      <div>
        <h2 class="font-semibold text-base">Favicon</h2>
        <p class="text-gray-500 text-xs">Browser-tab icon of this CV (all profiles and invites).</p>
      </div>
      <!-- Preview: large, real sizes and in a mock browser tab -->
      <div v-if="previewSvg" class="flex items-end gap-3" data-testid="favicon-preview">
        <img :src="svgDataUrl(previewSvg)" alt="Favicon preview" class="size-16">
        <img :src="svgDataUrl(previewSvg)" alt="" class="size-8">
        <img :src="svgDataUrl(previewSvg)" alt="" class="size-4">
        <div class="flex items-center gap-2 rounded-t-lg border border-b-0 border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-gray-800 px-3 py-1.5 max-w-48">
          <img :src="svgDataUrl(previewSvg)" alt="" class="size-4 shrink-0">
          <span class="truncate text-xs">{{ tenant.name || tenant.id }}</span>
        </div>
      </div>
    </div>

    <p v-if="loadError" class="text-red-600 dark:text-red-400">{{ loadError }}</p>

    <div class="space-y-2">
      <span class="text-gray-500 block">Symbol</span>
      <div class="flex flex-wrap gap-2">
        <button
          v-for="s in catalogue?.symbols ?? []" :key="s.name" type="button"
          class="flex flex-col items-center gap-1 rounded-md border p-2 w-20"
          :class="activeSymbol === s.name ? 'border-primary ring-1 ring-primary' : 'border-gray-200 dark:border-gray-700'"
          :aria-pressed="activeSymbol === s.name" :data-testid="`favicon-symbol-${s.name}`"
          @click="symbol = s.name"
        >
          <img :src="svgDataUrl(s.svg)" alt="" class="size-10">
          <span class="font-mono text-xs">{{ s.glyph }}</span>
        </button>
      </div>
    </div>

    <div class="grid gap-4 sm:grid-cols-2">
      <div v-for="field in (['color', 'background'] as const)" :key="field" class="space-y-2">
        <span class="text-gray-500 block">{{ field === 'color' ? 'Symbol colour' : 'Background' }}</span>
        <div class="flex items-center gap-1.5 flex-wrap">
          <button
            v-for="[name, hex] in colorNames" :key="name" type="button"
            class="size-6 rounded-full border border-black/15 dark:border-white/20"
            :class="(field === 'color' ? color : background) === name ? 'ring-2 ring-primary ring-offset-1 dark:ring-offset-gray-900' : ''"
            :style="{ background: hex }" :title="name" :aria-label="`${field}: ${name}`" :data-testid="`favicon-${field}-${name}`"
            @click="field === 'color' ? color = name : background = name"
          />
          <input
            type="color" class="h-7 w-9 rounded border border-gray-300 dark:border-gray-700 bg-transparent"
            :value="hexOf(field === 'color' ? color : background, catalogue?.defaults[field] ?? '')"
            :aria-label="`${field} (custom)`"
            @input="field === 'color' ? color = ($event.target as HTMLInputElement).value : background = ($event.target as HTMLInputElement).value"
          >
          <UInput
            :model-value="field === 'color' ? color : background" size="xs" class="w-24 font-mono"
            :placeholder="catalogue?.defaults[field]" :aria-label="`${field} (name or hex)`"
            :color="isValidColor(field === 'color' ? color : background) ? undefined : 'error'"
            @update:model-value="field === 'color' ? color = String($event).trim() : background = String($event).trim()"
          />
        </div>
      </div>
    </div>

    <div class="space-y-2 border-t border-gray-200 dark:border-gray-800 pt-4">
      <div class="flex gap-2 flex-wrap">
        <UButton
          icon="i-lucide-save" label="Save to tenant.json" :loading="saving" data-testid="save-favicon"
          :disabled="!dirty || !!loadError || !isValidColor(color) || !isValidColor(background)" @click="save"
        />
        <UButton icon="i-lucide-copy" label="Copy JSON" color="neutral" variant="outline" @click="copySnippet" />
        <UButton label="Reset to default" color="neutral" variant="ghost" :disabled="!Object.keys(current).length" @click="resetToDefault" />
        <UButton v-if="dirty" label="Discard" color="neutral" variant="ghost" @click="applyStored" />
      </div>
      <p v-if="message" class="text-xs" data-testid="favicon-message">{{ message }}</p>
      <p class="text-xs text-gray-500">
        Not set = default: {{ defaultGlyph }} in {{ catalogue?.defaults.color }} on {{ catalogue?.defaults.background }}.
        If this tenant's files come from a Git repository (<code>tools/cv-sync.sh</code>), copy the JSON into
        <code>tenant.json</code> there – the next sync overwrites changes saved here.
      </p>
    </div>
  </section>
</template>
