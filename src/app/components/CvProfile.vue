<script setup lang="ts">
const { t } = useI18n()

// Profile information with academic titles
const profile = ref({
  name: 'Max Mustermann',
  title: 'Software Architect',
  photoUrl: '/images/profile-small.jpg',
  academicTitlePrefix: 'Dr.', // Prepended academic title (e.g., Dr., Prof., Prof. Dr.)
  academicTitleSuffix: 'Ph.D.' // Appended academic title (e.g., Ph.D., M.Sc., MBA)
})

// Design option for displaying academic titles
// Options: 'inline', 'separate-row', 'above-name', 'below-title'
const titleDisplayMode = ref<'inline' | 'separate-row' | 'above-name' | 'below-title'>('separate-row')
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
      <!-- Option 1: Academic titles on separate row above name -->
      <div v-if="titleDisplayMode === 'above-name' && (profile.academicTitlePrefix || profile.academicTitleSuffix)" class="mb-2">
        <p class="text-sm font-medium text-gray-700 dark:text-gray-300 print:text-gray-600">
          <span v-if="profile.academicTitlePrefix">{{ profile.academicTitlePrefix }}</span>
          <span v-if="profile.academicTitlePrefix && profile.academicTitleSuffix"> · </span>
          <span v-if="profile.academicTitleSuffix">{{ profile.academicTitleSuffix }}</span>
        </p>
      </div>

      <h1 class="text-3xl font-bold text-gray-900 dark:text-white print:text-black">
        <!-- Option 2: Inline academic titles with name -->
        <template v-if="titleDisplayMode === 'inline'">
          <span v-if="profile.academicTitlePrefix" class="text-2xl">{{ profile.academicTitlePrefix }}&nbsp;</span>
          <span>{{ profile.name }}</span>
          <span v-if="profile.academicTitleSuffix" class="text-2xl">,&nbsp;{{ profile.academicTitleSuffix }}</span>
        </template>
        <!-- Default: Just name -->
        <template v-else>
          {{ profile.name }}
        </template>
      </h1>
      
      <p class="text-lg text-gray-600 dark:text-gray-400 print:text-gray-700 mt-1">
        {{ profile.title }}
      </p>

      <!-- Option 3: Academic titles on separate row below title -->
      <div v-if="titleDisplayMode === 'below-title' && (profile.academicTitlePrefix || profile.academicTitleSuffix)" class="mt-2">
        <p class="text-sm font-medium text-gray-700 dark:text-gray-300 print:text-gray-600">
          <span v-if="profile.academicTitlePrefix">{{ profile.academicTitlePrefix }}</span>
          <span v-if="profile.academicTitlePrefix && profile.academicTitleSuffix"> · </span>
          <span v-if="profile.academicTitleSuffix">{{ profile.academicTitleSuffix }}</span>
        </p>
      </div>

      <!-- Option 4: Academic titles on completely separate row with emphasis -->
      <div v-if="titleDisplayMode === 'separate-row' && (profile.academicTitlePrefix || profile.academicTitleSuffix)" class="mt-3 pt-3 border-t border-gray-300 dark:border-gray-600 print:border-gray-400">
        <p class="text-xs uppercase tracking-wider text-gray-500 dark:text-gray-400 print:text-gray-500 mb-1">
          {{ t('profile.academicTitles') }}
        </p>
        <p class="text-sm font-semibold text-gray-800 dark:text-gray-200 print:text-gray-700">
          <span v-if="profile.academicTitlePrefix">{{ profile.academicTitlePrefix }}</span>
          <span v-if="profile.academicTitlePrefix && profile.academicTitleSuffix"> · </span>
          <span v-if="profile.academicTitleSuffix">{{ profile.academicTitleSuffix }}</span>
        </p>
      </div>
    </div>
  </div>
</template>

<i18n lang="json">
{
  "en": {
    "profile": {
      "title": "Profile",
      "academicTitles": "Academic Titles"
    }
  },
  "de": {
    "profile": {
      "title": "Profil",
      "academicTitles": "Akademische Titel"
    }
  }
}
</i18n>
