# CV Mascot Feature

The CV Mascot is an SVG-based animated character that guides users through the CV and helps improve user retention and engagement.

## Overview

The mascot appears in the bottom-right corner of the page (bottom-left on mobile) and provides contextual messages as users scroll through different sections of the CV. It uses smooth animations to catch attention and create a more engaging, personalized experience.

## Features

### Visual Design
- **SVG-based**: Fully scalable vector graphics for crisp display on all screen sizes
- **Friendly appearance**: Blue character with expressive features
- **Animated elements**: Eyes, mouth, arms, and body can all animate independently
- **Speech bubbles**: Clean, modern speech bubbles with contextual messages

### Animations
The mascot supports six different animations:

1. **Wave** - Friendly greeting animation (arm waves back and forth)
2. **Bounce** - Energetic bouncing motion to encourage scrolling
3. **Point-up** - Points upward to draw attention to content above
4. **Point-down** - Points downward to encourage scrolling
5. **Celebrate** - Celebratory animation with bouncing and arm movements
6. **Think** - Thoughtful animation with head nodding and moving eyes

### Scroll-based Behavior
The mascot automatically updates its message and animation based on the user's scroll position:

| Section | Message | Animation |
|---------|---------|-----------|
| Hero (top) | Welcome message | Wave |
| Hero (bottom) | Scroll down prompt | Point-down |
| Intro | Keep going encouragement | Bounce |
| Experiences | Check out work experience | Point-up |
| Projects | Amazing projects | Celebrate |
| Other | Almost at the end | Bounce |
| End | Thanks for viewing | Celebrate |

### Internationalization
All mascot messages are fully internationalized and support:
- English (default)
- German (Deutsch)

Messages are stored in the component's i18n block and use the Nuxt i18n system.

### Responsive Design
- **Desktop**: Appears in bottom-right corner (80x80px)
- **Mobile**: Appears in bottom-right corner (64x64px)
- **Print**: Hidden automatically for print media
- **Speech bubbles**: Responsive sizing based on screen width

## Implementation

### Components
- **CvMascot.vue** - Main mascot component with SVG and animations
- **useMascot.ts** - Composable for state management and scroll handling

### Usage
The mascot is automatically integrated into the main app layout (`app.vue`). It initializes when the page loads and begins tracking scroll position.

```vue
<!-- Automatically included in app.vue -->
<CvMascot />
```

### State Management
The mascot uses Vue's `useState` composable to manage:
- Visibility state
- Current message
- Current animation
- Scroll position tracking
- Current section detection

### Customization
To customize the mascot behavior, edit `src/app/composables/useMascot.ts`:

```typescript
// Example: Change when messages appear
if (scrollY < heroHeight * 0.5) {
  updateMessage('mascot.welcome', 'wave')
}
```

To customize the appearance, edit the SVG in `src/app/components/CvMascot.vue`:

```vue
<!-- Example: Change mascot color -->
<circle cx="50" cy="30" r="18" fill="#3B82F6" />
```

### Adding New Animations
1. Add the CSS animation in the `<style>` section:
```css
.mascot-container.my-animation .mascot-arm-right {
  animation: myAnimation 0.5s ease-out;
}

@keyframes myAnimation {
  0% { transform: rotate(0deg); }
  100% { transform: rotate(45deg); }
}
```

2. Trigger it from the composable:
```typescript
triggerAnimation('my-animation')
```

### Adding New Messages
1. Add the message key to the i18n block in `CvMascot.vue`:
```json
{
  "en": {
    "mascot": {
      "myMessage": "My custom message!"
    }
  },
  "de": {
    "mascot": {
      "myMessage": "Meine benutzerdefinierte Nachricht!"
    }
  }
}
```

2. Use it in the composable:
```typescript
updateMessage('mascot.myMessage', 'wave')
```

## Testing

Tests are located in `src/tests/components/CvMascot.spec.ts` and cover:
- State initialization
- Show/hide functionality
- Message updates
- Animation triggers
- Composable behavior

Run tests with:
```bash
npm test
```

## Performance Considerations

- **Throttled scroll events**: Scroll events are throttled to 200ms to prevent performance issues
- **CSS animations**: Uses CSS animations instead of JavaScript for smooth 60fps performance
- **Lazy initialization**: Mascot initializes after a 1-second delay to avoid impacting initial page load
- **Auto-hide messages**: Messages auto-hide after 6 seconds to reduce visual clutter

## Browser Support

The mascot works in all modern browsers that support:
- CSS animations
- SVG
- Intersection Observer API (for future enhancements)

## Accessibility

- **No interaction required**: Mascot is purely visual and doesn't require user interaction
- **Hidden for print**: Automatically hidden when printing
- **Non-intrusive**: Positioned in corner to avoid blocking content
- **Keyboard friendly**: Doesn't interfere with keyboard navigation

## Future Enhancements

Potential improvements for the mascot:
- Click interactions (e.g., click to dismiss, click for tips)
- Customizable positions (left/right, top/bottom)
- More animation variations
- Customizable appearance per tenant
- Advanced AI-driven contextual messages
- Voice interaction support
- Theme-aware color schemes (dark/light mode)
