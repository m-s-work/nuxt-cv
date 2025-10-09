/**
 * Composable for safe scrolling that prevents scrolling above the hero element
 */
export function useSafeScroll() {
  /**
   * Get the height of the hero section
   */
  function getHeroHeight(): number {
    const hero = document.querySelector('.hero-section') as HTMLElement
    return hero ? hero.offsetHeight : 0
  }

  /**
   * Scroll to an element safely, ensuring we don't scroll above the hero section
   * @param elementId - The ID of the element to scroll to
   * @param behavior - The scroll behavior (smooth or auto)
   */
  function scrollToElementSafely(elementId: string, behavior: ScrollBehavior = 'smooth') {
    const element = document.getElementById(elementId)
    if (!element) return

    const heroHeight = getHeroHeight()
    const elementRect = element.getBoundingClientRect()
    const elementTop = elementRect.top + window.scrollY
    
    // Calculate the target scroll position
    // We want to show the element, but not scroll above the hero
    let targetScroll = elementTop
    
    // If scrolling to center would go above hero, just scroll to top of element + hero height
    const viewportHeight = window.innerHeight
    const centerOffset = (viewportHeight / 2) - (elementRect.height / 2)
    const centeredScroll = elementTop - centerOffset
    
    // Use the centered position if it doesn't go above hero, otherwise use hero height as minimum
    targetScroll = Math.max(centeredScroll, heroHeight)
    
    window.scrollTo({
      top: targetScroll,
      behavior
    })
  }

  return {
    scrollToElementSafely,
    getHeroHeight
  }
}
