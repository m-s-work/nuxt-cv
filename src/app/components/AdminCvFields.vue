<script setup lang="ts">
/**
 * Form for one object of the CV (a block like "profile" or a list item like an experience), generated from the
 * editor schema (utils/cvEditorSchema.ts). Edits the object in place; keys without a field are left untouched.
 */
import { setField, toLines, unknownKeys, type CvEditorField } from '~/utils/cvEditorSchema'

const props = defineProps<{
  fields: CvEditorField[]
  value: Record<string, unknown>
  /** Offer grants per field (`fieldRequires`). */
  fieldRequires?: boolean
  /** Keys handled elsewhere (e.g. list item ids), not reported as unknown. */
  ignore?: string[]
}>()

const get = (key: string) => props.value[key]
const text = (key: string) => {
  const v = get(key)
  return v === null || v === undefined ? '' : String(v)
}
const list = (key: string) => {
  const v = get(key)
  return Array.isArray(v) ? v.map(String) : []
}
const items = (key: string) => get(key) as Array<Record<string, unknown>> | undefined

function setNumber(key: string, v: string | number) {
  setField(props.value, key, v === '' || v === null || Number.isNaN(Number(v)) ? null : Number(v))
}

function setLines(key: string, event: Event) {
  setField(props.value, key, toLines((event.target as HTMLTextAreaElement).value))
}

// --- Grants per field (details.fieldRequires) --------------------------------------------

const fieldGrants = (key: string) => {
  const map = get('fieldRequires') as Record<string, string[]> | undefined
  return Array.isArray(map?.[key]) ? map[key] : []
}

function setFieldGrants(key: string, grants: string[]) {
  const map = { ...(get('fieldRequires') as Record<string, string[]> | undefined ?? {}) }
  setField(map, key, grants)
  setField(props.value, 'fieldRequires', Object.keys(map).length ? map : null)
}

const scalarFields = computed(() => props.fields.filter(f => f.type !== 'list' && f.type !== 'object'))
const others = computed(() => unknownKeys(props.value, props.fields, [...(props.ignore ?? []), ...(props.fieldRequires ? ['fieldRequires'] : [])]))
</script>

<template>
  <div class="space-y-3">
    <div class="grid gap-3 sm:grid-cols-2">
      <template v-for="f in fields" :key="f.key">
        <div :class="{ 'sm:col-span-2': f.wide || f.type === 'list' }">
          <!-- Nested list (e.g. intro stats) -->
          <div v-if="f.type === 'list'" class="space-y-1">
            <span class="text-xs font-medium text-gray-500 block">{{ f.label }}</span>
            <AdminCvList :field="f" :items="items(f.key)" @create="setField(value, f.key, $event)" />
          </div>

          <label v-else-if="f.type === 'boolean'" class="flex items-center gap-2 text-sm pt-5">
            <USwitch :model-value="get(f.key) === true" @update:model-value="setField(value, f.key, $event ? true : null)" />
            {{ f.label }}
          </label>

          <label v-else class="block space-y-1">
            <span class="text-xs font-medium text-gray-500 block">{{ f.label }}</span>
            <UTextarea
              v-if="f.type === 'textarea'" :model-value="text(f.key)" :placeholder="f.placeholder" autoresize :rows="3" class="w-full"
              @update:model-value="setField(value, f.key, $event)"
            />
            <UInput
              v-else-if="f.type === 'number'" type="number" :model-value="text(f.key)" :placeholder="f.placeholder" class="w-full"
              @update:model-value="setNumber(f.key, $event)"
            />
            <UInputTags
              v-else-if="f.type === 'tags' || f.type === 'grants'" :model-value="list(f.key)" class="w-full"
              :placeholder="f.type === 'grants' ? 'grant, e.g. private' : 'Add…'" :color="f.type === 'grants' ? 'warning' : 'primary'"
              @update:model-value="setField(value, f.key, $event)"
            />
            <textarea
              v-else-if="f.type === 'lines'" :value="list(f.key).join('\n')" :placeholder="`${f.placeholder ?? ''} (one per line)`" rows="2"
              class="w-full text-sm font-mono rounded-md px-2.5 py-1.5 bg-transparent ring ring-inset ring-gray-300 dark:ring-gray-700 focus:outline-none focus:ring-2 focus:ring-primary"
              @change="setLines(f.key, $event)"
            />
            <UInput
              v-else :model-value="text(f.key)" :placeholder="f.placeholder" class="w-full"
              @update:model-value="setField(value, f.key, $event)"
            />
            <span v-if="f.hint" class="text-xs text-gray-500 block">{{ f.hint }}</span>
          </label>
        </div>
      </template>
    </div>

    <details v-if="fieldRequires" class="rounded border border-gray-200 dark:border-gray-800 p-2 text-sm">
      <summary class="cursor-pointer text-xs font-medium text-gray-500">Grants per field</summary>
      <p class="text-xs text-gray-500 my-2">A field is only delivered to profiles / invites that have all of its grants (e.g. <code>contact</code>).</p>
      <div class="grid gap-2 sm:grid-cols-2">
        <label v-for="f in scalarFields" :key="f.key" class="block space-y-1">
          <span class="text-xs text-gray-500 block">{{ f.label }}</span>
          <UInputTags
            :model-value="fieldGrants(f.key)" placeholder="grant" color="warning" size="sm" class="w-full"
            @update:model-value="setFieldGrants(f.key, $event)"
          />
        </label>
      </div>
    </details>

    <p v-if="others.length" class="text-xs text-gray-500">
      Also contains <code>{{ others.join(', ') }}</code> – kept as is, edit them in the Files tab.
    </p>
  </div>
</template>
