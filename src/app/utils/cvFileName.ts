// Download names like "cv-max-mustermann-en.pdf" (same rules as api/CvApi/Pdf/PdfFileName.cs – keep in sync).
const transliterations: Record<string, string> = { ä: 'ae', ö: 'oe', ü: 'ue', ß: 'ss', Ä: 'ae', Ö: 'oe', Ü: 'ue' }

export function slug(text?: string | null): string {
  if (!text?.trim()) return ''
  return text.trim()
    .replace(/[äöüßÄÖÜ]/g, c => transliterations[c] ?? c)
    .normalize('NFD').replace(/\p{M}/gu, '')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
}

/** "cv-<name>-<locale>.pdf", or "cv-<locale>.pdf" when the name is hidden. */
export function cvPdfFileName(name: string | null | undefined, locale: string): string {
  const s = slug(name)
  return s ? `cv-${s}-${locale}.pdf` : `cv-${locale}.pdf`
}
