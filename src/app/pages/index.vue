<script setup lang="ts">
const { t } = useI18n()
const { scrollToElementSafely } = useSafeScroll()

useSeoMeta({
  title: t('cv.title'),
  description: t('cv.description')
})

// CV data comes from the API; tenant and visible fields are decided server-side.
const { locale } = useI18n()
const { cv, status, hostKind, ensure, consent, versions, arrivedViaLink, decideConsent, withdrawConsent } = useCv()
const apiBase = useRuntimeConfig().public.apiBase as string

const experiences = computed(() => cv.value?.experiences ?? [])
const studies = computed(() => cv.value?.studies ?? [])
const projects = computed(() => cv.value?.projects ?? [])
const otherEntries = computed(() => cv.value?.otherEntries ?? [])

await ensure(locale.value)
watch(locale, (newLocale, oldLocale) => {
  trackEvent('locale_switch', { from: oldLocale, to: newLocale })
  ensure(newLocale)
})

// --- Visitor tracking: consent modal first, tracker only after "Accept" (docs/VISITOR_SESSION_TRACKING.md) ----------
const isPrint = import.meta.client && new URLSearchParams(window.location.search).has('print')
const heatmapView = isHeatmapView()
const consentOpen = useState<boolean>('consent-modal-open', () => false)
const consentReopened = ref(false)
const ownerName = computed(() => cv.value?.profile?.name ?? '')

async function syncTracking() {
  if (isPrint || status.value !== 'ready' || !consent.value.required) {
    stopTracking()
    return
  }
  if (consent.value.state === 'accept') {
    startTracking({
      apiBase,
      locale: locale.value,
      viaLink: arrivedViaLink.value,
      versions: { appSha: await loadAppSha(), cvVersion: versions.value.cvVersion, cvSourceSha: versions.value.cvSourceSha }
    })
  } else {
    stopTracking()
  }
}

async function onAccept() {
  consentOpen.value = false
  await decideConsent('accept', consentReopened.value ? 'footer' : 'modal')
  await syncTracking()
}

async function onDecline() {
  consentOpen.value = false
  const wasAccepted = consent.value.state === 'accept'
  stopTracking()
  if (wasAccepted) await withdrawConsent()
  else await decideConsent('decline', consentReopened.value ? 'footer' : 'modal')
}

// Footer "Privacy" link re-opens the modal with the current choice.
watch(consentOpen, (open) => { if (open && consent.value.state) consentReopened.value = true })

function maybeAskConsent() {
  if (isPrint || status.value !== 'ready' || !consent.value.required || consent.value.state) return
  useSplashScreen().onSplashHidden(() => {
    consentReopened.value = false
    consentOpen.value = true
  })
}

onMounted(() => {
  if (isPrint) return
  maybeAskConsent()
  syncTracking()
})
// E.g. an invite code entered on the no-access page.
watch(status, () => { maybeAskConsent(); syncTracking() })
// Re-fetched CV (locale switch, …): new versions split the session (R4.7).
watch(versions, () => syncTracking())
onUnmounted(() => stopTracking())

function onTimelineTrack(entryId: number | string) {
  const anchor = timelineAnchor(entryId)
  if (anchor) trackEvent('timeline', { a: anchor, action: 'select' })
}

// PDF renderer mode (?print=1): tell the renderer when the page is complete.
// window.__CV_READY__ = 'ready' | 'no-access' | 'error' (see pdf/server.mjs)
onMounted(async () => {
  if (!new URLSearchParams(window.location.search).has('print')) return
  await nextTick()
  // Wait for visible images only (lazy images of the hidden screen layout never load), at most 10 s.
  const pending = Array.from(document.images)
    .filter(img => !img.complete && img.getClientRects().length > 0)
    .map(img => new Promise(resolve => { img.onload = img.onerror = resolve }))
  await Promise.race([Promise.all(pending), new Promise(resolve => setTimeout(resolve, 10_000))])
  // Let the intro counters finish animating.
  await new Promise(resolve => setTimeout(resolve, 2500))
  ;(window as unknown as { __CV_READY__?: string }).__CV_READY__ = status.value === 'ready' ? 'ready' : status.value
})

// Track active entries based on scroll position
const activeEntryIds = ref<(number | string)[]>([])
const clickedEntryId = ref<number | string | null>(null) // Track clicked entry

