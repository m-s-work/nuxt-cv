import { describe, it, expect, beforeEach } from 'vitest'
import { useLightbox } from '~/composables/useLightbox'
import type { LightboxImage } from '~/composables/useLightbox'

describe('Lightbox functionality', () => {
  const mockImages: LightboxImage[] = [
    { src: '/image1.jpg', alt: 'Image 1', groupId: 'project-1' },
    { src: '/image2.jpg', alt: 'Image 2', groupId: 'project-1' },
    { src: '/image3.jpg', alt: 'Image 3', groupId: 'project-1' }
  ]

  beforeEach(() => {
    const { close } = useLightbox()
    close()
  })

  it('should start with lightbox closed', () => {
    const { isOpen } = useLightbox()
    expect(isOpen.value).toBe(false)
  })

  it('should open lightbox with images', () => {
    const { open, isOpen, images } = useLightbox()
    open(mockImages, 0, 'project-1')
    
    expect(isOpen.value).toBe(true)
    expect(images.value).toHaveLength(3)
  })

  it('should set current index when opening', () => {
    const { open, currentIndex } = useLightbox()
    open(mockImages, 1, 'project-1')
    
    expect(currentIndex.value).toBe(1)
  })

  it('should close lightbox', () => {
    const { open, close, isOpen } = useLightbox()
    open(mockImages, 0, 'project-1')
    expect(isOpen.value).toBe(true)
    
    close()
    expect(isOpen.value).toBe(false)
  })

  it('should navigate to next image', () => {
    const { open, next, currentIndex } = useLightbox()
    open(mockImages, 0, 'project-1')
    
    next()
    expect(currentIndex.value).toBe(1)
    
    next()
    expect(currentIndex.value).toBe(2)
  })

  it('should not go beyond last image', () => {
    const { open, next, currentIndex } = useLightbox()
    open(mockImages, 2, 'project-1')
    
    next()
    expect(currentIndex.value).toBe(2)
  })

  it('should navigate to previous image', () => {
    const { open, prev, currentIndex } = useLightbox()
    open(mockImages, 2, 'project-1')
    
    prev()
    expect(currentIndex.value).toBe(1)
    
    prev()
    expect(currentIndex.value).toBe(0)
  })

  it('should not go before first image', () => {
    const { open, prev, currentIndex } = useLightbox()
    open(mockImages, 0, 'project-1')
    
    prev()
    expect(currentIndex.value).toBe(0)
  })

  it('should go to specific index', () => {
    const { open, goTo, currentIndex } = useLightbox()
    open(mockImages, 0, 'project-1')
    
    goTo(2)
    expect(currentIndex.value).toBe(2)
  })

  it('should return current image', () => {
    const { open, currentImage } = useLightbox()
    open(mockImages, 1, 'project-1')
    
    expect(currentImage.value).toEqual(mockImages[1])
  })

  it('should track hasNext correctly', () => {
    const { open, hasNext } = useLightbox()
    
    open(mockImages, 0, 'project-1')
    expect(hasNext.value).toBe(true)
    
    open(mockImages, 2, 'project-1')
    expect(hasNext.value).toBe(false)
  })

  it('should track hasPrev correctly', () => {
    const { open, hasPrev } = useLightbox()
    
    open(mockImages, 0, 'project-1')
    expect(hasPrev.value).toBe(false)
    
    open(mockImages, 2, 'project-1')
    expect(hasPrev.value).toBe(true)
  })
})
