/**
 * Mascot type definitions
 */

/**
 * Position of the mascot on screen
 */
export type MascotPosition = 'bottom-right' | 'bottom-left' | 'top-right' | 'top-left'

/**
 * Mascot design variants
 */
export type MascotDesign = 'cat' | 'robot' | 'owl'

/**
 * Theme configuration for mascot
 */
export interface MascotTheme {
  primaryColor: string
  secondaryColor: string
  accentColor: string
}

/**
 * Configuration for mascot behavior
 */
export interface MascotConfig {
  design: MascotDesign
  position: MascotPosition
  theme?: MascotTheme
  enableIdleAnimation: boolean
  enableHoverEffect: boolean
  enableScrollInteraction: boolean
}

/**
 * Mascot state information
 */
export interface MascotState {
  isVisible: boolean
  isHovered: boolean
  scrollProgress: number // 0-100
  currentAnimation: 'idle' | 'tickle' | 'wave' | 'celebrate'
}
