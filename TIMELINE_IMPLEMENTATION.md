# Timeline Feature Implementation

## Overview
This document describes the implementation of the interactive SVG timeline feature for the CV application.

## Components

### CvTimeline.vue
The main timeline component that renders an SVG visualization of experiences and studies.

**Features:**
- Vertical timeline with year markers
- Proportional bar heights based on actual duration
- Scroll-based highlighting using IntersectionObserver
- Automatic column assignment for overlapping periods
- Responsive design (hidden on mobile, visible on desktop and print)

**Props:**
- `experiences`: Array of experience objects with period information
- `studies`: Array of study objects with period information

**Rendering Logic:**
1. Parses period strings (e.g., "2020 - Present", "2015 - 2017")
2. Calculates timeline items with column assignments to handle overlaps
3. Determines year range (min/max) and total years
4. Renders SVG with:
   - Central axis line
   - Year markers at 2-year intervals
   - Timeline bars for each item (positioned based on column)
   - Connector lines from bars to central axis

**Scroll Tracking:**
- Uses IntersectionObserver to monitor which items are visible in viewport
- Updates `activeItems` ref to track currently visible items
- Applies different styling (color, opacity) to active vs inactive items

### timeline.ts Utilities

**parsePeriod(period: string): ParsedPeriod**
- Parses period strings like "2020 - Present" or "2017 - 2020"
- Handles "Present" by using current year
- Returns start, end, and duration

**calculateColumns(items): TimelineItem[]**
- Assigns column positions to timeline items
- Items that overlap are placed in different columns
- Uses greedy algorithm: finds first available column for each item

**getYearRange(items): { minYear, maxYear, totalYears }**
- Calculates the year range spanned by all timeline items
- Used for proportional sizing and positioning

## Integration

### index.vue
- Added `experiences` and `studies` data refs
- Integrated `CvTimeline` component into layout
- Created grid layout with timeline on left, content on right

### CvExperiences.vue & CvStudies.vue
- Added `data-item-id` attributes to each card
- Format: `experience-{id}` or `study-{id}`
- Required for IntersectionObserver to track visibility

## Styling

### Responsive Behavior
- **Mobile/Tablet (< 1024px)**: Timeline hidden
- **Desktop (≥ 1024px)**: Timeline visible, sticky positioning
- **Print**: Timeline visible, static positioning

### Color Scheme
- **Experiences (active)**: `fill-primary-600` (primary blue)
- **Studies (active)**: `fill-blue-600` (blue)
- **Inactive items**: Same colors with 50% opacity
- **Year markers**: Gray dots and labels
- **Connector lines**: Dashed gray lines

### Layout
- Timeline container: Sticky positioning, spans viewport height
- Grid layout: `auto 1fr` (timeline width auto-adjusts based on columns)
- SVG dimensions: Width adapts to column count, height 800px

## Testing

### timeline.spec.ts
Tests for utility functions:
- Period parsing (including "Present")
- Column calculation for non-overlapping and overlapping items
- Year range calculation
- Edge cases (empty items array)

All 20 tests passing across 6 test files.

## Usage Example

```vue
<CvTimeline 
  :experiences="experiences" 
  :studies="studies" 
/>
```

Where `experiences` and `studies` are arrays with objects containing:
- `id`: Unique identifier
- `period`: String like "2020 - Present" or "2015 - 2017"
- Other fields (company, position, etc.) are not used by timeline but displayed in cards

## Future Enhancements

Potential improvements:
1. Click-to-scroll: Click timeline item to scroll to corresponding card
2. Tooltips: Show details on hover over timeline bars
3. Customizable colors: Support different colors per item or category
4. Animation: Smooth transitions when items become active/inactive
5. Year labels: Show start/end years on timeline bars
6. Milestone markers: Support for single-year events (not just periods)
