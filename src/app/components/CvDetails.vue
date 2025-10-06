<script setup lang="ts">
const { t } = useI18n()

// Personal details - this would eventually come from an API
const details = ref({
  name: 'Max Mustermann',
  title: 'Software Architect',
  location: 'Berlin, Germany',
  citizenship: 'German',
  email: 'max.mustermann@example.com',
  phone: '+49 123 456789',
  birthDate: '01.01.1990'
})
</script>

<template>
  <div class="space-y-4">
    <h2 class="text-2xl font-bold text-gray-900 dark:text-white print:text-black mb-4">
      {{ t('details.title') }}
    </h2>
    
    <div class="space-y-3">
      <div class="flex items-start">
        <span class="text-sm font-semibold text-gray-600 dark:text-gray-400 print:text-gray-700 w-32 flex-shrink-0">
          {{ t('details.location') }}:
        </span>
        <span class="text-sm text-gray-800 dark:text-gray-200 print:text-black">
          {{ details.location }}
        </span>
      </div>
      
      <div class="flex items-start">
        <span class="text-sm font-semibold text-gray-600 dark:text-gray-400 print:text-gray-700 w-32 flex-shrink-0">
          {{ t('details.citizenship') }}:
        </span>
        <span class="text-sm text-gray-800 dark:text-gray-200 print:text-black">
          {{ details.citizenship }}
        </span>
      </div>
      
      <div class="flex items-start">
        <span class="text-sm font-semibold text-gray-600 dark:text-gray-400 print:text-gray-700 w-32 flex-shrink-0">
          {{ t('details.birthDate') }}:
        </span>
        <span class="text-sm text-gray-800 dark:text-gray-200 print:text-black">
          {{ details.birthDate }}
        </span>
      </div>
      
      <div class="flex items-start">
        <span class="text-sm font-semibold text-gray-600 dark:text-gray-400 print:text-gray-700 w-32 flex-shrink-0">
          {{ t('details.email') }}:
        </span>
        <span class="text-sm text-gray-800 dark:text-gray-200 print:text-black">
          {{ details.email }}
        </span>
      </div>
      
      <div class="flex items-start">
        <span class="text-sm font-semibold text-gray-600 dark:text-gray-400 print:text-gray-700 w-32 flex-shrink-0">
          {{ t('details.phone') }}:
        </span>
        <span class="text-sm text-gray-800 dark:text-gray-200 print:text-black">
          {{ details.phone }}
        </span>
      </div>
    </div>
  </div>
</template>

<i18n lang="json">
{
  "en": {
    "details": {
      "title": "Personal Details",
      "location": "Location",
      "citizenship": "Citizenship",
      "birthDate": "Date of Birth",
      "email": "Email",
      "phone": "Phone"
    }
  },
  "de": {
    "details": {
      "title": "Persönliche Daten",
      "location": "Wohnort",
      "citizenship": "Staatsangehörigkeit",
      "birthDate": "Geburtsdatum",
      "email": "E-Mail",
      "phone": "Telefon"
    }
  }
}
</i18n>
