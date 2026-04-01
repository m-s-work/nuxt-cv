import { describe, it, expect, beforeEach } from 'vitest'
import { useMascot } from '~/composables/useMascot'

describe('useMascot composable', () => {
  beforeEach(() => {
    // Reset state between tests by creating fresh instances
  })

  it('should have default configuration', () => {
    const { config } = useMascot()
    
    expect(config.value.design).toBe('robot')
    expect(config.value.theme).toBe('default')
    expect(config.value.enabled).toBe(true)
    expect(config.value.position).toBe('bottom-right')
    expect(config.value.showMessages).toBe(true)
  })

  it('should update design', () => {
    const { config, setDesign } = useMascot()
    
    setDesign('owl')
    expect(config.value.design).toBe('owl')
  })

  it('should update theme', () => {
    const { config, setTheme } = useMascot()
    
    setTheme('dark')
    expect(config.value.theme).toBe('dark')
  })

  it('should enable/disable mascot', () => {
    const { config, setEnabled } = useMascot()
    
    setEnabled(false)
    expect(config.value.enabled).toBe(false)
    
    setEnabled(true)
    expect(config.value.enabled).toBe(true)
  })

  it('should change animation state', () => {
    const { animationState, setAnimationState } = useMascot()
    
    expect(animationState.value).toBe('idle')
    
    setAnimationState('hover')
    expect(animationState.value).toBe('hover')
    
    setAnimationState('greeting')
    expect(animationState.value).toBe('greeting')
  })

  it('should update scroll context', () => {
    const { scrollContext, updateScrollContext } = useMascot()
    
    updateScrollContext({
      scrollPercent: 50,
      section: 'experiences',
      direction: 'down'
    })
    
    expect(scrollContext.value.scrollPercent).toBe(50)
    expect(scrollContext.value.section).toBe('experiences')
    expect(scrollContext.value.direction).toBe('down')
  })

  it('should show and clear messages', async () => {
    const { currentMessage, showMessage } = useMascot()
    
    expect(currentMessage.value).toBe(null)
    
    showMessage('Test message', 100)
    expect(currentMessage.value).toBe('Test message')
    
    // Wait for message to clear
    await new Promise(resolve => setTimeout(resolve, 150))
    expect(currentMessage.value).toBe(null)
  })

  it('should update configuration partially', () => {
    const { config, updateConfig } = useMascot()
    
    const originalPosition = config.value.position
    
    updateConfig({ design: 'owl', theme: 'colorful' })
    
    expect(config.value.design).toBe('owl')
    expect(config.value.theme).toBe('colorful')
    expect(config.value.position).toBe(originalPosition) // Should remain unchanged
  })
})
