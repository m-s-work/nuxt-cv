<script setup lang="ts">
import type { LightboxImage } from '~/composables/useLightbox'

// Landing page of the shared host (e.g. cv.velarix.space) for visitors without an invite.
// Screenshots show the sample tenant only (api/sample-data), never a real CV.
//
// Only PUBLIC features may be listed here. Analytics / tracking features (e.g. heatmap tracking,
// visitor statistics) must never be mentioned – see docs/REQUIREMENTS_ACCESS_AND_TENANCY.md §11.
const features = [
  { key: 'cvascode', icon: 'M10 20l4-16m4 4l4 4-4 4M6 16l-4-4 4-4' },
  { key: 'git', icon: 'M6 3v12m0 0a3 3 0 103 3m-3-3a3 3 0 013 3m0 0h3a6 6 0 006-6V9m0 0a3 3 0 10-3-3 3 3 0 003 3z' },
  { key: 'gating', icon: 'M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z' },
  { key: 'flags', icon: 'M13.875 18.825A10.05 10.05 0 0112 19c-4.478 0-8.268-2.943-9.543-7a9.97 9.97 0 011.563-3.029m5.858.908a3 3 0 114.243 4.243M9.878 9.878l4.242 4.242M9.88 9.88l-3.29-3.29m7.532 7.532l3.29 3.29M3 3l3.59 3.59m0 0A9.953 9.953 0 0112 5c4.478 0 8.268 2.943 9.543 7a10.025 10.025 0 01-4.132 5.411m0 0L21 21' },
  { key: 'invites', icon: 'M13.828 10.172a4 4 0 00-5.656 0l-4 4a4 4 0 105.656 5.656l1.102-1.101m-.758-4.899a4 4 0 005.656 0l4-4a4 4 0 00-5.656-5.656l-1.1 1.1' },
  { key: 'pdf', icon: 'M12 10v6m0 0l-3-3m3 3l3-3M6 20h12a2 2 0 002-2V8l-6-6H6a2 2 0 00-2 2v14a2 2 0 002 2z' },
  { key: 'tenants', icon: 'M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0z' },
  { key: 'i18n', icon: 'M3 5h12M9 3v2m1.048 9.5A18.022 18.022 0 016.412 9m6.088 9h7M11 21l5-10 5 10M12.751 5C11.783 10.77 8.07 15.61 3 18.129' },
  { key: 'timeline', icon: 'M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z' },
  { key: 'filter', icon: 'M3 4a1 1 0 011-1h16a1 1 0 011 1v2.586a1 1 0 01-.293.707l-6.414 6.414a1 1 0 00-.293.707V17l-4 4v-6.586a1 1 0 00-.293-.707L3.293 7.293A1 1 0 013 6.586V4z' },
  { key: 'responsive', icon: 'M12 18h.01M8 21h8a2 2 0 002-2V5a2 2 0 00-2-2H8a2 2 0 00-2 2v14a2 2 0 002 2z' },
  { key: 'selfhosted', icon: 'M5 12h14M5 12a2 2 0 01-2-2V6a2 2 0 012-2h14a2 2 0 012 2v4a2 2 0 01-2 2M5 12a2 2 0 00-2 2v4a2 2 0 002 2h14a2 2 0 002-2v-4a2 2 0 00-2-2m-2-4h.01M17 16h.01' }
] as const

const screenshots = [
  { src: '/showcase/cv-desktop.jpg', key: 'desktop' },
  { src: '/showcase/cv-german.jpg', key: 'german' },
  { src: '/showcase/cv-dark.jpg', key: 'dark' },
  { src: '/showcase/cv-mobile.jpg', key: 'mobile' },
  { src: '/showcase/pdf-recruiter-en.jpg', key: 'pdfRecruiter' },
  { src: '/showcase/pdf-full-de.jpg', key: 'pdfFull' }
] as const

const comparison = [
  { src: '/showcase/compare-full.jpg', key: 'full' },
  { src: '/showcase/compare-public.jpg', key: 'redacted' }
] as const

const steps = ['maintain', 'invite', 'open'] as const

