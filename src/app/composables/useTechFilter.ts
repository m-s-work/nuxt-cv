export const useTechFilter = () => {
  const route = useRoute()
  const router = useRouter()
  
  // Parse techs from query params
  const selectedTechs = computed<string[]>(() => {
    const techsParam = route.query.techs
    if (!techsParam) return []
    if (typeof techsParam === 'string') {
      return techsParam.split(',').filter(t => t.length > 0)
    }
    return []
  })
  
  // Check if filtering is active
  const isFiltering = computed(() => selectedTechs.value.length > 0)
  
  // Toggle a technology filter
  const toggleTech = (tech: string) => {
    const current = [...selectedTechs.value]
    const index = current.indexOf(tech)
    
    if (index === -1) {
      current.push(tech)
    } else {
      current.splice(index, 1)
    }
    
    updateQueryParams(current)
  }
  
  // Check if a tech is selected
  const isTechSelected = (tech: string) => {
    return selectedTechs.value.includes(tech)
  }
  
  // Clear all filters
  const clearFilters = () => {
    updateQueryParams([])
  }
  
  // Update URL query params
  const updateQueryParams = (techs: string[]) => {
    const query = { ...route.query }
    
    if (techs.length > 0) {
      query.techs = techs.join(',')
    } else {
      delete query.techs
    }
    
    // Use replace instead of push to avoid page jump and not add to history
    router.replace({ query })
  }
  
  // Check if an item should be visible based on selected techs
  const shouldShowItem = (itemTechs: string[]) => {
    if (!isFiltering.value) return true
    
    // Show item if it has at least one of the selected technologies
    return selectedTechs.value.some(selectedTech => 
      itemTechs.includes(selectedTech)
    )
  }
  
  return {
    selectedTechs,
    isFiltering,
    toggleTech,
    isTechSelected,
    clearFilters,
    shouldShowItem
  }
}
