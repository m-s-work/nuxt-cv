<script setup lang="ts">
// Print/PDF template "banner" – the owner's design:
// - dark header band across the page (name: first name light, last name bold; degrees; role)
// - round photo overlapping header and sidebar
// - full-height navy sidebar on every page (contact, personal data, skills, languages, licences, QR)
// - accent colour for heading initials and timeline dots; optionally one colour per chapter (chapterColors)
// - name: first name light; last name light with only its first letter bold
// - main column: entries with date on the right
import type { TemplateVarValue } from '~/utils/templateVars'
import type { PrintLinkMode } from '~/composables/usePrintLinks'

// Template variables (see utils/printTemplates.ts): colours, chapterColors, chapterPalette, degreesStyle.
const props = defineProps<{ vars?: Record<string, TemplateVarValue> }>()
const v = computed(() => props.vars ?? {})
const style = computed(() => {
  const palette = Array.isArray(v.value.chapterPalette) ? v.value.chapterPalette : []
  const css: Record<string, string> = {}
  for (const [cssVar, key] of [['--accent', 'accent'], ['--sidebar', 'sidebar'], ['--sidebar-text', 'sidebarText'], ['--band', 'band']] as const) {
    if (typeof v.value[key] === 'string') css[cssVar] = v.value[key] as string
  }
  palette.slice(0, 5).forEach((color, i) => { css[`--c${i + 1}`] = color })
  return css
})

// Full bleed (no side/top page margin, see useHead below); the bottom margin holds the running footer.
const {
  locale, label, getAssetPath, qrDataUrl, qrUrl, onlineHost, platformUrl, platformHost,
  profile, details, intro, photo, period,
  skills, liked, languages, licenses, experiences, studies, projects, otherEntries
} = usePrintData()

// Website links of entries (template variable "links", composables/usePrintLinks.ts).
const { link } = usePrintLinks(() => (props.vars?.links as PrintLinkMode | undefined) ?? 'qr')

// Full bleed: no side/top page margin while this template is used; the bottom margin keeps room for the
// running footer. Injected via useHead (after the global @page rule) instead of a named page, because a
// named page forces an extra page break before any trailing element outside it.
useHead({ style: [{ key: 'print-banner-page', textContent: '@page { size: A4; margin: 0 0 14mm 0; }' }] })

// The running footer lives in that bottom margin, outside the page content: tell the PDF renderer to
// continue the sidebar there (pdf/server.mjs). Width = --side-w below, height = the bottom margin above.
type FooterWindow = { __CV_PDF_FOOTER_SIDE__?: { width: string, height: string, background: string, color: string } }
watchEffect(() => {
  if (!import.meta.client) return
  ;(window as unknown as FooterWindow).__CV_PDF_FOOTER_SIDE__ = {
    width: '68mm',
    height: '14mm',
    background: typeof v.value.sidebar === 'string' ? v.value.sidebar : '#1f3864',
    color: typeof v.value.sidebarText === 'string' ? v.value.sidebarText : '#e8edf6'
  }
})
onBeforeUnmount(() => { delete (window as unknown as FooterWindow).__CV_PDF_FOOTER_SIDE__ })

const nameParts = computed(() => {
  const words = (profile.value.name ?? '').trim().split(/\s+/).filter(Boolean)
  const last = words.pop() ?? ''
  return { first: words.join(' '), last }
})

// Stand-in for the round photo when there is none or the profile hides it (hidePhoto): initials.
const initials = computed(() =>
  [nameParts.value.first.charAt(0), nameParts.value.last.charAt(0)].filter(Boolean).join('').toUpperCase())

const degrees = computed(() =>
  [profile.value.academicTitlePrefix, profile.value.academicTitleSuffix].filter(Boolean).join('  ·  '))

const contactLines = computed(() =>
  [details.value.email, details.value.phone, details.value.location].filter(Boolean) as string[])

