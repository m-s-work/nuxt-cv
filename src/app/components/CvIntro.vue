<script setup lang="ts">
const { t } = useI18n()

// Intro data - would eventually come from API/tenant config
const introData = ref({
  yearsOfExperience: 25,
  programmingSince: 2000,
  projectsCompleted: 150,
  technologiesMastered: 40,
  companiesWorkedWith: 12,
  // Progress percentages for circular/bar animations
  yearsProgress: 100,
  projectsProgress: 85
})

// Animated values
const animatedYears = ref(0)
const animatedProjects = ref(0)
const animatedTechs = ref(0)
const animatedCompanies = ref(0)

// Progress for circular animations
const yearsProgress = ref(0)
const projectsProgress = ref(0)

// Track if component is visible and if animations have started
const introRef = ref<HTMLElement | null>(null)
const isVisible = ref(false)
const hasAnimationsStarted = ref(false)

// Easing function for smooth animation
const easeOutQuad = (t: number) => t * (2 - t)

// Generic animation function with easing
function animateValue(
  valueRef: Ref<number>,
  target: number,
  duration: number = 2000,
  roundValue: boolean = false
) {
  const startTime = Date.now()
  const step = () => {
    const elapsed = Date.now() - startTime
    const progress = Math.min(elapsed / duration, 1)
    
    const easedValue = target * easeOutQuad(progress)
    valueRef.value = roundValue ? Math.floor(easedValue) : easedValue
    
    if (progress < 1) {
      requestAnimationFrame(step)
    } else {
      valueRef.value = target
    }
  }
  requestAnimationFrame(step)
}

// Start animations when component becomes visible
function startAnimations() {
  if (hasAnimationsStarted.value) return
  
  hasAnimationsStarted.value = true
  isVisible.value = true
  
  // Animate numbers with different delays (rounded to integers)
  setTimeout(() => animateValue(animatedYears, introData.value.yearsOfExperience, 2000, true), 200)
  setTimeout(() => animateValue(animatedProjects, introData.value.projectsCompleted, 2000, true), 400)
  setTimeout(() => animateValue(animatedTechs, introData.value.technologiesMastered, 2000, true), 600)
  setTimeout(() => animateValue(animatedCompanies, introData.value.companiesWorkedWith, 2000, true), 800)
  
  // Animate progress circles (smooth decimal values)
  setTimeout(() => animateValue(yearsProgress, introData.value.yearsProgress, 2000), 200)
  setTimeout(() => animateValue(projectsProgress, introData.value.projectsProgress, 2000), 400)
}

onMounted(() => {
  if (typeof window === 'undefined' || !introRef.value) return
  
  // Use Intersection Observer to trigger animations when scrolled into view
  const observer = new IntersectionObserver(
    (entries) => {
      entries.forEach((entry) => {
        if (entry.isIntersecting) {
          startAnimations()
          observer.disconnect()
        }
      })
    },
    {
      threshold: 0.2 // Trigger when 20% visible
    }
  )
  
  observer.observe(introRef.value)
})
</script>

