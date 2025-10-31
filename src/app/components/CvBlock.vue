<script setup lang="ts">
import type { LightboxImage } from '~/composables/useLightbox'

const { t } = useI18n()
const { getAssetPath } = useAssetPath()

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
  techClickable?: boolean
  techSelected?: (tech: string) => boolean
  imagesClickable?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  activeIds: () => [],
  type: 'other',
  showPeriod: true,
  showTechnologies: true,
  imagesClickable: true
})

const emit = defineEmits(['tech-click'])

// Lightbox support
const { open: openLightbox } = useLightbox()

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

// Handle image click to open lightbox
function handleImageClick(index: number) {
  if (!props.imagesClickable || !props.images || props.images.length === 0) return
  
  const lightboxImages: LightboxImage[] = props.images.map((src, i) => ({
    src: getAssetPath(src),
    alt: `${props.title} image ${i + 1}`,
    groupId: `${props.type}-${props.id}`
  }))
  
  openLightbox(lightboxImages, index, `${props.type}-${props.id}`)
}
</script>

<template>
  <!-- 
    Border removal: UCard components have default borders AND rings in Nuxt UI.
    We override with !border-0 to remove borders and !ring-0 to remove the ring.
    The ring creates a box-shadow that appears as a border.
  -->
  <UCard 
    :id="`${type}-${id}`"
    :class="{
      '!border-0 !ring-0': true,
      'print:!shadow-none': true,
      'transition-all duration-300': true,
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
        <!-- 
          Period badge: Remove border and ring by adding !border-0 and !ring-0 classes.
          UBadge with variant="subtle" may have default border/ring in Nuxt UI.
        -->
        <UBadge 
          v-if="period && showPeriod" 
          color="primary" 
          variant="subtle" 
          class="!border-0 !ring-0 print:!bg-gray-100 print:!text-black"
        >
          {{ period }}
        </UBadge>
      </div>
    </template>

    <!-- Images (for projects or other entries with images) -->
    <div v-if="images && images.length > 0" class="mb-4 flex flex-wrap gap-2">
      <div
        v-for="(image, index) in images" 
        :key="index"
        class="relative group"
        :class="{
          'cursor-pointer': imagesClickable
        }"
        @click="handleImageClick(index)"
      >
        <img 
          :src="getAssetPath(image)"
          :alt="`${title} image ${index + 1}`"
          class="h-32 w-auto object-cover rounded border border-gray-200 dark:border-gray-700 print:border-gray-300 transition-all duration-200"
          :class="{
            'group-hover:brightness-75 group-hover:scale-105': imagesClickable
          }"
          loading="lazy"
        />
        <!-- Zoom icon overlay -->
        <div 
          v-if="imagesClickable"
          class="absolute inset-0 flex items-center justify-center opacity-0 group-hover:opacity-100 transition-opacity duration-200 print:hidden"
        >
          <div class="bg-black/50 rounded-full p-2">
            <svg class="w-6 h-6 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0zM10 7v6m3-3H7" />
            </svg>
          </div>
        </div>
      </div>
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
        :clickable="techClickable"
        :selected="techSelected ? techSelected(tech) : false"
        @click="(t) => emit('tech-click', t)"
      />
    </div>
  </UCard>
</template>
