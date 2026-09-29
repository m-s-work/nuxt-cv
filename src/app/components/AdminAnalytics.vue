<script setup lang="ts">
/**
 * Owner-only visitor tracking reports (docs/VISITOR_SESSION_TRACKING.md §7, §8.2): per invite reach, time,
 * consent and interest; per visitor group attention, technology intent, networks, versions, sessions and a heatmap.
 */
import {
  anchorName, errorMessage, formatDuration, shortSha,
  type AdminTenant, type AnalyticsGroup, type AnalyticsGroupDetail, type AnalyticsSessionDetail, type ConsentStats, type TrackingSettings
} from '~/composables/useAdmin'

const props = defineProps<{ tenant: AdminTenant }>()
const admin = useAdmin()

const groups = ref<AnalyticsGroup[]>([])
const settings = ref<TrackingSettings | null>(null)
const consent = ref<ConsentStats | null>(null)
const loading = ref(false)
const error = ref('')

const selectedGroup = ref<string | null>(null)
const detail = ref<AnalyticsGroupDetail | null>(null)
const detailLoading = ref(false)
const session = ref<AnalyticsSessionDetail | null>(null)
const showHeatmap = ref(false)

async function load() {
  loading.value = true
  error.value = ''
  try {
    const [g, s, c] = await Promise.all([
      admin.analyticsGroups(props.tenant.id),
      admin.trackingSettings(props.tenant.id),
      admin.consentStats(props.tenant.id)
    ])
    groups.value = g
    settings.value = s
    consent.value = c
    if (selectedGroup.value) await openGroup(selectedGroup.value)
  } catch (e) {
    error.value = errorMessage(e)
  } finally {
    loading.value = false
  }
}

async function openGroup(key: string) {
  selectedGroup.value = key
  session.value = null
  showHeatmap.value = false
  detailLoading.value = true
  try {
    detail.value = await admin.analyticsGroup(props.tenant.id, key)
  } catch (e) {
    error.value = errorMessage(e)
  } finally {
    detailLoading.value = false
  }
}

async function openSession(id: string) {
  try {
    session.value = await admin.analyticsSession(props.tenant.id, id)
  } catch (e) {
    error.value = errorMessage(e)
  }
}

async function erase(visitorId: string) {
  if (!confirm('Delete everything recorded about this visitor (sessions, events, IPs, fingerprints)? This cannot be undone.')) return
  try {
    await admin.eraseVisitor(props.tenant.id, visitorId)
    await load()
  } catch (e) {
    error.value = errorMessage(e)
  }
}

const selected = computed(() => groups.value.find(g => g.groupKey === selectedGroup.value))
const trackingOn = computed(() => !!settings.value?.privacy?.controller && settings.value.tenantEnabled)
const rate = (c: { accept: number, decline: number }) =>
  c.accept + c.decline ? `${Math.round((c.accept / (c.accept + c.decline)) * 100)} %` : '–'
const maxVisible = computed(() => Math.max(1, ...(detail.value?.anchors.map(a => a.visibleMs) ?? [1])))
const scoreLabels: Record<string, string> = {
  time: 'Active time', coverage: 'Coverage', returns: 'Returns', detail: 'Detail seeking', intent: 'Contact / keep', spread: 'Spread'
}
const scoreMax: Record<string, number> = { time: 25, coverage: 20, returns: 15, detail: 15, intent: 15, spread: 10 }

function formatDate(value?: string) {
  return value ? new Date(value).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' }) : '–'
}

function payloadText(payload?: Record<string, unknown>) {
  return payload ? Object.entries(payload).map(([k, v]) => `${k}: ${v}`).join(', ') : ''
}

onMounted(load)
</script>

