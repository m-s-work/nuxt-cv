<script setup lang="ts">
/**
 * Heatmap of a visitor group, or of the whole tenant without a group: the CV snapshot of the chosen version rendered at
 * the chosen breakpoint in an iframe (/cv?heatmap=1), with the aggregated cells painted over it
 * (docs/VISITOR_SESSION_TRACKING.md §6). Attention also covers touch-only layouts (R6.9).
 */
import { errorMessage, shortSha, type AdminTenant, type HeatmapFacet, type HeatmapType } from '~/composables/useAdmin'
import { BREAKPOINT_WIDTH, type Breakpoint } from '~/utils/tracking'
import { upgradeReason, type UpgradeReason } from '~/utils/account'

const props = defineProps<{ tenant: AdminTenant, group?: string | null }>()
const admin = useAdmin()

const facets = ref<HeatmapFacet[]>([])
const type = ref<HeatmapType>('move')
const selection = ref('')
const error = ref('')
const loaded = ref(false)
// Heatmaps are Pro (402 plan_limit, feature "heatmaps"): locked state with an upgrade hint.
const locked = ref<UpgradeReason | null>(null)

const facetKey = (f: HeatmapFacet) => `${f.breakpoint}|${f.appSha}|${f.cvVersion}`
/** Data of a facet for the chosen type; older APIs only report the total cell weight. */
const amount = (f: HeatmapFacet, t: HeatmapType) =>
  t === 'attention' ? f.attentionMs ?? 0 : t === 'click' ? f.click ?? f.weight : f.move ?? f.weight
const facetItems = computed(() => facets.value
  .filter(f => amount(f, type.value) > 0)
  .sort((a, b) => amount(b, type.value) - amount(a, type.value))
  .map(f => ({
    label: `${f.breakpoint} · app ${shortSha(f.appSha)} · CV ${f.cvVersion}${f.snapshot === false ? ' (no snapshot)' : ''}`,
    value: facetKey(f)
  })))
const typeItems = [
  { label: 'Cursor', value: 'move' },
  { label: 'Clicks', value: 'click' },
  { label: 'Attention (time per section, incl. mobile)', value: 'attention' }
]
const legend: Record<HeatmapType, string> = {
  move: 'Cursor dwell time per spot',
  click: 'Clicks / taps per spot',
  attention: 'Time each section / entry was in view'
}

const current = computed(() => facets.value.find(f => facetKey(f) === selection.value))
const width = computed(() => BREAKPOINT_WIDTH[(current.value?.breakpoint ?? 'xl') as Breakpoint] ?? 1440)
const src = computed(() => {
  const f = current.value
  if (!f || f.snapshot === false) return ''
  const q = new URLSearchParams({ heatmap: '1', tenant: props.tenant.id, bp: f.breakpoint, app: f.appSha, cv: f.cvVersion, type: type.value })
  if (props.group) q.set('group', props.group)
  return `/cv?${q}`
})
const otherApp = computed(() => new Set(facets.value.map(f => f.appSha)).size > 1)

function pick() {
  if (!facetItems.value.some(i => i.value === selection.value)) selection.value = facetItems.value[0]?.value ?? ''
}

async function load() {
  error.value = ''
  try {
    facets.value = (await admin.heatmap(props.tenant.id, { group: props.group ?? undefined, type: 'move' })).facets
    pick()
  } catch (e) {
    locked.value = upgradeReason(e)
    if (!locked.value) error.value = errorMessage(e)
  } finally {
    loaded.value = true
  }
}

watch(type, pick)
watch(() => props.group, load)
onMounted(load)
</script>

<template>
  <AdminUpgradeHint v-if="locked" :reason="locked" locked data-testid="admin-heatmap-locked" />
  <div v-else class="space-y-2" data-testid="admin-heatmap">
    <div class="flex gap-2 flex-wrap items-center">
      <USelect v-model="type" :items="typeItems" class="min-w-56" aria-label="Heatmap type" />
      <USelect v-model="selection" :items="facetItems" class="min-w-72" aria-label="Layout and version" :disabled="!facetItems.length" />
      <span class="text-xs text-gray-500">Rendered with the CV text this version's visitors saw.</span>
    </div>
    <div class="flex items-center gap-2 text-xs text-gray-500" data-testid="heatmap-legend">
      <span>{{ legend[type] }}:</span>
      <span>low</span>
      <span class="h-2 w-32 rounded" style="background: linear-gradient(90deg, #2563eb 10%, #10b981 40%, #facc15 70%, #dc2626 100%)" />
      <span>high</span>
      <span>(relative to the busiest spot; the page shows the maximum)</span>
    </div>
    <p v-if="otherApp" class="text-xs text-amber-700 dark:text-amber-300">
      Visitors saw several app versions. The page below uses the current app; cells are exact only for sessions of the same app version (R6.13).
    </p>
    <p v-if="error" class="text-sm text-red-600">{{ error }}</p>
    <p v-else-if="loaded && !facetItems.length" class="text-sm text-gray-500">
      <template v-if="type === 'attention'">No section views recorded yet.</template>
      <template v-else-if="facets.some(f => (f.attentionMs ?? 0) > 0)">
        No {{ type === 'click' ? 'clicks' : 'cursor data' }} yet – touch devices only record taps and attention. Choose “Attention” to see what they read.
      </template>
      <template v-else>No heatmap data yet.</template>
    </p>
    <p v-else-if="current && current.snapshot === false" class="text-sm text-amber-700 dark:text-amber-300" data-testid="heatmap-no-snapshot">
      No CV snapshot is stored for CV version <code>{{ current.cvVersion }}</code>, so this heatmap cannot be rendered.
      Snapshots are stored when a visitor's session starts; data recorded before that (or under a version the server could
      not reproduce) has nothing to be painted on. Choose another version.
    </p>
    <div v-if="src" class="overflow-x-auto rounded-md border border-gray-200 dark:border-gray-800 bg-gray-100 dark:bg-gray-950">
      <iframe :key="src" :src="src" :style="{ width: `${width}px` }" class="h-[80vh] bg-white" title="Heatmap" />
    </div>
  </div>
</template>