<template>
  <div ref="introRef" class="intro-section">
    <div class="intro-container">
      <!-- Intro Text -->
      <div class="intro-text">
        <h2 class="intro-title">
          {{ t('intro.title') }}
        </h2>
        <p class="intro-summary">
          {{ t('intro.summary') }}
        </p>
      </div>

      <!-- Statistics Grid -->
      <div class="stats-grid">
        <!-- Years of Experience with Circle Animation -->
        <div class="stat-card" :class="{ 'visible': isVisible }">
          <div class="stat-visual">
            <svg class="circular-progress" viewBox="0 0 120 120">
              <circle
                class="progress-bg"
                cx="60"
                cy="60"
                r="54"
              />
              <circle
                class="progress-fill"
                cx="60"
                cy="60"
                r="54"
                :stroke-dasharray="`${(yearsProgress / 100) * 339.292} 339.292`"
              />
              <text
                x="60"
                y="60"
                class="progress-text"
                text-anchor="middle"
                dominant-baseline="middle"
              >
                {{ animatedYears }}
              </text>
            </svg>
          </div>
          <div class="stat-content">
            <h3 class="stat-title">{{ t('intro.yearsTitle') }}</h3>
            <p class="stat-detail">{{ t('intro.programmingSince', { year: introData.programmingSince }) }}</p>
          </div>
        </div>

        <!-- Projects with Bar Animation -->
        <div class="stat-card" :class="{ 'visible': isVisible }">
          <div class="stat-visual-bar">
            <div class="bar-container">
              <div class="bar-fill" :style="{ width: `${projectsProgress}%` }"></div>
            </div>
            <div class="stat-number">{{ animatedProjects }}+</div>
          </div>
          <div class="stat-content">
            <h3 class="stat-title">{{ t('intro.projectsTitle') }}</h3>
            <p class="stat-detail">{{ t('intro.projectsDetail') }}</p>
          </div>
        </div>

        <!-- Technologies -->
        <div class="stat-card" :class="{ 'visible': isVisible }">
          <div class="stat-icon">
            <svg class="w-16 h-16" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10 20l4-16m4 4l4 4-4 4M6 16l-4-4 4-4"/>
            </svg>
          </div>
          <div class="stat-number-large">{{ animatedTechs }}+</div>
          <div class="stat-content">
            <h3 class="stat-title">{{ t('intro.techsTitle') }}</h3>
            <p class="stat-detail">{{ t('intro.techsDetail') }}</p>
          </div>
        </div>

        <!-- Companies -->
        <div class="stat-card" :class="{ 'visible': isVisible }">
          <div class="stat-icon">
            <svg class="w-16 h-16" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4"/>
            </svg>
          </div>
          <div class="stat-number-large">{{ animatedCompanies }}+</div>
          <div class="stat-content">
            <h3 class="stat-title">{{ t('intro.companiesTitle') }}</h3>
            <p class="stat-detail">{{ t('intro.companiesDetail') }}</p>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.intro-section {
  background: linear-gradient(to bottom, rgb(37 99 235), rgb(255 255 255));
  padding: 4rem 1rem;
  position: relative;
}

@media (prefers-color-scheme: dark) {
  .intro-section {
    background: linear-gradient(to bottom, rgb(29 78 216), rgb(17 24 39));
  }
}

.intro-container {
  max-width: 1200px;
  margin: 0 auto;
}

.intro-text {
  text-align: center;
  margin-bottom: 3rem;
  color: white;
}

.intro-title {
  font-size: 2.5rem;
  font-weight: bold;
  margin-bottom: 1rem;
}

@media (min-width: 768px) {
  .intro-title {
    font-size: 3rem;
  }
}

.intro-summary {
  font-size: 1.125rem;
  max-width: 800px;
  margin: 0 auto;
  line-height: 1.75;
  color: rgb(219 234 254);
}

@media (min-width: 768px) {
  .intro-summary {
    font-size: 1.25rem;
  }
}

.stats-grid {
  display: grid;
  grid-template-columns: 1fr;
  gap: 2rem;
  margin-top: 3rem;
}

@media (min-width: 640px) {
  .stats-grid {
    grid-template-columns: repeat(2, 1fr);
  }
}

@media (min-width: 1024px) {
  .stats-grid {
    grid-template-columns: repeat(4, 1fr);
  }
}

.stat-card {
  background: white;
  border-radius: 1rem;
  padding: 2rem;
  text-align: center;
  box-shadow: 0 10px 25px rgba(0, 0, 0, 0.1);
  opacity: 0;
  transform: translateY(20px);
  transition: all 0.6s ease;
}

.stat-card.visible {
  opacity: 1;
  transform: translateY(0);
}

.stat-card:nth-child(1).visible {
  transition-delay: 0.1s;
}

.stat-card:nth-child(2).visible {
  transition-delay: 0.2s;
}

.stat-card:nth-child(3).visible {
  transition-delay: 0.3s;
}

