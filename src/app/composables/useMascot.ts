import type { MascotDesign, MascotPosition, MascotConfig, MascotState } from '~/types/mascot'

/**
 * Composable for managing mascot state and behavior
 */
export const useMascot = () => {
  // State management
  const isVisible = useState('mascot-visible', () => true)
  const isHovered = useState('mascot-hovered', () => false)
  const scrollProgress = useState('mascot-scroll-progress', () => 0)
  const currentAnimation = useState<'idle' | 'tickle' | 'wave' | 'celebrate'>('mascot-animation', () => 'idle')
  
  // Configuration
  const design = useState<MascotDesign>('mascot-design', () => 'cat')
  const position = useState<MascotPosition>('mascot-position', () => 'bottom-right')
  const enableIdleAnimation = useState('mascot-idle-animation', () => true)
  const enableHoverEffect = useState('mascot-hover-effect', () => true)
  const enableScrollInteraction = useState('mascot-scroll-interaction', () => true)

  /**
   * Get current mascot configuration
   */
  const getConfig = (): MascotConfig => {
    return {
      design: design.value,
      position: position.value,
      enableIdleAnimation: enableIdleAnimation.value,
      enableHoverEffect: enableHoverEffect.value,
      enableScrollInteraction: enableScrollInteraction.value
    }
  }

  /**
   * Get current mascot state
   */
  const getState = (): MascotState => {
    return {
      isVisible: isVisible.value,
      isHovered: isHovered.value,
      scrollProgress: scrollProgress.value,
      currentAnimation: currentAnimation.value
    }
  }

  /**
   * Set mascot design
   */
  const setDesign = (newDesign: MascotDesign) => {
    design.value = newDesign
  }

  /**
   * Set mascot position
   */
  const setPosition = (newPosition: MascotPosition) => {
    position.value = newPosition
  }

  /**
   * Show mascot
   */
  const show = () => {
    isVisible.value = true
  }

  /**
   * Hide mascot
   */
  const hide = () => {
    isVisible.value = false
  }

  /**
   * Set hover state
   */
  const setHovered = (hovered: boolean) => {
    isHovered.value = hovered
    if (hovered && enableHoverEffect.value) {
      currentAnimation.value = 'tickle'
    } else if (!hovered && currentAnimation.value === 'tickle') {
      currentAnimation.value = 'idle'
    }
  }

  /**
   * Update scroll progress (0-100)
   */
  const updateScrollProgress = (progress: number) => {
    scrollProgress.value = Math.max(0, Math.min(100, progress))
    
    // Trigger celebrate animation when reaching bottom
    if (enableScrollInteraction.value && progress >= 95 && currentAnimation.value !== 'celebrate') {
      currentAnimation.value = 'celebrate'
      setTimeout(() => {
        if (currentAnimation.value === 'celebrate') {
          currentAnimation.value = 'idle'
        }
      }, 2000)
    }
  }

  /**
   * Trigger wave animation
   */
  const wave = () => {
    currentAnimation.value = 'wave'
    setTimeout(() => {
      if (currentAnimation.value === 'wave') {
        currentAnimation.value = 'idle'
      }
    }, 1500)
  }

  return {
    // State (readonly)
    isVisible: readonly(isVisible),
    isHovered: readonly(isHovered),
    scrollProgress: readonly(scrollProgress),
    currentAnimation: readonly(currentAnimation),
    design: readonly(design),
    position: readonly(position),
    
    // Methods
    getConfig,
    getState,
    setDesign,
    setPosition,
    show,
    hide,
    setHovered,
    updateScrollProgress,
    wave
  }
}
