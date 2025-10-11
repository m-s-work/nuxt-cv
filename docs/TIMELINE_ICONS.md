# Timeline Icons

This document lists all available icons for timeline entries in the CV application.

## Available Icons

### Professional Experience
- `briefcase` or `experience` - Default briefcase icon for professional experience
- `briefcase-business`, `self-employed`, `business` - Briefcase with star for self-employed/business ventures

### Education
- `graduation-cap` or `study` - Default graduation cap icon for studies/education
- `award` or `abitur` - Award/medal icon for Abitur (high school diploma)
- `book-open` or `school` - Book icon for school education

### Projects
- `code` or `project` - Default code icon for projects

### Military & Service
- `shield` or `military` - Shield icon for military service
- `helmet` or `army-helmet` - Army helmet icon for military service

### Internships
- `users` or `internship` - Users/team icon for general internships
- `hammer` or `construction` - Hammer icon for construction site internships

### Entrepreneurship
- `lightbulb` or `entrepreneur` - Lightbulb icon for entrepreneurship/innovation
- `briefcase-business`, `self-employed`, `business` - Business icon for self-employed work

## Usage

Icons can be specified per entry using the `icon` property:

```typescript
const otherEntries = ref([
  {
    id: 1,
    title: 'Mandatory Military Service',
    icon: 'helmet'  // or 'army-helmet'
  },
  {
    id: 2,
    title: 'Construction Internship',
    icon: 'hammer'  // or 'construction'
  },
  {
    id: 3,
    title: 'Self-Employed Consultant',
    icon: 'lightbulb'  // or 'entrepreneur'
  }
])
```

## Default Icons by Section Type

If no icon is specified, the following defaults are used:
- **Experience**: `briefcase`
- **Study**: `graduation-cap`
- **Project**: `code`
- **Other**: `code` (fallback)

## Icon Aliases

Many icons have multiple aliases for convenience:
- Military: `shield`, `military`, `helmet`, `army-helmet`
- Internship: `users`, `internship`, `hammer`, `construction`
- Business: `briefcase-business`, `self-employed`, `business`
- School: `book-open`, `school`
- Entrepreneurship: `lightbulb`, `entrepreneur`