const birthDate = computed(() => {
  const value = details.value.birthDate
  if (!value || !/^\d{4}-\d{2}-\d{2}$/.test(value)) return value
  return new Date(`${value}T00:00:00`).toLocaleDateString(locale.value === 'de' ? 'de-DE' : 'en-GB')
})
</script>

<template>
  <article
    class="cv-print banner"
    :class="{ 'chapter-colors': v.chapterColors === true, [`degrees-${v.degreesStyle ?? 'accent'}`]: true }"
    :lang="locale"
    :style="style"
  >
    <!-- Sidebar colour on every page (fixed elements repeat on each printed page) -->
    <div class="sidebar-bg" aria-hidden="true" />

    <header class="band">
      <div class="band-name">
        <h1 class="name">
          <span v-if="nameParts.first" class="first">{{ nameParts.first }}</span>
          <span class="last"><b>{{ nameParts.last.charAt(0) }}</b>{{ nameParts.last.slice(1) }}</span>
        </h1>
        <p v-if="degrees" class="degrees">{{ degrees }}</p>
        <p v-if="profile.title" class="role">{{ profile.title }}</p>
      </div>
    </header>

    <div class="body">
      <aside class="sidebar">
        <img v-if="photo" :src="getAssetPath(photo)" :alt="profile.name" class="photo">
        <div v-else-if="initials" class="photo monogram" aria-hidden="true">
          <span class="first">{{ initials.slice(0, -1) }}</span><b>{{ initials.slice(-1) }}</b>
        </div>
        <div v-else class="photo-spacer" />

        <section v-if="contactLines.length" class="side">
          <h2 class="side-heading">{{ label('sidebar.contact') }}</h2>
          <p v-for="line in contactLines" :key="line" class="line">{{ line }}</p>
        </section>

        <section v-if="birthDate || details.citizenship" class="side">
          <h2 class="side-heading">{{ label('sidebar.personal') }}</h2>
          <dl class="pairs">
            <template v-if="birthDate">
              <dt>{{ label('contact.born') }}</dt>
              <dd>{{ birthDate }}</dd>
            </template>
            <template v-if="details.citizenship">
              <dt>{{ label('contact.citizenship') }}</dt>
              <dd>{{ details.citizenship }}</dd>
            </template>
          </dl>
        </section>

        <section v-if="skills.length" class="side">
          <h2 class="side-heading">{{ label('skills') }}</h2>
          <ul class="bullets">
            <li v-for="skill in skills" :key="skill">{{ skill }}</li>
          </ul>
        </section>

        <section v-if="liked.length" class="side">
          <h2 class="side-heading">{{ label('interests') }}</h2>
          <ul class="bullets">
            <li v-for="skill in liked" :key="skill">{{ skill }}</li>
          </ul>
        </section>

        <section v-if="languages.length" class="side">
          <h2 class="side-heading">{{ label('languages') }}</h2>
          <p v-for="language in languages" :key="language.code" class="line">
            {{ language.name }}: <span class="soft">{{ language.level }}</span>
          </p>
        </section>

        <section v-if="licenses.length" class="side">
          <h2 class="side-heading">{{ label('licenses') }}</h2>
          <p class="line">{{ licenses.map(l => l.type).join(', ') }}</p>
        </section>

        <section v-if="qrDataUrl" class="side qr">
          <img :src="qrDataUrl" :alt="label('online')" class="qr-image">
          <p class="qr-caption">{{ label('online') }}</p>
          <a v-if="onlineHost" :href="qrUrl" class="qr-link">{{ onlineHost }}</a>
        </section>
      </aside>

      <main class="main">
        <section v-if="intro?.text || intro?.yearsOfExperience" class="section">
          <h2 class="heading">{{ label('profile') }}</h2>
          <p v-if="intro?.text" class="summary">{{ intro.text }}</p>
          <p v-if="intro?.yearsOfExperience" class="facts">
            {{ label('years', { years: intro.yearsOfExperience }) }}<template v-if="intro.programmingSince"> · {{ label('since', { year: intro.programmingSince }) }}</template>
          </p>
        </section>

        <section v-if="experiences.length" class="section">
          <h2 class="heading">{{ label('experience') }}</h2>
          <div class="timeline">
            <div v-for="exp in experiences" :key="`exp-${exp.id}`" class="entry">
              <p class="entry-head">
                <strong>{{ exp.position }}</strong>
                <span class="date">{{ period(exp.period) }}</span>
              </p>
              <p v-if="exp.company" class="org">{{ exp.company }}</p>
              <p v-if="exp.description" class="text">{{ exp.description }}</p>
              <p v-if="exp.technologies?.length" class="tech">{{ exp.technologies.join(' · ') }}</p>
              <PrintEntryLink :link="link(exp)" />
            </div>
          </div>
        </section>

        <section v-if="studies.length" class="section">
          <h2 class="heading">{{ label('education') }}</h2>
          <div class="timeline">
            <div v-for="study in studies" :key="`study-${study.id}`" class="entry">
              <p class="entry-head">
                <strong>{{ study.degree }}</strong>
                <span class="date">{{ period(study.period) }}</span>
              </p>
              <p v-if="study.institution" class="org">{{ study.institution }}</p>
              <p v-if="study.focus" class="text">{{ study.focus }}</p>
              <PrintEntryLink :link="link(study)" />
            </div>
          </div>
        </section>

        <section v-if="projects.length" class="section">
          <h2 class="heading">{{ label('projects') }}</h2>
          <div class="timeline">
            <div v-for="project in projects" :key="`project-${project.id}`" class="entry">
              <p class="entry-head">
                <strong>{{ project.name }}</strong>
                <span class="date">{{ period(project.period) }}</span>
              </p>
              <p v-if="project.type || project.client" class="org">{{ [project.type, project.client].filter(Boolean).join(' · ') }}</p>
              <p v-if="project.description" class="text">{{ project.description }}</p>
              <p v-if="project.technologies?.length" class="tech">{{ project.technologies.join(' · ') }}</p>
              <PrintEntryLink :link="link(project)" />
            </div>
          </div>
        </section>

        <section v-if="otherEntries.length" class="section">
          <h2 class="heading">{{ label('other') }}</h2>
          <template v-for="entry in otherEntries" :key="`other-${entry.id}`">
            <p class="entry-head compact">
              <span><strong>{{ entry.title }}</strong><span v-if="entry.institution" class="ctx"> ({{ entry.institution }})</span></span>
              <span class="date">{{ entry.showPeriod === false ? '' : period(entry.period) }}</span>
            </p>
            <PrintEntryLink :link="link(entry)" />
          </template>
        </section>

        <p class="notice">
          <span>{{ label('notice') }}</span>
          <span v-if="platformHost">{{ label('createdWith') }} <a :href="platformUrl">{{ platformHost }}</a></span>
        </p>
      </main>
    </div>
  </article>