// Refs for tracking elements in viewport
const experienceSectionRef = ref<HTMLElement | null>(null)
const studiesSectionRef = ref<HTMLElement | null>(null)
const projectsSectionRef = ref<HTMLElement | null>(null)
const otherEntriesSectionRef = ref<HTMLElement | null>(null)

// Handle hover events on experience/study/project cards
function handleCardMouseEnter(id: number | string, type: 'exp' | 'study' | 'project' | 'other') {
  const fullId = type === 'exp' ? `exp-${id}` : type === 'study' ? `study-${id}` : type === 'project' ? `project-${id}` : `other-${id}`
  clickedEntryId.value = null // Clear clicked state on first hover
  if (!activeEntryIds.value.includes(fullId)) {
    activeEntryIds.value = [fullId]
  }
}

function handleCardMouseLeave() {
  // Only clear if no clicked entry is active
  if (clickedEntryId.value === null) {
    activeEntryIds.value = []
  }
}

// Handle timeline hover events
function handleTimelineHover(entryId: number | string) {
  clickedEntryId.value = null // Clear clicked state on first hover
  activeEntryIds.value = [entryId]
}

function handleTimelineLeave() {
  // Only clear if no clicked entry is active
  if (clickedEntryId.value === null) {
    activeEntryIds.value = []
  }
}

// Handle timeline click - set as active until first hover
function handleTimelineClick(entryId: number | string) {
  onTimelineTrack(entryId)
  clickedEntryId.value = entryId
  activeEntryIds.value = [entryId]
}

onMounted(() => {
  if (typeof window === 'undefined') return
  
  // Add hover listeners to all experience cards
  experiences.value.forEach(exp => {
    const element = document.getElementById(`experience-${exp.id}`)
    if (element) {
      element.addEventListener('mouseenter', () => handleCardMouseEnter(exp.id, 'exp'))
      element.addEventListener('mouseleave', handleCardMouseLeave)
    }
  })
  
  // Add hover listeners to all study cards
  studies.value.forEach(study => {
    const element = document.getElementById(`study-${study.id}`)
    if (element) {
      element.addEventListener('mouseenter', () => handleCardMouseEnter(study.id, 'study'))
      element.addEventListener('mouseleave', handleCardMouseLeave)
    }
  })

  // Add hover listeners to all project cards
  projects.value.forEach(project => {
    const element = document.getElementById(`project-${project.id}`)
    if (element) {
      element.addEventListener('mouseenter', () => handleCardMouseEnter(project.id, 'project'))
      element.addEventListener('mouseleave', handleCardMouseLeave)
    }
  })

  // Add hover listeners to all other entry cards
  otherEntries.value.forEach(entry => {
    const element = document.getElementById(`other-${entry.id}`)
    if (element) {
      element.addEventListener('mouseenter', () => handleCardMouseEnter(entry.id, 'other'))
      element.addEventListener('mouseleave', handleCardMouseLeave)
    }
  })
  
  // Restore scroll position from URL hash AFTER splash screen is hidden
  const { onSplashHidden } = useSplashScreen()
  if (typeof window !== 'undefined' && window.location.hash) {
    onSplashHidden(() => {
      setTimeout(() => {
        const hash = window.location.hash.substring(1)
        const element = document.getElementById(hash)
        if (element) {
          scrollToElementSafely(hash, 'smooth')
          // Extract ID and activate it
          if (hash.startsWith('experience-')) {
            const id = parseInt(hash.replace('experience-', ''))
            if (!isNaN(id)) {
              handleCardMouseEnter(id, 'exp')
              clickedEntryId.value = `exp-${id}`
            }
          } else if (hash.startsWith('study-')) {
            const id = parseInt(hash.replace('study-', ''))
            if (!isNaN(id)) {
              handleCardMouseEnter(id, 'study')
              clickedEntryId.value = `study-${id}`
            }
          } else if (hash.startsWith('project-')) {
            const id = parseInt(hash.replace('project-', ''))
            if (!isNaN(id)) {
              handleCardMouseEnter(id, 'project')
              clickedEntryId.value = `project-${id}`
            }
          } else if (hash.startsWith('other-')) {
            const id = parseInt(hash.replace('other-', ''))
            if (!isNaN(id)) {
              handleCardMouseEnter(id, 'other')
              clickedEntryId.value = `other-${id}`
            }
          }
        }
      }, 500) // Delay to ensure page is fully loaded
    })
  }
})