<template>
  <div class="space-y-6">
    <!-- Settings and consent -->
    <section class="grid gap-4 md:grid-cols-2">
      <div class="rounded-lg border border-gray-200 dark:border-gray-800 bg-white dark:bg-gray-900 p-4 text-sm" data-testid="tracking-settings">
        <div class="flex items-center gap-2 mb-2">
          <h3 class="font-semibold">Tracking</h3>
          <UBadge :color="trackingOn ? 'success' : 'neutral'" variant="subtle" :label="trackingOn ? 'on (consent modal)' : 'off'" />
          <UButton icon="i-lucide-refresh-cw" size="xs" color="neutral" variant="ghost" class="ml-auto" aria-label="Reload" :loading="loading" @click="load" />
        </div>
        <template v-if="settings">
          <p v-if="!settings.privacy?.controller" class="text-amber-700 dark:text-amber-300">
            Add <code>"privacy": {"controller": "…", "contact": "…"}</code> to <code>tenant.json</code> to enable the consent modal.
          </p>
          <dl class="grid grid-cols-[auto_1fr] gap-x-4 gap-y-1">
            <dt class="text-gray-500">Controller</dt><dd>{{ settings.privacy?.controller ?? '–' }} <span class="text-gray-500">{{ settings.privacy?.contact }}</span></dd>
            <dt class="text-gray-500">Profiles</dt>
            <dd class="flex gap-1 flex-wrap">
              <UBadge
                v-for="(enabled, name) in settings.profiles" :key="name" size="sm" variant="subtle"
                :color="enabled === false ? 'neutral' : 'primary'"
                :label="`${name}: ${enabled === false ? 'off' : enabled === true ? 'on' : 'default'}`"
              />
            </dd>
            <dt class="text-gray-500">Consent</dt>
            <dd>
              {{ { modal: 'consent modal', notice: 'notice + opt-out (non-EU)', prior: 'consent given elsewhere' }[settings.consentMode] }}
              <span v-for="(mode, name) in settings.profileModes" :key="name" class="text-gray-500"> · {{ name }}: {{ mode }}</span>
            </dd>
            <dt class="text-gray-500">Retention</dt>
            <dd>IP / fingerprint {{ settings.retention.identifiersMonths }} · events {{ settings.retention.eventsMonths }} · summaries {{ settings.retention.summaryMonths }} · heatmap {{ settings.retention.heatMonths }} months after the last visit</dd>
            <dt class="text-gray-500">DNT / GPC</dt><dd>{{ settings.honorBrowserSignals ? 'treated as decline' : 'modal is shown anyway' }}</dd>
            <dt class="text-gray-500">Location data</dt>
            <dd>
              <template v-if="settings.geoSource === 'service'">
                geo service ·
                <template v-if="settings.geoStatus?.error">unreachable</template>
                <template v-else>city {{ settings.geoStatus?.databases?.city ?? 'downloading…' }}, network {{ settings.geoStatus?.databases?.asn ?? 'downloading…' }}</template>
                · <a href="https://db-ip.com" target="_blank" rel="noopener" class="underline">IP Geolocation by DB-IP</a>
              </template>
              <template v-else-if="settings.geoSource === 'files'">local database files</template>
              <template v-else>off (no geo service configured)</template>
            </dd>
            <dt class="text-gray-500">Policy version</dt><dd><code>{{ settings.policyVersion }}</code></dd>
          </dl>
          <p class="mt-2 text-xs text-gray-500">Invites can switch the modal and tracking on or off; otherwise the profile, then the tenant decides.</p>
        </template>
      </div>
      <div class="rounded-lg border border-gray-200 dark:border-gray-800 bg-white dark:bg-gray-900 p-4 text-sm" data-testid="consent-stats">
        <h3 class="font-semibold mb-2">Consent</h3>
        <template v-if="consent">
          <p>
            <strong>{{ consent.total.accept }}</strong> accepted · <strong>{{ consent.total.decline }}</strong> declined ·
            <strong>{{ consent.total.withdraw }}</strong> withdrawn · rate {{ rate(consent.total) }}
          </p>
          <p v-if="consent.withSignals.accept + consent.withSignals.decline" class="text-gray-500">
            Browsers with DNT / GPC: {{ consent.withSignals.accept }} accepted, {{ consent.withSignals.decline }} declined
          </p>
          <table v-if="consent.byPolicyVersion.length" class="mt-2 w-full">
            <thead class="text-left text-gray-500"><tr><th class="font-medium">Text version</th><th class="font-medium">Accept / decline</th><th class="font-medium">Rate</th></tr></thead>
            <tbody>
              <tr v-for="v in consent.byPolicyVersion" :key="v.policyVersion">
                <td><code>{{ v.policyVersion }}</code></td>
                <td>{{ v.counts.accept }} / {{ v.counts.decline }}</td>
                <td>{{ v.acceptRate != null ? `${Math.round(v.acceptRate * 100)} %` : '–' }}</td>
              </tr>
            </tbody>
          </table>
        </template>
      </div>
    </section>

    <p v-if="error" class="text-sm text-red-600 dark:text-red-400">{{ error }}</p>

    <!-- Visitor groups -->
    <section class="rounded-lg border border-gray-200 dark:border-gray-800 bg-white dark:bg-gray-900">
      <h3 class="font-semibold p-4 border-b border-gray-200 dark:border-gray-800">Invites</h3>
      <p v-if="!loading && !groups.length" class="p-4 text-sm text-gray-500">No visits recorded yet.</p>
      <div v-else class="overflow-x-auto">
        <table class="w-full text-sm">
          <thead class="text-left text-gray-500">
            <tr>
              <th class="px-4 py-2 font-medium">Invite</th>
              <th class="px-4 py-2 font-medium">Visitors</th>
              <th class="px-4 py-2 font-medium">Visits</th>
              <th class="px-4 py-2 font-medium">Active time</th>
              <th class="px-4 py-2 font-medium">Last visit</th>
              <th class="px-4 py-2 font-medium">Consent</th>
              <th class="px-4 py-2 font-medium">Interest</th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="g in groups" :key="g.groupKey"
              class="border-t border-gray-100 dark:border-gray-800 cursor-pointer hover:bg-gray-50 dark:hover:bg-gray-800/50"
              :class="{ 'bg-blue-50 dark:bg-blue-950/40': g.groupKey === selectedGroup }"
              :data-testid="`group-${g.groupKey}`"
              @click="openGroup(g.groupKey)"
            >
              <td class="px-4 py-2">
                <UIcon v-if="g.source === 'pdf-qr'" name="i-lucide-qr-code" class="align-middle mr-1" />
                {{ g.source === 'pdf-qr' ? `QR in PDF · ${g.label}` : g.label }}
                <span v-if="g.profile" class="text-gray-500"> · {{ g.profile }}</span>
                <UBadge v-if="g.revoked" size="sm" color="error" variant="subtle" label="revoked" class="ml-1" />
              </td>
              <td class="px-4 py-2 tabular-nums">{{ g.visitors }}<span v-if="g.persons !== g.visitors" class="text-gray-500"> ({{ g.persons }} persons)</span></td>
              <td class="px-4 py-2 tabular-nums">{{ g.visits }}</td>
              <td class="px-4 py-2 tabular-nums">{{ formatDuration(g.activeMs) }}</td>
              <td class="px-4 py-2 whitespace-nowrap">{{ formatDate(g.lastVisit) }}</td>
              <td class="px-4 py-2 whitespace-nowrap">{{ g.consent.accept }} ✓ · {{ g.consent.decline }} ✗</td>
              <td class="px-4 py-2">
                <div class="flex items-center gap-2">
                  <div class="h-2 w-20 rounded bg-gray-200 dark:bg-gray-700 overflow-hidden"><div class="h-full bg-blue-500" :style="{ width: `${g.score}%` }" /></div>
                  <span class="tabular-nums">{{ g.score }}</span>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </section>

    <!-- Group detail -->
    <section v-if="selectedGroup" class="rounded-lg border border-gray-200 dark:border-gray-800 bg-white dark:bg-gray-900 p-4 space-y-5 text-sm" data-testid="group-detail">
      <div class="flex items-center gap-2 flex-wrap">
        <h3 class="font-semibold text-base">{{ selected?.label ?? selectedGroup }}</h3>
        <UButton size="sm" icon="i-lucide-flame" color="neutral" variant="outline" :label="showHeatmap ? 'Hide heatmap' : 'Heatmap'" @click="showHeatmap = !showHeatmap" />
        <UButton size="sm" icon="i-lucide-x" color="neutral" variant="ghost" aria-label="Close" class="ml-auto" @click="selectedGroup = null" />
      </div>
      <p v-if="detailLoading" class="text-gray-500">Loading…</p>
      <template v-else-if="detail">
        <AdminHeatmap v-if="showHeatmap" :tenant="tenant" :group="selectedGroup" />

        <div class="grid gap-4 lg:grid-cols-3">
          <div>
            <h4 class="font-medium mb-1">Interest {{ detail.score.total }} / 100</h4>
            <div v-for="(value, key) in detail.score.parts" :key="key" class="flex items-center gap-2">
              <span class="w-28 text-gray-500">{{ scoreLabels[key] }}</span>
              <div class="h-1.5 flex-1 rounded bg-gray-200 dark:bg-gray-700 overflow-hidden"><div class="h-full bg-blue-500" :style="{ width: `${(value / scoreMax[key]!) * 100}%` }" /></div>
              <span class="w-10 text-right tabular-nums">{{ value }}</span>
            </div>
          </div>
          <div>
            <h4 class="font-medium mb-1">Looking for (technologies)</h4>
            <p v-if="!detail.techIntent.length" class="text-gray-500">No filter used.</p>
            <div class="flex gap-1 flex-wrap">
              <UBadge v-for="tech in detail.techIntent" :key="tech.tech" variant="subtle" :label="`${tech.tech} × ${tech.count}`" />
            </div>
            <h4 class="font-medium mt-3 mb-1">Actions</h4>
            <p v-if="!Object.keys(detail.actions).length" class="text-gray-500">None.</p>
            <div class="flex gap-1 flex-wrap">
              <UBadge v-for="(count, name) in detail.actions" :key="name" color="neutral" variant="subtle" :label="`${name} × ${count}`" />
            </div>
          </div>
          <div>
            <h4 class="font-medium mb-1">Networks</h4>
            <p v-for="(n, i) in detail.networks" :key="i">
              {{ n.org ?? 'unknown network' }}<span class="text-gray-500"> · {{ [n.city, n.country].filter(Boolean).join(', ') || 'no location' }} · {{ n.sessions }} sessions, {{ n.visitors }} visitors</span>
            </p>
          </div>
        </div>

        <div>
          <h4 class="font-medium mb-1">Attention</h4>
          <div class="space-y-1">
            <div v-for="a in detail.anchors" :key="a.anchor" class="grid grid-cols-[minmax(10rem,18rem)_1fr_auto] gap-2 items-center">
              <span class="truncate" :title="a.anchor">{{ anchorName(a.anchor, a.label) }}</span>
              <div class="h-2 rounded bg-gray-200 dark:bg-gray-700 overflow-hidden"><div class="h-full bg-blue-500" :style="{ width: `${(a.visibleMs / maxVisible) * 100}%` }" /></div>
              <span class="tabular-nums text-gray-500 whitespace-nowrap">
                {{ formatDuration(a.visibleMs) }}<template v-if="a.reading"> · {{ a.reading }}</template><template v-if="a.clicks"> · {{ a.clicks }} clicks</template><template v-if="a.hoverMs"> · hover {{ formatDuration(a.hoverMs) }}</template>
              </span>
            </div>
          </div>
        </div>

        <div class="overflow-x-auto">
          <h4 class="font-medium mb-1">Visitors</h4>
          <table class="w-full">
            <thead class="text-left text-gray-500"><tr><th class="font-medium">Device</th><th class="font-medium">Person</th><th class="font-medium">Visits</th><th class="font-medium">Active</th><th class="font-medium">Last seen</th><th /></tr></thead>
            <tbody>
              <tr v-for="v in detail.visitors" :key="v.id" class="border-t border-gray-100 dark:border-gray-800">
                <td>{{ v.device }} · {{ v.browser }} · {{ v.os }} <span class="text-gray-500">{{ v.language }}</span></td>
                <td><code :title="v.personId">{{ v.personId.slice(0, 8) }}</code><span v-if="v.personReason === 'fingerprint'" class="text-gray-500"> (linked by fingerprint)</span></td>
                <td class="tabular-nums">{{ v.visits }}</td>
                <td class="tabular-nums">{{ formatDuration(v.activeMs) }}</td>
                <td class="whitespace-nowrap">{{ formatDate(v.lastSeen) }}</td>
                <td class="text-right"><UButton size="xs" color="error" variant="ghost" icon="i-lucide-trash-2" label="Erase" @click="erase(v.id)" /></td>
              </tr>
            </tbody>
          </table>
        </div>

        <div class="overflow-x-auto">
          <h4 class="font-medium mb-1">Sessions</h4>
          <table class="w-full">
            <thead class="text-left text-gray-500"><tr><th class="font-medium">Start</th><th class="font-medium">Active / open</th><th class="font-medium">Scroll</th><th class="font-medium">Network</th><th class="font-medium">Versions</th></tr></thead>
            <tbody>
              <tr
                v-for="s in detail.sessions" :key="s.id"
                class="border-t border-gray-100 dark:border-gray-800 cursor-pointer hover:bg-gray-50 dark:hover:bg-gray-800/50"
                @click="openSession(s.id)"
              >
                <td class="whitespace-nowrap">{{ formatDate(s.startedAt) }}<span v-if="s.previousSessionId" class="text-gray-500"> ↳ {{ s.endReason ?? 'linked' }}</span></td>
                <td class="tabular-nums">{{ formatDuration(s.activeMs) }} / {{ formatDuration(s.openMs) }}</td>
                <td class="tabular-nums">{{ s.maxScroll }} %</td>
                <td>{{ s.ip ?? '–' }} <span class="text-gray-500">{{ [s.asOrg, s.ipCity, s.ipCountry].filter(Boolean).join(' · ') }}</span></td>
                <td><code class="text-xs">app {{ shortSha(s.appSha) }} · cv {{ shortSha(s.cvSourceSha) }} · {{ s.cvVersion }}</code><UBadge v-if="s.versionMismatch" size="sm" color="warning" variant="subtle" label="mismatch" class="ml-1" /></td>
              </tr>
            </tbody>
          </table>
        </div>

        <div v-if="session" class="rounded-md border border-gray-200 dark:border-gray-800 p-3" data-testid="session-detail">
          <div class="flex items-center gap-2 mb-2">
            <h4 class="font-medium">Session {{ formatDate(session.session.startedAt) }}</h4>
            <span class="text-gray-500">{{ session.session.breakpoint }} · {{ session.session.viewportW }}×{{ session.session.viewportH }} · {{ session.session.locale }} · local {{ session.session.localHour }}:00 · via {{ session.session.referrer }}</span>
            <UButton size="xs" icon="i-lucide-x" color="neutral" variant="ghost" aria-label="Close session" class="ml-auto" @click="session = null" />
          </div>
          <p class="text-gray-500">
            IPs: {{ session.ips.map(i => i.ip).join(', ') || session.session.ip || '–' }} · fingerprint <code>{{ session.session.fp?.slice(0, 12) ?? '–' }}</code>
            <template v-if="session.session.signals"> · signals {{ session.session.signals }}</template>
          </p>
          <p v-if="session.linked.length" class="text-gray-500">Same visit: {{ session.linked.map(l => `${formatDate(l.startedAt)} (${l.endReason ?? 'linked'})`).join(' → ') }}</p>
          <ol class="mt-2 space-y-0.5 max-h-80 overflow-y-auto">
            <li v-for="(e, i) in session.events" :key="i" class="grid grid-cols-[4rem_7rem_1fr] gap-2">
              <span class="tabular-nums text-gray-500">{{ formatDuration(e.t) }}</span>
              <span>{{ e.type }}</span>
              <span class="text-gray-500 truncate">{{ e.anchor ? anchorName(e.anchor, e.label) : '' }} {{ payloadText(e.payload) }}</span>
            </li>
          </ol>
        </div>
      </template>
    </section>
  </div>
</template>
