<script setup lang="ts">
const { t } = useI18n()

// This would eventually come from an API with multi-tenant support
const studies = ref([
  {
    id: 1,
    institution: 'Technical University',
    degree: 'Master of Science in Computer Science',
    period: '2015 - 2017',
    focus: 'Software Engineering & Distributed Systems'
  },
  {
    id: 2,
    institution: 'University of Technology',
    degree: 'Bachelor of Science in Computer Science',
    period: '2012 - 2015',
    focus: 'Computer Science Fundamentals'
  }
])
</script>

<template>
  <section>
    <h2 class="text-3xl font-bold text-gray-900 dark:text-white print:text-black mb-6">
      {{ t('studies.title') }}
    </h2>
    
    <div class="space-y-6">
      <UCard v-for="study in studies" :key="study.id" class="print:!shadow-none print:!border print:!border-gray-300">
        <template #header>
          <div class="flex justify-between items-start">
            <div>
              <h3 class="text-xl font-semibold text-gray-900 dark:text-white print:text-black">
                {{ study.degree }}
              </h3>
              <p class="text-gray-600 dark:text-gray-400 print:text-gray-700">
                {{ study.institution }}
              </p>
            </div>
            <UBadge color="primary" variant="subtle" class="print:!bg-gray-100 print:!text-black">
              {{ study.period }}
            </UBadge>
          </div>
        </template>

        <p class="text-gray-700 dark:text-gray-300 print:text-black">
          {{ t('studies.focus') }}: {{ study.focus }}
        </p>
      </UCard>
    </div>
  </section>
</template>

<i18n lang="json">
{
  "en": {
    "studies": {
      "title": "Education",
      "focus": "Focus"
    }
  },
  "de": {
    "studies": {
      "title": "Ausbildung",
      "focus": "Schwerpunkt"
    }
  }
}
</i18n>
