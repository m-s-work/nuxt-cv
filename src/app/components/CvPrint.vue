<script setup lang="ts">
// Dedicated print / PDF layout (A4). Only visible in print media; the screen layout is hidden there.
// Designed like a typeset document: fixed type scale in pt, a date gutter, a sidebar column and
// controlled page breaks. The running footer (name + page numbers) is added by the PDF renderer.
const { t, locale } = useI18n()
const { cv } = useCv()
const { getAssetPath } = useAssetPath()
const { dataUrl: qrDataUrl } = useCvQrCode(400)

const profile = computed(() => cv.value?.profile ?? {})
const details = computed(() => cv.value?.details ?? {})
const intro = computed(() => cv.value?.intro)
const photo = computed(() => profile.value.photoUrlLarge ?? profile.value.photoUrl)

const birthDate = computed(() => {
  const value = details.value.birthDate
  if (!value || !/^\d{4}-\d{2}-\d{2}$/.test(value)) return value
  return new Date(`${value}T00:00:00`).toLocaleDateString(locale.value === 'de' ? 'de-DE' : 'en-GB')
})

const contact = computed(() => ([
  { key: 'location', value: details.value.location },
  { key: 'email', value: details.value.email },
  { key: 'phone', value: details.value.phone },
  { key: 'citizenship', value: details.value.citizenship },
  { key: 'born', value: birthDate.value }
]).filter((item): item is { key: string, value: string } => !!item.value))

// Typographic dash between dates ("2017 – 2020").
const period = (value?: string) => value?.replace(' - ', '\u2009–\u2009') ?? ''

const skills = computed(() => cv.value?.skills?.skilled ?? [])
const liked = computed(() => cv.value?.skills?.liked ?? [])
const languages = computed(() => cv.value?.languages ?? [])
const licenses = computed(() => cv.value?.drivingLicenses ?? [])

// Newest first, independent of the order in the JSON.
const byStart = <T extends { startDate: string }>(items?: T[]) =>
  [...(items ?? [])].sort((a, b) => b.startDate.localeCompare(a.startDate))

const experiences = computed(() => byStart(cv.value?.experiences))
const studies = computed(() => byStart(cv.value?.studies))
const projects = computed(() => byStart(cv.value?.projects))
const otherEntries = computed(() => byStart(cv.value?.otherEntries))

// Footer text for the PDF renderer (pdf/server.mjs reads it for the running footer).
watchEffect(() => {
  if (import.meta.client && profile.value.name) {
    (window as unknown as { __CV_PDF_FOOTER__?: string }).__CV_PDF_FOOTER__ = `${profile.value.name} · ${t('print.cv')}`
  }
})
</script>

