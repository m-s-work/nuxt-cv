# Splashscreen Animation Feature

This document describes the splashscreen animation feature added to the nuxt-cv application.

## Overview

The application now includes 4 different animated splashscreen designs that display when the page loads. These splashscreens help catch user attention and provide a smooth transition into the CV content.

## Features

- **4 Different Designs**: Choose from 4 visually distinct animated splashscreens
- **CSS-based Animations**: Primarily uses CSS animations with minimal JavaScript
- **Fullscreen Display**: Covers the entire viewport during the animation
- **Smooth Blending**: Fades out smoothly to reveal the CV content
- **Internationalization**: All text supports English and German via i18n
- **Print-friendly**: Hidden when printing the CV
- **URL Parameter Control**: Select specific splashscreen via query parameter

## Splashscreen Designs

### SplashScreen1 - Gradient with Spinner
- **Background**: Purple gradient (135deg, #667eea to #764ba2)
- **Animation**: Spinning circle loader with fade-in text
- **Text**: "Welcome" / "Willkommen" and "Loading CV..."
- **Duration**: ~2.5 seconds

### SplashScreen2 - Geometric Shapes
- **Background**: Blue gradient (45deg, #1e3a8a to #3b82f6)
- **Animation**: Rotating geometric shapes (circles, squares, triangles) in SVG
- **Text**: "Curriculum Vitae" / "Lebenslauf"
- **Duration**: ~3 seconds

### SplashScreen3 - Particle Effect
- **Background**: Dark radial gradient (#0f172a to #000000)
- **Animation**: 50 floating particles with glow effect
- **Text**: "Professional CV" / "Professioneller Lebenslauf" with subtitle
- **Duration**: ~2.8 seconds

### SplashScreen4 - Wave Animation
- **Background**: Dark blue gradient (#1e1b4b to #312e81)
- **Animation**: Layered SVG waves with colorful gradients
- **Text**: "Hello" / "Hallo" with subtitle
- **Duration**: ~3.2 seconds

## Usage

### Default Behavior
By default, SplashScreen1 is displayed when the application loads.

### Selecting a Specific Splashscreen
Add a query parameter to the URL:

```
http://localhost:3001/?splash=1    # SplashScreen1
http://localhost:3001/?splash=2    # SplashScreen2
http://localhost:3001/?splash=3    # SplashScreen3
http://localhost:3001/?splash=4    # SplashScreen4
```

### Programmatic Control
Use the `useSplashScreen` composable:

```typescript
import { useSplashScreen } from '~/composables/useSplashScreen'

const { showSplash, splashType, hideSplash, showSplashScreen, setSplashType } = useSplashScreen()

// Hide splash screen
hideSplash()

// Show splash screen with specific type
showSplashScreen(2)

// Set splash type
setSplashType(3)
```

## Technical Implementation

### Components
- `SplashScreen1.vue` - First splashscreen design
- `SplashScreen2.vue` - Second splashscreen design
- `SplashScreen3.vue` - Third splashscreen design
- `SplashScreen4.vue` - Fourth splashscreen design
- `SplashScreenManager.vue` - Manages all splashscreens and transitions

### Composable
- `useSplashScreen.ts` - State management for splashscreen visibility and type

### Integration
The `SplashScreenManager` component is integrated into `app.vue` and uses Vue's `<Teleport>` to render at the body level with high z-index (10000).

## Customization

### Changing Duration
Edit the timeout values in each splashscreen component:

```typescript
onMounted(() => {
  const timer = setTimeout(() => {
    // Animation completes naturally via CSS
  }, 2500) // Change this value (in milliseconds)
  
  onUnmounted(() => clearTimeout(timer))
})
```

### Modifying Animations
All animations are defined in the `<style scoped>` section using CSS `@keyframes`. Modify these to change animation behavior.

### Adding Text/Content
Text is defined in the `<i18n>` blocks at the bottom of each component. Add or modify translations there.

## Testing

Tests are located in `src/tests/components/SplashScreen.spec.ts` and cover:
- Component creation and props
- State management
- Splash type selection
- Timing validation
- URL parameter handling

Run tests with:
```bash
npm test
```

## Performance Considerations

- All animations use CSS transforms and opacity for optimal performance
- No heavy JavaScript computations during animation
- Automatic cleanup of timers on component unmount
- SVG animations are GPU-accelerated where supported

## Browser Compatibility

The splashscreens use modern CSS features but gracefully degrade in older browsers:
- CSS animations
- CSS gradients
- SVG support
- Flexbox

All features are supported in modern browsers (Chrome, Firefox, Safari, Edge).
