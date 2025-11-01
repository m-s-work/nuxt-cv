import type { MascotConfig, MascotState } from '~/types/mascot'

const mascotState = ref<MascotState>('idle')
const mascotConfig = ref<MascotConfig>({
  design: 'robot',
  enabled: true,
  position: 'bottom-right',
  size: 120,
  theme: {
    primary: '#3b82f6',
    secondary: '#60a5fa',
    accent: '#fbbf24'
  }
})

let scrollTimeout: NodeJS.Timeout | null = null
let lastScrollY = 0
let scrollDirection: 'up' | 'down' | null = null

export const useMascot = () => {
  const setState = (state: MascotState) => {
    mascotState.value = state
  }

  const setConfig = (config: Partial<MascotConfig>) => {
    mascotConfig.value = { ...mascotConfig.value, ...config }
  }

  const handleScroll = () => {
    if (typeof window === 'undefined') return

    const currentScrollY = window.scrollY
    const newDirection = currentScrollY > lastScrollY ? 'down' : 'up'
    
    // Detect scroll direction change
    if (scrollDirection !== newDirection) {
      scrollDirection = newDirection
      lastScrollY = currentScrollY
    }

    // Set scrolling state
    setState('scrolling')

    // Clear existing timeout
    if (scrollTimeout) {
      clearTimeout(scrollTimeout)
    }

    // Return to idle after scrolling stops
    scrollTimeout = setTimeout(() => {
      setState('idle')
      scrollDirection = null
    }, 1000)
  }

  const handleTickle = () => {
    setState('tickled')
    setTimeout(() => {
      setState('idle')
    }, 2000)
  }

  const celebrate = () => {
    setState('celebrating')
    setTimeout(() => {
      setState('idle')
    }, 3000)
  }

  const greet = () => {
    setState('greeting')
    setTimeout(() => {
      setState('idle')
    }, 2000)
  }

  const think = () => {
    setState('thinking')
    setTimeout(() => {
      setState('idle')
    }, 3000)
  }

  // Initialize scroll listener
  const initScrollListener = () => {
    if (typeof window === 'undefined') return
    
    window.addEventListener('scroll', handleScroll, { passive: true })
  }

  // Cleanup scroll listener
  const cleanupScrollListener = () => {
    if (typeof window === 'undefined') return
    
    window.removeEventListener('scroll', handleScroll)
    if (scrollTimeout) {
      clearTimeout(scrollTimeout)
    }
  }

  return {
    state: readonly(mascotState),
    config: readonly(mascotConfig),
    setState,
    setConfig,
    handleTickle,
    celebrate,
    greet,
    think,
    initScrollListener,
    cleanupScrollListener,
    scrollDirection: computed(() => scrollDirection)
  }
}