<template>
  <article class="cv-print" :lang="locale">
    <!-- Masthead -->
    <header class="masthead">
      <div class="masthead-text">
        <h1 class="name">
          <span v-if="profile.academicTitlePrefix" class="academic">{{ profile.academicTitlePrefix }}&nbsp;</span>{{ profile.name }}<span v-if="profile.academicTitleSuffix" class="academic">, {{ profile.academicTitleSuffix }}</span>
        </h1>
        <p v-if="profile.title" class="role">{{ profile.title }}</p>
        <dl v-if="contact.length" class="contact">
          <div v-for="item in contact" :key="item.key" class="contact-item">
            <dt>{{ t(`print.contact.${item.key}`) }}</dt>
            <dd>{{ item.value }}</dd>
          </div>
        </dl>
      </div>
      <img v-if="photo" :src="getAssetPath(photo)" :alt="profile.name" class="photo">
    </header>

    <section v-if="intro?.text || intro?.yearsOfExperience" class="intro">
      <p v-if="intro?.text" class="intro-text">{{ intro.text }}</p>
      <p v-if="intro?.yearsOfExperience" class="intro-facts">
        {{ t('print.years', { years: intro.yearsOfExperience }) }}<template v-if="intro.programmingSince"> · {{ t('print.since', { year: intro.programmingSince }) }}</template>
      </p>
    </section>

    <div class="body">
      <!-- Sidebar column -->
      <aside class="sidebar">
        <section v-if="skills.length" class="side-section">
          <h2 class="side-heading">{{ t('print.skills') }}</h2>
          <ul class="tags">
            <li v-for="skill in skills" :key="skill">{{ skill }}</li>
          </ul>
        </section>

        <section v-if="liked.length" class="side-section">
          <h2 class="side-heading">{{ t('print.interests') }}</h2>
          <ul class="tags tags-light">
            <li v-for="skill in liked" :key="skill">{{ skill }}</li>
          </ul>
        </section>

        <section v-if="languages.length" class="side-section">
          <h2 class="side-heading">{{ t('print.languages') }}</h2>
          <dl class="pairs">
            <template v-for="language in languages" :key="language.code">
              <dt>{{ language.name }}</dt>
              <dd>{{ language.level }}</dd>
            </template>
          </dl>
        </section>

        <section v-if="licenses.length" class="side-section">
          <h2 class="side-heading">{{ t('print.licenses') }}</h2>
          <dl class="pairs">
            <template v-for="license in licenses" :key="license.type">
              <dt>{{ license.type }}</dt>
              <dd>{{ license.description }}</dd>
            </template>
          </dl>
        </section>

        <section v-if="qrDataUrl" class="side-section qr">
          <img :src="qrDataUrl" :alt="t('print.online')" class="qr-image">
          <p class="qr-caption">{{ t('print.online') }}</p>
        </section>
      </aside>

      <!-- Main column -->
      <main class="main">
        <section v-if="experiences.length" class="main-section">
          <h2 class="main-heading">{{ t('print.experience') }}</h2>
          <div v-for="exp in experiences" :key="`exp-${exp.id}`" class="entry">
            <p class="entry-date">{{ period(exp.period) }}</p>
            <div class="entry-body">
              <h3 class="entry-title">{{ exp.position }}</h3>
              <p v-if="exp.company" class="entry-org">{{ exp.company }}</p>
              <p v-if="exp.description" class="entry-text">{{ exp.description }}</p>
              <p v-if="exp.technologies?.length" class="entry-tech">{{ exp.technologies.join(' · ') }}</p>
            </div>
          </div>
        </section>

        <section v-if="studies.length" class="main-section">
          <h2 class="main-heading">{{ t('print.education') }}</h2>
          <div v-for="study in studies" :key="`study-${study.id}`" class="entry">
            <p class="entry-date">{{ period(study.period) }}</p>
            <div class="entry-body">
              <h3 class="entry-title">{{ study.degree }}</h3>
              <p v-if="study.institution" class="entry-org">{{ study.institution }}</p>
              <p v-if="study.focus" class="entry-text">{{ study.focus }}</p>
              <p v-if="study.technologies?.length" class="entry-tech">{{ study.technologies.join(' · ') }}</p>
            </div>
          </div>
        </section>

        <section v-if="projects.length" class="main-section">
          <h2 class="main-heading">{{ t('print.projects') }}</h2>
          <div v-for="project in projects" :key="`project-${project.id}`" class="entry">
            <p class="entry-date">{{ period(project.period) }}</p>
            <div class="entry-body">
              <h3 class="entry-title">{{ project.name }}</h3>
              <p v-if="project.type || project.client" class="entry-org">{{ [project.type, project.client].filter(Boolean).join(' · ') }}</p>
              <p v-if="project.description" class="entry-text">{{ project.description }}</p>
              <p v-if="project.technologies?.length" class="entry-tech">{{ project.technologies.join(' · ') }}</p>
            </div>
          </div>
        </section>

        <section v-if="otherEntries.length" class="main-section">
          <h2 class="main-heading">{{ t('print.other') }}</h2>
          <div v-for="entry in otherEntries" :key="`other-${entry.id}`" class="entry entry-compact">
            <p class="entry-date">{{ entry.showPeriod === false ? '' : period(entry.period) }}</p>
            <div class="entry-body">
              <h3 class="entry-title">{{ entry.title }}</h3>
              <p v-if="entry.institution" class="entry-org">{{ entry.institution }}</p>
            </div>
          </div>
        </section>
      </main>
    </div>

    <p class="notice">{{ t('print.notice') }}</p>
  </article>
</template>

<style scoped>
/* Print only */
.cv-print { display: none; }

