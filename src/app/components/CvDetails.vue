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
      <div class="sidebar-row-hover detail-row flex items-center gap-3">
        <svg class="w-5 h-5 flex-shrink-0 text-gray-600 dark:text-gray-400 print:text-gray-700" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17.657 16.657L13.414 20.9a1.998 1.998 0 01-2.827 0l-4.244-4.243a8 8 0 1111.314 0z"></path>
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 11a3 3 0 11-6 0 3 3 0 016 0z"></path>
        </svg>
        <span class="text-sm text-gray-800 dark:text-gray-200 print:text-black">
          {{ details.location }}
        </span>
      </div>
      
      <div class="sidebar-row-hover detail-row flex items-center gap-3">
        <svg class="w-5 h-5 flex-shrink-0 text-gray-600 dark:text-gray-400 print:text-gray-700" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 21v-4m0 0V5a2 2 0 012-2h6.5l1 1H21l-3 6 3 6h-8.5l-1-1H5a2 2 0 00-2 2zm9-13.5V9"></path>
        </svg>
        <span class="text-sm text-gray-800 dark:text-gray-200 print:text-black">
          {{ details.citizenship }}
        </span>
      </div>
      
      <div class="sidebar-row-hover detail-row flex items-center gap-3">
        <svg class="w-5 h-5 flex-shrink-0 text-gray-600 dark:text-gray-400 print:text-gray-700" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 15.546c-.523 0-1.046.151-1.5.454a2.704 2.704 0 01-3 0 2.704 2.704 0 00-3 0 2.704 2.704 0 01-3 0 2.704 2.704 0 00-3 0 2.704 2.704 0 01-3 0 2.701 2.701 0 00-1.5-.454M9 6v2m3-2v2m3-2v2M5 20h14a2 2 0 002-2V8a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z"></path>
        </svg>
        <span class="text-sm text-gray-800 dark:text-gray-200 print:text-black">
          {{ details.birthDate }}
        </span>
      </div>
      
      <div class="sidebar-row-hover detail-row flex items-center gap-3">
        <svg class="w-5 h-5 flex-shrink-0 text-gray-600 dark:text-gray-400 print:text-gray-700" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 8l7.89 5.26a2 2 0 002.22 0L21 8M5 19h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z"></path>
        </svg>
        <a 
          :href="`mailto:${details.email}`"
          class="text-sm text-gray-800 dark:text-gray-200 print:text-black hover:underline"
        >
          {{ details.email }}
        </a>
      </div>
      
      <div class="sidebar-row-hover detail-row flex items-center gap-3">
        <svg class="w-5 h-5 flex-shrink-0 text-gray-600 dark:text-gray-400 print:text-gray-700" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 5a2 2 0 012-2h3.28a1 1 0 01.948.684l1.498 4.493a1 1 0 01-.502 1.21l-2.257 1.13a11.042 11.042 0 005.516 5.516l1.13-2.257a1 1 0 011.21-.502l4.493 1.498a1 1 0 01.684.949V19a2 2 0 01-2 2h-1C9.716 21 3 14.284 3 6V5z"></path>
        </svg>
        <a 
          :href="`tel:${details.phone.replace(/\s/g, '')}`"
          class="text-sm text-gray-800 dark:text-gray-200 print:text-black hover:underline"
        >
          {{ details.phone }}
        </a>
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
