import type { MascotDesign, MascotState, MascotTheme } from '~/types/mascot'

export const robotMascot: MascotDesign = {
  id: 'robot',
  name: 'Friendly Robot',

  render(state: MascotState, theme?: MascotTheme): string {
    const primary = theme?.primary || '#3b82f6'
    const secondary = theme?.secondary || '#60a5fa'
    const accent = theme?.accent || '#fbbf24'

    // Different eye states based on mascot state
    const getEyes = () => {
      switch (state) {
        case 'tickled':
          return `
            <circle cx="35" cy="35" r="4" fill="${primary}" />
            <circle cx="65" cy="35" r="4" fill="${primary}" />
            <path d="M 30 28 Q 35 25 40 28" stroke="${primary}" stroke-width="2" fill="none" />
            <path d="M 60 28 Q 65 25 70 28" stroke="${primary}" stroke-width="2" fill="none" />
          `
        case 'celebrating':
          return `
            <circle cx="35" cy="35" r="5" fill="${primary}" />
            <circle cx="65" cy="35" r="5" fill="${primary}" />
            <circle cx="33" cy="33" r="2" fill="white" />
            <circle cx="63" cy="33" r="2" fill="white" />
          `
        case 'thinking':
          return `
            <ellipse cx="35" cy="35" rx="3" ry="5" fill="${primary}" />
            <ellipse cx="65" cy="35" rx="3" ry="5" fill="${primary}" />
          `
        case 'greeting':
        case 'scrolling':
          return `
            <circle cx="35" cy="35" r="4" fill="${primary}" />
            <circle cx="65" cy="35" r="4" fill="${primary}" />
          `
        default: // idle
          return `
            <circle cx="35" cy="35" r="4" fill="${primary}" />
            <circle cx="65" cy="35" r="4" fill="${primary}" />
          `
      }
    }

    // Different mouth states based on mascot state
    const getMouth = () => {
      switch (state) {
        case 'tickled':
          return `<path d="M 35 55 Q 50 65 65 55" stroke="${primary}" stroke-width="3" fill="none" stroke-linecap="round" />`
        case 'celebrating':
          return `<path d="M 30 50 Q 50 70 70 50" stroke="${primary}" stroke-width="3" fill="none" stroke-linecap="round" />`
        case 'thinking':
          return `<line x1="40" y1="55" x2="60" y2="55" stroke="${primary}" stroke-width="2" stroke-linecap="round" />`
        case 'greeting':
          return `<path d="M 35 50 Q 50 60 65 50" stroke="${primary}" stroke-width="3" fill="none" stroke-linecap="round" />`
        case 'scrolling':
          return `<circle cx="50" cy="55" r="3" fill="${primary}" />`
        default: // idle
          return `<path d="M 40 55 Q 50 60 60 55" stroke="${primary}" stroke-width="2" fill="none" stroke-linecap="round" />`
      }
    }

    return `
      <svg viewBox="0 0 100 100" xmlns="http://www.w3.org/2000/svg">
        <!-- Robot body -->
        <rect x="20" y="20" width="60" height="60" rx="10" fill="${secondary}" />
        
        <!-- Antenna -->
        <line x1="50" y1="20" x2="50" y2="10" stroke="${primary}" stroke-width="2" />
        <circle cx="50" cy="8" r="3" fill="${accent}" class="antenna-light" />
        
        <!-- Eyes -->
        ${getEyes()}
        
        <!-- Mouth -->
        ${getMouth()}
        
        <!-- Accents -->
        <rect x="30" y="70" width="8" height="3" rx="1" fill="${accent}" opacity="0.7" />
        <rect x="62" y="70" width="8" height="3" rx="1" fill="${accent}" opacity="0.7" />
      </svg>
    `
  },

  getAnimationClass(state: MascotState): string {
    switch (state) {
      case 'idle':
        return 'mascot-idle'
      case 'tickled':
        return 'mascot-tickled'
      case 'scrolling':
        return 'mascot-scrolling'
      case 'greeting':
        return 'mascot-greeting'
      case 'thinking':
        return 'mascot-thinking'
      case 'celebrating':
        return 'mascot-celebrating'
      default:
        return 'mascot-idle'
    }
  },

  getText(state: MascotState, customTexts?: Record<string, string>): string | null {
    const defaultTexts: Record<MascotState, string | null> = {
      idle: null,
      tickled: customTexts?.tickled || 'Hehe! 😊',
      scrolling: null,
      greeting: customTexts?.greeting || 'Hello! 👋',
      thinking: customTexts?.thinking || 'Hmm... 🤔',
      celebrating: customTexts?.celebrating || 'Awesome! 🎉'
    }

    return customTexts?.[state] || defaultTexts[state]
  }
}
