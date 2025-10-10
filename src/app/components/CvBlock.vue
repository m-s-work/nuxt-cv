<script setup lang="ts">
interface Props {
  id: number | string
  title: string
  subtitle?: string
  period?: string
  description?: string
  technologies?: string[]
  images?: string[]
  activeIds?: (number | string)[]
  type?: 'experience' | 'study' | 'project' | 'other'
  showPeriod?: boolean
  showTechnologies?: boolean
  icon?: string
}

const props = withDefaults(defineProps<Props>(), {
  activeIds: () => [],
  type: 'other',
  showPeriod: true,
  showTechnologies: true
})

// Check if this block is active
function isActive(): boolean {
  const idKey = props.type === 'experience' ? `exp-${props.id}` : 
                props.type === 'study' ? `study-${props.id}` : 
                props.type === 'project' ? `project-${props.id}` : 
                `other-${props.id}`
  return props.activeIds.includes(idKey)
}

// Handle heading click to update URL hash
function handleHeadingClick(elementId: string) {
  if (typeof window !== 'undefined') {
    window.history.pushState(null, '', `#${elementId}`)
  }
}

// Color classes based on type
const activeColorClass = computed(() => {
  switch (props.type) {
    case 'experience':
      return 'text-blue-600 dark:text-blue-400'
    case 'study':
      return 'text-green-600 dark:text-green-400'
    case 'project':
      return 'text-purple-600 dark:text-purple-400'
    default:
      return 'text-orange-600 dark:text-orange-400'
  }
})
</script>

<template>
  <UCard 
    :id="`${type}-${id}`"
    :class="{
      'print:!shadow-none print:!border print:!border-gray-300': true,
      'transition-all duration-300': true,
      'shadow-lg': isActive(),
      'translate-x-2': isActive()
    }"
  >
    <template #header>
      <div class="flex justify-between items-start">
        <div :class="{ 'flex-1': images && images.length > 0 }">
          <h3 
            @click="handleHeadingClick(`${type}-${id}`)"
            :class="{
              'text-xl font-semibold print:text-black cursor-pointer hover:underline': true,
              [activeColorClass]: isActive(),
              'text-gray-900 dark:text-white': !isActive()
            }"
          >
            {{ title }}
          </h3>
          <p v-if="subtitle" class="text-gray-600 dark:text-gray-400 print:text-gray-700">
            {{ subtitle }}
          </p>
        </div>
        <UBadge 
          v-if="period && showPeriod" 
          color="primary" 
          variant="subtle" 
          class="print:!bg-gray-100 print:!text-black"
        >
          {{ period }}
        </UBadge>
      </div>
    </template>

    <!-- Images (for projects or other entries with images) -->
    <div v-if="images && images.length > 0" class="mb-4 flex flex-wrap gap-2">
      <img 
        v-for="(image, index) in images" 
        :key="index"
        :src="image"
        :alt="`${title} image ${index + 1}`"
        class="h-32 w-auto object-cover rounded border border-gray-200 dark:border-gray-700 print:border-gray-300"
        loading="lazy"
      />
    </div>

    <!-- Description -->
    <p v-if="description" class="text-gray-700 dark:text-gray-300 print:text-black mb-4">
      {{ description }}
    </p>

    <!-- Technology Badges -->
    <div v-if="technologies && technologies.length > 0 && showTechnologies" class="flex flex-wrap gap-2">
      <TechBadge 
        v-for="tech in technologies" 
        :key="tech"
        :technology="tech"
      />
    </div>
  </UCard>
</template>
