<script setup lang="ts">
const { t } = useI18n()

const { cv } = useCv()
const licenses = computed(() => cv.value?.drivingLicenses ?? [])
</script>

<template>
  <div class="space-y-4">
    <h2 class="text-2xl font-bold text-gray-900 dark:text-white print:text-black mb-4">
      {{ t('licenses.title') }}
    </h2>
    
    <div class="flex flex-wrap gap-2">
      <UBadge 
        v-for="license in licenses" 
        :key="license.type"
        color="gray"
        variant="soft"
        size="lg"
        class="print:!bg-gray-100 print:!text-black"
      >
        {{ t('licenses.class') }} {{ license.type }}
      </UBadge>
    </div>
  </div>
</template>

<i18n lang="json">
{
  "en": {
    "licenses": {
      "title": "Driving Licenses",
      "class": "Class"
    }
  },
  "de": {
    "licenses": {
      "title": "Führerscheine",
      "class": "Klasse"
    }
  }
}
</i18n>
