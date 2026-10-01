<script setup lang="ts">
// Frame of the legal pages (/legal/imprint, /legal/privacy, /legal/terms): top bar, title, text, footer.
// The texts themselves live in the page components (one block per language).
const props = defineProps<{ title: string, updated?: string }>()

const { t } = useI18n()
const operator = useLegalOperator()
useSplashScreen().hideSplash()
useSeoMeta({ title: () => props.title })
</script>

<template>
  <div class="min-h-screen bg-white text-gray-900 dark:bg-gray-900 dark:text-white">
    <PublicTopBar />
    <main class="mx-auto max-w-3xl px-4 py-12">
      <h1 class="text-3xl font-bold sm:text-4xl">{{ title }}</h1>
      <p v-if="updated" class="mt-2 text-sm text-gray-500 dark:text-gray-400">{{ t('legalPage.updated', { date: updated }) }}</p>
      <p
        v-if="!operator.configured" role="note" data-testid="legal-not-configured"
        class="mt-6 rounded-lg border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900 dark:border-amber-700 dark:bg-amber-950 dark:text-amber-100"
      >
        {{ t('legalPage.notConfigured') }}
      </p>
      <article class="legal-text mt-8">
        <slot :operator="operator" />
      </article>
    </main>
    <PublicFooter />
  </div>
</template>

<style scoped>
.legal-text :deep(h2) {
  margin: 2.25rem 0 0.75rem;
  font-size: 1.25rem;
  font-weight: 700;
}
.legal-text :deep(h3) {
  margin: 1.5rem 0 0.5rem;
  font-weight: 600;
}
.legal-text :deep(p),
.legal-text :deep(ul),
.legal-text :deep(address) {
  margin: 0 0 0.875rem;
  line-height: 1.65;
  color: rgb(55 65 81);
  font-style: normal;
}
.legal-text :deep(ul) {
  list-style: disc;
  padding-left: 1.25rem;
}
.legal-text :deep(li) {
  margin-bottom: 0.25rem;
}
.legal-text :deep(a) {
  color: rgb(29 78 216);
  text-decoration: underline;
}
.legal-text :deep(code) {
  font-size: 0.875em;
  padding: 0 0.25rem;
  border-radius: 0.25rem;
  background: rgb(243 244 246);
}
.legal-text :deep(dl) {
  display: grid;
  grid-template-columns: max-content 1fr;
  gap: 0.25rem 1rem;
  margin: 0 0 0.875rem;
}
.legal-text :deep(dt) {
  font-weight: 600;
}
@media (prefers-color-scheme: dark) {
  .legal-text :deep(p),
  .legal-text :deep(ul),
  .legal-text :deep(address) {
    color: rgb(209 213 219);
  }
  .legal-text :deep(a) {
    color: rgb(147 197 253);
  }
  .legal-text :deep(code) {
    background: rgb(31 41 55);
  }
}
@media (max-width: 480px) {
  .legal-text :deep(dl) {
    grid-template-columns: 1fr;
  }
}
</style>

<i18n lang="json">
{
  "en": {
    "legalPage": {
      "updated": "Last updated: {date}",
      "notConfigured": "The operator details of this site have not been configured yet."
    }
  },
  "de": {
    "legalPage": {
      "updated": "Stand: {date}",
      "notConfigured": "Die Angaben zum Betreiber dieser Seite sind noch nicht hinterlegt."
    }
  }
}
</i18n>
