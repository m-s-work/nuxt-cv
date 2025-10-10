<script setup lang="ts">
interface Props {
  technology: string
  clickable?: boolean
  selected?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  clickable: false,
  selected: false
})

const emit = defineEmits<{
  click: [technology: string]
}>()

const handleClick = () => {
  if (props.clickable) {
    emit('click', props.technology)
  }
}
</script>

<template>
  <UBadge 
    :color="selected ? 'primary' : 'gray'"
    :variant="selected ? 'solid' : 'solid'"
    size="md"
    :class="{
      'print:!bg-gray-100 print:!text-black rounded-full px-3 py-1 border': true,
      'border-blue-600 dark:border-blue-400': selected,
      'border-gray-400 dark:border-gray-500': !selected,
      'cursor-pointer hover:border-blue-500 dark:hover:border-blue-400 hover:bg-gray-200 dark:hover:bg-gray-600 transition-all': clickable,
      'shadow-md': selected
    }"
    @click="handleClick"
  >
    {{ technology }}
  </UBadge>
</template>
