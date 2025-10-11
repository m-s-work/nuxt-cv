import { describe, it, expect } from 'vitest'

describe('Tech Filter Functionality', () => {
  it('should handle technology selection', () => {
    const techs = ['Vue.js', 'React', 'TypeScript']
    const selectedTechs = ['Vue.js']
    
    const isSelected = (tech: string) => selectedTechs.includes(tech)
    
    expect(isSelected('Vue.js')).toBe(true)
    expect(isSelected('React')).toBe(false)
    expect(isSelected('TypeScript')).toBe(false)
  })

  it('should filter items by technology', () => {
    const items = [
      { id: 1, name: 'Item 1', technologies: ['Vue.js', 'TypeScript'] },
      { id: 2, name: 'Item 2', technologies: ['React', 'JavaScript'] },
      { id: 3, name: 'Item 3', technologies: ['Vue.js', 'Node.js'] },
    ]
    
    const selectedTechs = ['Vue.js']
    
    const shouldShowItem = (itemTechs: string[]) => {
      if (selectedTechs.length === 0) return true
      return selectedTechs.some(selectedTech => itemTechs.includes(selectedTech))
    }
    
    const filteredItems = items.filter(item => shouldShowItem(item.technologies))
    
    expect(filteredItems).toHaveLength(2)
    expect(filteredItems.map(i => i.id)).toEqual([1, 3])
  })

  it('should support OR logic for multiple technologies', () => {
    const items = [
      { id: 1, name: 'Item 1', technologies: ['Vue.js'] },
      { id: 2, name: 'Item 2', technologies: ['React'] },
      { id: 3, name: 'Item 3', technologies: ['TypeScript'] },
      { id: 4, name: 'Item 4', technologies: ['Go'] },
    ]
    
    const selectedTechs = ['Vue.js', 'React']
    
    const shouldShowItem = (itemTechs: string[]) => {
      if (selectedTechs.length === 0) return true
      return selectedTechs.some(selectedTech => itemTechs.includes(selectedTech))
    }
    
    const filteredItems = items.filter(item => shouldShowItem(item.technologies))
    
    expect(filteredItems).toHaveLength(2)
    expect(filteredItems.map(i => i.id)).toEqual([1, 2])
  })

  it('should handle toggle tech selection', () => {
    let selectedTechs: string[] = []
    
    const toggleTech = (tech: string) => {
      const index = selectedTechs.indexOf(tech)
      if (index === -1) {
        selectedTechs.push(tech)
      } else {
        selectedTechs.splice(index, 1)
      }
    }
    
    // Add Vue.js
    toggleTech('Vue.js')
    expect(selectedTechs).toEqual(['Vue.js'])
    
    // Add React
    toggleTech('React')
    expect(selectedTechs).toEqual(['Vue.js', 'React'])
    
    // Remove Vue.js
    toggleTech('Vue.js')
    expect(selectedTechs).toEqual(['React'])
    
    // Remove React
    toggleTech('React')
    expect(selectedTechs).toEqual([])
  })

  it('should handle clear filters', () => {
    let selectedTechs = ['Vue.js', 'React', 'TypeScript']
    
    const clearFilters = () => {
      selectedTechs = []
    }
    
    clearFilters()
    expect(selectedTechs).toEqual([])
  })

  it('should parse tech query params', () => {
    const queryParamString = 'Vue.js,React,TypeScript'
    const techs = queryParamString.split(',').filter(t => t.length > 0)
    
    expect(techs).toEqual(['Vue.js', 'React', 'TypeScript'])
  })

  it('should handle empty tech query params', () => {
    const queryParamString = ''
    const techs = queryParamString.split(',').filter(t => t.length > 0)
    
    expect(techs).toEqual([])
  })

  it('should show all items when no filters are active', () => {
    const items = [
      { id: 1, technologies: ['Vue.js'] },
      { id: 2, technologies: ['React'] },
      { id: 3, technologies: ['TypeScript'] },
    ]
    
    const selectedTechs: string[] = []
    
    const shouldShowItem = (itemTechs: string[]) => {
      if (selectedTechs.length === 0) return true
      return selectedTechs.some(selectedTech => itemTechs.includes(selectedTech))
    }
    
    const filteredItems = items.filter(item => shouldShowItem(item.technologies))
    
    expect(filteredItems).toHaveLength(3)
  })
})
