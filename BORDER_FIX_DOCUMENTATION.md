# CvBlock Border Removal Documentation

## Issue Summary
The CvBlock component displayed unwanted borders on:
1. The Date/Period badge (UBadge component in the header)
2. The entire card wrapper (UCard component)

## Root Cause

### 1. UCard Default Border
**Component:** Nuxt UI's `UCard` component  
**Location:** `src/app/components/CvBlock.vue` line 60-67

The UCard component from Nuxt UI v4 includes a default border as part of its base styling. This border was visible on all CvBlock instances in light and dark modes.

**Solution:** Added `!border-0` and `!ring-0` classes to remove both border and ring (box-shadow) in all modes.

```vue
<!-- Before -->
<UCard 
  :class="{
    'print:!shadow-none print:!border print:!border-gray-300': true,
    ...
  }"
>

<!-- After -->
<UCard 
  :class="{
    '!border-0 !ring-0': true,
    'print:!shadow-none': true,
    ...
  }"
>
```

The ring utility in Tailwind creates a box-shadow that appears as a border around the element.

### 2. UBadge Subtle Variant Border
**Component:** Nuxt UI's `UBadge` component with `variant="subtle"`  
**Location:** `src/app/components/CvBlock.vue` line 85-92

The UBadge component with the "subtle" variant includes a default border as part of the Nuxt UI v4 design system. This border was visible around period badges like "2018 - Present".

**Solution:** Added `!border-0` and `!ring-0` classes to remove the border and ring from the badge.

```vue
<!-- Before -->
<UBadge 
  color="primary" 
  variant="subtle" 
  class="print:!bg-gray-100 print:!text-black"
>

<!-- After -->
<UBadge 
  color="primary" 
  variant="subtle" 
  class="!border-0 !ring-0 print:!bg-gray-100 print:!text-black"
>
```

## Technical Details

### Nuxt UI v4 Default Styling
Nuxt UI v4 uses Tailwind CSS and applies default styling to components:
- **UCard**: Has a border and ring (box-shadow) by default for visual separation
- **UBadge (subtle variant)**: Has a border and potentially ring by default to define the badge boundary

The `ring` utility in Tailwind CSS creates a box-shadow that appears as a border around elements.

### CSS Specificity
The `!` (important) flag is necessary because:
1. Nuxt UI components use inline styles with higher specificity
2. The `!important` flag ensures our border and ring removal takes precedence
3. Both borders and rings are removed in all modes (screen and print) for a consistent clean design

### Ring vs Border
Tailwind CSS uses two different mechanisms for creating outlines:
- **border**: Traditional CSS border property
- **ring**: Box-shadow that appears as an outline (used by Nuxt UI for cards)

Both needed to be removed to achieve a truly borderless appearance.

### Border Removal in All Modes
Borders and rings are removed in both screen and print modes:
- **UCard**: `!border-0 !ring-0` removes both border and ring (box-shadow)
- **UBadge**: `!border-0 !ring-0` removes both border and ring

This ensures a consistent, clean, borderless appearance whether viewing on screen or printing to PDF.

## Visual Impact

### Before Fix
- Visible black/gray borders around each experience/education/project card
- Visible borders around date/period badges (e.g., "2018 - Present")

### After Fix
- Clean card appearance without borders in all modes
- Smooth, modern badge styling without borders
- Better visual hierarchy and cleaner design
- Consistent borderless appearance in screen and print modes

## Files Modified
- `src/app/components/CvBlock.vue`: Added `!border-0` classes to UCard and UBadge components

## Testing
All existing tests pass (50/50):
- Component rendering tests
- Layout tests  
- Functionality tests
- No regressions introduced
