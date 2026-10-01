<script setup lang="ts">
/**
 * Heatmap overlay of the owner's heatmap view (/cv?heatmap=1, docs/VISITOR_SESSION_TRACKING.md R6.8): looks up every
 * anchor (`data-track`) in the rendered CV and paints the aggregated cells into it, so the heatmap fits the layout
 * even though visitors had different screen sizes. Only reachable with the admin key; never tracked.
 */
import { formatDuration, type HeatmapData, type HeatmapType } from '~/composables/useAdmin'

const params = new URLSearchParams(window.location.search)
const type = (params.get('type') ?? 'move') as HeatmapType
const canvas = ref<HTMLCanvasElement | null>(null)
const data = ref<HeatmapData | null>(null)
const error = ref('')
const missing = ref(0)
/** Highest weight on the page (legend scale): ms for cursor / attention, count for clicks. */
const maxWeight = ref(0)
const maxLabel = computed(() => type === 'click'
  ? `${maxWeight.value} click${maxWeight.value === 1 ? '' : 's'}`
  : formatDuration(maxWeight.value))
const GRADIENT = 'linear-gradient(90deg, #2563eb 10%, #10b981 40%, #facc15 70%, #dc2626 100%)'

/** First visible element of an anchor (mobile and desktop layouts both contain some anchors). */
function elementOf(anchor: string): HTMLElement | null {
  const candidates = Array.from(document.querySelectorAll<HTMLElement>(`[data-track="${CSS.escape(anchor)}"]`))
  return candidates.find(el => el.getClientRects().length > 0 && el.offsetParent !== null) ?? null
}

function pageRect(el: HTMLElement) {
  const r = el.getBoundingClientRect()
  return { left: r.left + window.scrollX, top: r.top + window.scrollY, width: r.width, height: r.height }
}

/** Blue → green → yellow → red for 0..1. */
function palette(): Uint8ClampedArray {
  const c = document.createElement('canvas')
  c.width = 256
  c.height = 1
  const ctx = c.getContext('2d')!
  const g = ctx.createLinearGradient(0, 0, 256, 0)
  g.addColorStop(0.1, '#2563eb')
  g.addColorStop(0.4, '#10b981')
  g.addColorStop(0.7, '#facc15')
  g.addColorStop(1, '#dc2626')
  ctx.fillStyle = g
  ctx.fillRect(0, 0, 256, 1)
  return ctx.getImageData(0, 0, 256, 1).data
}

function draw() {
  const el = canvas.value
  if (!el || !data.value) return
  const width = document.documentElement.scrollWidth
  const height = document.documentElement.scrollHeight
  el.width = width
  el.height = height
  const ctx = el.getContext('2d')!
  ctx.clearRect(0, 0, width, height)
  missing.value = 0

  if (data.value.type === 'attention') {
    const anchors = data.value.anchors ?? []
    const max = Math.max(1, ...anchors.map(a => a.weight))
    maxWeight.value = anchors.length ? max : 0
    const colors = palette()
    for (const a of anchors) {
      const target = elementOf(a.anchor)
      if (!target) { missing.value++; continue }
      const r = pageRect(target)
      const i = Math.min(255, Math.round((a.weight / max) * 255)) * 4
      ctx.fillStyle = `rgba(${colors[i]}, ${colors[i + 1]}, ${colors[i + 2]}, 0.35)`
      ctx.fillRect(r.left, r.top, r.width, r.height)
    }
    return
  }

  // Move / click: intensity pass in alpha, then colourised.
  const cells = data.value.cells ?? []
  const max = Math.max(1, ...cells.map(c => c.w))
  maxWeight.value = cells.length ? max : 0
  const radius = type === 'click' ? 18 : 28
  const rects = new Map<string, ReturnType<typeof pageRect> | null>()
  for (const cell of cells) {
    if (!rects.has(cell.anchor)) {
      const target = elementOf(cell.anchor)
      rects.set(cell.anchor, target ? pageRect(target) : null)
      if (!target) missing.value++
    }
    const r = rects.get(cell.anchor)
    if (!r) continue
    const x = r.left + (cell.x / 100) * r.width
    const y = r.top + (cell.y / 100) * r.height
    const g = ctx.createRadialGradient(x, y, 0, x, y, radius)
    g.addColorStop(0, `rgba(0, 0, 0, ${Math.max(0.08, cell.w / max)})`)
    g.addColorStop(1, 'rgba(0, 0, 0, 0)')
    ctx.fillStyle = g
    ctx.fillRect(x - radius, y - radius, radius * 2, radius * 2)
  }
  const image = ctx.getImageData(0, 0, width, height)
  const colors = palette()
  for (let i = 0; i < image.data.length; i += 4) {
    const alpha = image.data[i + 3]!
    if (!alpha) continue
    const c = Math.min(255, alpha) * 4
    image.data[i] = colors[c]!
    image.data[i + 1] = colors[c + 1]!
    image.data[i + 2] = colors[c + 2]!
    image.data[i + 3] = Math.min(200, 60 + alpha)
  }
  ctx.putImageData(image, 0, 0)
}

let timer: ReturnType<typeof setTimeout> | undefined
function redraw() {
  clearTimeout(timer)
  timer = setTimeout(draw, 200)
}

onMounted(async () => {
  try {
    data.value = await useAdmin().heatmap(params.get('tenant') ?? '', {
      group: params.get('group') ?? undefined,
      bp: params.get('bp') ?? undefined,
      appSha: params.get('app') ?? undefined,
      cvVersion: params.get('cv') ?? undefined,
      type
    })
  } catch (e) {
    const status = (e as { statusCode?: number })?.statusCode
    error.value = status === 401 || status === 403
      ? 'Heatmap data could not be loaded: the admin key is missing or wrong in this tab.'
      : status === 404
        ? 'Heatmap data could not be loaded: unknown tenant.'
        : `Heatmap data could not be loaded (${status ? `HTTP ${status}` : 'network error'}).`
    return
  }
  // Let fonts, images and the intro animation settle before measuring.
  setTimeout(draw, 1500)
  window.addEventListener('resize', redraw)
  new ResizeObserver(redraw).observe(document.body)
})
onUnmounted(() => window.removeEventListener('resize', redraw))
</script>

<template>
  <div class="print:hidden">
    <canvas ref="canvas" class="pointer-events-none absolute left-0 top-0 z-[90]" data-testid="heatmap-canvas" />
    <div class="fixed bottom-3 left-3 z-[95] rounded-lg bg-gray-900/85 text-white text-xs px-3 py-2 shadow-lg">
      <template v-if="error">{{ error }}</template>
      <template v-else>
        Heatmap · {{ type }} · {{ data?.cells?.length ?? data?.anchors?.length ?? 0 }} {{ type === 'attention' ? 'anchors' : 'cells' }}
        <span v-if="missing"> · {{ missing }} anchors not in this layout</span>
        <div v-if="maxWeight" class="mt-1 flex items-center gap-2" data-testid="heatmap-scale">
          <span>0</span>
          <span class="h-2 w-28 rounded" :style="{ background: GRADIENT }" />
          <span>{{ maxLabel }}{{ type === 'attention' ? ' in view' : type === 'move' ? ' dwell' : '' }}</span>
        </div>
      </template>
    </div>
  </div>
</template>
