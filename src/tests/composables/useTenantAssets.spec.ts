import { describe, it, expect } from 'vitest'
import { assetFileName, isImageFile } from '~/composables/useTenantAssets'

describe('tenant assets', () => {
  it('makes upload names safe for the API', () => {
    expect(assetFileName('My Logo (1).PNG')).toBe('My-Logo--1-.PNG')
    expect(assetFileName('..hidden.svg')).toBe('hidden.svg')
  })

  it('recognises images', () => {
    expect(isImageFile('assets/a.svg')).toBe(true)
    expect(isImageFile('assets/a.JPEG')).toBe(true)
    expect(isImageFile('assets/cv.pdf')).toBe(false)
  })
})
