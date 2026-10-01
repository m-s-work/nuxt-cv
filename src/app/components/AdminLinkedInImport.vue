<script setup lang="ts">
/**
 * LinkedIn data export import (docs/REQUIREMENTS_SAAS.md §7): upload the ZIP, preview what was found, then apply
 * it as cv.<locale>.json. The ZIP is parsed by the API in memory and not stored.
 */
import { errorMessage, type LinkedInImport } from '~/composables/useAdmin'
import { accountErrorText } from '~/utils/account'

const props = defineProps<{ locale: string, /** A CV in this locale exists and would be replaced. */ replaces?: boolean }>()
const emit = defineEmits<{ applied: [result: LinkedInImport] }>()
const admin = useAdmin()

const file = ref<File | null>(null)
const preview = ref<LinkedInImport | null>(null)
const busy = ref(false)
const error = ref('')
const input = ref<HTMLInputElement | null>(null)

const countLabels: Record<keyof LinkedInImport['counts'], string> = {
  experiences: 'Positions', studies: 'Education', skills: 'Skills', languages: 'Languages', projects: 'Projects', certifications: 'Certifications'
}

function describe(e: unknown) {
  const err = e as { statusCode?: number, data?: { error?: string, message?: string } }
  if (err.statusCode === 413) return 'The file is too large (max. 20 MB). Request the export again with fewer data categories.'
  if (err.data?.error === 'import_failed') return `${accountErrorText('import_failed')}${err.data.message ? ` (${err.data.message})` : ''}`
  return err.data?.error ? accountErrorText(err.data.error) : errorMessage(e)
}

async function choose(event: Event) {
  const target = event.target as HTMLInputElement
  const selected = target.files?.[0]
  target.value = ''
  if (!selected) return
  file.value = selected
  preview.value = null
  error.value = ''
  busy.value = true
  try {
    preview.value = await admin.importLinkedIn(selected, props.locale, false)
  } catch (e) {
    error.value = describe(e)
  } finally {
    busy.value = false
  }
}

async function apply() {
  if (!file.value) return
  if (props.replaces && !confirm(`This replaces your current CV (cv.${props.locale}.json). Continue?`)) return
  busy.value = true
  error.value = ''
  try {
    const result = await admin.importLinkedIn(file.value, props.locale, true)
    emit('applied', result)
  } catch (e) {
    error.value = describe(e)
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <div class="space-y-3 text-sm" data-testid="linkedin-import">
    <details class="rounded-md bg-gray-50 dark:bg-gray-950 border border-gray-200 dark:border-gray-800 p-3" open>
      <summary class="font-medium cursor-pointer">How to get your LinkedIn data export</summary>
      <ol class="list-decimal pl-5 mt-2 space-y-1 text-gray-600 dark:text-gray-300">
        <li>On LinkedIn open <b>Settings &amp; Privacy → Data privacy → Get a copy of your data</b>.</li>
        <li>Choose the larger archive, or select <b>Profile, Positions, Education, Skills</b> (plus Languages, Projects and Certifications if you like).</li>
        <li>Click <b>Request archive</b>. LinkedIn e-mails you a download link (the smaller selection is usually ready within minutes, the full archive can take up to a day).</li>
        <li>Download the ZIP and upload it here as it is – no need to unpack it.</li>
      </ol>
      <p class="text-xs text-gray-500 mt-2">The ZIP is read once to build your CV and is not stored.</p>
    </details>

    <div class="flex items-center gap-2 flex-wrap">
      <UButton icon="i-lucide-file-archive" label="Choose LinkedIn ZIP…" color="neutral" variant="outline" :loading="busy && !preview" @click="input?.click()" />
      <input ref="input" type="file" accept=".zip,application/zip" class="hidden" data-testid="linkedin-file" @change="choose">
      <span v-if="file" class="text-xs text-gray-500 truncate max-w-64">{{ file.name }}</span>
    </div>

    <div v-if="preview" class="rounded-md border border-gray-200 dark:border-gray-800 p-3 space-y-2" data-testid="linkedin-preview">
      <p class="font-medium">Found in your export:</p>
      <div class="flex gap-2 flex-wrap">
        <UBadge
          v-for="(label, key) in countLabels" :key="key" :label="`${label}: ${preview.counts[key]}`"
          :color="preview.counts[key] ? 'primary' : 'neutral'" variant="subtle"
        />
      </div>
      <ul v-if="preview.warnings.length" class="text-xs text-amber-700 dark:text-amber-300 list-disc pl-5">
        <li v-for="w in preview.warnings" :key="w">{{ w }}</li>
      </ul>
      <p class="text-xs text-gray-500">Files read: {{ preview.filesRead.join(', ') || '–' }}</p>
      <UButton icon="i-lucide-check" :label="`Use this as my CV (${locale})`" :loading="busy" @click="apply" />
    </div>
    <p v-if="error" class="text-red-600 dark:text-red-400">{{ error }}</p>
  </div>
</template>