</template>

<style scoped>
.cv-print { display: none; }

@media print {
  .cv-print {
    display: block;
    --band: #3b3b3d;
    --sidebar: #1f3864;
    --sidebar-text: #e8edf6;
    --accent: #29a8e0;
    /* Chapter colours (same hues as the degree gradient), assigned in order of the rendered sections */
    --c1: #29a8e0;
    --c2: #e8639a;
    --c3: #f0b429;
    --c4: #3fb68b;
    --c5: #8b6cd9;
    --ink: #1d1f23;
    --muted: #666b73;
    --rule: #d3d7de;
    --side-w: 68mm;
    color: var(--ink);
    font-family: 'Lato', 'Inter Variable', system-ui, sans-serif;
    font-size: 9.5pt;
    line-height: 1.42;
  }

  .sidebar-bg {
    position: fixed;
    top: 0;
    bottom: 0;
    left: 0;
    width: var(--side-w);
    background: var(--sidebar);
    z-index: -1;
  }

  /* ---------- Header band ---------- */
  .band {
    position: relative;
    height: 46mm;
    background: var(--band);
    color: #fff;
    display: grid;
    grid-template-columns: var(--side-w) 1fr;
    align-items: center;
  }
  .band-name {
    grid-column: 2;
    padding: 0 14mm 0 10mm;
  }
  .name {
    font-size: 27pt;
    line-height: 1.05;
    letter-spacing: 0.01em;
  }
  .name .first { font-weight: 300; margin-right: 0.25em; }
  .name .last { font-weight: 300; }
  .name .last b { font-weight: 700; }
  .degrees {
    margin-top: 1.5mm;
    font-size: 13pt;
    font-weight: 400;
    letter-spacing: 0.18em;
    color: var(--accent);
  }
  .degrees-plain .degrees { color: rgba(255, 255, 255, 0.85); }
  .degrees-gradient .degrees {
    background: linear-gradient(90deg, var(--c3), var(--c2), var(--c1));
    -webkit-background-clip: text;
    background-clip: text;
    color: transparent;
    display: inline-block;
  }
  .role {
    margin-top: 3mm;
    padding-top: 2.5mm;
    border-top: 0.6pt solid rgba(255, 255, 255, 0.7);
    font-size: 10.5pt;
    font-weight: 300;
    letter-spacing: 0.3em;
    text-transform: uppercase;
  }

  /* ---------- Body ---------- */
  .body {
    display: grid;
    grid-template-columns: var(--side-w) 1fr;
  }

  .sidebar {
    --sidebar-soft: color-mix(in srgb, var(--sidebar-text) 72%, transparent);
    --sidebar-rule: color-mix(in srgb, var(--sidebar-text) 35%, transparent);
    color: var(--sidebar-text);
    padding: 8mm 8mm 6mm 9mm;
    box-decoration-break: clone;
    -webkit-box-decoration-break: clone;
  }
  .photo, .photo-spacer {
    display: block;
    width: 44mm;
    height: 44mm;
    margin: -38mm auto 0;
    position: relative;
    z-index: 1;
  }
  .photo {
    border-radius: 50%;
    object-fit: cover;
    border: 1.2mm solid var(--band);
    box-shadow: 0 0 0 0.5mm rgba(255, 255, 255, 0.85);
  }
  .monogram {
    display: flex;
    align-items: center;
    justify-content: center;
    background: var(--sidebar);
    color: var(--sidebar-text);
    font-size: 30pt;
    font-weight: 300;
    letter-spacing: 0.04em;
  }
  .monogram b { font-weight: 700; color: var(--accent); }
  .photo-spacer { height: 0; margin-top: 0; }

  .side { break-inside: avoid; margin-top: 7mm; --chapter: var(--accent); }
  .chapter-colors .side:nth-of-type(5n + 1) { --chapter: var(--c1); }
  .chapter-colors .side:nth-of-type(5n + 2) { --chapter: var(--c2); }
  .chapter-colors .side:nth-of-type(5n + 3) { --chapter: var(--c3); }
  .chapter-colors .side:nth-of-type(5n + 4) { --chapter: var(--c4); }
  .chapter-colors .side:nth-of-type(5n + 5) { --chapter: var(--c5); }
  .side-heading {
    font-size: 10.5pt;
    font-weight: 700;
    letter-spacing: 0.12em;
    text-transform: uppercase;
    color: var(--sidebar-text);
    padding-bottom: 1.5mm;
    margin-bottom: 2.5mm;
    border-bottom: 0.5pt solid var(--sidebar-rule);
  }
  .side-heading::first-letter { color: var(--chapter); font-size: 1.3em; }
  .line { font-size: 8.75pt; overflow-wrap: anywhere; }
  .line + .line { margin-top: 0.8mm; }
  .soft { color: var(--sidebar-soft); }
  .pairs { font-size: 8.75pt; }
  .pairs dt { color: var(--sidebar-soft); font-size: 7.5pt; letter-spacing: 0.06em; text-transform: uppercase; }
  .pairs dd + dt { margin-top: 1.5mm; }
  .bullets {
    list-style: disc;
    padding-left: 4.5mm;
    font-size: 8.75pt;
    columns: 2;
    column-gap: 6mm;
  }
  .bullets li { break-inside: avoid; }
  .bullets li { margin-bottom: 0.6mm; }
  .bullets li::marker { color: var(--chapter); }

  .qr-image {
    width: 24mm;
    height: 24mm;
    padding: 1.5mm;
    background: #fff;
    border-radius: 1mm;
    image-rendering: pixelated;
  }
  .qr-caption { margin-top: 1.5mm; font-size: 7.5pt; color: var(--sidebar-soft); }
  .qr-link {
    font-size: 8pt;
    font-weight: 700;
    color: var(--sidebar-text);
    text-decoration: none;
  }

  .main {
    padding: 9mm 14mm 0 10mm;
    box-decoration-break: clone;
    -webkit-box-decoration-break: clone;
  }
  .section { --chapter: var(--accent); }
  .chapter-colors .section:nth-of-type(5n + 1) { --chapter: var(--c1); }
  .chapter-colors .section:nth-of-type(5n + 2) { --chapter: var(--c2); }
  .chapter-colors .section:nth-of-type(5n + 3) { --chapter: var(--c3); }
  .chapter-colors .section:nth-of-type(5n + 4) { --chapter: var(--c4); }
  .chapter-colors .section:nth-of-type(5n + 5) { --chapter: var(--c5); }
  .section + .section { margin-top: 7mm; }
  .heading {
    font-size: 17pt;
    font-weight: 400;
    line-height: 1.1;
    padding-bottom: 1.5mm;
    margin-bottom: 3.5mm;
    border-bottom: 0.5pt solid var(--rule);
    break-after: avoid;
  }
  .heading::first-letter { color: var(--chapter); font-weight: 700; }

  .summary { font-size: 9.75pt; line-height: 1.5; }
  .facts {
    margin-top: 1.5mm;
    font-size: 8pt;
    font-weight: 700;
    letter-spacing: 0.08em;
    text-transform: uppercase;
    color: var(--muted);
  }

  .timeline {
    border-left: 0.6pt solid var(--rule);
    padding-left: 5mm;
    margin-left: 1mm;
  }
  .entry {
    position: relative;
    break-inside: avoid;
  }
  .entry + .entry { margin-top: 4.5mm; }
  .entry::before {
    content: '';
    position: absolute;
    left: calc(-5mm - 1.3mm);
    top: 1.3mm;
    width: 2mm;
    height: 2mm;
    border-radius: 50%;
    background: var(--chapter);
    box-shadow: 0 0 0 0.6mm #fff;
  }
  .entry-head {
    display: flex;
    justify-content: space-between;
    align-items: baseline;
    gap: 5mm;
    font-size: 10.5pt;
  }
  .entry-head.compact { font-size: 9.5pt; }
  .entry-head.compact + .entry-head.compact { margin-top: 1.5mm; }
  .ctx { font-size: 8.5pt; color: var(--muted); font-weight: 400; }
  .org { font-size: 8.75pt; color: var(--muted); font-style: italic; }
  .date {
    font-size: 8.75pt;
    font-weight: 700;
    white-space: nowrap;
  }
  .text { margin-top: 1mm; hyphens: auto; }
  .tech { margin-top: 1mm; font-size: 8pt; color: var(--muted); }

  .notice {
    display: flex;
    justify-content: space-between;
    gap: 6mm;
    margin-top: 9mm;
    padding-top: 2mm;
    border-top: 0.5pt solid var(--rule);
    font-size: 7pt;
    color: var(--muted);
    break-inside: avoid;
  }
  .notice a { color: var(--accent); text-decoration: none; }
}
</style>
