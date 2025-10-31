# CV Mascot Feature

This document describes the animated mascot feature added to the nuxt-cv application.

## Overview

The application now includes a friendly SVG-based animated mascot that appears in the bottom-right corner of the CV. The mascot provides visual interest, guides user attention, and creates a more engaging user experience through idle animations and interactive hover effects.

## Features

- **SVG-based Graphics**: Lightweight, scalable vector graphics with gradients
- **Idle Animations**: Gentle floating, blinking, arm waving, and eye movements
- **Hover Interactions**: Color changes, excited animations, sparkle effects, and tooltip display
- **Internationalization**: Supports English and German via i18n
- **Responsive Design**: Adapts to mobile and desktop screens
- **Print-friendly**: Hidden when printing the CV
- **Accessibility**: Includes ARIA labels for screen readers

## Mascot Characteristics

### Visual Design

The mascot is a simple, friendly character with:
- Circular body with gradient fill (purple in idle, orange/red when hovered)
- Two expressive eyes with pupils that look around
- Simple smile (changes to big smile when excited)
- Two arms that wave gently
- Drop shadow for depth
- Sparkle effects on hover

### Animations

#### Idle State
- **Floating**: Gentle up-and-down motion (3s cycle, 8px travel)
- **Shadow Pulse**: Shadow opacity changes synchronized with float
- **Arm Wave**: Subtle alternating arm movements (4s cycle each)
- **Eye Movement**: Pupils move around naturally (5s cycle)
- **Blinking**: Random blinks every 3-5 seconds (200ms duration)

#### Hover State
- **Color Change**: Gradient shifts from purple to orange/red
- **Scale Effect**: Mascot grows 10% larger
- **Excited Wave**: Rapid arm waving animation (0.5s cycle, ±20° rotation)
- **Sparkles**: Four twinkling sparkles appear around the mascot
- **Tooltip**: Shows "Hi! I'm your CV guide 👋" message
- **Bigger Smile**: Mouth changes to a wider, happier expression

## Technical Implementation

### Component Structure

**Location**: `src/app/components/CvMascot.vue`

The component uses:
- Vue 3 Composition API with `<script setup>`
- TypeScript for type safety
- Reactive state for hover and blink animations
- SVG for graphics with CSS animations
- i18n for multilingual support

### State Management

```typescript
const isHovered = ref(false)              // Tracks hover state
const currentEmotion = ref('idle')        // 'idle' | 'happy' | 'excited'
const shouldBlink = ref(false)            // Controls blink animation
const blinkInterval = ref<NodeJS.Timeout | null>(null)  // Blink timer
```

### Key CSS Classes

- `.mascot-container`: Fixed positioning, z-index 100, bottom-right corner
- `.mascot-svg`: Full size with drop shadow
- `.mascot-body`: Main body with float animation
- `.mascot-arms`: Arms with wave animations
- `.mascot-eyes`: Eyes with look-around animation
- `.mascot-sparkles`: Sparkle effects on hover
- `.mascot-tooltip`: Tooltip with fade-in effect

### Positioning

```css
.mascot-container {
  position: fixed;
  bottom: 2rem;
  right: 2rem;
  width: 120px;
  height: 120px;
  z-index: 100;
}
```

On mobile (max-width: 768px):
```css
.mascot-container {
  width: 80px;
  height: 80px;
  bottom: 1rem;
  right: 1rem;
}
```

## Integration

### App Integration

The mascot is integrated into `src/app/app.vue`:

```vue
<template>
  <div>
    <SplashScreenManager />
    <LanguageSelector />
    <CvMascot />
    <NuxtPage />
  </div>
</template>
```

The mascot is rendered at the app level, making it visible across all pages.

## Internationalization

The mascot supports English and German translations:

### English
- **ARIA Label**: "Friendly CV mascot companion"
- **Tooltip**: "Hi! I'm your CV guide 👋"

### German
- **ARIA Label**: "Freundliches CV-Maskottchen"
- **Tooltip**: "Hallo! Ich bin dein CV-Begleiter 👋"

## Testing

Tests are located in `src/tests/components/CvMascot.spec.ts` and cover:
- State management (hover, blink, emotions)
- Event handlers (mouseenter, mouseleave)
- Animation triggers
- Positioning properties
- Tooltip display
- Print visibility

Run tests with:
```bash
npm test CvMascot
```