const { t } = useI18n()
const { getAssetPath } = useAssetPath()
const demoCode = useRuntimeConfig().public.demoInviteCode as string
const demoLink = computed(() => demoCode ? `/?c=${encodeURIComponent(demoCode)}` : '')
const { open: openLightbox } = useLightbox()

useSeoMeta({
  title: () => t('showcase.metaTitle'),
  description: () => t('showcase.metaDescription')
})

function openImages(list: ReadonlyArray<{ src: string, key: string }>, prefix: string, index: number) {
  const images: LightboxImage[] = list.map(item => ({
    src: getAssetPath(item.src),
    alt: t(`showcase.${prefix}.${item.key}`)
  }))
  openLightbox(images, index, `showcase-${prefix}`)
}
</script>

<template>
  <div class="min-h-screen bg-white dark:bg-gray-900 text-gray-900 dark:text-white">
    <!-- Hero with invite entry -->
    <section class="bg-gradient-to-br from-blue-600 to-blue-800 text-white">
      <div class="mx-auto max-w-6xl px-4 py-16 lg:py-24 grid gap-10 lg:grid-cols-[1fr_22rem] items-center">
        <div class="space-y-5">
          <p class="text-sm font-semibold uppercase tracking-widest text-blue-200">
            {{ t('showcase.eyebrow') }}
          </p>
          <h1 class="text-4xl md:text-5xl font-bold leading-tight">
            {{ t('showcase.title') }}
          </h1>
          <p class="text-lg text-blue-100 max-w-2xl">
            {{ t('showcase.subtitle') }}
          </p>
          <div class="flex flex-wrap gap-3">
            <a v-if="demoLink" :href="demoLink" class="inline-block rounded-lg bg-white px-4 py-2 font-medium text-blue-700 hover:bg-blue-50">
              {{ t('showcase.tryDemo') }}
            </a>
            <a href="#showcase-features" class="inline-block rounded-lg bg-white/10 px-4 py-2 font-medium hover:bg-white/20">
              {{ t('showcase.seeFeatures') }}
            </a>
          </div>
        </div>

        <div class="rounded-xl bg-white p-6 text-gray-900 shadow-xl dark:bg-gray-800 dark:text-white">
          <h2 class="text-lg font-semibold mb-1">{{ t('showcase.inviteTitle') }}</h2>
          <p class="text-sm text-gray-600 dark:text-gray-400 mb-4">{{ t('showcase.inviteText') }}</p>
          <CvInviteForm />
        </div>
      </div>
    </section>

    <!-- Screenshots -->
    <section class="mx-auto max-w-6xl px-4 py-16">
      <h2 class="text-3xl font-bold mb-2">{{ t('showcase.screenshotsTitle') }}</h2>
      <p class="text-gray-600 dark:text-gray-400 mb-8">{{ t('showcase.screenshotsText') }}</p>
      <div class="grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
        <figure v-for="(shot, index) in screenshots" :key="shot.key" class="space-y-2">
          <button
            type="button"
            class="block w-full overflow-hidden rounded-lg border border-gray-200 shadow-sm transition hover:shadow-lg dark:border-gray-700"
            @click="openImages(screenshots, 'shots', index)"
          >
            <img
              :src="getAssetPath(shot.src)"
              :alt="t(`showcase.shots.${shot.key}`)"
              class="aspect-[16/10] w-full object-cover object-top"
              loading="lazy"
            >
          </button>
          <figcaption class="text-sm text-gray-600 dark:text-gray-400">{{ t(`showcase.shots.${shot.key}`) }}</figcaption>
        </figure>
      </div>
    </section>

    <!-- Same CV, different invites -->
    <section class="bg-gray-50 dark:bg-gray-800/50">
      <div class="mx-auto max-w-6xl px-4 py-16">
        <h2 class="text-3xl font-bold mb-2">{{ t('showcase.compareTitle') }}</h2>
        <p class="text-gray-600 dark:text-gray-400 mb-8 max-w-3xl">{{ t('showcase.compareText') }}</p>
        <div class="grid gap-6 md:grid-cols-2">
          <figure v-for="(shot, index) in comparison" :key="shot.key" class="space-y-2">
            <figcaption class="text-sm font-semibold">{{ t(`showcase.compare.${shot.key}`) }}</figcaption>
            <button
              type="button"
              class="block w-full overflow-hidden rounded-lg border border-gray-200 bg-white shadow-sm transition hover:shadow-lg dark:border-gray-700"
              @click="openImages(comparison, 'compare', index)"
            >
              <img :src="getAssetPath(shot.src)" :alt="t(`showcase.compare.${shot.key}`)" class="w-full" loading="lazy">
            </button>
          </figure>
        </div>
      </div>
    </section>

    <!-- Features (public only) -->
    <section id="showcase-features" class="mx-auto max-w-6xl px-4 py-16">
      <h2 class="text-3xl font-bold mb-8">{{ t('showcase.featuresTitle') }}</h2>
      <div class="grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
        <div v-for="feature in features" :key="feature.key" class="rounded-lg border border-gray-200 p-5 dark:border-gray-700">
          <svg class="mb-3 h-7 w-7 text-blue-600 dark:text-blue-400" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" :d="feature.icon" />
          </svg>
          <h3 class="font-semibold mb-1">{{ t(`showcase.features.${feature.key}.title`) }}</h3>
          <p class="text-sm text-gray-600 dark:text-gray-400">{{ t(`showcase.features.${feature.key}.text`) }}</p>
        </div>
      </div>
    </section>

    <!-- How it works -->
    <section class="bg-gray-50 dark:bg-gray-800/50">
      <div class="mx-auto max-w-6xl px-4 py-16">
        <h2 class="text-3xl font-bold mb-8">{{ t('showcase.stepsTitle') }}</h2>
        <ol class="grid gap-6 md:grid-cols-3">
          <li v-for="(step, index) in steps" :key="step" class="space-y-2">
            <span class="flex h-9 w-9 items-center justify-center rounded-full bg-blue-600 font-bold text-white">{{ index + 1 }}</span>
            <h3 class="font-semibold">{{ t(`showcase.steps.${step}.title`) }}</h3>
            <p class="text-sm text-gray-600 dark:text-gray-400">{{ t(`showcase.steps.${step}.text`) }}</p>
          </li>
        </ol>
      </div>
    </section>

    <footer class="mx-auto max-w-6xl px-4 py-8 text-sm text-gray-500 dark:text-gray-400">
      {{ t('showcase.footer') }}
    </footer>
  </div>