onUnmounted(() => {
  // Clean up event listeners
  experiences.value.forEach(exp => {
    const element = document.getElementById(`experience-${exp.id}`)
    if (element) {
      element.removeEventListener('mouseenter', () => handleCardMouseEnter(exp.id, 'exp'))
      element.removeEventListener('mouseleave', handleCardMouseLeave)
    }
  })
  
  studies.value.forEach(study => {
    const element = document.getElementById(`study-${study.id}`)
    if (element) {
      element.removeEventListener('mouseenter', () => handleCardMouseEnter(study.id, 'study'))
      element.removeEventListener('mouseleave', handleCardMouseLeave)
    }
  })

  projects.value.forEach(project => {
    const element = document.getElementById(`project-${project.id}`)
    if (element) {
      element.removeEventListener('mouseenter', () => handleCardMouseEnter(project.id, 'project'))
      element.removeEventListener('mouseleave', handleCardMouseLeave)
    }
  })

  otherEntries.value.forEach(entry => {
    const element = document.getElementById(`other-${entry.id}`)
    if (element) {
      element.removeEventListener('mouseenter', () => handleCardMouseEnter(entry.id, 'other'))
      element.removeEventListener('mouseleave', handleCardMouseLeave)
    }
  })
})

</script>

<template>
  <CvShowcase v-if="status === 'no-access' && hostKind === 'shared'" />
  <CvNoAccess v-else-if="status === 'no-access' || status === 'error'" :error="status === 'error'" />
  <div v-else-if="status === 'loading'" class="min-h-screen bg-white dark:bg-gray-900" />
  <div v-else>
    <CvHeatmapOverlay v-if="heatmapView" />
    <CvConsentModal
      v-if="consentOpen && consent.required && !heatmapView"
      :consent="consent" :name="ownerName" :reopened="consentReopened"
      @accept="onAccept" @decline="onDecline" @close="consentOpen = false"
    />
    <!-- Typeset A4 layout for print / PDF; the screen layout below is hidden in print -->
    <CvPrint />
    <div class="min-h-screen bg-white dark:bg-gray-900 print:hidden">
    <!-- Hero Section - Full page height -->
    <CvHero data-track="section:hero" />
    
    <!-- Intro Section - Between hero and main content -->
    <CvIntro v-if="cv?.intro" data-track="section:intro" />
    
    <!-- Main Content with Sidebar Layout -->
    <div class="cv-container">
      <!-- Sidebar -->
      <aside 
        class="sidebar bg-gray-100 dark:bg-gray-800 print:bg-gray-50 lg:sticky lg:top-0 lg:self-start lg:min-h-screen lg:overflow-y-auto"
      >
        <div class="p-6 space-y-8">
          <!-- Profile with Picture (fades in on scroll) - Hidden on mobile -->
          <div class="sidebar-profile hidden lg:block print:block" data-track="section:profile">
            <CvProfile />
          </div>
          
          <!-- Personal Details - Hidden on mobile -->
          <div class="hidden lg:block print:block" data-track="section:details">
            <CvDetails />
          </div>
          
          <!-- Languages - Hidden on mobile -->
          <div class="hidden lg:block print:block" data-track="section:languages">
            <CvLanguages />
          </div>
          
          <!-- Preferred Technologies - Hidden on mobile -->
          <div class="hidden lg:block print:block" data-track="section:preferredTechs">
            <CvPreferredTechs />
          </div>
          
          <!-- Driving Licenses - Hidden on mobile -->
          <div class="hidden lg:block print:block" data-track="section:drivingLicenses">
            <CvDrivingLicenses />
          </div>

          <!-- PDF download (hidden when the PDF renderer is not configured) -->
          <div class="hidden lg:block" data-track="section:pdf">
            <CvPdfButton />
          </div>

          <!-- Spacer to push QR code to bottom on print -->
          <div class="flex-grow print:block hidden"></div>
          
          <!-- QR Code -->
          <div class="hidden lg:block print:block">
            <CvQrCode />
          </div>
        </div>
      </aside>
      
      <!-- Main Content -->
      <main class="main-content bg-white dark:bg-gray-900 print:bg-white">
        <div class="main-content-wrapper">
          <!-- Timeline (left side) -->
          <CvTimeline
            data-track="section:timeline" 
            :experiences="experiences"
            :studies="studies"
            :projects="projects"
            :other-entries="otherEntries"
            :active-ids="activeEntryIds"
            @entry-hover="handleTimelineHover"
            @entry-leave="handleTimelineLeave"
            @entry-click="handleTimelineClick"
          />
          
          <!-- Content (right side) -->
          <div class="content-area p-6 lg:p-8 space-y-8 mx-auto max-w-4xl">
            <!-- Skills Section -->
            <div id="skills-section" data-track="section:skills">
              <CvSkills />
            </div>
            
            <!-- Experiences Section -->
            <div v-if="experiences.length" id="experiences-section" ref="experienceSectionRef" data-track="section:experiences">
              <CvExperiences :experiences="experiences" :active-ids="activeEntryIds" />
            </div>

            <!-- Studies Section -->
            <div v-if="studies.length" id="studies-section" ref="studiesSectionRef" data-track="section:studies">
              <CvStudies :studies="studies" :active-ids="activeEntryIds" />
            </div>

            <!-- Projects Section -->
            <div v-if="projects.length" id="projects-section" ref="projectsSectionRef" data-track="section:projects">
              <CvProjects :projects="projects" :active-ids="activeEntryIds" />
            </div>

            <!-- Other Experiences Section -->
            <div v-if="otherEntries.length" id="other-section" ref="otherEntriesSectionRef" data-track="section:other">
              <CvOtherExperiences :entries="otherEntries" :active-ids="activeEntryIds" />
            </div>

            <!-- Sidebar sections on mobile (shown at end) -->
            <div class="lg:hidden print:hidden mobile-sidebar-sections space-y-8 mt-12 pt-8 border-t border-gray-200 dark:border-gray-700">
              <CvPdfButton data-track="section:pdf" />

              <!-- Personal Details -->
              <CvDetails data-track="section:details" />
              
              <!-- Languages -->
              <CvLanguages data-track="section:languages" />
              
              <!-- Preferred Technologies -->
              <CvPreferredTechs data-track="section:preferredTechs" />
              
              <!-- Driving Licenses -->
              <CvDrivingLicenses data-track="section:drivingLicenses" />
            </div>

            <!-- Footer -->
            <CvFooter />
          </div>
        </div>
      </main>
    </div>
  </div>
  </div>
