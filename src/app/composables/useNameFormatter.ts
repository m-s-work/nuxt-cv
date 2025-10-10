/**
 * Composable for formatting names with academic titles
 */
export const useNameFormatter = () => {
  /**
   * Format a full name with prepended and appended academic titles
   * @param name - The person's name
   * @param prependedTitles - Titles to prepend before the name (e.g., Dr., Prof.)
   * @param appendedTitles - Titles to append after the name (e.g., PhD, MSc)
   * @returns The formatted full name with titles
   */
  const formatFullName = (
    name: string,
    prependedTitles?: string[],
    appendedTitles?: string[]
  ): string => {
    const prepended = prependedTitles?.join(' ') ?? ''
    const appended = appendedTitles?.join(', ') ?? ''
    
    let result = name
    if (prepended) {
      result = `${prepended} ${result}`
    }
    if (appended) {
      result = `${result}, ${appended}`
    }
    return result
  }

  return {
    formatFullName
  }
}
