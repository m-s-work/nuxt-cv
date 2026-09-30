<script setup lang="ts">
// "/": the CV on a tenant host. On the shared host it is always the showcase (also for visitors with an
// invite), so the main page stays reachable; the CV lives at /cv there. Old invite links (/?c=…) redirect.
const { locale } = useI18n()
const localePath = useLocalePath()
const { status, hostKind, ensure } = useCv()

// Read before ensure(): it removes the code from the address bar.
const arrivedWithCode = import.meta.client && new URLSearchParams(window.location.search).has(INVITE_PARAM)
// PDF renderer and heatmap URLs of older versions pointed at "/".
const cvOnly = isPrintView() || isHeatmapView()

await ensure(locale.value)

const showcase = computed(() => hostKind.value === 'shared' && !cvOnly)
if (showcase.value && arrivedWithCode && status.value === 'ready') {
  await navigateTo(localePath('/cv'), { replace: true })
}
</script>

<template>
  <CvShowcase v-if="showcase" :has-access="status === 'ready'" />
  <CvView v-else />
</template>
