# Mascot Feature

An SVG-based animated mascot system that guides users through the CV and enhances user engagement.

## Overview

The mascot feature provides an interactive, animated character that appears on the CV page. It responds to user interactions such as mouse hover and scroll position, creating a more engaging and memorable user experience.

## Features

### Animation States

The mascot supports four animation states:

1. **Idle** - Default gentle animation when not interacting
2. **Tickle** - Playful animation triggered on mouse hover
3. **Wave** - Greeting animation shown on page load
4. **Celebrate** - Celebration animation when user scrolls to the bottom of the page

### Mascot Designs

Three mascot designs are currently available:

#### 1. Cat (Default)
- **Appearance**: Orange cat with whiskers and a curled tail
- **Idle Animation**: Gentle breathing with tail wagging
- **Tickle Effect**: Playful wiggling and ear twitching
- **Wave**: Left paw waving
- **Celebrate**: Jumping with rapid tail movement

#### 2. Robot
- **Appearance**: Retro-style blue robot with antenna
- **Idle Animation**: Gentle floating motion with blinking antenna light
- **Tickle Effect**: Shaking/vibrating
- **Wave**: Right arm waving
- **Celebrate**: Jumping with color-changing antenna light

#### 3. Owl
- **Appearance**: Brown owl with large eyes and ear tufts
- **Idle Animation**: Gentle swaying with wing movements
- **Tickle Effect**: Head tilting with ear tuft wiggling
- **Wave**: Wing flapping
- **Celebrate**: Hopping with excited wing movements

## Configuration

### Query Parameters

The mascot can be configured via URL query parameters:

```
# Select mascot design
?mascot=cat          # Cat mascot (default)
?mascot=robot        # Robot mascot
?mascot=owl          # Owl mascot

# Set mascot position
?mascotPosition=bottom-right  # Bottom right (default)
?mascotPosition=bottom-left   # Bottom left
?mascotPosition=top-right     # Top right
?mascotPosition=top-left      # Top left

# Combine parameters
?mascot=robot&mascotPosition=top-left
```

### Programmatic Configuration

Use the `useMascot` composable to control the mascot programmatically:

```typescript
const mascot = useMascot()

// Change design
mascot.setDesign('robot')

// Change position
mascot.setPosition('top-left')

// Trigger animations
mascot.wave()

// Show/hide
mascot.show()
mascot.hide()

// Get current state
const state = mascot.getState()
console.log(state.currentAnimation) // 'idle', 'tickle', 'wave', or 'celebrate'
```

## Technical Implementation

### Components

- **MascotManager.vue** - Main component that handles mascot display, positioning, and scroll tracking
- **mascots/MascotCat.vue** - Cat mascot SVG component
- **mascots/MascotRobot.vue** - Robot mascot SVG component
- **mascots/MascotOwl.vue** - Owl mascot SVG component

### Composable

- **useMascot.ts** - State management composable for mascot configuration and behavior

### Types

- **types/mascot.ts** - TypeScript type definitions for mascot configuration and state

## Behavior

### Scroll Interaction

The mascot tracks the user's scroll position:
- When the user scrolls to 95% or more of the page, the mascot triggers the celebrate animation
- The celebrate animation plays for 2 seconds before returning to idle state

### Hover Effect

When the user hovers over the mascot:
- The mascot enters the "tickle" animation state
- On mouse leave, it returns to idle state

### Initial Wave

The mascot waves 2 seconds after the page loads as a greeting to the user.

## Responsive Design

- **Desktop**: 120px × 120px
- **Mobile**: 80px × 80px
- **Print**: Hidden (mascot is not displayed when printing)

## Accessibility

- The mascot is purely decorative and does not interfere with page content
- It's positioned with `position: fixed` so it doesn't affect layout
- Hidden during print to keep the printed CV professional

## Future Extensions

The mascot system is designed to be extensible. Future enhancements could include:

1. **Additional Designs**: More mascot characters (dog, penguin, dragon, etc.)
2. **Themes**: Color variants for each mascot design
3. **Text/Speech Bubbles**: Display helpful tips or messages
4. **More Animations**: Additional animation states (sleeping, working, etc.)
5. **Configuration Files**: Load mascot settings from config files instead of URL parameters
6. **Internationalization**: Mascot-specific text translations
7. **Sound Effects**: Optional audio feedback for interactions
8. **Click Interactions**: Special actions when clicking the mascot

## Examples

### Basic Usage

The mascot is automatically included in the application and appears in the bottom-right corner by default.

### Testing Different Designs

Visit these URLs to test different mascots:
- `/` - Default cat mascot
- `/?mascot=robot` - Robot mascot
- `/?mascot=owl` - Owl mascot

### Testing Different Positions

- `/?mascotPosition=top-left` - Top left position
- `/?mascot=owl&mascotPosition=top-right` - Owl in top right

## Development

### Adding a New Mascot Design

1. Create a new Vue component in `src/app/components/mascots/` (e.g., `MascotDog.vue`)
2. Implement the required props: `animation` and `isHovered`
3. Create SVG artwork with CSS animations for each state
4. Add the design type to `src/app/types/mascot.ts`
5. Import and add the component to `MascotManager.vue`
6. Update tests in `src/tests/components/Mascot.spec.ts`

### Testing

Tests are located in `src/tests/components/Mascot.spec.ts` and cover:
- Component props validation
- Animation state transitions
- Design and position configuration
- State management
- Scroll progress calculation

Run tests with:
```bash
npm test
```

## Performance

The mascot is implemented entirely with SVG and CSS animations, ensuring:
- No JavaScript animation loops
- Hardware-accelerated transforms
- Minimal memory footprint
- Smooth 60fps animations

## Browser Compatibility

The mascot uses standard SVG and CSS features supported by all modern browsers:
- Chrome/Edge 90+
- Firefox 88+
- Safari 14+
- Mobile browsers with SVG and CSS animation support
