/**
 * Schema of the graphical CV editor (admin → Edit tab). Describes the blocks of a `cv.<locale>.json` file and
 * their fields; the editor renders forms from it. Keys the schema does not know are kept as they are, so the
 * editor never drops data it cannot show (edit those in the Files tab).
 *
 * Redaction markers (docs/REQUIREMENTS_ACCESS_AND_TENANCY.md) are editable too: `requires` (grants an entry
 * needs), `fieldRequires` (grants per field) and the aliases shown when companies / clients are hidden.
 */

export type CvFieldType =
  | 'text' | 'textarea' | 'number' | 'date' | 'boolean'
  /** List of short strings (technologies, skills). */
  | 'tags'
  /** List of longer strings, one per line (image URLs). */
  | 'lines'
  /** Grants needed to see an entry (`requires`). */
  | 'grants'
  | 'object' | 'list'

export interface CvEditorField {
  key: string
  label: string
  type: CvFieldType
  icon?: string
  hint?: string
  placeholder?: string
  /** Spans the full width of the form grid. */
  wide?: boolean
  /** object / list: fields of the object or of each list item. */
  fields?: CvEditorField[]
  /** list: title of a collapsed item. */
  itemTitle?: (item: Record<string, unknown>) => string
  /** list: singular name for "Add …". */
  itemName?: string
  /** list: items carry a numeric `id` (dated entries, referenced by the timeline and analytics). */
  ids?: boolean
  /** object: offers grants per field (`fieldRequires`). */
  fieldRequires?: boolean
}

const str = (value: unknown) => typeof value === 'string' ? value : ''
const join = (...parts: unknown[]) => parts.map(str).filter(Boolean).join(' · ')
const years = (item: Record<string, unknown>) => {
  const start = str(item.startDate).slice(0, 4)
  const end = item.endDate === null ? 'today' : str(item.endDate).slice(0, 4)
  return start || end ? `${start}–${end}` : ''
}

const DATES: CvEditorField[] = [
  { key: 'startDate', label: 'Start', type: 'date', placeholder: 'YYYY-MM-DD', hint: 'Year, month or day precision: 2020, 2020-03 or 2020-03-15' },
  { key: 'endDate', label: 'End', type: 'date', placeholder: 'empty = ongoing' }
]
const REQUIRES: CvEditorField = {
  key: 'requires', label: 'Only visible with grants', type: 'grants', wide: true,
  hint: 'Profiles / invites without all of these grants do not get this entry, e.g. private'
}
const ICON: CvEditorField = { key: 'icon', label: 'Icon', type: 'text', placeholder: 'e.g. briefcase, lightbulb' }
const TECHNOLOGIES: CvEditorField = { key: 'technologies', label: 'Technologies', type: 'tags', wide: true }

