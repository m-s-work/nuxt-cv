import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { useMascot } from '../../app/composables/useMascot'

describe('useMascot', () => {
  beforeEach(() => {
    vi.useFakeTimers()
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  describe('state management', () => {
    it('should initialize with idle state', () => {
      const { state } = useMascot()
      expect(state.value).toBe('idle')
    })

    it('should allow setting state', () => {
      const { state, setState } = useMascot()
      setState('greeting')
      expect(state.value).toBe('greeting')
    })

    it('should return to idle after tickle', () => {
      const { state, handleTickle } = useMascot()
      handleTickle()
      expect(state.value).toBe('tickled')
      
      vi.advanceTimersByTime(2000)
      expect(state.value).toBe('idle')
    })

    it('should return to idle after greeting', () => {
      const { state, greet } = useMascot()
      greet()
      expect(state.value).toBe('greeting')
      
      vi.advanceTimersByTime(2000)
      expect(state.value).toBe('idle')
    })

    it('should return to idle after celebrating', () => {
      const { state, celebrate } = useMascot()
      celebrate()
      expect(state.value).toBe('celebrating')
      
      vi.advanceTimersByTime(3000)
      expect(state.value).toBe('idle')
    })

    it('should return to idle after thinking', () => {
      const { state, think } = useMascot()
      think()
      expect(state.value).toBe('thinking')
      
      vi.advanceTimersByTime(3000)
      expect(state.value).toBe('idle')
    })
  })

  describe('configuration', () => {
    it('should have default configuration', () => {
      const { config } = useMascot()
      expect(config.value.design).toBe('robot')
      expect(config.value.enabled).toBe(true)
      expect(config.value.position).toBe('bottom-right')
      expect(config.value.size).toBe(120)
    })

    it('should allow updating configuration', () => {
      const { config, setConfig } = useMascot()
      setConfig({ position: 'top-left', size: 150 })
      expect(config.value.position).toBe('top-left')
      expect(config.value.size).toBe(150)
      // Should preserve other config values
      expect(config.value.design).toBe('robot')
    })

    it('should allow setting custom theme', () => {
      const { config, setConfig } = useMascot()
      const customTheme = {
        primary: '#ff0000',
        secondary: '#00ff00',
        accent: '#0000ff'
      }
      setConfig({ theme: customTheme })
      expect(config.value.theme).toEqual(customTheme)
    })

    it('should allow disabling mascot', () => {
      const { config, setConfig } = useMascot()
      setConfig({ enabled: false })
      expect(config.value.enabled).toBe(false)
    })
  })

  describe('scroll handling', () => {
    it('should initialize and cleanup scroll listener', () => {
      const { initScrollListener, cleanupScrollListener } = useMascot()
      
      // These methods should exist and be callable
      expect(typeof initScrollListener).toBe('function')
      expect(typeof cleanupScrollListener).toBe('function')
      
      // Call them to ensure they don't throw
      initScrollListener()
      cleanupScrollListener()
    })

    it('should provide scroll direction computed', () => {
      const { scrollDirection } = useMascot()
      
      // ScrollDirection should be a computed ref
      expect(scrollDirection.value).toBeNull()
    })
  })
})
