<script setup lang="ts">
const { t } = useI18n()

// Profile information
const profile = ref({
  name: 'Max Mustermann',
  title: 'Software Architect',
  photoUrl: '/images/profile-small.jpg',
  prependedTitles: ['Dr.'],
  appendedTitles: ['PhD', 'MSc']
})

// Compute full name with titles
const fullName = computed(() => {
  const prepended = profile.value.prependedTitles?.join(' ') || ''
  const appended = profile.value.appendedTitles?.join(', ') || ''
  
  let result = profile.value.name
  if (prepended) {
    result = `${prepended} ${result}`
  }
  if (appended) {
    result = `${result}, ${appended}`
  }
  return result
})
</script>

<template>
  <div class="flex flex-col items-center space-y-4">
    <!-- Profile Picture -->
    <div class="relative">
      <img 
        :src="profile.photoUrl"
        :srcset="`/images/profile-small.jpg 160w, /images/profile-medium.jpg 192w, /images/profile-large.jpg 320w, /images/profile-xlarge.jpg 384w`"
        sizes="(max-width: 640px) 160px, 192px"
        :alt="profile.name"
        class="w-40 h-48 object-cover rounded-lg shadow-lg border-4 border-white dark:border-gray-800 print:border-gray-300"
        loading="lazy"
      />
    </div>
    
    <!-- Name and Title -->
    <div class="text-center">
      <h1 class="text-3xl font-bold text-gray-900 dark:text-white print:text-black">
        {{ fullName }}
      </h1>
      <p class="text-lg text-gray-600 dark:text-gray-400 print:text-gray-700 mt-1">
        {{ profile.title }}
      </p>
    </div>
  </div>
</template>

<i18n lang="json">
{
  "en": {
    "profile": {
      "title": "Profile"
    }
  },
  "de": {
    "profile": {
      "title": "Profil"
    }
  }
}
</i18n>
