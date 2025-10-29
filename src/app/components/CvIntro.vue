<script setup lang="ts">
const { t } = useI18n()

// Intro data - in a real app, this would come from API or props
const introData = ref({
  text: '',
  yearsOfExperience: 25,
  programmingSince: 2000,
  stats: [
    { label: '', value: 100, max: 100, suffix: '+', icon: 'briefcase' },
    { label: '', value: 50, max: 50, suffix: '+', icon: 'users' },
    { label: '', value: 15, max: 20, suffix: '', icon: 'code' }
  ]
})

// Animation state
const isVisible = ref(false)
const animatedYears = ref(0)
const animatedStats = ref(introData.value.stats.map(stat => ({ ...stat, currentValue: 0 })))

// Intersection Observer to trigger animations
const sectionRef = ref<HTMLElement | null>(null)

onMounted(() => {
  if (typeof window === 'undefined') return

  const observer = new IntersectionObserver(
    (entries) => {
      entries.forEach((entry) => {
        if (entry.isIntersecting && !isVisible.value) {
          isVisible.value = true
          animateValues()
        }
      })
    },
    { threshold: 0.3 }
  )

  if (sectionRef.value) {
    observer.observe(sectionRef.value)
  }

  return () => {
    if (sectionRef.value) {
      observer.unobserve(sectionRef.value)
    }
  }
})

// Animate values when section becomes visible
function animateValues() {
  // Animate years of experience
  const yearsDuration = 2000
  const yearsStart = Date.now()
  const yearsTarget = introData.value.yearsOfExperience

  const animateYears = () => {
    const elapsed = Date.now() - yearsStart
    const progress = Math.min(elapsed / yearsDuration, 1)
    animatedYears.value = Math.floor(progress * yearsTarget)

    if (progress < 1) {
      requestAnimationFrame(animateYears)
    }
  }
  animateYears()

  // Animate stats with slight delay between each
  animatedStats.value.forEach((stat, index) => {
    setTimeout(() => {
      const duration = 1500
      const start = Date.now()

      const animateStat = () => {
        const elapsed = Date.now() - start
        const progress = Math.min(elapsed / duration, 1)
        // Easing function for smooth animation
        const eased = 1 - Math.pow(1 - progress, 3)
        stat.currentValue = Math.floor(eased * stat.value)

        if (progress < 1) {
          requestAnimationFrame(animateStat)
        }
      }
      animateStat()
    }, index * 200)
  })
}

// Calculate percentage for circular progress
function getCircleProgress(value: number, max: number): number {
  return (value / max) * 100
}

// Get stroke dasharray for circular progress
function getCircleDasharray(progress: number): string {
  const circumference = 2 * Math.PI * 45 // radius of 45
  const offset = circumference - (progress / 100) * circumference
  return `${circumference - offset} ${circumference}`
}
</script>

