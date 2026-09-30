<script setup lang="ts">
/**
 * Image field of the CV editor (photo, logos, images, screenshots): pick from the tenant's assets, upload new ones
 * or enter a link to an image on a live website (served to visitors through the API's proxy, /api/media).
 */
import { errorMessage } from '~/composables/useAdmin'
import { ASSET_PREFIX, isImageFile } from '~/composables/useTenantAssets'

const props = defineProps<{ tenantId: string, modelValue: string[], multiple?: boolean, placeholder?: string }>()
const emit = defineEmits<{ 'update:modelValue': [value: string[]] }>()

const { assets, loading, error, load, thumb, upload } = useTenantAssets(props.tenantId)

const isAsset = (url: string) => url.startsWith(ASSET_PREFIX)
const isExternal = (url: string) => /^https?:\/\//i.test(url)
// Images that failed to load (e.g. an external link that is not an image) show an icon instead.
const failed = ref(new Set<string>())
const src = (url: string) => failed.value.has(url) ? undefined : isAsset(url) ? thumb(url) : isExternal(url) ? url : undefined
const nameOf = (url: string) => isAsset(url) ? url.slice(ASSET_PREFIX.length) : url

function set(value: string[]) {
  emit('update:modelValue', props.multiple ? value : value.slice(-1))
}

function add(urls: string[]) {
  const fresh = urls.filter(u => !props.modelValue.includes(u))
  set(props.multiple ? [...props.modelValue, ...fresh] : urls.slice(-1))
}

function remove(index: number) {
  set(props.modelValue.filter((_, i) => i !== index))
}

function move(index: number, delta: -1 | 1) {
  const items = [...props.modelValue]
  const target = index + delta
  if (target < 0 || target >= items.length) return
  ;[items[index], items[target]] = [items[target]!, items[index]!]
  set(items)
}

// --- Link to an image on a live website, or a path typed by hand ---------------------------

const link = ref('')
const linkError = ref('')
function addLink() {
  const value = link.value.trim()
  if (!value) return
  if (!isExternal(value) && !value.startsWith('/')) {
    linkError.value = 'Enter an https:// link or an /api/assets/… path.'
    return
  }
  linkError.value = ''
  add([value])
  link.value = ''
}

// --- File picker ----------------------------------------------------------------------------

const open = ref(false)
const images = computed(() => assets.value.filter(f => isImageFile(f.path)))
const picked = ref<string[]>([])

function openPicker() {
  picked.value = []
  open.value = true
  load(true)
}

function togglePick(url: string) {
  if (!props.multiple) {
    add([url])
    open.value = false
    return
  }
  picked.value = picked.value.includes(url) ? picked.value.filter(u => u !== url) : [...picked.value, url]
}

function confirmPick() {
  add(picked.value)
  open.value = false
}

const uploading = ref(false)
const fileInput = ref<HTMLInputElement | null>(null)
async function onUpload(event: Event) {
  const input = event.target as HTMLInputElement
  const selected = Array.from(input.files ?? [])
  if (!selected.length) return
  uploading.value = true
  try {
    const urls = await upload(selected)
    if (open.value && props.multiple) picked.value = [...picked.value, ...urls]
    else {
      add(urls)
      open.value = false
    }
  } catch (e) {
    error.value = errorMessage(e)
  } finally {
    uploading.value = false
    input.value = ''
  }
}
</script>

