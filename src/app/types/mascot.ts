/**
 * Mascot type definitions and interfaces
 */

/**
 * Available mascot designs
 */
export type MascotDesign = 'robot' | 'owl' | 'cat'

/**
 * Available mascot themes
 */
export type MascotTheme = 'default' | 'dark' | 'colorful'

/**
 * Mascot animation states
 */
export type MascotAnimationState = 'idle' | 'hover' | 'scrolling' | 'greeting' | 'thinking' | 'celebrating'

/**
 * Scroll position context for mascot interactions
 */
export interface MascotScrollContext {
  scrollPercent: number
  section: 'top' | 'intro' | 'skills' | 'experiences' | 'studies' | 'projects' | 'other' | 'footer'
  direction: 'up' | 'down' | 'none'
}

/**
 * Configuration for mascot behavior and appearance
 */
export interface MascotConfig {
  design: MascotDesign
  theme: MascotTheme
  enabled: boolean
  position: 'bottom-right' | 'bottom-left' | 'top-right' | 'top-left'
  showMessages: boolean
  messages?: string[]
}

/**
 * Props for mascot component implementations
 */
export interface MascotProps {
  theme?: MascotTheme
  animationState?: MascotAnimationState
  scrollContext?: MascotScrollContext
  size?: number
}

/**
 * Interface that all mascot designs must implement
 */
export interface MascotDesignInterface {
  /**
   * Render the mascot SVG
   */
  render: (props: MascotProps) => any
  
  /**
   * Get animation duration in milliseconds for different states
   */
  getAnimationDuration: (state: MascotAnimationState) => number
  
  /**
   * Get default messages for this mascot design
   */
  getDefaultMessages: () => string[]
}
