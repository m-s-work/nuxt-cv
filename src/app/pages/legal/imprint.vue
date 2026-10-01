<script setup lang="ts">
// Imprint (Impressum): information per § 5 ECG, § 14 UGB and disclosure per § 25 MedienG (Austria).
// Operator details come from NUXT_PUBLIC_LEGAL_* (nuxt.config.ts, utils/legal.ts).
const { t, locale } = useI18n()
</script>

<template>
  <!--
    TEMPLATE TEXT – NOT LEGAL ADVICE. The operator MUST review and adapt this imprint with a lawyer before going
    live (e.g. trade licence / Gewerbebehörde, company register number and court, chamber membership if applicable).
  -->
  <LegalPage v-slot="{ operator }" :title="t('imprint.title')">
    <template v-if="locale === 'de'">
      <h2>Medieninhaber und Diensteanbieter</h2>
      <address data-testid="legal-operator">
        {{ operator.name }}<br>
        <template v-for="line in operator.address" :key="line">{{ line }}<br></template>
        {{ operator.country }}
      </address>
      <dl>
        <dt>E-Mail</dt>
        <dd><a v-if="operator.email" :href="`mailto:${operator.email}`">{{ operator.email }}</a></dd>
        <template v-if="operator.vatId">
          <dt>UID-Nummer</dt>
          <dd data-testid="legal-vat">{{ operator.vatId }}</dd>
        </template>
      </dl>

      <h2>Unternehmensgegenstand</h2>
      <p>Betrieb einer Online-Plattform, auf der Nutzer:innen ihren Lebenslauf erstellen, als Webseite und PDF veröffentlichen und über persönliche Einladungslinks mit ausgewählten Personen teilen.</p>

      <h2>Offenlegung gemäß § 25 Mediengesetz</h2>
      <p>Medieninhaber: siehe oben. Grundlegende Richtung: Information über den Dienst und seine Preise sowie Bereitstellung der Plattform. Die Lebensläufe auf der Plattform sind Inhalte ihrer jeweiligen Inhaber:innen.</p>

      <h2>Inhalte der Nutzer:innen</h2>
      <p>Für Lebensläufe und Dateien, die Nutzer:innen hochladen, sind diese selbst verantwortlich; wir speichern sie als Hostingdienst (§ 16 ECG). Rechtswidrige Inhalte melde bitte per E-Mail an die oben genannte Adresse – wir prüfen jede Meldung und entfernen rechtswidrige Inhalte unverzüglich.</p>

      <h2>Kontakt</h2>
      <p>Am schnellsten erreichst du uns per E-Mail. Zahlungen, Rechnungen und Rückerstattungen wickelt unser Wiederverkäufer Paddle ab (siehe <NuxtLinkLocale to="/legal/terms">Nutzungsbedingungen</NuxtLinkLocale>).</p>
    </template>

    <template v-else>
      <h2>Media owner and service provider</h2>
      <address data-testid="legal-operator">
        {{ operator.name }}<br>
        <template v-for="line in operator.address" :key="line">{{ line }}<br></template>
        {{ operator.country }}
      </address>
      <dl>
        <dt>E-mail</dt>
        <dd><a v-if="operator.email" :href="`mailto:${operator.email}`">{{ operator.email }}</a></dd>
        <template v-if="operator.vatId">
          <dt>VAT ID</dt>
          <dd data-testid="legal-vat">{{ operator.vatId }}</dd>
        </template>
      </dl>

      <h2>Business purpose</h2>
      <p>Operation of an online platform where users create their CV, publish it as a web page and PDF and share it with selected people through personal invite links.</p>

      <h2>Disclosure (§ 25 Austrian Media Act)</h2>
      <p>Media owner: see above. Editorial line: information about the service and its prices, and provision of the platform. The CVs on the platform are content of their respective owners.</p>

      <h2>User content</h2>
      <p>Users are responsible for the CVs and files they upload; we store them as a hosting service (§ 16 Austrian E-Commerce Act). Please report illegal content by e-mail to the address above – we review every report and remove illegal content without delay.</p>

      <h2>Contact</h2>
      <p>E-mail is the fastest way to reach us. Payments, invoices and refunds are handled by our reseller Paddle (see the <NuxtLinkLocale to="/legal/terms">terms of service</NuxtLinkLocale>).</p>
    </template>
  </LegalPage>
</template>

<i18n lang="json">
{
  "en": { "imprint": { "title": "Imprint" } },
  "de": { "imprint": { "title": "Impressum" } }
}
</i18n>
