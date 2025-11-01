/**
 * Mascot animation states
 */
export type MascotState = 'idle' | 'tickled' | 'scrolling' | 'greeting' | 'thinking' | 'celebrating'

/**
 * Mascot theme colors
 */
export interface MascotTheme {
  primary: string
  secondary: string
  accent: string
  background?: string
}

/**
 * Mascot configuration
 */
export interface MascotConfig {
  /** Design type identifier */
  design: string
  /** Theme colors */
  theme?: MascotTheme
  /** Enable/disable mascot */
  enabled: boolean
  /** Position on screen */
  position?: 'bottom-left' | 'bottom-right' | 'top-left' | 'top-right'
  /** Size of the mascot */
  size?: number
  /** Custom texts for different states */
  texts?: {
    greeting?: string
    idle?: string
    scrolling?: string
    celebrating?: string
  }
}

/**
 * Mascot design interface
 * All mascot designs must implement this interface
 */
export interface MascotDesign {
  /** Unique design identifier */
  id: string
  /** Design name */
  name: string
  /** Render the SVG for current state */
  render(state: MascotState, theme?: MascotTheme): string
  /** Get animation class for current state */
  getAnimationClass(state: MascotState): string
  /** Optional: Get text bubble for current state */
  getText?(state: MascotState, customTexts?: Record<string, string>): string | null
}
