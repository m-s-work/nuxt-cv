// Favicon picker (admin "Design" tab): read/write `favicon` in tenant.json without destroying comments
// or formatting. The API draws the icon (GET /api/favicon.svg); see docs/REQUIREMENTS_ACCESS_AND_TENANCY.md §7.1.
import { applyEdits, modify, parse, type ParseError } from 'jsonc-parser'

export interface FaviconConfig {
  symbol?: string
  color?: string
  background?: string
}

/** The `favicon` object of tenant.json (empty if not set). Throws on invalid JSONC. */
export function readFavicon(tenantJson: string): FaviconConfig {
  const errors: ParseError[] = []
  const root = parse(tenantJson, errors, { allowTrailingComma: true }) as Record<string, unknown> | undefined
  if (errors.length || !root || typeof root !== 'object') throw new Error('tenant.json is not valid JSON')
  const node = root.favicon
  if (!node || typeof node !== 'object') return {}
  const { symbol, color, background } = node as Record<string, unknown>
  return {
    ...(typeof symbol === 'string' ? { symbol } : {}),
    ...(typeof color === 'string' ? { color } : {}),
    ...(typeof background === 'string' ? { background } : {})
  }
}

/** Sets (or with an empty config removes) `favicon`. Only that property is edited. */
export function writeFavicon(tenantJson: string, config: FaviconConfig): string {
  const clean: FaviconConfig = {}
  if (config.symbol) clean.symbol = config.symbol
  if (config.color) clean.color = config.color
  if (config.background) clean.background = config.background
  const edits = modify(tenantJson, ['favicon'], Object.keys(clean).length ? clean : undefined, {
    formattingOptions: { insertSpaces: true, tabSize: 2, eol: '\n' }
  })
  return applyEdits(tenantJson, edits)
}

/** SVG markup as an <img> source. */
export function svgDataUrl(svg: string): string {
  return `data:image/svg+xml;charset=utf-8,${encodeURIComponent(svg)}`
}
