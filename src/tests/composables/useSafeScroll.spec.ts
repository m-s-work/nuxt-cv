import { describe, it, expect, beforeEach, vi } from 'vitest'
import { useSafeScroll } from '../../app/composables/useSafeScroll'

describe('useSafeScroll composable', () => {
  beforeEach(() => {
    // Reset DOM for each test
    document.body.innerHTML = ''
  })

  describe('getHeroHeight', () => {
    it('should return 0 if hero element does not exist', () => {
      const { getHeroHeight } = useSafeScroll()
      expect(getHeroHeight()).toBe(0)
    })

    it('should return the height of the hero element', () => {
      // Create a mock hero element
      const hero = document.createElement('div')
      hero.className = 'hero-section'
      Object.defineProperty(hero, 'offsetHeight', {
        configurable: true,
        value: 800
      })
      document.body.appendChild(hero)

      const { getHeroHeight } = useSafeScroll()
      expect(getHeroHeight()).toBe(800)
    })
  })

  describe('scrollToElementSafely', () => {
    beforeEach(() => {
      // Mock window.scrollTo
      window.scrollTo = vi.fn()
      
      // Mock window properties
      Object.defineProperty(window, 'innerHeight', {
        configurable: true,
        value: 1000
      })
      
      Object.defineProperty(window, 'scrollY', {
        configurable: true,
        value: 0
      })
    })

    it('should not scroll if element does not exist', () => {
      const { scrollToElementSafely } = useSafeScroll()
      scrollToElementSafely('non-existent')
      expect(window.scrollTo).not.toHaveBeenCalled()
    })

    it('should scroll to element respecting hero height', () => {
      // Create hero element
      const hero = document.createElement('div')
      hero.className = 'hero-section'
      Object.defineProperty(hero, 'offsetHeight', {
        configurable: true,
        value: 800
      })
      document.body.appendChild(hero)

      // Create target element
      const target = document.createElement('div')
      target.id = 'test-element'
      document.body.appendChild(target)

      // Mock getBoundingClientRect
      target.getBoundingClientRect = vi.fn(() => ({
        top: 500,
        left: 0,
        right: 0,
        bottom: 600,
        width: 0,
        height: 100,
        x: 0,
        y: 500,
        toJSON: () => ({})
      }))

      const { scrollToElementSafely } = useSafeScroll()
      scrollToElementSafely('test-element', 'smooth')

      expect(window.scrollTo).toHaveBeenCalled()
      const callArgs = (window.scrollTo as any).mock.calls[0][0]
      expect(callArgs.behavior).toBe('smooth')
      // The scroll should respect the hero height (800px minimum)
      expect(callArgs.top).toBeGreaterThanOrEqual(800)
    })

    it('should use centered position if it does not go above hero', () => {
      // Create hero element with smaller height
      const hero = document.createElement('div')
      hero.className = 'hero-section'
      Object.defineProperty(hero, 'offsetHeight', {
        configurable: true,
        value: 200
      })
      document.body.appendChild(hero)

      // Create target element far down the page
      const target = document.createElement('div')
      target.id = 'test-element'
      document.body.appendChild(target)

      // Mock getBoundingClientRect for element far down
      target.getBoundingClientRect = vi.fn(() => ({
        top: 2000,
        left: 0,
        right: 0,
        bottom: 2100,
        width: 0,
        height: 100,
        x: 0,
        y: 2000,
        toJSON: () => ({})
      }))

      const { scrollToElementSafely } = useSafeScroll()
      scrollToElementSafely('test-element', 'smooth')

      expect(window.scrollTo).toHaveBeenCalled()
      const callArgs = (window.scrollTo as any).mock.calls[0][0]
      // Should use centered position since it's well below hero
      // elementTop (2000) - centerOffset (1000/2 - 100/2 = 450) = 1550
      expect(callArgs.top).toBe(1550)
    })
  })
})
