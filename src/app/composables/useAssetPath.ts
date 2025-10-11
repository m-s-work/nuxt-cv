/**
 * Composable for generating correct asset paths that respect the app's baseURL
 * This is essential for GitHub Pages deployment where the app is hosted at a subpath
 */
export function useAssetPath() {
  const config = useRuntimeConfig()
  const baseURL = config.app.baseURL

  /**
   * Generate a full asset path that respects the app's baseURL configuration
   * @param path - The path to the asset (e.g., '/images/profile.jpg')
   * @returns The full path with baseURL prepended if necessary
   */
  function getAssetPath(path: string): string {
    // If path doesn't start with '/', return as is
    if (!path.startsWith('/')) {
      return path
    }

    // Remove trailing slash from baseURL and leading slash from path, then join
    const cleanBaseURL = baseURL.replace(/\/$/, '')
    const cleanPath = path.replace(/^\//, '')
    
    return `${cleanBaseURL}/${cleanPath}`
  }

  return {
    getAssetPath
  }
}
