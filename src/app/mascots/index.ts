import type { MascotDesign } from '~/types/mascot'
import { robotMascot } from './robot'

/**
 * Registry of all available mascot designs
 */
const mascotRegistry = new Map<string, MascotDesign>()

// Register available mascots
mascotRegistry.set('robot', robotMascot)

/**
 * Get a mascot design by ID
 */
export const getMascotDesign = (designId: string): MascotDesign | undefined => {
  return mascotRegistry.get(designId)
}

/**
 * Get all available mascot designs
 */
export const getAllMascotDesigns = (): MascotDesign[] => {
  return Array.from(mascotRegistry.values())
}

/**
 * Register a custom mascot design
 */
export const registerMascotDesign = (design: MascotDesign): void => {
  mascotRegistry.set(design.id, design)
}

/**
 * Get list of available mascot design IDs
 */
export const getAvailableDesignIds = (): string[] => {
  return Array.from(mascotRegistry.keys())
}
