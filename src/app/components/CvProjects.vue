<script setup lang="ts">
const { t } = useI18n()

// This would eventually come from an API with multi-tenant support
const projects = ref([
  {
    id: 1,
    name: 'E-Commerce Platform',
    type: 'Web Application',
    period: '2022 - 2023',
    description: 'Built a scalable e-commerce platform with microservices architecture, supporting millions of transactions per day',
    technologies: ['Vue.js', 'Node.js', 'MongoDB', 'Redis', 'Docker'],
    screenshots: ['/images/projects/ecommerce-1.png', '/images/projects/ecommerce-2.png'],
    logo: '/images/projects/ecommerce-logo.png'
  },
  {
    id: 2,
    name: 'Mobile Banking App',
    type: 'Mobile Application',
    period: '2021 - 2022',
    description: 'Developed a secure mobile banking application with biometric authentication and real-time transaction monitoring',
    technologies: ['React Native', 'TypeScript', 'GraphQL', 'PostgreSQL'],
    images: ['/images/projects/banking-app.png']
  },
  {
    id: 3,
    name: 'Cloud Infrastructure',
    type: 'DevOps Project',
    period: '2020 - 2021',
    description: 'Designed and implemented cloud infrastructure for enterprise applications using Infrastructure as Code',
    technologies: ['Kubernetes', 'Terraform', 'AWS', 'Jenkins', 'Prometheus'],
    logo: '/images/projects/cloud-logo.png'
  }
])
</script>

<template>
  <section>
    <h2 class="text-3xl font-bold text-gray-900 dark:text-white print:text-black mb-6">
      {{ t('projects.title') }}
    </h2>
    
    <div class="space-y-6">
      <UCard 
        v-for="project in projects" 
        :key="project.id"
        :data-item-id="`project-${project.id}`"
        class="print:!shadow-none print:!border print:!border-gray-300"
      >
        <template #header>
          <div class="flex justify-between items-start">
            <div class="flex-1">
              <div class="flex items-center gap-3 mb-1">
                <!-- Logo if available -->
                <img 
                  v-if="project.logo" 
                  :src="project.logo" 
                  :alt="project.name"
                  class="w-10 h-10 rounded object-contain print:w-8 print:h-8"
                  @error="(e) => (e.target as HTMLImageElement).style.display = 'none'"
                />
                <h3 class="text-xl font-semibold text-gray-900 dark:text-white print:text-black">
                  {{ project.name }}
                </h3>
              </div>
              <p class="text-gray-600 dark:text-gray-400 print:text-gray-700">
                {{ project.type }}
              </p>
            </div>
            <UBadge color="primary" variant="subtle" class="print:!bg-gray-100 print:!text-black">
              {{ project.period }}
            </UBadge>
          </div>
        </template>

        <!-- Screenshots/Images -->
        <div 
          v-if="project.screenshots || project.images" 
          class="mb-4 flex flex-wrap gap-3"
        >
          <img 
            v-for="(img, idx) in [...(project.screenshots || []), ...(project.images || [])]"
            :key="idx"
            :src="img"
            :alt="`${project.name} screenshot ${idx + 1}`"
            class="max-w-full h-auto rounded-lg shadow-sm max-h-48 object-cover print:max-h-32"
            @error="(e) => (e.target as HTMLImageElement).style.display = 'none'"
          />
        </div>

        <!-- Description -->
        <p class="text-gray-700 dark:text-gray-300 print:text-black mb-4">
          {{ project.description }}
        </p>

        <!-- Technologies -->
        <div class="flex flex-wrap gap-2">
          <UBadge 
            v-for="tech in project.technologies" 
            :key="tech"
            color="gray"
            variant="soft"
            class="print:!bg-gray-100 print:!text-black"
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
    "projects": {
      "title": "Projects"
    }
  },
  "de": {
    "projects": {
      "title": "Projekte"
    }
  }
}
</i18n>
