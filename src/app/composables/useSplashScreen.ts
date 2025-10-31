/**
 * Composable for managing splash screen state
 * Supports different splash screen designs and controls visibility
 */
export const useSplashScreen = () => {
  const showSplash = useState('showSplash', () => true)
  const splashType = useState<1 | 2 | 3 | 4>('splashType', () => 1)
  
  /**
   * Hide the splash screen
   */
  const hideSplash = () => {
    showSplash.value = false
  }
  
  /**
   * Show the splash screen with optional type
   */
  const showSplashScreen = (type: 1 | 2 | 3 | 4 = 1) => {
    splashType.value = type
    showSplash.value = true
  }
  
  /**
   * Set splash screen type
   */
  const setSplashType = (type: 1 | 2 | 3 | 4) => {
    splashType.value = type
  }
  
  return {
    showSplash: readonly(showSplash),
    splashType: readonly(splashType),
    hideSplash,
    showSplashScreen,
    setSplashType
  }
}
