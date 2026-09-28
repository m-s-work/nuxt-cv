<script setup lang="ts">
// Print/PDF template "classic": single column, black and white, no photo. Compact and easy for
// applicant tracking systems to parse (linear reading order, real text, no multi-column flow).
const {
  locale, label, qrDataUrl, profile, intro, contact, period,
  skills, liked, languages, licenses, experiences, studies, projects, otherEntries
} = usePrintData()

const fullName = computed(() => [
  profile.value.academicTitlePrefix,
  profile.value.name
].filter(Boolean).join(' ') + (profile.value.academicTitleSuffix ? `, ${profile.value.academicTitleSuffix}` : ''))
</script>

<template>
  <article class="cv-print" :lang="locale">
    <header class="head">
      <h1 class="name">{{ fullName }}</h1>
      <p v-if="profile.title" class="role">{{ profile.title }}</p>
      <p v-if="contact.length" class="contact">
        <span v-for="item in contact" :key="item.key">{{ item.value }}</span>
      </p>
    </header>

    <section v-if="intro?.text || intro?.yearsOfExperience" class="section">
      <h2 class="heading">{{ label('profile') }}</h2>
      <p v-if="intro?.text">{{ intro.text }}</p>
      <p v-if="intro?.yearsOfExperience" class="facts">
        {{ label('years', { years: intro.yearsOfExperience }) }}<template v-if="intro.programmingSince"> · {{ label('since', { year: intro.programmingSince }) }}</template>
      </p>
    </section>

    <section v-if="experiences.length" class="section">
      <h2 class="heading">{{ label('experience') }}</h2>
      <div v-for="exp in experiences" :key="`exp-${exp.id}`" class="entry">
        <p class="entry-line">
          <strong>{{ exp.position }}</strong>
          <span class="date">{{ period(exp.period) }}</span>
        </p>
        <p v-if="exp.company" class="org">{{ exp.company }}</p>
        <p v-if="exp.description">{{ exp.description }}</p>
        <p v-if="exp.technologies?.length" class="tech">{{ exp.technologies.join(', ') }}</p>
      </div>
    </section>

    <section v-if="studies.length" class="section">
      <h2 class="heading">{{ label('education') }}</h2>
      <div v-for="study in studies" :key="`study-${study.id}`" class="entry">
        <p class="entry-line">
          <strong>{{ study.degree }}</strong>
          <span class="date">{{ period(study.period) }}</span>
        </p>
        <p v-if="study.institution" class="org">{{ study.institution }}</p>
        <p v-if="study.focus">{{ study.focus }}</p>
      </div>
    </section>

    <section v-if="projects.length" class="section">
      <h2 class="heading">{{ label('projects') }}</h2>
      <div v-for="project in projects" :key="`project-${project.id}`" class="entry">
        <p class="entry-line">
          <strong>{{ project.name }}</strong>
          <span class="date">{{ period(project.period) }}</span>
        </p>
        <p v-if="project.type || project.client" class="org">{{ [project.type, project.client].filter(Boolean).join(' · ') }}</p>
        <p v-if="project.description">{{ project.description }}</p>
        <p v-if="project.technologies?.length" class="tech">{{ project.technologies.join(', ') }}</p>
      </div>
    </section>

    <section v-if="skills.length || liked.length || languages.length || licenses.length" class="section">
      <h2 class="heading">{{ label('skillsAndLanguages') }}</h2>
      <dl class="facts-list">
        <template v-if="skills.length">
          <dt>{{ label('skills') }}</dt>
          <dd>{{ skills.join(', ') }}</dd>
        </template>
        <template v-if="liked.length">
          <dt>{{ label('interests') }}</dt>
          <dd>{{ liked.join(', ') }}</dd>
        </template>
        <template v-if="languages.length">
          <dt>{{ label('languages') }}</dt>
          <dd>{{ languages.map(l => `${l.name} (${l.level})`).join(', ') }}</dd>
        </template>
        <template v-if="licenses.length">
          <dt>{{ label('licenses') }}</dt>
          <dd>{{ licenses.map(l => l.type).join(', ') }}</dd>
        </template>
      </dl>
    </section>

    <section v-if="otherEntries.length" class="section">
      <h2 class="heading">{{ label('other') }}</h2>
      <p v-for="entry in otherEntries" :key="`other-${entry.id}`" class="entry-line compact">
        <span><strong>{{ entry.title }}</strong><template v-if="entry.institution">, {{ entry.institution }}</template></span>
        <span class="date">{{ entry.showPeriod === false ? '' : period(entry.period) }}</span>
      </p>
    </section>

    <footer class="end">
      <div v-if="qrDataUrl" class="qr">
        <img :src="qrDataUrl" :alt="label('online')">
        <span>{{ label('online') }}</span>
      </div>
      <p class="notice">{{ label('notice') }}</p>
    </footer>
  </article>
</template>

<style scoped>
.cv-print { display: none; }

@media print {
  .cv-print {
    display: block;
    color: #000;
    font-family: 'Source Serif 4 Variable', 'Source Serif 4', Georgia, serif;
    font-size: 10pt;
    line-height: 1.4;
    font-feature-settings: 'onum' 1, 'kern' 1;
  }

  .head {
    text-align: center;
    padding-bottom: 4mm;
    border-bottom: 0.75pt solid #000;
  }
  .name {
    font-size: 22pt;
    font-weight: 600;
    letter-spacing: 0.02em;
  }
  .role {
    margin-top: 1mm;
    font-size: 11pt;
    font-style: italic;
  }
  .contact {
    margin-top: 2.5mm;
    font-size: 9pt;
  }
  .contact span + span::before {
    content: '·';
    margin: 0 2mm;
  }

  .section { margin-top: 6mm; }
  .heading {
    font-size: 11.5pt;
    font-weight: 700;
    font-variant: small-caps;
    letter-spacing: 0.12em;
    text-transform: lowercase;
    border-bottom: 0.5pt solid #000;
    padding-bottom: 0.8mm;
    margin-bottom: 2.5mm;
    break-after: avoid;
  }

  .entry { break-inside: avoid; }
  .entry + .entry { margin-top: 3.5mm; }
  .entry-line {
    display: flex;
    justify-content: space-between;
    gap: 6mm;
  }
  .entry-line.compact + .entry-line.compact { margin-top: 1mm; }
  .date {
    white-space: nowrap;
    font-feature-settings: 'lnum' 1, 'tnum' 1;
  }
  .org { font-style: italic; }
  .tech {
    margin-top: 0.8mm;
    font-size: 8.75pt;
    color: #333;
  }
  .facts { margin-top: 1mm; font-size: 9pt; color: #333; }

  .facts-list {
    display: grid;
    grid-template-columns: 38mm 1fr;
    row-gap: 1.5mm;
    break-inside: avoid;
  }
  .facts-list dt { font-weight: 600; }

  .end {
    margin-top: 8mm;
    display: flex;
    align-items: flex-end;
    justify-content: space-between;
    gap: 6mm;
    break-inside: avoid;
  }
  .qr {
    display: flex;
    align-items: center;
    gap: 3mm;
    font-size: 8pt;
  }
  .qr img {
    width: 18mm;
    height: 18mm;
    image-rendering: pixelated;
  }
  .notice {
    font-size: 7.5pt;
    color: #444;
    text-align: right;
  }
}
</style>
