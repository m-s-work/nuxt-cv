<script setup lang="ts">
const { t } = useI18n()

// This would eventually come from an API with multi-tenant support
const experiences = ref([
  {
    id: 1,
    company: 'Tech Company Inc.',
    position: 'Senior Software Architect',
    period: '2020 - Present',
    description: 'Leading architecture design and implementation for cloud-native applications',
    technologies: ['Nuxt', 'Vue.js', 'Node.js', 'Docker', 'Kubernetes']
  },
  {
    id: 2,
    company: 'Software Solutions Ltd.',
    position: 'Full Stack Developer',
    period: '2017 - 2020',
    description: 'Developed enterprise web applications and microservices',
    technologies: ['Vue.js', 'Express', 'PostgreSQL', 'Redis']
  }
])
</script>

<template>
  <section>
    <h2 class="text-3xl font-bold text-gray-900 dark:text-white mb-6">
      {{ t('experiences.title') }}
    </h2>
    
    <div class="space-y-6">
      <UCard v-for="exp in experiences" :key="exp.id">
        <template #header>
          <div class="flex justify-between items-start">
            <div>
              <h3 class="text-xl font-semibold text-gray-900 dark:text-white">
                {{ exp.position }}
              </h3>
              <p class="text-gray-600 dark:text-gray-400">
                {{ exp.company }}
              </p>
            </div>
            <UBadge color="primary" variant="subtle">
              {{ exp.period }}
            </UBadge>
          </div>
        </template>

        <p class="text-gray-700 dark:text-gray-300 mb-4">
          {{ exp.description }}
        </p>

        <div class="flex flex-wrap gap-2">
          <UBadge 
            v-for="tech in exp.technologies" 
            :key="tech"
            color="gray"
            variant="soft"
          >
            {{ tech }}
          </UBadge>
        </div>
      </UCard>
    </div>
  </section>
</template>

<i18n lang="json">
{
  "en": {
    "experiences": {
      "title": "Professional Experience"
    }
  },
  "de": {
    "experiences": {
      "title": "Berufserfahrung"
    }
  }
}
</i18n>
