import type { MascotConfig, MascotAnimationState, MascotScrollContext, MascotDesign, MascotTheme } from '~/types/mascot'

/**
 * Composable for managing mascot state and behavior
 */
export const useMascot = () => {
  // Mascot configuration state
  const config = useState<MascotConfig>('mascotConfig', () => ({
    design: 'robot',
    theme: 'default',
    enabled: true,
    position: 'bottom-right',
    showMessages: true,
    messages: []
  }))

  // Current animation state
  const animationState = useState<MascotAnimationState>('mascotAnimationState', () => 'idle')

  // Scroll context
  const scrollContext = useState<MascotScrollContext>('mascotScrollContext', () => ({
    scrollPercent: 0,
    section: 'top',
    direction: 'none'
  }))

  // Message queue
  const messageQueue = useState<string[]>('mascotMessageQueue', () => [])
  const currentMessage = useState<string | null>('mascotCurrentMessage', () => null)

  /**
   * Update mascot configuration
   */
  const updateConfig = (newConfig: Partial<MascotConfig>) => {
    config.value = { ...config.value, ...newConfig }
  }

  /**
   * Set mascot design
   */
  const setDesign = (design: MascotDesign) => {
    config.value.design = design
  }

  /**
   * Set mascot theme
   */
  const setTheme = (theme: MascotTheme) => {
    config.value.theme = theme
  }

  /**
   * Enable or disable mascot
   */
  const setEnabled = (enabled: boolean) => {
    config.value.enabled = enabled
  }

  /**
   * Set animation state
   */
  const setAnimationState = (state: MascotAnimationState) => {
    animationState.value = state
  }

  /**
   * Update scroll context
   */
  const updateScrollContext = (context: Partial<MascotScrollContext>) => {
    scrollContext.value = { ...scrollContext.value, ...context }
  }

  /**
   * Show a message from the mascot
   */
  const showMessage = (message: string, duration = 3000) => {
    currentMessage.value = message
    setTimeout(() => {
      currentMessage.value = null
    }, duration)
  }

  /**
   * Queue multiple messages
   */
  const queueMessages = (messages: string[]) => {
    messageQueue.value = [...messageQueue.value, ...messages]
  }

  /**
   * Process message queue
   */
  const processMessageQueue = () => {
    if (messageQueue.value.length > 0 && !currentMessage.value) {
      const message = messageQueue.value.shift()
      if (message) {
        showMessage(message)
      }
    }
  }

  /**
   * Handle scroll events to update mascot behavior
   */
  const handleScroll = () => {
    if (typeof window === 'undefined') return

    const scrollY = window.scrollY
    const documentHeight = document.documentElement.scrollHeight - window.innerHeight
    const scrollPercent = documentHeight > 0 ? (scrollY / documentHeight) * 100 : 0
    const previousScrollY = (scrollContext.value.scrollPercent * documentHeight / 100)

    // Determine current section
    let section: MascotScrollContext['section'] = 'top'
    if (scrollPercent < 10) section = 'top'
    else if (scrollPercent < 20) section = 'intro'
    else if (scrollPercent < 30) section = 'skills'
    else if (scrollPercent < 50) section = 'experiences'
    else if (scrollPercent < 65) section = 'studies'
    else if (scrollPercent < 80) section = 'projects'
    else if (scrollPercent < 95) section = 'other'
    else section = 'footer'

    // Determine scroll direction
    const direction = scrollY > previousScrollY ? 'down' : 
                     scrollY < previousScrollY ? 'up' : 'none'

    updateScrollContext({
      scrollPercent,
      section,
      direction
    })

    // Update animation state based on scroll
    if (direction !== 'none') {
      setAnimationState('scrolling')
      // Return to idle after scrolling stops
      setTimeout(() => {
        if (scrollContext.value.direction === 'none') {
          setAnimationState('idle')
        }
      }, 500)
    }
  }

  return {
    config: readonly(config),
    animationState: readonly(animationState),
    scrollContext: readonly(scrollContext),
    currentMessage: readonly(currentMessage),
    updateConfig,
    setDesign,
    setTheme,
    setEnabled,
    setAnimationState,
    updateScrollContext,
    showMessage,
    queueMessages,
    processMessageQueue,
    handleScroll
  }
}