export const cvEditorSchema: CvEditorField[] = [
  {
    key: 'profile', label: 'Profile', type: 'object', icon: 'i-lucide-user',
    fields: [
      { key: 'name', label: 'Name', type: 'text' },
      { key: 'title', label: 'Title', type: 'text', placeholder: 'e.g. Software Architect' },
      { key: 'academicTitlePrefix', label: 'Academic title (prefix)', type: 'text', placeholder: 'e.g. Dr.' },
      { key: 'academicTitleSuffix', label: 'Academic title (suffix)', type: 'text', placeholder: 'e.g. Ph.D.' },
      { key: 'photoUrl', label: 'Photo', type: 'text', placeholder: '/api/assets/photo.jpg' },
      { key: 'photoUrlLarge', label: 'Photo (large)', type: 'text', placeholder: '/api/assets/photo-large.jpg' }
    ]
  },
  {
    key: 'details', label: 'Personal details', type: 'object', icon: 'i-lucide-id-card', fieldRequires: true,
    fields: [
      { key: 'location', label: 'Location', type: 'text' },
      { key: 'citizenship', label: 'Citizenship', type: 'text' },
      { key: 'email', label: 'E-mail', type: 'text' },
      { key: 'phone', label: 'Phone', type: 'text' },
      { key: 'birthDate', label: 'Birth date', type: 'date', placeholder: 'YYYY-MM-DD' }
    ]
  },
  {
    key: 'intro', label: 'Introduction', type: 'object', icon: 'i-lucide-text-quote',
    fields: [
      { key: 'text', label: 'Text', type: 'textarea', wide: true },
      { key: 'yearsOfExperience', label: 'Years of experience', type: 'number' },
      { key: 'programmingSince', label: 'Programming since (year)', type: 'number' },
      {
        key: 'stats', label: 'Stats', type: 'list', wide: true, itemName: 'stat',
        itemTitle: item => join(`${item.value ?? ''}${str(item.suffix)}`, item.label, item.icon),
        fields: [
          { key: 'label', label: 'Label', type: 'text' },
          { key: 'icon', label: 'Icon', type: 'text', placeholder: 'e.g. briefcase, users, code' },
          { key: 'value', label: 'Value', type: 'number' },
          { key: 'max', label: 'Max', type: 'number' },
          { key: 'suffix', label: 'Suffix', type: 'text', placeholder: 'e.g. +' }
        ]
      }
    ]
  },
  {
    key: 'skills', label: 'Skills', type: 'object', icon: 'i-lucide-sparkles',
    fields: [
      { key: 'skilled', label: 'Skilled in', type: 'tags', wide: true },
      { key: 'liked', label: 'Also like', type: 'tags', wide: true }
    ]
  },
  { key: 'preferredTechs', label: 'Preferred technologies', type: 'tags', icon: 'i-lucide-heart' },
  {
    key: 'languages', label: 'Languages', type: 'list', icon: 'i-lucide-languages', itemName: 'language',
    itemTitle: item => join(item.name, item.level),
    fields: [
      { key: 'name', label: 'Language', type: 'text' },
      { key: 'level', label: 'Level', type: 'text', placeholder: 'e.g. Fluent (C1)' },
      { key: 'code', label: 'Code', type: 'text', placeholder: 'e.g. en' }
    ]
  },
  {
    key: 'drivingLicenses', label: 'Driving licences', type: 'list', icon: 'i-lucide-car', itemName: 'licence',
    itemTitle: item => join(item.type, item.description),
    fields: [
      { key: 'type', label: 'Class', type: 'text', placeholder: 'e.g. B' },
      { key: 'description', label: 'Description', type: 'text' }
    ]
  },
  {
    key: 'experiences', label: 'Experience', type: 'list', icon: 'i-lucide-briefcase', itemName: 'experience', ids: true,
    itemTitle: item => join(item.position, item.company, years(item)),
    fields: [
      { key: 'position', label: 'Position', type: 'text' },
      { key: 'company', label: 'Company', type: 'text' },
      { key: 'companyAlias', label: 'Company alias', type: 'text', hint: 'Shown instead of the company when a profile hides companies' },
      ICON,
      ...DATES,
      { key: 'description', label: 'Description', type: 'textarea', wide: true },
      TECHNOLOGIES,
      { key: 'images', label: 'Images', type: 'lines', wide: true, placeholder: '/api/assets/…' },
      { key: 'logos', label: 'Logos', type: 'lines', wide: true, placeholder: '/api/assets/…' },
      REQUIRES
    ]
  },
  {
    key: 'studies', label: 'Education', type: 'list', icon: 'i-lucide-graduation-cap', itemName: 'study', ids: true,
    itemTitle: item => join(item.degree, item.institution, years(item)),
    fields: [
      { key: 'degree', label: 'Degree', type: 'text' },
      { key: 'institution', label: 'Institution', type: 'text' },
      { key: 'focus', label: 'Focus', type: 'text', wide: true },
      ICON,
      ...DATES,
      TECHNOLOGIES,
      REQUIRES
    ]
  },
  {
    key: 'projects', label: 'Projects', type: 'list', icon: 'i-lucide-folder-kanban', itemName: 'project', ids: true,
    itemTitle: item => join(item.name, item.client, years(item)),
    fields: [
      { key: 'name', label: 'Name', type: 'text' },
      { key: 'type', label: 'Type', type: 'text', placeholder: 'e.g. Web Application' },
      { key: 'client', label: 'Client', type: 'text' },
      { key: 'clientAlias', label: 'Client alias', type: 'text', hint: 'Shown instead of the client when a profile hides companies' },
      ICON,
      ...DATES,
      { key: 'description', label: 'Description', type: 'textarea', wide: true },
      TECHNOLOGIES,
      { key: 'screenshots', label: 'Screenshots', type: 'lines', wide: true, placeholder: '/api/assets/…' },
      { key: 'images', label: 'Images', type: 'lines', wide: true, placeholder: '/api/assets/…' },
      { key: 'logos', label: 'Logos', type: 'lines', wide: true, placeholder: '/api/assets/…' },
      REQUIRES
    ]
  },
  {
    key: 'otherEntries', label: 'Other', type: 'list', icon: 'i-lucide-award', itemName: 'entry', ids: true,
    itemTitle: item => join(item.title, item.institution, years(item)),
    fields: [
      { key: 'title', label: 'Title', type: 'text' },
      { key: 'institution', label: 'Institution', type: 'text' },
      ICON,
      ...DATES,
      { key: 'showPeriod', label: 'Show period', type: 'boolean' },
      { key: 'description', label: 'Description', type: 'textarea', wide: true },
      { key: 'images', label: 'Images', type: 'lines', wide: true, placeholder: '/api/assets/…' },
      REQUIRES
    ]
  }
]

