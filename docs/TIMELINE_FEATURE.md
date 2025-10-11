# Timeline Feature

## Overview

The CV application now includes an interactive timeline visualization that displays experiences and education entries on a vertical timeline. The timeline adapts to the user's scroll position, highlighting entries currently visible in the viewport.

## Features

### Visual Timeline
- **Vertical SVG-based timeline** on the left side of the main content area
- **Year markers** showing the full range of experiences and studies
- **Color-coded entries**:
  - Blue for experiences (lighter shade when inactive, darker when active)
  - Green for studies (lighter shade when inactive, darker when active)
- **Proportional sizing**: Timeline entries are sized based on their actual duration

### Overlapping Periods
When multiple experiences or studies overlap in time, they are automatically arranged in separate columns to prevent visual conflicts.

### Scroll-based Highlighting
As you scroll through the CV, the timeline entries corresponding to the visible content are highlighted, making it easy to see where you are in the timeline.

### Responsive Design
- **Hidden on small screens**: The timeline is only visible on screens wider than 1280px (Tailwind's `xl` breakpoint)
- **Visible in print**: The timeline appears when printing the CV
- **Sticky positioning**: The timeline stays visible as you scroll down the page

## Technical Implementation

### Data Structure
Each experience and study entry includes:
```typescript
{
  id: number,
  startDate: string,  // ISO date format (YYYY-MM-DD)
  endDate: string | null,  // ISO date or null for ongoing
  // ... other fields
}
```

### Components

#### `CvTimeline.vue`
The main timeline component that:
- Receives experiences, studies, and active IDs as props
- Calculates timeline layout using the `useTimeline` composable
- Renders an SVG with year markers and entry bars
- Highlights active entries based on scroll position

#### `useTimeline.ts` Composable
Provides utility functions:
- `parseDate()`: Converts date strings to Date objects
- `getYearRange()`: Calculates min/max years from entries
- `assignColumns()`: Assigns entries to columns to handle overlaps
- `calculatePositions()`: Calculates Y positions and heights for entries

### Scroll Detection
The main `index.vue` page:
- Listens to scroll events
- Calculates which entries are in the viewport center
- Updates the `activeEntryIds` array
- Passes active IDs to the timeline component

## Usage

The timeline automatically works with the existing CV data. To add new entries:

1. Add the experience or study with `startDate` and `endDate` fields
2. Ensure the entry has a unique `id`
3. The timeline will automatically include and position the new entry

## Styling

The timeline uses Tailwind CSS classes and supports:
- Light mode
- Dark mode (with `dark:` variants)
- Print mode (with `print:` variants)

## Browser Support

The timeline uses modern CSS features:
- CSS Grid for layout
- SVG for graphics
- Intersection Observer API for scroll detection (with fallback)

Tested on modern browsers (Chrome, Firefox, Safari, Edge).
