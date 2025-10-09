<script setup lang="ts">
interface Props {
  technology: string
}

defineProps<Props>()
</script>

<template>
  <UBadge 
    color="gray"
    variant="solid"
    size="md"
    class="print:!bg-gray-100 print:!text-black rounded-full px-3 py-1 border border-gray-400 dark:border-gray-500"
  >
    {{ technology }}
  </UBadge>
</template>
