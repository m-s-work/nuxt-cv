import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { describe, it, expect } from 'vitest'
import { cvEditorSchema } from '~/utils/cvEditorSchema'

// The static build bundles only icons found in .vue files plus the list in nuxt.config.ts.
describe('cv editor icons', () => {
  it('are bundled', () => {
    const config = readFileSync(resolve(process.cwd(), 'nuxt.config.ts'), 'utf8')
    for (const block of cvEditorSchema.filter(b => b.icon))
      expect(config).toContain(`'${block.icon!.replace(/^i-lucide-/, 'lucide:')}'`)
  })
})
