import { describe, it, expect } from 'vitest'
import { cvPdfFileName } from '~/utils/cvFileName'

// Same cases as api/CvApi.Tests/PdfFileNameTests.cs
describe('cvPdfFileName', () => {
  it.each([
    ['Max Mustermann', 'en', 'cv-max-mustermann-en.pdf'],
    ['Jürgen Müßig-Öztürk', 'de', 'cv-juergen-muessig-oeztuerk-de.pdf'],
    ["  José  O'Brien ", 'en', 'cv-jose-o-brien-en.pdf'],
    [null, 'en', 'cv-en.pdf'],
    ['', 'de', 'cv-de.pdf'],
    ['***', 'en', 'cv-en.pdf']
  ])('%s → %s', (name, locale, expected) => {
    expect(cvPdfFileName(name, locale)).toBe(expected)
  })
})