/** Empty value of a field (used when a block or list item is added). */
export function emptyValue(field: CvEditorField): unknown {
  switch (field.type) {
    case 'object': return {}
    case 'list': case 'tags': case 'lines': case 'grants': return []
    case 'boolean': return false
    case 'number': return null
    default: return ''
  }
}

/** New list item: dated entries get the next free id and an ongoing period (endDate null). */
export function newListItem(field: CvEditorField, items: Array<Record<string, unknown>>): Record<string, unknown> {
  const item: Record<string, unknown> = {}
  if (field.ids) item.id = items.reduce((max, i) => Math.max(max, typeof i.id === 'number' ? i.id : 0), 0) + 1
  for (const f of field.fields ?? []) {
    if (f.type === 'text' || f.type === 'textarea' || f.type === 'number') item[f.key] = emptyValue(f)
  }
  if (field.ids) {
    item.startDate = ''
    item.endDate = null
  }
  return item
}

/** Moves an item within a list (no-op at the ends). */
export function moveItem<T>(items: T[], index: number, delta: -1 | 1): void {
  const target = index + delta
  if (target < 0 || target >= items.length) return
  const [item] = items.splice(index, 1)
  items.splice(target, 0, item!)
}

/**
 * Sets a field value, removing optional keys when cleared (an empty string, empty list or null) so the file
 * stays clean – except `endDate`, where null means "ongoing".
 */
export function setField(target: Record<string, unknown>, key: string, value: unknown): void {
  if (key === 'endDate') {
    target[key] = value === '' || value === undefined ? null : value
    return
  }
  const empty = value === '' || value === null || value === undefined || (Array.isArray(value) && value.length === 0)
  if (empty) delete target[key]
  else target[key] = value
}

/** Keys of an object the schema does not describe (kept, shown as "more fields" in the editor). */
export function unknownKeys(value: Record<string, unknown>, fields: CvEditorField[], extra: string[] = []): string[] {
  const known = new Set([...fields.map(f => f.key), ...extra])
  return Object.keys(value).filter(k => !known.has(k))
}

/** Splits a multi-line text into its non-empty lines. */
export function toLines(text: string): string[] {
  return text.split('\n').map(l => l.trim()).filter(Boolean)
}
