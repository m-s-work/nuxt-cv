<script setup lang="ts">
const { t } = useI18n()
const { isOpen, currentImage, currentIndex, images, hasNext, hasPrev, close, next, prev, goTo } = useLightbox()

// Keyboard navigation
function handleKeydown(event: KeyboardEvent) {
  if (!isOpen.value) return
  
  switch (event.key) {
    case 'Escape':
      close()
      break
    case 'ArrowLeft':
      prev()
      break
    case 'ArrowRight':
      next()
      break
  }
}

// Set up keyboard listener
onMounted(() => {
  if (typeof window !== 'undefined') {
    window.addEventListener('keydown', handleKeydown)
  }
})

onUnmounted(() => {
  if (typeof window !== 'undefined') {
    window.removeEventListener('keydown', handleKeydown)
  }
})
</script>

<template>
  <Teleport to="body">
    <Transition name="lightbox-fade">
      <div
        v-if="isOpen"
        class="fixed inset-0 z-50 flex items-center justify-center bg-black/90 backdrop-blur-sm print:hidden"
        @click="close"
      >
        <!-- Close button -->
        <button
          class="absolute top-4 right-4 z-10 p-2 text-white hover:bg-white/10 rounded-full transition-colors"
          :aria-label="t('lightbox.close')"
          @click="close"
        >
          <svg class="w-8 h-8" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
          </svg>
        </button>

        <!-- Previous button -->
        <button
          v-if="hasPrev"
          class="absolute left-4 z-10 p-2 text-white hover:bg-white/10 rounded-full transition-colors"
          :aria-label="t('lightbox.previous')"
          @click.stop="prev"
        >
          <svg class="w-8 h-8" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7" />
          </svg>
        </button>

        <!-- Next button -->
        <button
          v-if="hasNext"
          class="absolute right-4 z-10 p-2 text-white hover:bg-white/10 rounded-full transition-colors"
          :aria-label="t('lightbox.next')"
          @click.stop="next"
        >
          <svg class="w-8 h-8" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5l7 7-7 7" />
          </svg>
        </button>

        <!-- Image container -->
        <div class="relative max-w-7xl max-h-[90vh] mx-4" @click.stop>
          <img
            v-if="currentImage"
            :src="currentImage.src"
            :alt="currentImage.alt"
            class="max-w-full max-h-[90vh] w-auto h-auto object-contain"
          />
          
          <!-- Image counter -->
          <div class="absolute bottom-4 left-1/2 transform -translate-x-1/2 px-4 py-2 bg-black/50 rounded-full text-white text-sm">
            {{ currentIndex + 1 }} / {{ images.length }}
          </div>
        </div>

        <!-- Thumbnails -->
        <div
          v-if="images.length > 1"
          class="absolute bottom-4 left-1/2 transform -translate-x-1/2 flex gap-2 max-w-[90vw] overflow-x-auto px-4 py-2 bg-black/50 rounded-lg"
          @click.stop
        >
          <button
            v-for="(image, index) in images"
            :key="index"
            class="flex-shrink-0 w-16 h-16 rounded overflow-hidden border-2 transition-all"
            :class="currentIndex === index ? 'border-white scale-110' : 'border-transparent opacity-60 hover:opacity-100'"
            @click="goTo(index)"
          >
            <img
              :src="image.src"
              :alt="image.alt"
              class="w-full h-full object-cover"
            />
          </button>
        </div>
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped>
.lightbox-fade-enter-active,
.lightbox-fade-leave-active {
  transition: opacity 0.3s ease;
}

.lightbox-fade-enter-from,
.lightbox-fade-leave-to {
  opacity: 0;
}
</style>

<i18n lang="json">
{
  "en": {
    "lightbox": {
      "close": "Close",
      "previous": "Previous image",
      "next": "Next image"
    }
  },
  "de": {
    "lightbox": {
      "close": "Schließen",
      "previous": "Vorheriges Bild",
      "next": "Nächstes Bild"
    }
  }
}
</i18n>
