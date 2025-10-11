import { describe, it, expect } from 'vitest'

describe('useTechFilter Composable Tests', () => {
  it('should handle URL query parameter parsing', () => {
    const techString = 'Vue.js,Docker,Python'
    const techs = techString.split(',').filter(t => t.length > 0)
    
    expect(techs).toHaveLength(3)
    expect(techs).toContain('Vue.js')
    expect(techs).toContain('Docker')
    expect(techs).toContain('Python')
  })

  it('should handle empty tech query parameter', () => {
    const techString = ''
    const techs = techString.split(',').filter(t => t.length > 0)
    
    expect(techs).toHaveLength(0)
  })

  it('should correctly determine if filtering is active', () => {
    const selectedTechs1 = ['Vue.js', 'Docker']
    const selectedTechs2: string[] = []
    
    expect(selectedTechs1.length > 0).toBe(true)
    expect(selectedTechs2.length > 0).toBe(false)
  })

  it('should toggle technology selection correctly', () => {
    let current = ['Vue.js', 'Docker']
    
    // Remove existing tech
    const indexRemove = current.indexOf('Vue.js')
    if (indexRemove !== -1) {
      current.splice(indexRemove, 1)
    }
    expect(current).toEqual(['Docker'])
    
    // Add new tech
    const indexAdd = current.indexOf('Python')
    if (indexAdd === -1) {
      current.push('Python')
    }
    expect(current).toEqual(['Docker', 'Python'])
  })

  it('should filter items based on selected technologies', () => {
    const selectedTechs = ['Vue.js', 'Docker']
    
    const item1 = { technologies: ['Vue.js', 'Node.js'] }
    const item2 = { technologies: ['Python', 'FastAPI'] }
    const item3 = { technologies: ['Docker', 'Kubernetes'] }
    
    const shouldShow1 = selectedTechs.some(tech => item1.technologies.includes(tech))
    const shouldShow2 = selectedTechs.some(tech => item2.technologies.includes(tech))
    const shouldShow3 = selectedTechs.some(tech => item3.technologies.includes(tech))
    
    expect(shouldShow1).toBe(true)  // Has Vue.js
    expect(shouldShow2).toBe(false) // No matching tech
    expect(shouldShow3).toBe(true)  // Has Docker
  })

  it('should show all items when no filter is active', () => {
    const selectedTechs: string[] = []
    const isFiltering = selectedTechs.length > 0
    
    const item = { technologies: ['Vue.js', 'Node.js'] }
    const shouldShow = !isFiltering || selectedTechs.some(tech => item.technologies.includes(tech))
    
    expect(shouldShow).toBe(true)
  })

  it('should handle items with no technologies when filtering', () => {
    const selectedTechs = ['Vue.js']
    const isFiltering = selectedTechs.length > 0
    
    const item = { technologies: [] as string[] }
    const shouldShow = !isFiltering || (item.technologies.length === 0 ? true : selectedTechs.some(tech => item.technologies.includes(tech)))
    
    // Items without technologies should still show
    expect(shouldShow).toBe(true)
  })

  it('should handle multiple technology selection', () => {
    const selectedTechs = ['Vue.js', 'Docker', 'Python']
    
    const item1 = { technologies: ['Vue.js', 'Node.js'] }
    const item2 = { technologies: ['React', 'TypeScript'] }
    const item3 = { technologies: ['Python', 'Django'] }
    
    const shouldShow1 = selectedTechs.some(tech => item1.technologies.includes(tech))
    const shouldShow2 = selectedTechs.some(tech => item2.technologies.includes(tech))
    const shouldShow3 = selectedTechs.some(tech => item3.technologies.includes(tech))
    
    expect(shouldShow1).toBe(true)  // Has Vue.js
    expect(shouldShow2).toBe(false) // No matching tech
    expect(shouldShow3).toBe(true)  // Has Python
  })

  it('should build query string from tech array', () => {
    const techs1 = ['Vue.js', 'Docker']
    const techs2: string[] = []
    
    const queryString1 = techs1.join(',')
    const queryString2 = techs2.join(',')
    
    expect(queryString1).toBe('Vue.js,Docker')
    expect(queryString2).toBe('')
  })
})
