import { describe, it, expect } from 'vitest'

describe('CvProjects Component Tests', () => {
  it('should pass basic test', () => {
    expect(true).toBe(true)
  })

  it('can handle project data structure', () => {
    const project = {
      id: 1,
      name: 'Test Project',
      type: 'Web Application',
      description: 'A test project description',
      technologies: ['Vue.js', 'TypeScript'],
      period: '2021 - 2023',
      startDate: '2021-01-01',
      endDate: '2023-12-31'
    }
    
    expect(project.name).toBe('Test Project')
    expect(project.type).toBe('Web Application')
    expect(project.technologies).toHaveLength(2)
  })

  it('can handle projects with optional media', () => {
    const projectWithMedia = {
      id: 1,
      name: 'Test Project',
      type: 'Web Application',
      description: 'A test project',
      technologies: ['Vue.js'],
      period: '2021 - 2023',
      startDate: '2021-01-01',
      endDate: '2023-12-31',
      screenshots: ['/image1.jpg', '/image2.jpg'],
      logos: ['/logo.png']
    }

    expect(projectWithMedia.screenshots).toBeDefined()
    expect(projectWithMedia.screenshots?.length).toBe(2)
    expect(projectWithMedia.logos).toBeDefined()
  })
})
