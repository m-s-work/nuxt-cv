/**
 * Composable for managing the CV mascot's state and behavior
 * 
 * Features:
 * - Scroll-based message updates
 * - Animation triggers based on page sections
 * - User retention through interactive guidance
 */

export const useMascot = () => {
  // State
  const isVisible = useState<boolean>('mascot-visible', () => false)
  const currentMessage = useState<string | null>('mascot-message', () => null)
  const currentAnimation = useState<string | null>('mascot-animation', () => null)
  const lastScrollY = useState<number>('mascot-last-scroll', () => 0)
  const currentSection = useState<string>('mascot-section', () => 'hero')
  const currentPosition = useState<'bottom-right' | 'bottom-left' | 'top-right' | 'top-left'>('mascot-position', () => 'bottom-right')
  const moveAroundEnabled = useState<boolean>('mascot-move-around', () => true) // Flag to enable/disable movement

  // Show mascot after splash screen and initial scroll
  const showMascot = () => {
    isVisible.value = true
  }

  // Hide mascot
  const hideMascot = () => {
    isVisible.value = false
  }

  // Update message with optional animation
  const updateMessage = (messageKey: string, animation?: string) => {
    currentMessage.value = messageKey
    if (animation) {
      currentAnimation.value = animation
    }
  }

  // Clear current message
  const clearMessage = () => {
    currentMessage.value = null
    currentAnimation.value = null
  }

  // Trigger specific animation
  const triggerAnimation = (animation: string) => {
    currentAnimation.value = animation
  }

  // Change mascot position
  const changePosition = (position: 'bottom-right' | 'bottom-left' | 'top-right' | 'top-left') => {
    currentPosition.value = position
  }

  // Get random position different from current
  const getRandomPosition = (): 'bottom-right' | 'bottom-left' | 'top-right' | 'top-left' => {
    const positions: ('bottom-right' | 'bottom-left' | 'top-right' | 'top-left')[] = ['bottom-right', 'bottom-left', 'top-right', 'top-left']
    const availablePositions = positions.filter(p => p !== currentPosition.value)
    return availablePositions[Math.floor(Math.random() * availablePositions.length)]
  }

  // Handle scroll events to update mascot behavior
  const handleScroll = () => {
    if (typeof window === 'undefined') return

    const scrollY = window.scrollY
    const windowHeight = window.innerHeight
    const documentHeight = document.documentElement.scrollHeight

    // Determine current section based on scroll position
    const heroHeight = windowHeight
    const introSection = document.querySelector('.intro-section')
    const experiencesSection = document.getElementById('experiences-section')
    const projectsSection = document.getElementById('projects-section')
    const otherSection = document.getElementById('other-section')

    let newSection = currentSection.value

    // Update section and show appropriate message
    if (scrollY < heroHeight * 0.5) {
      newSection = 'hero'
      if (currentSection.value !== 'hero') {
        updateMessage('mascot.welcome', 'wave')
      }
    } else if (scrollY < heroHeight) {
      newSection = 'hero-bottom'
      if (currentSection.value !== 'hero-bottom') {
        updateMessage('mascot.scrollDown', 'point-down')
      }
    } else if (introSection && scrollY < (introSection as HTMLElement).offsetTop + (introSection as HTMLElement).offsetHeight) {
      newSection = 'intro'
      if (currentSection.value !== 'intro') {
        updateMessage('mascot.keepGoing', 'bounce')
      }
    } else if (experiencesSection && scrollY < experiencesSection.offsetTop + experiencesSection.offsetHeight) {
      newSection = 'experiences'
      if (currentSection.value !== 'experiences') {
        updateMessage('mascot.checkExperience', 'point-up')
      }
    } else if (projectsSection && scrollY < projectsSection.offsetTop + projectsSection.offsetHeight) {
      newSection = 'projects'
      if (currentSection.value !== 'projects') {
        updateMessage('mascot.seeProjects', 'celebrate')
      }
    } else if (otherSection && scrollY < otherSection.offsetTop + otherSection.offsetHeight) {
      newSection = 'other'
      if (currentSection.value !== 'other') {
        updateMessage('mascot.almostThere', 'bounce')
      }
    } else if (scrollY + windowHeight >= documentHeight - 100) {
      newSection = 'end'
      if (currentSection.value !== 'end') {
        updateMessage('mascot.goodbye', 'celebrate')
      }
    }

    currentSection.value = newSection
    lastScrollY.value = scrollY
  }

  // Initialize mascot behavior
  const initializeMascot = () => {
    if (typeof window === 'undefined') return

    // Show mascot after a short delay
    setTimeout(() => {
      showMascot()
      updateMessage('mascot.welcome', 'wave')
    }, 1000)

    // Auto-hide message after 5 seconds
    setTimeout(() => {
      clearMessage()
    }, 6000)

    // Set up scroll listener with throttle
    let scrollTimeout: NodeJS.Timeout | null = null
    const throttledScroll = () => {
      if (scrollTimeout) return
      
      scrollTimeout = setTimeout(() => {
        handleScroll()
        scrollTimeout = null
      }, 200)
    }

    window.addEventListener('scroll', throttledScroll, { passive: true })

    // Set up position change interval if move around is enabled
    let positionInterval: NodeJS.Timeout | null = null
    if (moveAroundEnabled.value) {
      positionInterval = setInterval(() => {
        const newPosition = getRandomPosition()
        changePosition(newPosition)
      }, 15000) // Change position every 15 seconds
    }

    // Cleanup function
    return () => {
      window.removeEventListener('scroll', throttledScroll)
      if (scrollTimeout) clearTimeout(scrollTimeout)
      if (positionInterval) clearInterval(positionInterval)
    }
  }

  return {
    isVisible,
    currentMessage,
    currentAnimation,
    currentSection,
    currentPosition,
    moveAroundEnabled,
    showMascot,
    hideMascot,
    updateMessage,
    clearMessage,
    triggerAnimation,
    changePosition,
    handleScroll,
    initializeMascot
  }
}
