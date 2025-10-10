import type { RouterConfig } from '@nuxt/schema'

// Custom router configuration to handle scroll behavior
export default <RouterConfig>{
  scrollBehavior(to, from, savedPosition) {
    // Check if we're currently switching languages
    const isSwitchingLanguage = useState('languageSwitching', () => false)
    
    // If switching languages, don't scroll - let the component handle it
    if (isSwitchingLanguage.value) {
      return false
    }
    
    // For all other navigation, use default behavior
    if (savedPosition) {
      return savedPosition
    }
    
    // Check if there's a hash to scroll to
    if (to.hash) {
      return {
        el: to.hash,
        behavior: 'smooth'
      }
    }
    
    // Default: scroll to top for new pages
    return { top: 0 }
  }
}
