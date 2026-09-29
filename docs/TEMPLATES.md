# Templates

A CV's content (JSON, see [requirements](REQUIREMENTS_ACCESS_AND_TENANCY.md)) is separate from its
design. Each output has **templates**; each person (tenant) chooses one, and profiles or single invites
can override the choice.

| Output | Status | Templates |
|---|---|---|
| PDF / print | ✅ implemented (#76, #85) | `editorial` (default), `classic`, `banner` |
| HTML (web) | 📝 planned (#77) | `default` (today's page) |

---

## Selection

The API resolves the template for every visitor; the most specific level wins:

| # | Level | Where | Notes |
|---|---|---|---|
| 1 | Admin preview | `GET /api/admin/tenants/{t}/pdf-preview?template=…` | only for previews |
| 2 | Invite | invite `overrides.templates` | **allowed by default**; a tenant can forbid it with `allowInviteTemplateOverride: false` |
| 3 | Profile | `profiles.<name>.templates` in `tenant.json` | e.g. a plain template for recruiters |
| 4 | Tenant | `templates` in `tenant.json` | the person's global choice |
| 5 | Default | frontend registry | `editorial` for PDF |

```jsonc
// tenant.json
{
  "templates": { "pdf": "editorial" },          // global choice of this person
  "allowInviteTemplateOverride": true,           // default
  "profiles": {
    "recruiter": { "grants": ["contact"], "templates": { "pdf": "classic" } }
  }
}
```

```bash
# invite with its own template
curl -X POST -H "X-Admin-Key: $KEY" -H "Content-Type: application/json" \
  -d '{ "profile": "full", "label": "ACME", "overrides": { "templates": { "pdf": "classic" } } }' \
  $API/admin/tenants/bob/invites

# compare templates with your own CV (PDF, not cached)
curl -H "X-Admin-Key: $KEY" -o classic.pdf \
  "$API/admin/tenants/bob/pdf-preview?profile=full&template=classic&locale=en"
```

- Template names match `^[a-z0-9][a-z0-9-]{0,31}$`; invalid names are ignored by the API.
- Unknown (but valid) names fall back to the default in the frontend.
- `/api/cv` returns the resolved names: `templates: { pdf, html }` (null = default).
- The PDF cache key contains the template, so changing a template at any level re-renders the PDFs.

---

## PDF / print templates

| Name | Look | Good for |
|---|---|---|
| `editorial` | Typeset two-column A4: serif name, photo, labelled contact grid, sidebar (skills, languages, licences, QR code), main column with date gutter | the default, design-conscious applications |
| `classic` | Single column, black and white, no photo, dates right-aligned, skills and languages as text | recruiters and applicant tracking systems (ATS), plain printing |
| `banner` | Full bleed: dark header band (first name light, last name with bold initial, degrees, letter-spaced role), round photo overlapping header and sidebar, full-height coloured sidebar on every page (contact, personal data, two-column skill lists, languages, QR), headings with an accent initial, timeline | a personal, designed look |

### Template variables

Templates can declare **variables** – colours, toggles, palettes, choices – with defaults and named
**presets** (colour sets). The owner picks them per tenant / profile / invite with `pdfVars`, merged per key
(invite > profile > tenant; invites only if template overrides are allowed):

```jsonc
// tenant.json
"templates": {
  "pdf": "banner",
  "pdfVars": { "preset": "graphite", "accent": "#e0a526", "chapterColors": true }
}
```

Resolution in the frontend: template defaults → preset → explicit values. Only variables the template
declares, with type-valid values (colours as hex), are applied; the API additionally limits keys and values.

| Template | Variables | Presets |
|---|---|---|
| `editorial` | `accent` | – |
| `classic` | – | – |
| `banner` | `accent`, `sidebar`, `sidebarText`, `band` (colours), `chapterColors` (bool, default off: one accent for all chapters), `chapterPalette` (5 colours used when `chapterColors` is on), `degreesStyle` (`accent` \| `gradient` \| `plain`) | `navy` (default look), `graphite`, `forest`, `burgundy`, `light` |

Preview any combination:

```bash
curl -H "X-Admin-Key: $KEY" -o preview.pdf -G \
  --data-urlencode 'vars={"preset":"light","chapterColors":true}' \
  "$API/admin/tenants/bob/pdf-preview?profile=full&template=banner"
```

The schemas live in `src/app/utils/printTemplates.ts` (types in `utils/templateVars.ts`).

### Template builder (admin "Design" tab)

`/admin` → tenant → **Design** renders these schemas as a form with a live PDF preview:

1. **Applies to**: the tenant default or one profile (invites keep their own overrides).
2. **PDF template**: pick one, or "Not set here" to inherit (profile → tenant → system default).
3. **Colour set**: the template's presets; **Variables**: colour pickers with suggestions, switches,
   palettes and choices generated from the schema. "Reset to defaults" returns to the template defaults.
4. The preview (any profile and locale) re-renders about 0.7 s after the last change (`…/pdf-preview`).
5. **Save to tenant.json** writes only the `templates` object of that scope; comments and formatting in
   the rest of the file are kept. Only the preset and values that differ from it are stored, so template
   defaults can evolve. "Copy JSON" gives the same snippet for the CV repository.

If the tenant's files are deployed from Git (`cv-sync.sh`), the next sync overwrites a choice saved in
the UI: copy the snippet into the repository as well.

### Adding a PDF template

1. Create `src/app/components/print/Print<Name>.vue`.
   - Get all data from `usePrintData()`: redacted CV sections (sorted newest first), `contact`,
     `period()` for dates, `label()` for texts, `qrDataUrl`.
   - Wrap everything in `<article class="cv-print">` and show it only in print
     (`.cv-print { display: none }` + `@media print { .cv-print { display: block } }`).
   - Use pt/mm units, `break-inside: avoid` for entries and `break-after: avoid` for headings.
   - Show only fields that exist: every field can be hidden by redaction.
   - Include the notice `label('notice')`, a link to the online version (`qrUrl`, shown as `onlineHost`)
     and the credit `label('createdWith')` + `platformUrl`/`platformHost` (see existing templates).
2. Register it in `src/app/utils/printTemplates.ts` (name, title, description, component, optional `vars`
   schema with defaults and presets). The component receives the resolved values as `vars` prop.
3. Add labels to `src/app/utils/printLabels.ts` if needed (EN + DE).
4. Check it: `pdf-preview` with `template=<name>` for a full and a heavily redacted profile.
5. After changing an existing template, bump `CV_PDF_LAYOUT_VERSION` so cached PDFs are re-rendered.

The page size and margins (`@page`, A4) and the running footer (name, page x / y) are shared by all
templates.

---

## HTML (web) templates (planned, #77)

Goal: the same mechanism for the web view, so each person can pick a web design that matches their PDF.

### Plan

1. **Contract.** A web template is a component `src/app/components/web/Web<Name>.vue` that renders the
   whole CV page. It receives no props; data comes from composables (`useCv()`, and a new
   `useWebData()` analogous to `usePrintData()`).
2. **Default template.** Move today's `pages/index.vue` layout (hero, intro, sidebar, timeline, sections)
   into `WebDefault.vue`; `pages/index.vue` becomes a dispatcher like `CvPrint.vue`, using
   `templates.html` from `/api/cv` and a registry `utils/webTemplates.ts`.
3. **Shared building blocks.** Keep features independent of layout so every template can reuse them:
   timeline (`CvTimeline`), technology filter (`useTechFilter`), lightbox, PDF download button,
   language switcher, QR code, invite form. Templates may omit blocks, but not re-implement access logic.
4. **Not templated.** The showcase, the neutral no-access page and the print output keep their own
   components (print has its own template choice).
5. **Selection.** Identical to PDF (`templates.html`); already resolved and returned by the API today.
6. **Pairing (optional).** A template pair can be suggested (e.g. `html: minimal` ↔ `pdf: classic`) but is
   not enforced.
7. **Showcase.** Screenshots per web template (regenerated from the sample tenant, requirements §11).
8. **Tests.** Registry fallback, a smoke test per template with a full and a heavily redacted CV.
