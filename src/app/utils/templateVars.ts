/**
 * Template variables: each template declares its variables (type, default, label, suggestions) and named
 * presets (colour sets). The owner's choice comes from the API (`templates.pdfVars`, merged per key across
 * tenant > profile > invite) and is resolved here: defaults → preset → explicit values.
 * Only declared keys with type-valid values are applied (e.g. colours must be hex), so nothing arbitrary
 * reaches CSS. The schema is also what the planned template builder UI (#87) renders as a form.
 */

export type TemplateVarDef =
  | { type: 'color', default: string, label: string, suggestions?: string[] }
  | { type: 'boolean', default: boolean, label: string }
  | { type: 'palette', default: string[], label: string }
  | { type: 'enum', default: string, label: string, options: readonly string[] }

export type TemplateVarValue = string | boolean | string[]

export interface TemplateVarsSchema {
  vars: Record<string, TemplateVarDef>
  /** Named colour sets / variants; each sets some of the vars. */
  presets?: Record<string, { label: string, values: Record<string, TemplateVarValue> }>
}

const colorRegex = /^#(?:[0-9a-f]{3}|[0-9a-f]{6}|[0-9a-f]{8})$/i

function coerce(def: TemplateVarDef, value: unknown): TemplateVarValue | undefined {
  switch (def.type) {
    case 'color':
      return typeof value === 'string' && colorRegex.test(value) ? value : undefined
    case 'boolean':
      return typeof value === 'boolean' ? value : undefined
    case 'palette':
      return Array.isArray(value) && value.length > 0 && value.every(v => typeof v === 'string' && colorRegex.test(v))
        ? value as string[]
        : undefined
    case 'enum':
      return typeof value === 'string' && def.options.includes(value) ? value : undefined
  }
}

export function resolveTemplateVars(
  schema: TemplateVarsSchema | undefined,
  input?: Record<string, unknown> | null
): Record<string, TemplateVarValue> {
  if (!schema) return {}
  const result: Record<string, TemplateVarValue> = {}
  for (const [key, def] of Object.entries(schema.vars)) result[key] = def.default

  const apply = (values: Record<string, unknown>) => {
    for (const [key, value] of Object.entries(values)) {
      const def = schema.vars[key]
      const coerced = def ? coerce(def, value) : undefined
      if (coerced !== undefined) result[key] = coerced
    }
  }

  const presetName = typeof input?.preset === 'string' ? input.preset : undefined
  const preset = presetName ? schema.presets?.[presetName] : undefined
  if (preset) apply(preset.values)
  if (input) apply(input)
  return result
}
