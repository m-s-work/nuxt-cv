<script setup lang="ts">
// Website link of an entry in the PDF: text link, in mode "qr" with a small QR code to the tracked link.
import type { PrintLink } from '~/composables/usePrintLinks'

defineProps<{ link: PrintLink | null }>()
</script>

<template>
  <p v-if="link" class="print-entry-link">
    <img v-if="link.qr" :src="link.qr" alt="" class="print-entry-link-qr">
    <a :href="link.href">{{ link.text }}</a>
  </p>
</template>

<style scoped>
.print-entry-link {
  display: flex;
  align-items: center;
  gap: 2mm;
  margin: 1mm 0 0;
  font-size: 0.9em;
  break-inside: avoid;
}

.print-entry-link a {
  color: inherit;
  text-decoration: underline;
  text-decoration-thickness: 0.5px;
  overflow-wrap: anywhere;
}

.print-entry-link-qr {
  width: 12mm;
  height: 12mm;
  flex: none;
}
</style>