<template>
  <div class="space-y-2" data-testid="asset-picker">
    <ul v-if="modelValue.length" class="flex flex-wrap gap-2">
      <li
        v-for="(url, index) in modelValue" :key="url"
        class="group relative w-24 rounded border border-gray-200 dark:border-gray-800 bg-gray-50 dark:bg-gray-950 overflow-hidden"
        :title="url"
      >
        <img
          v-if="src(url)" :src="src(url)" alt="" class="h-16 w-full object-contain bg-white/60 dark:bg-white/5" referrerpolicy="no-referrer"
          @error="failed.add(url)"
        >
        <div v-else class="h-16 flex items-center justify-center text-gray-400"><UIcon name="i-lucide-image-off" /></div>
        <p class="text-[10px] leading-tight px-1 py-0.5 truncate flex items-center gap-0.5">
          <UIcon v-if="isExternal(url)" name="i-lucide-globe" class="shrink-0 text-primary" />
          {{ nameOf(url) }}
        </p>
        <div class="absolute top-0.5 right-0.5 flex gap-0.5 opacity-0 group-hover:opacity-100 focus-within:opacity-100">
          <template v-if="multiple">
            <UButton size="xs" color="neutral" variant="solid" icon="i-lucide-arrow-left" aria-label="Move left" :disabled="index === 0" @click="move(index, -1)" />
            <UButton size="xs" color="neutral" variant="solid" icon="i-lucide-arrow-right" aria-label="Move right" :disabled="index === modelValue.length - 1" @click="move(index, 1)" />
          </template>
          <UButton size="xs" color="error" variant="solid" icon="i-lucide-x" :aria-label="`Remove ${nameOf(url)}`" @click="remove(index)" />
        </div>
      </li>
    </ul>

    <div class="flex gap-2 flex-wrap">
      <UButton size="xs" color="neutral" variant="outline" icon="i-lucide-folder-open" :label="multiple ? 'Add from files' : 'Choose file'" @click="openPicker" />
      <form class="flex gap-1 flex-1 min-w-48" @submit.prevent="addLink">
        <UInput v-model="link" size="xs" :placeholder="placeholder ?? 'or link: https://example.com/logo.png'" class="flex-1" aria-label="Image link" />
        <UButton type="submit" size="xs" color="neutral" variant="ghost" icon="i-lucide-link" aria-label="Add link" :disabled="!link.trim()" />
      </form>
    </div>
    <p v-if="linkError" class="text-xs text-red-600 dark:text-red-400">{{ linkError }}</p>

    <UModal v-model:open="open" :title="multiple ? 'Add images' : 'Choose an image'" description="Assets of this tenant. Uploads are stored under assets/." :ui="{ content: 'max-w-3xl' }">
      <template #body>
        <div class="flex items-center gap-2 mb-3">
          <UButton size="sm" icon="i-lucide-upload" label="Upload" :loading="uploading" @click="fileInput?.click()" />
          <input ref="fileInput" type="file" accept="image/*" :multiple="multiple" class="hidden" @change="onUpload">
          <span v-if="loading" class="text-sm text-gray-500">Loading…</span>
          <span v-if="error" class="text-sm text-red-600 dark:text-red-400">{{ error }}</span>
        </div>
        <p v-if="!loading && !images.length" class="text-sm text-gray-500">No images yet: upload one.</p>
        <div class="grid grid-cols-3 sm:grid-cols-5 gap-2 max-h-[60vh] overflow-y-auto" data-testid="asset-grid">
          <button
            v-for="f in images" :key="f.path" type="button"
            class="rounded border-2 overflow-hidden text-left transition-colors"
            :class="picked.includes(ASSET_PREFIX + f.path.slice(7)) || modelValue.includes(ASSET_PREFIX + f.path.slice(7))
              ? 'border-primary' : 'border-transparent hover:border-gray-300 dark:hover:border-gray-700'"
            :aria-pressed="picked.includes(ASSET_PREFIX + f.path.slice(7))"
            @click="togglePick(ASSET_PREFIX + f.path.slice(7))"
          >
            <img v-if="thumb(f.path)" :src="thumb(f.path)" :alt="f.path.slice(7)" class="h-20 w-full object-contain bg-white/60 dark:bg-white/5">
            <div v-else class="h-20" />
            <span class="block text-[10px] px-1 py-0.5 truncate">{{ f.path.slice(7) }}</span>
          </button>
        </div>
      </template>
      <template v-if="multiple" #footer>
        <div class="flex justify-end gap-2 w-full">
          <UButton color="neutral" variant="ghost" label="Cancel" @click="open = false" />
          <UButton :label="`Add ${picked.length || ''}`" :disabled="!picked.length" @click="confirmPick" />
        </div>
      </template>
    </UModal>
  </div>
</template>
