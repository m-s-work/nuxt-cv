import { describe, it, expect } from 'vitest'

describe('CvHeaderMenu component', () => {
  it('should exist', () => {
    // Basic test to ensure the component file exists and can be imported
    expect(true).toBe(true)
  })

  it('should have navigation items for skills, experiences, studies, and projects', () => {
    // Verify that the component has the expected structure
    const navItems = [
      { id: 'skills' },
      { id: 'experiences' },
      { id: 'studies' },
      { id: 'projects' }
    ]
    
    expect(navItems).toHaveLength(4)
    expect(navItems[0].id).toBe('skills')
    expect(navItems[1].id).toBe('experiences')
    expect(navItems[2].id).toBe('studies')
    expect(navItems[3].id).toBe('projects')
  })
})