.stat-card:nth-child(4).visible {
  transition-delay: 0.4s;
}

@media (prefers-color-scheme: dark) {
  .stat-card {
    background: rgb(31 41 55);
    box-shadow: 0 10px 25px rgba(0, 0, 0, 0.3);
  }
}

.stat-visual {
  margin: 0 auto 1.5rem;
  width: 120px;
  height: 120px;
}

.circular-progress {
  transform: rotate(-90deg);
  width: 100%;
  height: 100%;
}

.progress-bg {
  fill: none;
  stroke: rgb(229 231 235);
  stroke-width: 8;
}

@media (prefers-color-scheme: dark) {
  .progress-bg {
    stroke: rgb(55 65 81);
  }
}

.progress-fill {
  fill: none;
  stroke: rgb(37 99 235);
  stroke-width: 8;
  stroke-linecap: round;
  transition: stroke-dasharray 0.3s ease;
}

.progress-text {
  font-size: 2rem;
  font-weight: bold;
  fill: rgb(37 99 235);
  transform: rotate(90deg);
  transform-origin: center;
}

.stat-visual-bar {
  margin-bottom: 1.5rem;
}

.bar-container {
  width: 100%;
  height: 12px;
  background: rgb(229 231 235);
  border-radius: 6px;
  overflow: hidden;
  margin-bottom: 1rem;
}

@media (prefers-color-scheme: dark) {
  .bar-container {
    background: rgb(55 65 81);
  }
}

.bar-fill {
  height: 100%;
  background: linear-gradient(to right, rgb(37 99 235), rgb(59 130 246));
  border-radius: 6px;
  transition: width 0.3s ease;
}

.stat-number {
  font-size: 2.5rem;
  font-weight: bold;
  color: rgb(37 99 235);
}

.stat-icon {
  margin: 0 auto 1rem;
  color: rgb(37 99 235);
  display: flex;
  justify-content: center;
}

.stat-number-large {
  font-size: 3rem;
  font-weight: bold;
  color: rgb(37 99 235);
  margin-bottom: 0.5rem;
}

.stat-content {
  margin-top: 1rem;
}

.stat-title {
  font-size: 1.125rem;
  font-weight: 600;
  color: rgb(17 24 39);
  margin-bottom: 0.5rem;
}

@media (prefers-color-scheme: dark) {
  .stat-title {
    color: white;
  }
}

.stat-detail {
  font-size: 0.875rem;
  color: rgb(107 114 128);
}

@media (prefers-color-scheme: dark) {
  .stat-detail {
    color: rgb(156 163 175);
  }
}

@media print {
  .intro-section {
    display: none;
  }
}
</style>

<i18n lang="json">
{
  "en": {
    "intro": {
      "title": "About Me",
      "summary": "Passionate software architect with extensive experience in building scalable cloud-native applications and leading development teams.",
      "yearsTitle": "Years of Experience",
      "programmingSince": "Programming since {year}",
      "projectsTitle": "Projects Completed",
      "projectsDetail": "Successful deliveries",
      "techsTitle": "Technologies",
      "techsDetail": "Mastered & applied",
      "companiesTitle": "Companies",
      "companiesDetail": "Trusted partnerships"
    }
  },
  "de": {
    "intro": {
      "title": "Über Mich",
      "summary": "Leidenschaftlicher Software-Architekt mit umfangreicher Erfahrung im Aufbau skalierbarer Cloud-nativer Anwendungen und in der Leitung von Entwicklungsteams.",
      "yearsTitle": "Jahre Erfahrung",
      "programmingSince": "Programmierung seit {year}",
      "projectsTitle": "Abgeschlossene Projekte",
      "projectsDetail": "Erfolgreiche Lieferungen",
      "techsTitle": "Technologien",
      "techsDetail": "Beherrscht & angewendet",
      "companiesTitle": "Unternehmen",
      "companiesDetail": "Vertrauensvolle Partnerschaften"
    }
  }
}
</i18n>