<template>
  <section 
    ref="sectionRef"
    class="intro-section bg-gradient-to-br from-gray-50 to-gray-100 dark:from-gray-800 dark:to-gray-900 py-16 px-6 print:bg-white print:py-8"
  >
    <div class="max-w-6xl mx-auto">
      <!-- Intro Text -->
      <div class="text-center mb-12">
        <h2 class="text-3xl md:text-4xl font-bold text-gray-900 dark:text-white print:text-black mb-4">
          {{ t('intro.title') }}
        </h2>
        <p class="text-lg text-gray-700 dark:text-gray-300 print:text-black max-w-3xl mx-auto">
          {{ t('intro.text') }}
        </p>
      </div>

      <!-- Stats Grid -->
      <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-8 mb-12">
        <!-- Years of Experience - Large Feature -->
        <div class="md:col-span-2 lg:col-span-1 flex flex-col items-center justify-center p-8 bg-white dark:bg-gray-800 rounded-lg shadow-lg print:shadow-none transition-all duration-500"
          :class="{ 'translate-y-0 opacity-100': isVisible, 'translate-y-4 opacity-0': !isVisible }"
        >
          <div class="text-6xl font-bold text-blue-600 dark:text-blue-400 mb-2">
            {{ animatedYears }}
          </div>
          <div class="text-xl font-semibold text-gray-900 dark:text-white print:text-black mb-1">
            {{ t('intro.yearsLabel') }}
          </div>
          <div class="text-sm text-gray-600 dark:text-gray-400 print:text-gray-700">
            {{ t('intro.programmingSince', { year: introData.programmingSince }) }}
          </div>
        </div>

        <!-- Animated Stats with Circular Progress -->
        <div 
          v-for="(stat, index) in animatedStats" 
          :key="index"
          class="flex flex-col items-center justify-center p-6 bg-white dark:bg-gray-800 rounded-lg shadow-lg print:shadow-none transition-all duration-500"
          :class="{ 'translate-y-0 opacity-100': isVisible, 'translate-y-4 opacity-0': !isVisible }"
          :style="{ transitionDelay: `${index * 100}ms` }"
        >
          <!-- Circular Progress -->
          <div class="relative w-24 h-24 mb-4">
            <svg class="transform -rotate-90 w-24 h-24">
              <!-- Background circle -->
              <circle
                cx="48"
                cy="48"
                r="45"
                stroke="currentColor"
                stroke-width="8"
                fill="none"
                class="text-gray-200 dark:text-gray-700"
              />
              <!-- Progress circle -->
              <circle
                cx="48"
                cy="48"
                r="45"
                stroke="currentColor"
                stroke-width="8"
                fill="none"
                class="text-blue-600 dark:text-blue-400 transition-all duration-1000"
                :style="{ strokeDasharray: getCircleDasharray(getCircleProgress(stat.currentValue, stat.max)) }"
              />
            </svg>
            <!-- Icon in center -->
            <div class="absolute inset-0 flex items-center justify-center">
              <UIcon 
                :name="`i-heroicons-${stat.icon}`" 
                class="w-8 h-8 text-blue-600 dark:text-blue-400"
              />
            </div>
          </div>
          
          <!-- Stat Value -->
          <div class="text-3xl font-bold text-gray-900 dark:text-white print:text-black">
            {{ stat.currentValue }}{{ stat.suffix }}
          </div>
          <div class="text-sm text-gray-600 dark:text-gray-400 print:text-gray-700 text-center">
            {{ t(`intro.stats.${index}.label`) }}
          </div>
        </div>
      </div>

      <!-- Key Highlights with Bullet Points -->
      <div class="max-w-4xl mx-auto">
        <h3 class="text-2xl font-bold text-gray-900 dark:text-white print:text-black mb-6 text-center">
          {{ t('intro.highlightsTitle') }}
        </h3>
        <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div 
            v-for="(highlight, index) in 4" 
            :key="index"
            class="flex items-start space-x-3 p-4 bg-white dark:bg-gray-800 rounded-lg transition-all duration-500 print:bg-transparent"
            :class="{ 'translate-x-0 opacity-100': isVisible, '-translate-x-4 opacity-0': !isVisible }"
            :style="{ transitionDelay: `${(index + 3) * 100}ms` }"
          >
            <!-- Animated bullet point -->
            <div class="flex-shrink-0 w-6 h-6 rounded-full bg-blue-600 dark:bg-blue-400 flex items-center justify-center transition-transform duration-500"
              :class="{ 'scale-100': isVisible, 'scale-0': !isVisible }"
              :style="{ transitionDelay: `${(index + 3) * 100}ms` }"
            >
              <UIcon name="i-heroicons-check" class="w-4 h-4 text-white" />
            </div>
            <p class="text-gray-700 dark:text-gray-300 print:text-black flex-1">
              {{ t(`intro.highlights.${index}`) }}
            </p>
          </div>
        </div>
      </div>
    </div>
  </section>
</template>

<style scoped>
.intro-section {
  scroll-margin-top: 2rem;
}

@media print {
  .intro-section {
    page-break-inside: avoid;
  }
}
</style>

<i18n lang="json">
{
  "en": {
    "intro": {
      "title": "About Me",
      "text": "I'm a passionate software architect with extensive experience in building scalable applications and leading development teams. I believe in clean code, continuous learning, and delivering value to users.",
      "yearsLabel": "Years",
      "programmingSince": "Programming since {year}",
      "stats": {
        "0": {
          "label": "Projects Completed"
        },
        "1": {
          "label": "Happy Clients"
        },
        "2": {
          "label": "Technologies Mastered"
        }
      },
      "highlightsTitle": "Key Highlights",
      "highlights": {
        "0": "Expert in modern web technologies and cloud architecture",
        "1": "Strong focus on code quality, testing, and best practices",
        "2": "Experienced in leading cross-functional development teams",
        "3": "Committed to continuous learning and staying current with industry trends"
      }
    }
  },
  "de": {
    "intro": {
      "title": "Über Mich",
      "text": "Ich bin ein leidenschaftlicher Software-Architekt mit umfangreicher Erfahrung im Aufbau skalierbarer Anwendungen und in der Leitung von Entwicklungsteams. Ich glaube an sauberen Code, kontinuierliches Lernen und die Bereitstellung von Mehrwert für Benutzer.",
      "yearsLabel": "Jahre",
      "programmingSince": "Programmierung seit {year}",
      "stats": {
        "0": {
          "label": "Abgeschlossene Projekte"
        },
        "1": {
          "label": "Zufriedene Kunden"
        },
        "2": {
          "label": "Beherrschte Technologien"
        }
      },
      "highlightsTitle": "Wichtige Highlights",
      "highlights": {
        "0": "Experte für moderne Webtechnologien und Cloud-Architektur",
        "1": "Starker Fokus auf Codequalität, Testing und Best Practices",
        "2": "Erfahren in der Leitung funktionsübergreifender Entwicklungsteams",
        "3": "Engagiert für kontinuierliches Lernen und aktuelle Branchentrends"
      }
    }
  }
}
</i18n>
