<script setup lang="ts">
/** Editable list of CV entries (experiences, languages, stats, …): collapse, reorder, duplicate, remove, add. */
import { moveItem, newListItem, type CvEditorField } from '~/utils/cvEditorSchema'

const props = defineProps<{
  field: CvEditorField
  /** Undefined while the CV has no such list yet ("create" adds it). */
  items?: Array<Record<string, unknown>>
}>()
const emit = defineEmits<{ create: [items: Array<Record<string, unknown>>] }>()

// Stable keys and open state per item object (indexes change when items move).
const keys = new WeakMap<object, number>()
let nextKey = 0
const keyOf = (item: object) => {
  const raw = toRaw(item)
  if (!keys.has(raw)) keys.set(raw, nextKey++)
  return keys.get(raw)!
}
const open = ref(new Set<number>())
const isOpen = (item: object) => open.value.has(keyOf(item))
function toggle(item: object) {
  const key = keyOf(item)
  if (open.value.has(key)) open.value.delete(key)
  else open.value.add(key)
}

const titleOf = (item: Record<string, unknown>, index: number) =>
  props.field.itemTitle?.(item) || `${props.field.itemName ?? 'item'} ${index + 1}`

function add() {
  const item = newListItem(props.field, props.items ?? [])
  // Dated entries are listed newest first; others (languages, stats) in reading order.
  if (!props.items) emit('create', [item])
  else if (props.field.ids) props.items.unshift(item)
  else props.items.push(item)
  open.value.add(keyOf(item))
}

function duplicate(index: number) {
  const items = props.items!
  const copy = JSON.parse(JSON.stringify(items[index])) as Record<string, unknown>
  if (props.field.ids) copy.id = newListItem(props.field, items).id
  items.splice(index + 1, 0, copy)
  open.value.add(keyOf(copy))
}

function remove(index: number) {
  const item = props.items![index]!
  if (!confirm(`Remove "${titleOf(item, index)}"?`)) return
  props.items!.splice(index, 1)
}
</script>

<template>
  <div class="space-y-2">
    <UButton size="xs" color="neutral" variant="outline" icon="i-lucide-plus" :label="`Add ${field.itemName ?? 'item'}`" @click="add" />
    <p v-if="!items?.length" class="text-xs text-gray-500">None yet.</p>
    <div
      v-for="(item, index) in items" :key="keyOf(item)"
      class="rounded-md border border-gray-200 dark:border-gray-800 bg-gray-50/50 dark:bg-gray-950/40"
      data-testid="cv-list-item"
    >
      <div class="flex items-center gap-1 px-2 py-1">
        <button type="button" class="flex-1 min-w-0 flex items-center gap-2 text-left text-sm py-1" :aria-expanded="isOpen(item)" @click="toggle(item)">
          <UIcon :name="isOpen(item) ? 'i-lucide-chevron-down' : 'i-lucide-chevron-right'" class="shrink-0 text-gray-500" />
          <span class="truncate">{{ titleOf(item, index) }}</span>
          <UBadge v-if="Array.isArray(item.requires) && item.requires.length" :label="`needs ${item.requires.join(', ')}`" color="warning" variant="subtle" size="sm" class="shrink-0" />
        </button>
        <UButton size="xs" color="neutral" variant="ghost" icon="i-lucide-arrow-up" aria-label="Move up" :disabled="index === 0" @click="moveItem(items!, index, -1)" />
        <UButton size="xs" color="neutral" variant="ghost" icon="i-lucide-arrow-down" aria-label="Move down" :disabled="index === items!.length - 1" @click="moveItem(items!, index, 1)" />
        <UButton size="xs" color="neutral" variant="ghost" icon="i-lucide-copy" aria-label="Duplicate" @click="duplicate(index)" />
        <UButton size="xs" color="error" variant="ghost" icon="i-lucide-trash-2" aria-label="Remove" @click="remove(index)" />
      </div>
      <div v-if="isOpen(item)" class="px-3 pb-3 pt-1 border-t border-gray-200 dark:border-gray-800">
        <AdminCvFields :fields="field.fields ?? []" :value="item" :ignore="field.ids ? ['id', 'period'] : []" />
      </div>
    </div>
  </div>
</template>
