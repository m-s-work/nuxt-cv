import { describe, it, expect, vi } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { mockNuxtImport } from '@nuxt/test-utils/runtime'
import AdminHeatmap from '~/components/AdminHeatmap.vue'
import type { HeatmapFacet } from '~/composables/useAdmin'

const { heatmap } = vi.hoisted(() => ({ heatmap: vi.fn() }))
mockNuxtImport('useAdmin', () => () => ({ heatmap }))

const tenant = { id: 'demo', name: 'Demo' }
const facet = (patch: Partial<HeatmapFacet>): HeatmapFacet =>
  ({ breakpoint: 'xl', appSha: 'abc', cvVersion: '0123456789abcdef', weight: 0, move: 0, click: 0, attentionMs: 0, snapshot: true, ...patch })

// Nuxt UI select replaced by a plain <select>.
const USelect = {
  props: ['modelValue', 'items', 'disabled'],
  emits: ['update:modelValue'],
  template: '<select :value="modelValue" :disabled="disabled" @change="$emit(\'update:modelValue\', $event.target.value)"><option v-for="i in items" :key="i.value" :value="i.value">{{ i.label }}</option></select>'
}

async function render(facets: HeatmapFacet[], group?: string) {
  heatmap.mockResolvedValue({ type: 'move', facets })
  const wrapper = mount(AdminHeatmap, { props: { tenant: tenant as never, group }, global: { stubs: { USelect } } })
  await flushPromises()
  return wrapper
}

describe('AdminHeatmap', () => {
  it('offers attention for layouts with section dwell only (phones)', async () => {
    const wrapper = await render([facet({ breakpoint: 'sm', attentionMs: 9000 })], 'g1')
    expect(wrapper.find('iframe').exists()).toBe(false)
    expect(wrapper.text()).toContain('touch devices only record taps and attention')

    await wrapper.findAll('select')[0]!.setValue('attention')
    await flushPromises()
    const src = wrapper.find('iframe').attributes('src')!
    expect(src).toContain('bp=sm')
    expect(src).toContain('type=attention')
    expect(src).toContain('group=g1')
  })

  it('renders tenant-wide without a group and shows a legend', async () => {
    const wrapper = await render([facet({ move: 500, weight: 500 })])
    expect(heatmap).toHaveBeenLastCalledWith('demo', { group: undefined, type: 'move' })
    expect(wrapper.find('iframe').attributes('src')).not.toContain('group=')
    expect(wrapper.find('[data-testid="heatmap-legend"]').text()).toContain('Cursor dwell')
  })

  it('explains a missing CV snapshot instead of loading the page', async () => {
    const wrapper = await render([facet({ move: 500, weight: 500, snapshot: false })], 'g1')
    expect(wrapper.find('iframe').exists()).toBe(false)
    expect(wrapper.find('[data-testid="heatmap-no-snapshot"]').text()).toContain('No CV snapshot is stored for CV version 0123456789abcdef')
  })
})