</template>

<i18n lang="json">
{
  "en": {
    "showcase": {
      "metaTitle": "Invite-only CVs",
      "metaDescription": "Share your CV with exactly the people you choose – and decide per invite what they see.",
      "eyebrow": "Invite-only CVs",
      "title": "One CV. Every recipient sees exactly what they should.",
      "subtitle": "CV as Code: maintain your CV once as JSON, versioned in Git, and share it through personal invite links. Each invite decides which details are visible – online and in its own PDF.",
      "seeFeatures": "See the features",
      "inviteTitle": "Got an invitation?",
      "inviteText": "Enter the code from your invite link to open the CV.",
      "screenshotsTitle": "What a CV looks like",
      "screenshotsText": "Screenshots of the demo CV (sample data) – online and as PDF.",
      "shots": {
        "desktop": "Profile sidebar and interactive timeline next to the entries",
        "german": "German version with project screenshots and logos",
        "dark": "Dark mode",
        "mobile": "Mobile layout",
        "pdfRecruiter": "PDF of a recruiter invite – company names replaced by neutral descriptions",
        "pdfFull": "German PDF of a full invite, with QR code to the online version"
      },
      "compareTitle": "Same CV, different invites",
      "compareText": "Both views come from the same JSON file. The anonymised invite replaces company names with neutral descriptions, reduces dates to years and hides confidential entries – the hidden data never reaches the browser.",
      "compare": {
        "full": "Full invite",
        "redacted": "Anonymised invite"
      },
      "featuresTitle": "Features",
      "features": {
        "cvascode": {
          "title": "CV as Code",
          "text": "Like Infrastructure as Code: the CV and who may see which part of it are plain JSON files – one source of truth, every view derived from it. Changes are live within seconds, no rebuild."
        },
        "git": {
          "title": "Versioned in Git",
          "text": "Keep the CV in a Git repository: full history, review via pull request, validation in CI, automatic deployment on push and rollback with a revert."
        },
        "gating": {
          "title": "Field-level gating",
          "text": "Any entry or single field can require a grant. Only invites with that grant receive it; everything else is removed server-side."
        },
        "flags": {
          "title": "Privacy switches",
          "text": "Hide company names behind neutral descriptions, reduce dates to month or year, hide photo, contact details, birth date or media."
        },
        "invites": {
          "title": "Personal invite links",
          "text": "One link per recipient, with optional expiry and usage limit. Revoking an invite takes effect immediately."
        },
        "pdf": {
          "title": "PDF per invite",
          "text": "Every invite gets its own PDF with exactly the visible parts – rendered when the invite is created and refreshed automatically when the CV changes. A4 print layout with QR code included."
        },
        "tenants": {
          "title": "Multiple people",
          "text": "Host several CVs: each on its own subdomain or on a shared address where the invite code decides whose CV opens."
        },
        "i18n": {
          "title": "Multilingual",
          "text": "Content and interface in English and German, switchable at any time."
        },
        "timeline": {
          "title": "Interactive timeline",
          "text": "Experiences, studies and projects on one timeline – overlapping periods included, linked with the entries."
        },
        "filter": {
          "title": "Technology filter",
          "text": "Click a technology to show only the experiences and projects that used it."
        },
        "responsive": {
          "title": "Responsive & dark mode",
          "text": "Works on phones, tablets and desktops, in light and dark mode – with project screenshots and logos in a built-in image viewer."
        },
        "selfhosted": {
          "title": "Self-hosted",
          "text": "Nuxt frontend and C# API as Docker containers, ready for Coolify."
        }
      },
      "stepsTitle": "How it works",
      "steps": {
        "maintain": {
          "title": "Maintain the CV as code",
          "text": "Write the complete CV once as JSON – ideally in a Git repository – and mark what is confidential. CI validates and deploys it."
        },
        "invite": {
          "title": "Create an invite",
          "text": "Pick a profile – e.g. recruiter, anonymised, full – and send the personal link."
        },
        "open": {
          "title": "Recipient opens it",
          "text": "The link unlocks exactly the parts of the CV that the invite allows – online and as matching PDF."
        }
      },
      "footer": "Invite-only CV hosting · self-hosted",
      "tryDemo": "Try the demo CV"
    }
  },
  "de": {
    "showcase": {
      "metaTitle": "Lebensläufe nur auf Einladung",
      "metaDescription": "Teile deinen Lebenslauf mit genau den Personen, die du auswählst – und entscheide pro Einladung, was sie sehen.",
      "eyebrow": "Lebensläufe nur auf Einladung",
      "title": "Ein Lebenslauf. Jede:r sieht genau das Richtige.",
      "subtitle": "CV as Code: Pflege deinen Lebenslauf einmal als JSON, versioniert in Git, und teile ihn über persönliche Einladungslinks. Jede Einladung legt fest, welche Details sichtbar sind – online und im eigenen PDF.",
      "seeFeatures": "Funktionen ansehen",
      "inviteTitle": "Eingeladen?",
      "inviteText": "Gib den Code aus deinem Einladungslink ein, um den Lebenslauf zu öffnen.",
      "screenshotsTitle": "So sieht ein Lebenslauf aus",
      "screenshotsText": "Screenshots des Demo-Lebenslaufs (Beispieldaten) – online und als PDF.",
      "shots": {
        "desktop": "Profil-Seitenleiste und interaktive Zeitleiste neben den Einträgen",
        "german": "Deutsche Version mit Projekt-Screenshots und Logos",
        "dark": "Dunkelmodus",
        "mobile": "Mobile Ansicht",
        "pdfRecruiter": "PDF einer Recruiter-Einladung – Firmennamen durch neutrale Beschreibungen ersetzt",
        "pdfFull": "Deutsches PDF einer vollständigen Einladung, mit QR-Code zur Online-Version"
      },
      "compareTitle": "Ein Lebenslauf, verschiedene Einladungen",
      "compareText": "Beide Ansichten stammen aus derselben JSON-Datei. Die anonymisierte Einladung ersetzt Firmennamen durch neutrale Beschreibungen, reduziert Daten auf Jahre und blendet vertrauliche Einträge aus – die verborgenen Daten erreichen den Browser nie.",
      "compare": {
        "full": "Vollständige Einladung",
        "redacted": "Anonymisierte Einladung"
      },
      "featuresTitle": "Funktionen",
      "features": {
        "cvascode": {
          "title": "CV as Code",
          "text": "Wie Infrastructure as Code: Der Lebenslauf und wer welchen Teil sehen darf, sind einfache JSON-Dateien – eine Quelle, alle Ansichten daraus abgeleitet. Änderungen sind in Sekunden live, ohne Neubau."
        },
        "git": {
          "title": "Versioniert in Git",
          "text": "Den Lebenslauf in einem Git-Repository pflegen: vollständige Historie, Review per Pull Request, Prüfung in der CI, automatisches Deployment beim Push und Rollback per Revert."
        },
        "gating": {
          "title": "Freigabe pro Feld",
          "text": "Jeder Eintrag und jedes einzelne Feld kann eine Freigabe verlangen. Nur Einladungen mit dieser Freigabe erhalten es; alles andere wird serverseitig entfernt."
        },
        "flags": {
          "title": "Datenschutz-Schalter",
          "text": "Firmennamen durch neutrale Beschreibungen ersetzen, Daten auf Monat oder Jahr reduzieren, Foto, Kontaktdaten, Geburtsdatum oder Medien ausblenden."
        },
        "invites": {
          "title": "Persönliche Einladungslinks",
          "text": "Ein Link pro Empfänger:in, optional mit Ablaufdatum und Nutzungslimit. Widerrufen wirkt sofort."
        },
        "pdf": {
          "title": "PDF pro Einladung",
          "text": "Jede Einladung erhält ein eigenes PDF mit genau den sichtbaren Teilen – erzeugt beim Anlegen der Einladung und automatisch aktualisiert, wenn sich der Lebenslauf ändert. Inklusive A4-Drucklayout mit QR-Code."
        },
        "tenants": {
          "title": "Mehrere Personen",
          "text": "Mehrere Lebensläufe hosten: jeweils unter eigener Subdomain oder unter einer gemeinsamen Adresse, bei der der Einladungscode entscheidet."
        },
        "i18n": {
          "title": "Mehrsprachig",
          "text": "Inhalte und Oberfläche auf Deutsch und Englisch, jederzeit umschaltbar."
        },
        "timeline": {
          "title": "Interaktive Zeitleiste",
          "text": "Berufserfahrung, Ausbildung und Projekte auf einer Zeitleiste – inklusive überlappender Zeiträume, verknüpft mit den Einträgen."
        },
        "filter": {
          "title": "Technologie-Filter",
          "text": "Ein Klick auf eine Technologie zeigt nur die Stationen und Projekte, in denen sie eingesetzt wurde."
        },
        "responsive": {
          "title": "Responsiv & Dunkelmodus",
          "text": "Funktioniert auf Smartphone, Tablet und Desktop, hell und dunkel – mit Projekt-Screenshots und Logos im integrierten Bildbetrachter."
        },
        "selfhosted": {
          "title": "Selbst gehostet",
          "text": "Nuxt-Frontend und C#-API als Docker-Container, bereit für Coolify."
        }
      },
      "stepsTitle": "So funktioniert's",
      "steps": {
        "maintain": {
          "title": "Lebenslauf als Code pflegen",
          "text": "Den vollständigen Lebenslauf einmal als JSON schreiben – idealerweise in einem Git-Repository – und Vertrauliches markieren. Die CI prüft und veröffentlicht ihn."
        },
        "invite": {
          "title": "Einladung erstellen",
          "text": "Ein Profil wählen – z. B. Recruiter, anonymisiert, vollständig – und den persönlichen Link verschicken."
        },
        "open": {
          "title": "Empfänger:in öffnet",
          "text": "Der Link schaltet genau die Teile des Lebenslaufs frei, die die Einladung erlaubt – online und als passendes PDF."
        }
      },
      "footer": "Lebensläufe nur auf Einladung · selbst gehostet",
      "tryDemo": "Demo-Lebenslauf ansehen"
    }
  }
}
</i18n>