@media print {
  .cv-print {
    display: block;
    --ink: #111827;
    --muted: #5b6472;
    --faint: #9aa1ad;
    --accent: #1d4ed8;
    --rule: #d6dae1;
    color: var(--ink);
    font-family: 'Inter Variable', 'Inter', system-ui, sans-serif;
    font-size: 9.25pt;
    line-height: 1.45;
    font-feature-settings: 'tnum' 1, 'cv11' 1;
    -webkit-font-smoothing: antialiased;
  }

  /* ---------- Masthead ---------- */
  .masthead {
    display: grid;
    grid-template-columns: 1fr auto;
    gap: 8mm;
    align-items: end;
    padding-bottom: 5mm;
    border-bottom: 1.25pt solid var(--ink);
  }
  .name {
    font-family: 'Source Serif 4 Variable', 'Source Serif 4', Georgia, serif;
    font-size: 30pt;
    font-weight: 600;
    line-height: 1.05;
    letter-spacing: -0.015em;
  }
  .academic {
    font-weight: 400;
    font-size: 17pt;
    color: var(--muted);
    letter-spacing: 0;
  }
  .role {
    margin-top: 2.5mm;
    font-size: 9.5pt;
    font-weight: 600;
    letter-spacing: 0.18em;
    text-transform: uppercase;
    color: var(--accent);
  }
  .contact {
    display: grid;
    grid-template-columns: repeat(3, auto);
    justify-content: start;
    column-gap: 8mm;
    row-gap: 2mm;
    margin-top: 5mm;
  }
  .contact dt {
    font-size: 6.5pt;
    font-weight: 700;
    letter-spacing: 0.14em;
    text-transform: uppercase;
    color: var(--faint);
  }
  .contact dd {
    font-size: 8.5pt;
    color: var(--ink);
  }
  .photo {
    width: 30mm;
    height: 37mm;
    object-fit: cover;
    border-radius: 1.5mm;
  }

  /* ---------- Intro ---------- */
  .intro {
    padding: 4mm 0;
    border-bottom: 0.5pt solid var(--rule);
  }
  .intro-text {
    font-family: 'Source Serif 4 Variable', 'Source Serif 4', Georgia, serif;
    font-size: 11pt;
    line-height: 1.5;
    max-width: 150mm;
  }
  .intro-facts {
    margin-top: 1.5mm;
    font-size: 8.25pt;
    font-weight: 600;
    letter-spacing: 0.06em;
    text-transform: uppercase;
    color: var(--muted);
  }

  /* ---------- Two columns ---------- */
  .body {
    display: grid;
    grid-template-columns: 50mm 1fr;
    gap: 9mm;
    margin-top: 6mm;
  }

  /* Sidebar */
  .side-section { break-inside: avoid; }
  .side-section + .side-section { margin-top: 6mm; }
  .side-heading,
  .main-heading {
    font-size: 7.5pt;
    font-weight: 700;
    letter-spacing: 0.16em;
    text-transform: uppercase;
    color: var(--accent);
    padding-bottom: 1.5mm;
    margin-bottom: 2.5mm;
    border-bottom: 0.5pt solid var(--rule);
    break-after: avoid;
  }
  .tags {
    display: flex;
    flex-wrap: wrap;
    gap: 1.2mm;
  }
  .tags li {
    font-size: 7.75pt;
    line-height: 1;
    padding: 1.1mm 1.8mm;
    border: 0.5pt solid var(--ink);
    border-radius: 1mm;
  }
  .tags-light li {
    border-color: var(--faint);
    color: var(--muted);
  }
  .pairs {
    display: grid;
    grid-template-columns: auto 1fr;
    column-gap: 3mm;
    row-gap: 1.2mm;
    font-size: 8.5pt;
  }
  .pairs dt { font-weight: 600; }
  .pairs dd { color: var(--muted); text-align: right; }
  .qr { text-align: left; }
  .qr-image {
    width: 24mm;
    height: 24mm;
    image-rendering: pixelated;
  }
  .qr-caption {
    margin-top: 1.5mm;
    max-width: 30mm;
    line-height: 1.3;
    font-size: 7pt;
    color: var(--muted);
  }

  /* Main column */
  .main-section + .main-section { margin-top: 7mm; }
  .entry {
    display: grid;
    grid-template-columns: 27mm 1fr;
    gap: 3mm;
    break-inside: avoid;
  }
  .entry + .entry { margin-top: 4.5mm; }
  .entry-compact + .entry-compact { margin-top: 2.5mm; }
  .entry-date {
    padding-top: 0.6mm;
    font-size: 7.75pt;
    font-weight: 500;
    color: var(--muted);
    white-space: nowrap;
  }
  .entry-title {
    font-size: 10.5pt;
    font-weight: 650;
    line-height: 1.3;
  }
  .entry-org {
    font-size: 8.75pt;
    font-weight: 500;
    color: var(--accent);
  }
  .entry-text {
    margin-top: 1.2mm;
    hyphens: auto;
  }
  .entry-tech {
    margin-top: 1.2mm;
    font-size: 7.75pt;
    color: var(--muted);
  }

  .notice {
    margin-top: 8mm;
    padding-top: 2mm;
    border-top: 0.5pt solid var(--rule);
    font-size: 7pt;
    color: var(--muted);
    break-inside: avoid;
  }
}
</style>

<i18n lang="json">
{
  "en": {
    "print": {
      "cv": "Curriculum Vitae",
      "contact": { "location": "Location", "email": "E-mail", "phone": "Phone", "citizenship": "Citizenship", "born": "Date of birth" },
      "years": "{years} years of experience",
      "since": "programming since {year}",
      "skills": "Core skills",
      "interests": "Further skills & interests",
      "languages": "Languages",
      "licenses": "Driving licences",
      "online": "Scan for the online version",
      "experience": "Experience",
      "education": "Education",
      "projects": "Projects",
      "other": "Further stations",
      "notice": "Please do not use this CV with AI tools or systems."
    }
  },
  "de": {
    "print": {
      "cv": "Lebenslauf",
      "contact": { "location": "Wohnort", "email": "E-Mail", "phone": "Telefon", "citizenship": "Staatsangehörigkeit", "born": "Geburtsdatum" },
      "years": "{years} Jahre Erfahrung",
      "since": "programmiert seit {year}",
      "skills": "Kernkompetenzen",
      "interests": "Weitere Kenntnisse & Interessen",
      "languages": "Sprachen",
      "licenses": "Führerscheine",
      "online": "Scannen für die Online-Version",
      "experience": "Berufserfahrung",
      "education": "Ausbildung",
      "projects": "Projekte",
      "other": "Weitere Stationen",
      "notice": "Bitte verwenden Sie diesen Lebenslauf nicht mit KI-Tools oder -Systemen."
    }
  }
}
</i18n>
