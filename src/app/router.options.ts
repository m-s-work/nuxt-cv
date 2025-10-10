import type { RouterConfig } from '@nuxt/schema'

// Custom router configuration to handle scroll behavior
export default <RouterConfig>{
  scrollBehavior(to, from, savedPosition) {
    // If navigating to a different locale of the same route (language switch)
    // prevent scroll and let the component handle it
    if (to.path !== from.path && to.name === from.name) {
      // This is a language switch - don't scroll
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
