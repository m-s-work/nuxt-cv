# Mascot System Documentation

## Overview

The mascot system provides an interactive, animated SVG-based character that guides users through the CV and enhances user engagement. The mascot responds to user interactions and scroll events with different animations and states.

## Features

- **Idle Animations**: Gentle floating animation when the mascot is idle
- **Tickle Effect**: Interactive response when the user clicks/taps on the mascot
- **Scroll Interaction**: The mascot reacts to scroll events with bouncing animations
- **Text Bubbles**: Shows contextual messages based on the current state
- **Configurable**: Multiple designs, themes, and customizable texts
- **Modular Architecture**: Designs are implemented as separate modules following the `MascotDesign` interface

## Architecture

### Type Definitions (`app/types/mascot.ts`)

```typescript
// Mascot states
type MascotState = 'idle' | 'tickled' | 'scrolling' | 'greeting' | 'thinking' | 'celebrating'

// Mascot theme
interface MascotTheme {
  primary: string
  secondary: string
  accent: string
  background?: string
}

// Mascot configuration
interface MascotConfig {
  design: string
  theme?: MascotTheme
  enabled: boolean
  position?: 'bottom-left' | 'bottom-right' | 'top-left' | 'top-right'
  size?: number
  texts?: Record<string, string>
}

// Mascot design interface
interface MascotDesign {
  id: string
  name: string
  render(state: MascotState, theme?: MascotTheme): string
  getAnimationClass(state: MascotState): string
  getText?(state: MascotState, customTexts?: Record<string, string>): string | null
}
```

### Composable (`app/composables/useMascot.ts`)

The `useMascot` composable manages the mascot's state and behavior:

```typescript
const { 
  state,              // Current mascot state (readonly)
  config,             // Current configuration (readonly)
  setState,           // Set mascot state
  setConfig,          // Update configuration
  handleTickle,       // Trigger tickle animation
  celebrate,          // Trigger celebration animation
  greet,              // Trigger greeting animation
  think,              // Trigger thinking animation
  initScrollListener, // Start listening to scroll events
  cleanupScrollListener, // Remove scroll listeners
  scrollDirection     // Current scroll direction (computed)
} = useMascot()
```

### Component (`app/components/CvMascot.vue`)

The main mascot component that renders the SVG and handles interactions.

### Mascot Designs

Designs are located in `app/mascots/` directory. Each design implements the `MascotDesign` interface.

#### Robot Design (`app/mascots/robot.ts`)

The default friendly robot mascot with:
- Animated eyes that change based on state
- Expressive mouth animations
- Blinking antenna light during scrolling
- Customizable colors via theme

## Usage

### Basic Setup

The mascot is automatically included in the app. To configure it:

```typescript
const { setConfig } = useMascot()

setConfig({
  design: 'robot',
  position: 'bottom-right',
  size: 120,
  theme: {
    primary: '#3b82f6',
    secondary: '#60a5fa',
    accent: '#fbbf24'
  }
})
```

### Disabling the Mascot

```typescript
const { setConfig } = useMascot()

setConfig({ enabled: false })
```

### Triggering Animations

```typescript
const { greet, celebrate, think } = useMascot()

// Greet the user
greet()

// Celebrate an achievement
celebrate()

// Show thinking state
think()
```

### Custom Texts

```typescript
const { setConfig } = useMascot()

setConfig({
  texts: {
    greeting: 'Welcome!',
    celebrating: 'Great job!',
    thinking: 'Let me think...',
    tickled: 'That tickles!'
  }
})
```

## Creating a Custom Mascot Design

1. Create a new file in `app/mascots/` (e.g., `my-mascot.ts`)
2. Implement the `MascotDesign` interface:

```typescript
import type { MascotDesign, MascotState, MascotTheme } from '~/types/mascot'

export const myMascot: MascotDesign = {
  id: 'my-mascot',
  name: 'My Custom Mascot',

  render(state: MascotState, theme?: MascotTheme): string {
    // Return SVG string
    return `<svg viewBox="0 0 100 100">...</svg>`
  },

  getAnimationClass(state: MascotState): string {
    // Return CSS class for animation
    return `mascot-${state}`
  },

  getText(state: MascotState, customTexts?: Record<string, string>): string | null {
    // Return text for bubble or null
    return customTexts?.[state] || null
  }
}
```

3. Register the design in `app/mascots/index.ts`:

```typescript
import { myMascot } from './my-mascot'

mascotRegistry.set('my-mascot', myMascot)
```

4. Use your custom mascot:

```typescript
const { setConfig } = useMascot()

setConfig({ design: 'my-mascot' })
```

## CSS Animations

The component includes several predefined animations:

- `mascot-idle`: Gentle floating animation
- `mascot-tickled`: Shake and scale animation
- `mascot-scrolling`: Bounce animation
- `mascot-greeting`: Wave animation
- `mascot-thinking`: Bob animation
- `mascot-celebrating`: Jump animation

## Accessibility

- The mascot is hidden during printing (uses `.no-print` class)
- Mobile responsive with smaller size on mobile devices
- Text bubbles support dark mode

## Configuration via URL Parameters

Currently, the mascot configuration is set programmatically. Future enhancements could include URL parameters like:

```
?mascot=robot&mascot_position=bottom-left&mascot_size=150
```

## Best Practices

1. **Keep SVG Simple**: Use simple SVG shapes for better performance
2. **Theme Consistency**: Use theme colors to match the site's color scheme
3. **Animation Timing**: Keep animations short (1-3 seconds) to avoid annoyance
4. **Responsive Design**: Test on mobile devices and adjust size accordingly
5. **State Management**: Use appropriate states for different user interactions

## Troubleshooting

### Mascot Not Visible

- Check if `enabled` is `true` in configuration
- Verify the design ID exists in the registry
- Check z-index conflicts with other elements

### Animations Not Working

- Ensure CSS animations are not disabled by browser settings
- Check for CSS conflicts with other styles
- Verify animation classes are properly applied

### Text Bubbles Not Showing

- Check if the design implements the `getText` method
- Verify custom texts are properly configured
- Ensure text bubble CSS is not overridden

## Future Enhancements

- Multiple mascot designs (cat, dog, alien, etc.)
- Sound effects for interactions
- More complex animations (walking, flying)
- Integration with CV sections (mascot comments on projects)
- User preference persistence (localStorage)
- Mascot customization UI
