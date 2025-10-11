import { describe, it, expect } from 'vitest'
import { useAssetPath } from '../../app/composables/useAssetPath'

describe('useAssetPath', () => {
  it('should return path with baseURL prepended when path starts with /', () => {
    const { getAssetPath } = useAssetPath()
    
    // In test environment, baseURL defaults to '/'
    // We'll test the logic by checking that the function works correctly
    const result = getAssetPath('/images/profile.jpg')
    
    // Should start with / and contain the path
    expect(result).toContain('images/profile.jpg')
    expect(result.startsWith('/')).toBe(true)
  })

  it('should return path as-is if it does not start with /', () => {
    const { getAssetPath } = useAssetPath()
    
    expect(getAssetPath('images/profile.jpg')).toBe('images/profile.jpg')
    expect(getAssetPath('https://example.com/image.jpg')).toBe('https://example.com/image.jpg')
  })

  it('should handle multiple image paths correctly', () => {
    const { getAssetPath } = useAssetPath()
    
    const small = getAssetPath('/images/profile-small.jpg')
    const medium = getAssetPath('/images/profile-medium.jpg')
    const large = getAssetPath('/images/profile-large.jpg')
    
    // All should contain the correct filename
    expect(small).toContain('profile-small.jpg')
    expect(medium).toContain('profile-medium.jpg')
    expect(large).toContain('profile-large.jpg')
    
    // All should start with /
    expect(small.startsWith('/')).toBe(true)
    expect(medium.startsWith('/')).toBe(true)
    expect(large.startsWith('/')).toBe(true)
  })
})
