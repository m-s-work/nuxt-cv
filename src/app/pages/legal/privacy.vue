<script setup lang="ts">
// Privacy policy of the platform (GDPR Art. 13/14). Operator details come from NUXT_PUBLIC_LEGAL_*.
// Public page: the section about CV visits describes data processing factually (required disclosure), it does
// not present analytics as a feature (REQUIREMENTS_ACCESS_AND_TENANCY.md R11.4).
const { t, locale } = useI18n()
const updated = computed(() => new Intl.DateTimeFormat(locale.value, { dateStyle: 'long' }).format(new Date(Date.UTC(2026, 9, 1))))
</script>

<template>
  <!--
    TEMPLATE TEXT – NOT LEGAL ADVICE. The operator MUST review and adapt this privacy policy with a lawyer before
    going live: hosting provider and location, log retention, the exact cookie list, transfer mechanisms (Resend,
    sign-in providers) and the processing agreement with CV owners (Art. 28 GDPR).
  -->
  <LegalPage v-slot="{ operator }" :title="t('privacy.title')" :updated="updated">
    <template v-if="locale === 'de'">
      <p>Diese Datenschutzerklärung beschreibt, welche personenbezogenen Daten wir verarbeiten, wenn du diese Plattform nutzt – als Inhaber:in eines Kontos, als Besucher:in eines Lebenslaufs oder als Besucher:in dieser Webseite.</p>

      <h2>1. Verantwortlicher</h2>
      <address>
        {{ operator.name }}<br>
        <template v-for="line in operator.address" :key="line">{{ line }}<br></template>
        {{ operator.country }}<br>
        E-Mail: <a v-if="operator.email" :href="`mailto:${operator.email}`">{{ operator.email }}</a>
      </address>
      <p>Für Anfragen zum Datenschutz erreichst du uns unter dieser E-Mail-Adresse.</p>

      <h2>2. Dein Konto</h2>
      <p>Für dein Konto verarbeiten wir deine E-Mail-Adresse, deinen Namen, dein Profilbild (Adresse des Bildes beim Anmeldedienst), die Anmeldedienste, mit denen du dich angemeldet hast, samt der Kennung, die dich dort identifiziert, den Zeitpunkt deiner Registrierung und letzten Anmeldung sowie deinen Plan (Gratis oder Pro, Pro-Laufzeit). Rechtsgrundlage ist die Erfüllung des Vertrags mit dir (Art. 6 Abs. 1 lit. b DSGVO). Wir speichern diese Daten, bis du dein Konto löschst.</p>

      <h2>3. Anmeldung über Google, Microsoft, GitHub oder LinkedIn</h2>
      <p>Wählst du eine Anmeldung über einen dieser Dienste, wirst du zu ihm weitergeleitet und meldest dich dort an. Für diesen Anmeldevorgang ist der jeweilige Anbieter eigenständiger Verantwortlicher; es gilt seine Datenschutzerklärung:</p>
      <ul>
        <li>Google Ireland Limited, Gordon House, Barrow Street, Dublin 4, Irland</li>
        <li>Microsoft Ireland Operations Limited, One Microsoft Place, South County Business Park, Leopardstown, Dublin 18, Irland</li>
        <li>GitHub, Inc., 88 Colin P. Kelly Jr. Street, San Francisco, CA 94107, USA</li>
        <li>LinkedIn Ireland Unlimited Company, Wilton Plaza, Wilton Place, Dublin 2, Irland</li>
      </ul>
      <p>Wir erhalten vom Anbieter nur deinen Namen, deine E-Mail-Adresse (und ob sie bestätigt ist), dein Profilbild und eine Kennung deines Kontos dort. Meldest du dich mit einem Anmeldelink per E-Mail an, schicken wir diesen Link über unseren E-Mail-Dienstleister (Punkt 8); er ist 15 Minuten gültig und nur einmal verwendbar.</p>

      <h2>4. Dein Lebenslauf</h2>
      <p>Wir speichern die Inhalte und Dateien, die du für deinen Lebenslauf eingibst oder hochlädst (z. B. Werdegang, Foto, Kontaktdaten, Logos), und veröffentlichen sie so, wie du es festlegst: Du bestimmst pro Profil und Einladung, welche Teile sichtbar sind; ausgeblendete Daten werden nicht an Besucher:innen übertragen. Rechtsgrundlage ist die Vertragserfüllung (Art. 6 Abs. 1 lit. b DSGVO). Die Daten bleiben gespeichert, bis du sie änderst oder dein Konto löschst. Für Inhalte über andere Personen bist du selbst verantwortlich.</p>

      <h2>5. Einladungslinks</h2>
      <p>Zu jeder Einladung speichern wir die Bezeichnung, die du vergibst, das Profil, Ablaufdatum und Nutzungslimit, die Anzahl der Einlösungen und den Zeitpunkt der letzten Einlösung. Öffnet jemand einen Einladungslink, setzen wir das Cookie <code>cv_access</code>, damit der Lebenslauf in diesem Browser geöffnet bleibt. Es enthält einen signierten Verweis auf die Einladung, keine weiteren Daten. Für die Einladung erzeugte PDFs werden zwischengespeichert, bis sich der Lebenslauf oder die Einladung ändert.</p>

      <h2>6. Besuche von Lebensläufen mit eingeschalteter Besuchsstatistik</h2>
      <p>Inhaber:innen eines Lebenslaufs können für ihren Lebenslauf eine Erfassung von Besuchen einschalten. Besucher:innen werden dann vorher in einem Dialog auf dem Lebenslauf um ihre Einwilligung gebeten; der Dialog nennt die Inhaberin bzw. den Inhaber, alle erfassten Daten und die Speicherdauer. Ohne Einwilligung werden keine solchen Daten erfasst. Für diese Verarbeitung ist die Inhaberin bzw. der Inhaber des Lebenslaufs Verantwortlicher (Rechtsgrundlage: Einwilligung, Art. 6 Abs. 1 lit. a DSGVO); wir verarbeiten die Daten in ihrem bzw. seinem Auftrag als Auftragsverarbeiter (Art. 28 DSGVO) und nutzen sie nicht für eigene Zwecke. Die Entscheidung wird im Cookie <code>cv_consent</code> gespeichert, nach einer Einwilligung zusätzlich eine zufällige Browser-Kennung im Cookie <code>cv_vid</code>. Die Einwilligung kann jederzeit über „Datenschutz“ am Ende des Lebenslaufs widerrufen werden. Anfragen zu diesen Daten richte bitte an die im Dialog genannte Kontaktadresse; wir unterstützen die Inhaber:innen bei deren Beantwortung.</p>

      <h2>7. Zahlungen über Paddle</h2>
      <p>Pro-Pässe verkauft Paddle.com Market Limited, Judd House, 18–29 Mora Street, London EC1V 8BT, Vereinigtes Königreich, als Wiederverkäufer (Merchant of Record). Der Kaufvertrag kommt mit Paddle zustande; Paddle verarbeitet deine Zahlungs- und Rechnungsdaten als eigenständiger Verantwortlicher nach seiner Datenschutzerklärung. Deine Kartendaten sehen wir nie. Von Paddle erhalten wir die Transaktionskennung, den gekauften Pass, Betrag, Währung und Status sowie unsere Kennung deines Kontos, um deine Pro-Zeit gutzuschreiben (Art. 6 Abs. 1 lit. b DSGVO). Für das Vereinigte Königreich besteht ein Angemessenheitsbeschluss der EU-Kommission. Zahlungsdatensätze bewahren wir wegen gesetzlicher Aufbewahrungspflichten sieben Jahre auf (§ 132 BAO, Art. 6 Abs. 1 lit. c DSGVO), nach Löschung deines Kontos ohne Bezug zu dir.</p>

      <h2>8. E-Mail-Versand</h2>
      <p>Anmeldelinks und Service-E-Mails versenden wir über Resend (Resend, Inc., USA) als Auftragsverarbeiter. Dabei werden deine E-Mail-Adresse und der Inhalt der E-Mail übermittelt. Die Übermittlung in die USA stützt sich auf Standardvertragsklauseln der EU-Kommission (Art. 46 DSGVO) bzw. – soweit der Anbieter zertifiziert ist – auf das EU-US Data Privacy Framework.</p>

      <h2>9. Hosting und Server-Protokolle</h2>
      <p>Die Plattform läuft auf Servern eines Hosting-Anbieters, der als Auftragsverarbeiter für uns tätig ist. Bei jedem Aufruf protokollieren unsere Server technisch notwendige Daten: IP-Adresse, Datum und Uhrzeit, aufgerufene Adresse, Statuscode, Browser-Kennung (User-Agent) und verweisende Seite. Wir verwenden sie, um den Betrieb abzusichern und Fehler zu finden (berechtigtes Interesse, Art. 6 Abs. 1 lit. f DSGVO), und löschen sie nach spätestens 30 Tagen, außer sie werden zur Aufklärung eines Sicherheitsvorfalls benötigt. Die öffentlichen Seiten laden keine Schriften, Skripte oder Bilder von Dritten; nur der Bezahlvorgang im Dashboard lädt das Skript von Paddle.</p>

      <h2>10. Cookies</h2>
      <p>Wir verwenden nur Cookies, die für die jeweilige Funktion nötig sind (§ 165 Abs. 3 TKG 2021), keine Werbe-Cookies:</p>
      <dl>
        <dt><code>cv_session</code></dt><dd>Hält dich in deinem Konto angemeldet; 30 Tage, verlängert sich bei Nutzung.</dd>
        <dt><code>cv_access</code></dt><dd>Zugang zu einem Lebenslauf über einen Einladungslink; bis zu 365 Tage.</dd>
        <dt><code>i18n_redirected</code></dt><dd>Merkt sich die gewählte Sprache.</dd>
        <dt><code>cv_consent</code>, <code>cv_vid</code></dt><dd>Nur auf Lebensläufen mit eingeschalteter Besuchsstatistik, siehe Punkt 6.</dd>
      </dl>
      <p>Während der Anmeldung über einen Anmeldedienst setzen wir zusätzlich kurzlebige technische Cookies (wenige Minuten), die den Anmeldevorgang absichern.</p>

      <h2>11. Deine Rechte</h2>
      <p>Du hast das Recht auf Auskunft (Art. 15 DSGVO), Berichtigung (Art. 16), Löschung (Art. 17), Einschränkung der Verarbeitung (Art. 18), Mitteilung an Empfänger (Art. 19), Datenübertragbarkeit (Art. 20) und Widerspruch (Art. 21). Eine Einwilligung kannst du jederzeit mit Wirkung für die Zukunft widerrufen. Wende dich dazu an die E-Mail-Adresse unter Punkt 1.</p>
      <p>Du kannst dich außerdem bei einer Aufsichtsbehörde beschweren, in Österreich bei der Datenschutzbehörde, Barichgasse 40–42, 1030 Wien, <a href="https://www.dsb.gv.at" target="_blank" rel="noopener">www.dsb.gv.at</a>.</p>

      <h2>12. Export und Löschung im Dashboard</h2>
      <p>Im Dashboard kannst du jederzeit alle Daten deines Kontos als ZIP-Datei herunterladen (Kontodaten, Zahlungen und alle Dateien deines Lebenslaufs). Dort kannst du dein Konto auch selbst löschen: Wir löschen dann dein Konto, deine Anmeldungen, deinen Lebenslauf mit allen Dateien, deine Einladungen, zwischengespeicherte PDFs und die Besuchsdaten deines Lebenslaufs. Zahlungsdatensätze bleiben ohne Bezug zu dir erhalten (Punkt 7).</p>

      <h2>13. Keine automatisierten Entscheidungen</h2>
      <p>Wir treffen keine automatisierten Entscheidungen im Sinne von Art. 22 DSGVO und erstellen keine Profile zu Werbezwecken.</p>

      <h2>14. Änderungen</h2>
      <p>Ändert sich die Plattform, passen wir diese Erklärung an. Über wesentliche Änderungen informieren wir Kontoinhaber:innen per E-Mail.</p>
    </template>

    <template v-else>
      <p>This privacy policy describes which personal data we process when you use this platform – as an account holder, as a visitor of a CV or as a visitor of this website.</p>

      <h2>1. Controller</h2>
      <address>
        {{ operator.name }}<br>
        <template v-for="line in operator.address" :key="line">{{ line }}<br></template>
        {{ operator.country }}<br>
        E-mail: <a v-if="operator.email" :href="`mailto:${operator.email}`">{{ operator.email }}</a>
      </address>
      <p>Please send privacy requests to this e-mail address.</p>

      <h2>2. Your account</h2>
      <p>For your account we process your e-mail address, your name, your profile picture (the picture's address at the sign-in provider), the sign-in providers you used together with the id that identifies you there, when you registered and last signed in, and your plan (Free or Pro, Pro end date). The legal basis is the performance of our contract with you (Art. 6(1)(b) GDPR). We keep these data until you delete your account.</p>

      <h2>3. Sign-in with Google, Microsoft, GitHub or LinkedIn</h2>
      <p>If you sign in with one of these services, you are redirected to it and sign in there. For this sign-in, the provider is a separate controller and its own privacy policy applies:</p>
      <ul>
        <li>Google Ireland Limited, Gordon House, Barrow Street, Dublin 4, Ireland</li>
        <li>Microsoft Ireland Operations Limited, One Microsoft Place, South County Business Park, Leopardstown, Dublin 18, Ireland</li>
        <li>GitHub, Inc., 88 Colin P. Kelly Jr. Street, San Francisco, CA 94107, USA</li>
        <li>LinkedIn Ireland Unlimited Company, Wilton Plaza, Wilton Place, Dublin 2, Ireland</li>
      </ul>
      <p>From the provider we only receive your name, your e-mail address (and whether it is verified), your profile picture and an id of your account there. If you sign in with a link by e-mail, we send it through our e-mail service provider (section 8); the link is valid for 15 minutes and can be used once.</p>

      <h2>4. Your CV</h2>
      <p>We store the content and files you enter or upload for your CV (e.g. career, photo, contact details, logos) and publish them as you decide: per profile and invite you choose which parts are visible; hidden data are not sent to visitors. The legal basis is the performance of our contract (Art. 6(1)(b) GDPR). The data are kept until you change them or delete your account. You are responsible for any content about other people.</p>

      <h2>5. Invite links</h2>
      <p>For each invite we store the label you give it, the profile, expiry and usage limit, how often it was redeemed and when it was last redeemed. When someone opens an invite link, we set the cookie <code>cv_access</code> so the CV stays open in that browser. It contains a signed reference to the invite and nothing else. PDFs created for an invite are cached until the CV or the invite changes.</p>

      <h2>6. Visits of CVs with visit statistics switched on</h2>
      <p>CV owners can switch on the recording of visits for their CV. Visitors are then asked for their consent first, in a dialog on the CV that names the owner, every recorded data point and how long it is kept. Without consent no such data are recorded. For this processing the CV owner is the controller (legal basis: consent, Art. 6(1)(a) GDPR); we process the data on the owner's behalf as a processor (Art. 28 GDPR) and do not use them for our own purposes. The choice is stored in the cookie <code>cv_consent</code>; after consent, a random browser id is additionally stored in the cookie <code>cv_vid</code>. Consent can be withdrawn at any time via "Privacy" at the end of the CV. Please send requests about these data to the contact named in the dialog; we support CV owners in answering them.</p>

      <h2>7. Payments via Paddle</h2>
      <p>Pro passes are sold by Paddle.com Market Limited, Judd House, 18–29 Mora Street, London EC1V 8BT, United Kingdom, as reseller (merchant of record). The purchase contract is concluded with Paddle; Paddle processes your payment and billing data as a separate controller under its own privacy policy. We never see your card details. From Paddle we receive the transaction id, the pass bought, amount, currency and status, and our id of your account, so we can credit your Pro time (Art. 6(1)(b) GDPR). The United Kingdom is covered by an adequacy decision of the European Commission. Because of statutory retention duties we keep payment records for seven years (§ 132 Austrian Federal Fiscal Code, Art. 6(1)(c) GDPR); after you delete your account, without reference to you.</p>

      <h2>8. E-mail delivery</h2>
      <p>We send sign-in links and service e-mails through Resend (Resend, Inc., USA) as our processor; your e-mail address and the content of the e-mail are transmitted. The transfer to the USA is based on the European Commission's standard contractual clauses (Art. 46 GDPR) or, where the provider is certified, the EU-US Data Privacy Framework.</p>

      <h2>9. Hosting and server logs</h2>
      <p>The platform runs on servers of a hosting provider acting as our processor. For every request our servers log technically necessary data: IP address, date and time, requested address, status code, browser identification (user agent) and referring page. We use them to keep the service secure and to find errors (legitimate interest, Art. 6(1)(f) GDPR) and delete them after 30 days at the latest, unless they are needed to investigate a security incident. The public pages load no fonts, scripts or images from third parties; only the checkout in the dashboard loads Paddle's script.</p>

      <h2>10. Cookies</h2>
      <p>We only use cookies that are needed for the respective function (§ 165(3) Austrian Telecommunications Act 2021), no advertising cookies:</p>
      <dl>
        <dt><code>cv_session</code></dt><dd>Keeps you signed in to your account; 30 days, extended while you use it.</dd>
        <dt><code>cv_access</code></dt><dd>Access to a CV through an invite link; up to 365 days.</dd>
        <dt><code>i18n_redirected</code></dt><dd>Remembers the chosen language.</dd>
        <dt><code>cv_consent</code>, <code>cv_vid</code></dt><dd>Only on CVs with visit statistics switched on, see section 6.</dd>
      </dl>
      <p>While you sign in with a sign-in provider, we additionally set short-lived technical cookies (a few minutes) that secure the sign-in.</p>

      <h2>11. Your rights</h2>
      <p>You have the right of access (Art. 15 GDPR), rectification (Art. 16), erasure (Art. 17), restriction of processing (Art. 18), notification of recipients (Art. 19), data portability (Art. 20) and to object (Art. 21). You can withdraw a consent at any time with effect for the future. Please contact the e-mail address in section 1.</p>
      <p>You can also lodge a complaint with a supervisory authority; in Austria this is the Datenschutzbehörde (Austrian Data Protection Authority), Barichgasse 40–42, 1030 Vienna, <a href="https://www.dsb.gv.at" target="_blank" rel="noopener">www.dsb.gv.at</a>.</p>

      <h2>12. Export and deletion in the dashboard</h2>
      <p>In the dashboard you can download all data of your account as a ZIP file at any time (account data, payments and all files of your CV). There you can also delete your account yourself: we then delete your account, your sign-ins, your CV with all files, your invites, cached PDFs and the visit data of your CV. Payment records are kept without reference to you (section 7).</p>

      <h2>13. No automated decisions</h2>
      <p>We make no automated decisions within the meaning of Art. 22 GDPR and do not create profiles for advertising.</p>

      <h2>14. Changes</h2>
      <p>When the platform changes, we update this policy. We inform account holders about material changes by e-mail.</p>
    </template>
  </LegalPage>
</template>

<i18n lang="json">
{
  "en": { "privacy": { "title": "Privacy policy" } },
  "de": { "privacy": { "title": "Datenschutzerklärung" } }
}
</i18n>
