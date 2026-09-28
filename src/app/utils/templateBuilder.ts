// Helpers of the template builder (admin "Design" tab): read/write the template choice in tenant.json
// without destroying comments or formatting, and reduce variables to what differs from the defaults.
import { applyEdits, modify, parse, type ParseError } from 'jsonc-parser'
import { resolveTemplateVars, type TemplateVarsSchema, type TemplateVarValue } from '~/utils/templateVars'

/** Where the choice is stored: tenant-wide default or one profile (invites have their own overrides form). */
export type TemplateScope = { kind: 'tenant' } | { kind: 'profile', name: string }

export interface TemplatesSelection {
  pdf?: string
  html?: string
  pdfVars?: Record<string, TemplateVarValue>
}

function path(scope: TemplateScope): (string | number)[] {
  return scope.kind === 'tenant' ? ['templates'] : ['profiles', scope.name, 'templates']
}

/** The `templates` object at the scope (empty if not set). Throws on invalid JSONC. */
export function readTemplatesSelection(tenantJson: string, scope: TemplateScope): TemplatesSelection {
  const errors: ParseError[] = []
  const root = parse(tenantJson, errors, { allowTrailingComma: true }) as Record<string, unknown> | undefined
  if (errors.length || !root || typeof root !== 'object') throw new Error('tenant.json is not valid JSON')
  let node: unknown = root
  for (const key of path(scope)) node = (node as Record<string, unknown> | undefined)?.[key as string]
  return node && typeof node === 'object' ? node as TemplatesSelection : {}
}

/**
 * Sets (or with an empty selection removes) the `templates` object at the scope. Only that property is
 * edited; comments and formatting elsewhere stay as they are.
 */
export function writeTemplatesSelection(tenantJson: string, scope: TemplateScope, selection: TemplatesSelection): string {
  const empty = !selection.pdf && !selection.html && !(selection.pdfVars && Object.keys(selection.pdfVars).length)
  const clean: TemplatesSelection = {}
  if (selection.pdf) clean.pdf = selection.pdf
  if (selection.html) clean.html = selection.html
  if (selection.pdfVars && Object.keys(selection.pdfVars).length) clean.pdfVars = selection.pdfVars
  const edits = modify(tenantJson, path(scope), empty ? undefined : clean, {
    formattingOptions: { insertSpaces: true, tabSize: 2, eol: '\n' }
  })
  return applyEdits(tenantJson, edits)
}

const same = (a: TemplateVarValue | undefined, b: TemplateVarValue | undefined) =>
  JSON.stringify(a) === JSON.stringify(b)

/**
 * The variables worth storing: the preset (if any) plus only the values that differ from what the
 * template default + preset already give. Keeps tenant.json short and lets template defaults evolve.
 */
export function minimalVars(
  schema: TemplateVarsSchema | undefined,
  preset: string | undefined,
  values: Record<string, TemplateVarValue>
): Record<string, TemplateVarValue> {
  if (!schema) return {}
  const base = resolveTemplateVars(schema, preset ? { preset } : null)
  const result: Record<string, TemplateVarValue> = preset && schema.presets?.[preset] ? { preset } : {}
  for (const key of Object.keys(schema.vars)) {
    if (values[key] !== undefined && !same(values[key], base[key])) result[key] = values[key]
  }
  return result
}
