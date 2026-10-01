<script setup lang="ts">
/**
 * Heatmap of a visitor group: the CV snapshot of the chosen version rendered at the chosen breakpoint in an
 * iframe (/cv?heatmap=1), with the aggregated cells painted over it (docs/VISITOR_SESSION_TRACKING.md §6).
 */
import { errorMessage, shortSha, type AdminTenant, type HeatmapFacet, type HeatmapType } from '~/composables/useAdmin'
import { BREAKPOINT_WIDTH, type Breakpoint } from '~/utils/tracking'

const props = defineProps<{ tenant: AdminTenant, group: string }>()
const admin = useAdmin()

const facets = ref<HeatmapFacet[]>([])
const type = ref<HeatmapType>('move')
const selection = ref('')
const error = ref('')

const facetKey = (f: HeatmapFacet) => `${f.breakpoint}|${f.appSha}|${f.cvVersion}`
const facetItems = computed(() => facets.value
  .slice().sort((a, b) => b.weight - a.weight)
  .map(f => ({ label: `${f.breakpoint} · app ${shortSha(f.appSha)} · CV ${f.cvVersion}`, value: facetKey(f) })))
const typeItems = [
  { label: 'Cursor', value: 'move' },
  { label: 'Clicks', value: 'click' },
  { label: 'Attention (time per section, incl. mobile)', value: 'attention' }
]

const current = computed(() => facets.value.find(f => facetKey(f) === selection.value))
const width = computed(() => BREAKPOINT_WIDTH[(current.value?.breakpoint ?? 'xl') as Breakpoint] ?? 1440)
const src = computed(() => {
  const f = current.value
  if (!f) return ''
  const q = new URLSearchParams({
    heatmap: '1', tenant: props.tenant.id, group: props.group, bp: f.breakpoint, app: f.appSha, cv: f.cvVersion, type: type.value
  })
  return `/cv?${q}`
})
const otherApp = computed(() => new Set(facets.value.map(f => f.appSha)).size > 1)

async function load() {
  error.value = ''
  try {
    facets.value = (await admin.heatmap(props.tenant.id, { group: props.group, type: 'move' })).facets
    if (!facets.value.some(f => facetKey(f) === selection.value)) selection.value = facetItems.value[0]?.value ?? ''
  } catch (e) {
    error.value = errorMessage(e)
  }
}

onMounted(load)
</script>

<template>
  <div class="space-y-2" data-testid="admin-heatmap">
    <div class="flex gap-2 flex-wrap items-center">
      <USelect v-model="type" :items="typeItems" class="min-w-56" aria-label="Heatmap type" />
      <USelect v-model="selection" :items="facetItems" class="min-w-72" aria-label="Layout and version" :disabled="!facetItems.length" />
      <span class="text-xs text-gray-500">Rendered with the CV text this version's visitors saw.</span>
    </div>
    <p v-if="otherApp" class="text-xs text-amber-700 dark:text-amber-300">
      Visitors saw several app versions. The page below uses the current app; cells are exact only for sessions of the same app version (R6.13).
    </p>
    <p v-if="error" class="text-sm text-red-600">{{ error }}</p>
    <p v-else-if="!facetItems.length" class="text-sm text-gray-500">No cursor data yet (touch devices only record taps and attention).</p>
    <div v-if="src" class="overflow-x-auto rounded-md border border-gray-200 dark:border-gray-800 bg-gray-100 dark:bg-gray-950">
      <iframe :key="src" :src="src" :style="{ width: `${width}px` }" class="h-[80vh] bg-white" title="Heatmap" />
    </div>
  </div>
</template>
