/**
 * Composable for managing splash screen state
 * Supports different splash screen designs and controls visibility
 */
export const useSplashScreen = () => {
  const showSplash = useState('showSplash', () => true)
  const splashType = useState<1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10>('splashType', () => 1)
  const onHiddenCallbacks = useState<(() => void)[]>('splashHiddenCallbacks', () => [])
  
  /**
   * Hide the splash screen
   */
  const hideSplash = () => {
    showSplash.value = false
    // Execute all callbacks after splash is hidden
    nextTick(() => {
      onHiddenCallbacks.value.forEach(callback => callback())
      onHiddenCallbacks.value = []
    })
  }
  
  /**
   * Show the splash screen with optional type
   */
  const showSplashScreen = (type: 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 = 1) => {
    splashType.value = type
    showSplash.value = true
  }
  
  /**
   * Set splash screen type
   */
  const setSplashType = (type: 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10) => {
    splashType.value = type
  }
  
  /**
   * Register a callback to be executed when splash is hidden
   */
  const onSplashHidden = (callback: () => void) => {
    if (!showSplash.value) {
      // If splash is already hidden, execute immediately
      callback()
    } else {
      // Otherwise, queue it for later
      onHiddenCallbacks.value.push(callback)
    }
  }
  
  return {
    showSplash: readonly(showSplash),
    splashType: readonly(splashType),
    hideSplash,
    showSplashScreen,
    setSplashType,
    onSplashHidden
  }
}
