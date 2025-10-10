# Academic Titles Feature

## Overview
This feature adds support for academic titles (both prepended and appended) to the CV's hero section and sidebar profile components.

## Implementation

### Components Updated
- **CvHero.vue** - Hero section with academic titles
- **CvProfile.vue** - Sidebar profile with academic titles

### Data Model

Both components now include academic title fields:

```typescript
const profile = ref({
  name: 'Max Mustermann',
  title: 'Software Architect',
  academicTitlePrefix: 'Dr.',      // Prepended titles: Dr., Prof., Prof. Dr.
  academicTitleSuffix: 'Ph.D.'     // Appended titles: Ph.D., M.Sc., MBA, etc.
})
```

## Design Options

The feature includes 4 different display modes controlled by the `titleDisplayMode` variable:

### 1. Inline Mode (`'inline'`)
Academic titles are displayed inline with the name:
- **Format**: `Dr. Max Mustermann, Ph.D.`
- **Best for**: Formal CVs where titles are integral to the name
- **Hero**: Slightly smaller font size for titles to maintain visual hierarchy
- **Sidebar**: Integrated into the name heading

### 2. Separate Row Mode (`'separate-row'`) - **DEFAULT**
Academic titles are displayed in a distinct, emphasized section:
- **Format**: 
  ```
  ACADEMIC TITLES
  Dr. · Ph.D.
  ```
- **Best for**: Highlighting academic credentials
- **Hero**: Displayed in a semi-transparent card with backdrop blur
- **Sidebar**: Separated with a border-top and labeled section

### 3. Above Name Mode (`'above-name'`)
Academic titles are shown in a subtle row above the name:
- **Format**:
  ```
  Dr. · Ph.D.
  Max Mustermann
  Software Architect
  ```
- **Best for**: Emphasizing credentials without cluttering the name
- **Hero**: Light blue text matching the hero color scheme
- **Sidebar**: Medium gray text for subtlety

### 4. Below Title Mode (`'below-title'`)
Academic titles are shown below the job title:
- **Format**:
  ```
  Max Mustermann
  Software Architect
  Dr. · Ph.D.
  ```
- **Best for**: De-emphasizing academic titles while still displaying them
- **Hero**: Light blue text to maintain visual consistency
- **Sidebar**: Medium gray text for subtlety

## Switching Between Modes

To change the display mode, update the `titleDisplayMode` ref in both components:

```typescript
const titleDisplayMode = ref<'inline' | 'separate-row' | 'above-name' | 'below-title'>('separate-row')
```

## Internationalization

Both components support English and German translations:

**English:**
- Label: "Academic Titles"

**German:**
- Label: "Akademische Titel"

## Styling Considerations

### Hero Section
- Uses white/light blue colors to maintain contrast against the gradient background
- Separate row mode includes a semi-transparent card effect
- Responsive font sizes for mobile and desktop

### Sidebar Profile  
- Uses gray tones to match the sidebar color scheme
- Print-friendly styles included
- Separate row mode includes subtle border separation

## Graceful Degradation

The feature gracefully handles missing data:
- If both `academicTitlePrefix` and `academicTitleSuffix` are empty/null, no academic title section is displayed
- The separator (·) between prefix and suffix only appears when both are present
- Components fall back to standard name display when no titles are provided

## Print Support

Academic titles are included in print outputs:
- Print-optimized colors (gray tones)
- Maintains readability in black and white printing
- Layout remains clean and professional

## Examples

### Example 1: Both Prefix and Suffix
```typescript
academicTitlePrefix: 'Prof. Dr.'
academicTitleSuffix: 'MBA'
```
Display: `Prof. Dr. · MBA`

### Example 2: Prefix Only
```typescript
academicTitlePrefix: 'Dr.'
academicTitleSuffix: ''
```
Display: `Dr.`

### Example 3: Suffix Only
```typescript
academicTitlePrefix: ''
academicTitleSuffix: 'Ph.D.'
```
Display: `Ph.D.`

### Example 4: No Titles
```typescript
academicTitlePrefix: ''
academicTitleSuffix: ''
```
Display: (No academic titles section shown)
