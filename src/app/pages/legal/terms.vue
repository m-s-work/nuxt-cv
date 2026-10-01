<script setup lang="ts">
// Terms of service (Free plan and prepaid Pro passes, docs/REQUIREMENTS_SAAS.md §4). Operator details come from
// NUXT_PUBLIC_LEGAL_*. Consumer-friendly wording for Austria (KSchG, FAGG); review required, see the comment below.
const { t, locale } = useI18n()
const updated = computed(() => new Intl.DateTimeFormat(locale.value, { dateStyle: 'long' }).format(new Date(Date.UTC(2026, 9, 1))))
</script>

<template>
  <!--
    TEMPLATE TEXT – NOT LEGAL ADVICE. The operator MUST review and adapt these terms with a lawyer before going live
    (in particular liability, withdrawal rights for digital services, notice periods and Paddle's current buyer terms).
  -->
  <LegalPage v-slot="{ operator }" :title="t('terms.title')" :updated="updated">
    <template v-if="locale === 'de'">
      <h2>1. Anbieter und Geltungsbereich</h2>
      <p>Diese Nutzungsbedingungen gelten für die Nutzung dieser Plattform, betrieben von {{ operator.name || '…' }} ({{ operator.country }}), Kontakt: <a v-if="operator.email" :href="`mailto:${operator.email}`">{{ operator.email }}</a> (weitere Angaben im <NuxtLinkLocale to="/legal/imprint">Impressum</NuxtLinkLocale>). Mit der Registrierung stimmst du ihnen zu. Wie wir mit deinen Daten umgehen, beschreibt die <NuxtLinkLocale to="/legal/privacy">Datenschutzerklärung</NuxtLinkLocale>.</p>

      <h2>2. Leistungen</h2>
      <p>Auf der Plattform kannst du deinen Lebenslauf erstellen, ihn als Webseite und PDF veröffentlichen und über persönliche Einladungslinks mit ausgewählten Personen teilen. Pro Profil und Einladung legst du fest, welche Teile sichtbar sind. Wir entwickeln die Plattform weiter und dürfen Funktionen ändern, solange der Kern der vereinbarten Leistung erhalten bleibt und die Änderung für dich zumutbar ist.</p>

      <h2>3. Gratis-Plan</h2>
      <p>Der Gratis-Plan ist unbefristet und kostenlos. Er umfasst die auf der <NuxtLinkLocale to="/pricing">Preisseite</NuxtLinkLocale> genannten Leistungen und Grenzen (z. B. Anzahl aktiver Einladungslinks, Speicherplatz).</p>

      <h2>4. Pro-Pässe</h2>
      <ul>
        <li>Pro wird als vorausbezahlter Pass für eine feste Anzahl an Tagen verkauft. Es gibt kein Abo und keine automatische Verlängerung; eine Kündigung ist nicht nötig.</li>
        <li>Kaufst du während einer aktiven Pro-Zeit einen weiteren Pass, verlängert sich deine Pro-Zeit um dessen Tage.</li>
        <li>Alle Preise verstehen sich inklusive der gesetzlichen Umsatzsteuer.</li>
        <li>Verkäufer der Pässe ist unser Wiederverkäufer Paddle.com Market Limited (Merchant of Record). Paddle wickelt die Zahlung ab, stellt die Rechnung aus und bearbeitet Rückerstattungen; dafür gelten die Kaufbedingungen von Paddle (Paddle Buyer Terms), einschließlich der gesetzlichen Rücktrittsrechte für Verbraucher:innen.</li>
        <li>Endet die Pro-Zeit, wird nichts gelöscht. Bestehende Einladungslinks funktionieren bis zu ihrem Ablauf weiter; Pro-Leistungen wie die eigene Domain pausieren, bis du wieder Pro hast.</li>
      </ul>

      <h2>5. Deine Inhalte</h2>
      <ul>
        <li>Du darfst nur deinen eigenen Lebenslauf veröffentlichen und nur Inhalte hochladen, an denen du die nötigen Rechte hast.</li>
        <li>Rechtswidrige Inhalte sind verboten, insbesondere solche, die Rechte Dritter (z. B. Urheber-, Marken- oder Persönlichkeitsrechte) verletzen, sowie irreführende Angaben über andere Personen oder Unternehmen. Die Plattform darf nicht für Spam, Schadsoftware oder Angriffe genutzt werden.</li>
        <li>Deine Inhalte bleiben deine. Du räumst uns nur die Rechte ein, die nötig sind, um sie zu speichern, darzustellen und so zu veröffentlichen, wie du es festlegst.</li>
        <li>Erhalten wir Hinweise auf rechtswidrige Inhalte, dürfen wir diese sperren und dich darüber informieren.</li>
      </ul>

      <h2>6. Verfügbarkeit</h2>
      <p>Wir bemühen uns um eine hohe Verfügbarkeit, können aber keinen unterbrechungsfreien Betrieb zusagen. Wartungen kündigen wir nach Möglichkeit vorher an. Bitte bewahre eine eigene Kopie deiner Daten auf – den Export findest du im Dashboard. Gesetzliche Gewährleistungsrechte bleiben unberührt.</p>

      <h2>7. Haftung</h2>
      <p>Wir haften unbeschränkt für Schäden, die wir vorsätzlich oder grob fahrlässig verursachen, sowie für Personenschäden. Bei leichter Fahrlässigkeit haften wir nur für die Verletzung wesentlicher Vertragspflichten und nur für den vorhersehbaren, typischen Schaden. Zwingende gesetzliche Haftungsbestimmungen, insbesondere nach dem Produkthaftungsgesetz und dem Konsumentenschutzgesetz, bleiben unberührt.</p>

      <h2>8. Kündigung und Löschung</h2>
      <p>Du kannst dein Konto jederzeit im Dashboard löschen; damit werden dein Lebenslauf und deine Einladungen gelöscht. Wir können ein Gratis-Konto mit einer Frist von zwei Monaten kündigen. Bei schwerwiegenden Verstößen gegen diese Bedingungen können wir dein Konto sofort sperren oder kündigen. Kündigen wir ohne einen solchen Grund, erstatten wir den Anteil einer bezahlten, noch nicht verbrauchten Pro-Zeit.</p>

      <h2>9. Änderungen dieser Bedingungen</h2>
      <p>Änderungen teilen wir dir mindestens 30 Tage vor ihrem Inkrafttreten per E-Mail mit. Bist du nicht einverstanden, kannst du dein Konto bis dahin löschen; eine bezahlte, noch nicht verbrauchte Pro-Zeit erstatten wir dann anteilig.</p>

      <h2>10. Anwendbares Recht</h2>
      <p>Es gilt österreichisches Recht unter Ausschluss der Verweisungsnormen und des UN-Kaufrechts. Bist du Verbraucher:in, bleiben die zwingenden Schutzbestimmungen des Staates, in dem du deinen gewöhnlichen Aufenthalt hast, unberührt, und du kannst auch dort klagen und geklagt werden.</p>
    </template>

    <template v-else>
      <h2>1. Provider and scope</h2>
      <p>These terms apply to the use of this platform, operated by {{ operator.name || '…' }} ({{ operator.country }}), contact: <a v-if="operator.email" :href="`mailto:${operator.email}`">{{ operator.email }}</a> (more details in the <NuxtLinkLocale to="/legal/imprint">imprint</NuxtLinkLocale>). By registering you accept them. How we handle your data is described in the <NuxtLinkLocale to="/legal/privacy">privacy policy</NuxtLinkLocale>.</p>

      <h2>2. Service</h2>
      <p>On the platform you can create your CV, publish it as a web page and PDF and share it with selected people through personal invite links. Per profile and invite you decide which parts are visible. We keep developing the platform and may change features as long as the core of the agreed service is kept and the change is reasonable for you.</p>

      <h2>3. Free plan</h2>
      <p>The free plan is free of charge and not limited in time. It includes the features and limits shown on the <NuxtLinkLocale to="/pricing">pricing page</NuxtLinkLocale> (e.g. number of active invite links, storage).</p>

      <h2>4. Pro passes</h2>
      <ul>
        <li>Pro is sold as a prepaid pass for a fixed number of days. There is no subscription and no automatic renewal; nothing needs to be cancelled.</li>
        <li>If you buy another pass while Pro is active, your Pro time is extended by its days.</li>
        <li>All prices include the statutory VAT.</li>
        <li>The passes are sold by our reseller Paddle.com Market Limited (merchant of record). Paddle handles the payment, issues the invoice and processes refunds under Paddle's buyer terms, including the statutory withdrawal rights of consumers.</li>
        <li>When your Pro time ends, nothing is deleted. Existing invite links keep working until they expire; Pro features such as your own domain pause until you have Pro again.</li>
      </ul>

      <h2>5. Your content</h2>
      <ul>
        <li>You may only publish your own CV and only upload content you have the necessary rights to.</li>
        <li>Illegal content is not allowed, in particular content that infringes the rights of others (e.g. copyright, trademarks, personality rights) and misleading statements about other people or companies. The platform must not be used for spam, malware or attacks.</li>
        <li>Your content remains yours. You only grant us the rights needed to store it, display it and publish it the way you decide.</li>
        <li>If we are notified of illegal content, we may block it and will inform you.</li>
      </ul>

      <h2>6. Availability</h2>
      <p>We aim for high availability but cannot promise uninterrupted operation. Where possible we announce maintenance in advance. Please keep your own copy of your data – the export is in the dashboard. Statutory warranty rights remain unaffected.</p>

      <h2>7. Liability</h2>
      <p>We are liable without limitation for damage caused intentionally or by gross negligence, and for personal injury. For slight negligence we are only liable for the breach of essential contractual obligations and only for the foreseeable, typical damage. Mandatory statutory liability, in particular under the Austrian Product Liability Act and Consumer Protection Act, remains unaffected.</p>

      <h2>8. Termination and deletion</h2>
      <p>You can delete your account in the dashboard at any time; this deletes your CV and your invites. We may terminate a free account with two months' notice. In case of serious breaches of these terms we may block or terminate your account immediately. If we terminate without such a reason, we refund the unused part of paid Pro time.</p>

      <h2>9. Changes to these terms</h2>
      <p>We notify you of changes by e-mail at least 30 days before they take effect. If you do not agree, you can delete your account until then; we then refund the unused part of paid Pro time pro rata.</p>

      <h2>10. Governing law</h2>
      <p>Austrian law applies, excluding its conflict-of-law rules and the UN Convention on Contracts for the International Sale of Goods. If you are a consumer, the mandatory consumer protection rules of the country of your habitual residence remain unaffected, and you can also sue and be sued there.</p>
    </template>
  </LegalPage>
</template>

<i18n lang="json">
{
  "en": { "terms": { "title": "Terms of service" } },
  "de": { "terms": { "title": "Nutzungsbedingungen" } }
}
</i18n>
