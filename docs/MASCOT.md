# Mascot Feature

The mascot feature adds an interactive SVG-based animated character to guide users through the CV and enhance user retention.

## Features

- **Idle animations**: Gentle breathing, blinking, and subtle movements
- **Hover effects**: Interactive "tickle" effect when hovering over the mascot
- **Click interactions**: Greeting animations and messages when clicking
- **Scroll interactions**: Mascot reacts to page scrolling
- **Multiple designs**: Robot, Owl, and configurable for more designs
- **Theme support**: Default, dark, and colorful themes
- **Internationalization**: Messages in English and German
- **Fully configurable**: Position, design, theme, and behavior can be customized

## Available Designs

### Robot
A friendly blue robot with an antenna, blinking eyes, and animated torso.

**Animations:**
- Idle: Breathing, antenna light blinking, eye blinking
- Hover: Bouncing with excited eyes
- Scrolling: Head and eyes follow scroll direction
- Greeting: Waving with right arm
- Thinking: Antenna wiggling
- Celebrating: Jumping with flashing lights

### Owl
A wise owl with feathered wings and expressive eyes.

**Animations:**
- Idle: Breathing, blinking, pupil movement
- Hover: Wing flapping
- Scrolling: Head turning and pupil tracking
- Greeting: Wing waving and head nodding
- Thinking: Head tilting with ear twitching
- Celebrating: Bouncing with wing flapping

## Configuration

The mascot can be configured via URL parameters or programmatically:

### URL Parameters

- `?mascot=robot` - Set design to robot (default)
- `?mascot=owl` - Set design to owl
- `?mascotTheme=default` - Use default theme
- `?mascotTheme=dark` - Use dark theme
- `?mascotTheme=colorful` - Use colorful theme

Example: `http://localhost:3000/?mascot=owl&mascotTheme=colorful`

### Programmatic Configuration

```typescript
import { useMascot } from '~/composables/useMascot'

const { updateConfig, setDesign, setTheme } = useMascot()

// Update full configuration
updateConfig({
  design: 'owl',
  theme: 'colorful',
  position: 'bottom-left',
  showMessages: true,
  enabled: true
})

// Or update individual properties
setDesign('robot')
setTheme('dark')
```

## Configuration Options

```typescript
interface MascotConfig {
  design: 'robot' | 'owl' | 'cat'        // Mascot design
  theme: 'default' | 'dark' | 'colorful' // Color theme
  enabled: boolean                        // Enable/disable mascot
  position: 'bottom-right' | 'bottom-left' | 'top-right' | 'top-left'
  showMessages: boolean                   // Show/hide speech bubbles
  messages?: string[]                     // Custom messages
}
```

## Usage

The mascot is automatically added to the application via `app.vue`. It appears in the configured position (default: bottom-right) and responds to user interactions.

### Composable API

```typescript
const {
  config,              // Current configuration (readonly)
  animationState,      // Current animation state (readonly)
  scrollContext,       // Current scroll context (readonly)
  currentMessage,      // Currently displayed message (readonly)
  updateConfig,        // Update configuration
  setDesign,          // Set mascot design
  setTheme,           // Set color theme
  setEnabled,         // Enable/disable mascot
  setAnimationState,  // Change animation state
  showMessage,        // Show a message
  handleScroll        // Handle scroll events
} = useMascot()
```

## Creating New Mascot Designs

To create a new mascot design:

1. Create a new Vue component in `src/app/components/mascots/`
2. Implement the `MascotProps` interface
3. Add SVG animations using CSS keyframes
4. Support all animation states (idle, hover, scrolling, greeting, thinking, celebrating)
5. Import and add to the Mascot.vue component

Example structure:

```vue
<script setup lang="ts">
import type { MascotProps } from '~/types/mascot'

const props = withDefaults(defineProps<MascotProps>(), {
  theme: 'default',
  animationState: 'idle',
  size: 120
})

const colors = computed(() => {
  // Define theme colors
})

const animationClass = computed(() => {
  // Map animation state to CSS class
})
</script>

<template>
  <svg :width="size" :height="size" :class="animationClass">
    <!-- SVG content with animations -->
  </svg>
</template>

<style scoped>
/* Animation keyframes */
</style>
```

## Internationalization

The mascot messages are defined in the Mascot.vue component's i18n block. To add new languages, edit the i18n section:

```json
{
  "en": {
    "mascot": {
      "welcomeMessages": ["Hi there! 👋 Welcome to my CV!"],
      "hoverMessages": ["Hehe, that tickles! 😄"],
      "greetingMessages": ["Thanks for clicking! 🎉"]
    }
  },
  "de": {
    "mascot": {
      "welcomeMessages": ["Hallo! 👋 Willkommen zu meinem Lebenslauf!"],
      "hoverMessages": ["Hehe, das kitzelt! 😄"],
      "greetingMessages": ["Danke fürs Klicken! 🎉"]
    }
  }
}
```

## Accessibility

The mascot component includes:
- ARIA labels for screen readers
- Keyboard navigation support (Tab, Enter, Space)
- Hidden from print view
- Semantic HTML structure

## Performance

- Animations use CSS transforms for optimal performance
- Event listeners are properly cleaned up on unmount
- Passive scroll listeners for better scrolling performance
- No external dependencies

## Browser Support

The mascot works in all modern browsers that support:
- CSS animations and transforms
- SVG rendering
- ES6+ JavaScript features

## Future Enhancements

Potential features to add:
- More mascot designs (cat, dragon, etc.)
- Voice/sound effects
- Advanced scroll-based interactions
- Custom user-defined mascots
- Animation speed control
- More interaction patterns (drag, swipe, etc.)