</template>

<style scoped>
.cv-container {
  display: grid;
  grid-template-columns: 1fr;
}

@media (min-width: 1024px) {
  .cv-container {
    grid-template-columns: 350px 1fr;
  }
}

/* CSS-based fade-in animation for sidebar profile */
.sidebar-profile {
  opacity: 0;
  animation: fadeInOnScroll 0.5s ease-out 0.3s forwards;
}

@keyframes fadeInOnScroll {
  from {
    opacity: 0;
    transform: translateY(-10px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

@media print {
  .cv-container {
    grid-template-columns: 300px 1fr;
    min-height: 100vh;
  }
  
  .sidebar {
    height: auto !important;
    position: static !important;
    overflow: visible !important;
    min-height: 100vh;
  }
  
  .sidebar-content {
    display: flex !important;
    flex-direction: column;
    min-height: calc(100vh - 3rem); /* Account for padding */
  }
  
  .sidebar-profile {
    opacity: 1 !important;
    animation: none !important;
  }
}

/* Main content wrapper for timeline layout */
.main-content-wrapper {
  display: flex;
  gap: 1rem;
}

.content-area {
  flex: 1;
  min-width: 0; /* Prevent flex item from overflowing */
}

@media (max-width: 1279px) {
  .main-content-wrapper {
    display: block;
  }
}
</style>

<i18n lang="json">
{
  "en": {
    "cv": {
      "title": "Software Architect CV",
      "subtitle": "Professional Experience & Education",
      "description": "Curriculum Vitae of a Software Architect"
    }
  },
  "de": {
    "cv": {
      "title": "Lebenslauf Software-Architekt",
      "subtitle": "Berufserfahrung & Ausbildung",
      "description": "Lebenslauf eines Software-Architekten"
    }
  }
}
</i18n>
