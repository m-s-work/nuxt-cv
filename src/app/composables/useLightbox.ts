import { ref, computed } from 'vue'

export interface LightboxImage {
  src: string
  alt: string
  groupId?: string | number
}

const isOpen = ref(false)
const currentIndex = ref(0)
const images = ref<LightboxImage[]>([])
const currentGroupId = ref<string | number | undefined>(undefined)

export function useLightbox() {
  const currentImage = computed(() => images.value[currentIndex.value])
  
  const hasNext = computed(() => currentIndex.value < images.value.length - 1)
  const hasPrev = computed(() => currentIndex.value > 0)

  function open(imageList: LightboxImage[], index: number = 0, groupId?: string | number) {
    images.value = imageList
    currentIndex.value = index
    currentGroupId.value = groupId
    isOpen.value = true
    
    // Prevent body scroll when lightbox is open
    if (typeof document !== 'undefined') {
      document.body.style.overflow = 'hidden'
    }
  }

  function close() {
    isOpen.value = false
    currentGroupId.value = undefined
    
    // Restore body scroll
    if (typeof document !== 'undefined') {
      document.body.style.overflow = ''
    }
  }

  function next() {
    if (hasNext.value) {
      currentIndex.value++
    }
  }

  function prev() {
    if (hasPrev.value) {
      currentIndex.value--
    }
  }

  function goTo(index: number) {
    if (index >= 0 && index < images.value.length) {
      currentIndex.value = index
    }
  }

  return {
    isOpen: computed(() => isOpen.value),
    currentImage,
    currentIndex: computed(() => currentIndex.value),
    images: computed(() => images.value),
    hasNext,
    hasPrev,
    open,
    close,
    next,
    prev,
    goTo
  }
}