## Performance Considerations

- **GPU Acceleration**: Uses CSS transforms and opacity for animations
- **Lightweight**: Pure SVG graphics, no external images
- **Efficient Timers**: Proper cleanup of intervals on component unmount
- **No Heavy Computations**: All animations are CSS-based
- **Optimized Rendering**: Uses Vue's reactivity system efficiently

## Browser Compatibility

The mascot uses modern CSS and JavaScript features:
- CSS animations and transforms
- CSS gradients
- SVG support
- ES6+ JavaScript

All features are supported in modern browsers (Chrome, Firefox, Safari, Edge).

## Customization

### Changing Colors

Modify the gradient definitions in the `<defs>` section:

```svg
<linearGradient id="bodyGradient" x1="0%" y1="0%" x2="0%" y2="100%">
  <stop offset="0%" style="stop-color:#667eea;stop-opacity:1" />
  <stop offset="100%" style="stop-color:#764ba2;stop-opacity:1" />
</linearGradient>
```

### Adjusting Animation Speed

Modify animation durations in the CSS:

```css
.mascot-body {
  animation: float 3s ease-in-out infinite; /* Change 3s to desired duration */
}
```

### Changing Position

Modify the `.mascot-container` CSS:

```css
.mascot-container {
  bottom: 2rem;  /* Distance from bottom */
  right: 2rem;   /* Distance from right */
}
```

### Modifying Blink Frequency

Adjust the interval in the component:

```typescript
blinkInterval.value = setInterval(() => {
  shouldBlink.value = true
  setTimeout(() => {
    shouldBlink.value = false
  }, 200)
}, 3000 + Math.random() * 2000) // Change 3000-5000ms range
```

## User Experience Benefits

1. **Engagement**: Animated character catches user attention and creates emotional connection
2. **Guidance**: Visual companion that "guides" through the CV content
3. **Personality**: Adds personality and uniqueness to the CV presentation
4. **Interactivity**: Provides immediate feedback to user interactions
5. **Memorability**: Helps the CV stand out and be more memorable
6. **User Retention**: Engaging animations encourage users to explore the CV more thoroughly

## Accessibility

- **ARIA Labels**: Proper labels for screen readers
- **No Motion Preference**: Respects user's motion preferences (can be enhanced)
- **Color Contrast**: Sufficient contrast for visibility
- **Keyboard Navigation**: Does not interfere with keyboard navigation
- **Non-Essential**: Decorative element that doesn't prevent CV access

## Future Enhancement Ideas

Potential improvements for future iterations:

1. **More Emotions**: Add happy, surprised, or thinking expressions
2. **Gesture Recognition**: Respond to scroll position or section changes
3. **Sound Effects**: Optional gentle sound effects on interaction
4. **Multiple Mascots**: Different mascot designs selectable via settings
5. **Animation Variety**: More complex idle animations
6. **Scroll Guidance**: Point to or highlight different CV sections
7. **Theme Integration**: Color matching with selected theme
8. **Disable Option**: User preference to hide the mascot
9. **Easter Eggs**: Special animations on certain interactions
10. **Progress Indication**: Reflect CV reading progress through animations

## Troubleshooting

### Mascot Not Visible
- Check z-index conflicts with other fixed elements
- Verify `.no-print` class isn't affecting display incorrectly
- Ensure component is imported and used in app.vue

### Animations Not Working
- Check browser support for CSS animations
- Verify no `prefers-reduced-motion` media query conflicts
- Check console for JavaScript errors

### Performance Issues
- Reduce animation complexity if needed
- Check for multiple instances of the component
- Consider adding `will-change` CSS property for smoother animations

## Best Practices

1. **Keep It Subtle**: Don't let the mascot distract from CV content
2. **Test on Mobile**: Ensure animations work well on smaller screens
3. **Monitor Performance**: Check frame rates on lower-end devices
4. **User Feedback**: Gather feedback on mascot usefulness and appeal
5. **Cultural Sensitivity**: Consider if mascot design suits target audience
6. **Print Preview**: Always test that mascot is hidden in print view
7. **Accessibility First**: Ensure mascot doesn't hinder accessibility

## Conclusion

The CV mascot adds a delightful, engaging element to the nuxt-cv application. It enhances user experience through gentle animations and interactive feedback while maintaining professional presentation. The implementation is lightweight, performant, and follows Vue.js best practices.
