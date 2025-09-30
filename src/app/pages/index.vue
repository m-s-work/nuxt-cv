<script setup lang="ts">
const { t } = useI18n()

useSeoMeta({
  title: t('cv.title'),
  description: t('cv.description')
})
</script>

<template>
  <div class="min-h-screen bg-gray-50 dark:bg-gray-900">
    <UContainer class="py-8">
      <div class="space-y-8">
        <!-- Header -->
        <header class="text-center space-y-4">
          <h1 class="text-4xl font-bold text-gray-900 dark:text-white">
            {{ t('cv.title') }}
          </h1>
          <p class="text-lg text-gray-600 dark:text-gray-400">
            {{ t('cv.subtitle') }}
          </p>
        </header>

        <!-- Experiences Section -->
        <CvExperiences />

        <!-- Studies Section -->
        <CvStudies />
      </div>
    </UContainer>
  </div>
</template>

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
