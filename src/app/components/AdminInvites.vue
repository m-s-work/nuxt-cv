<script setup lang="ts">
import {
  REDACTION_FLAGS, buildOverrides, errorMessage, formatBytes, groupInvites, inviteStatus,
  type AdminInvite, type AdminTenant, type CreatedInvite, type FlagChoice, type OverridesForm, type PdfOutcome
} from '~/composables/useAdmin'

const props = defineProps<{ tenant: AdminTenant }>()
const admin = useAdmin()

const invites = ref<AdminInvite[]>([])
const loading = ref(false)
const error = ref('')
const showInactive = ref(false)

const rows = computed(() => groupInvites(invites.value)
  .filter(i => showInactive.value || inviteStatus(i) === 'active'))
const inactiveCount = computed(() => invites.value.filter(i => inviteStatus(i) !== 'active').length)

async function load() {
  loading.value = true
  error.value = ''
  try {
    invites.value = await admin.invites(props.tenant.id)
  } catch (e) {
    error.value = errorMessage(e)
  } finally {
    loading.value = false
  }
}

// --- Create -------------------------------------------------------------------------------

const profileItems = computed(() => props.tenant.profiles.map(p => ({ label: p, value: p })))
const flagItems = [
  { label: 'profile default', value: 'inherit' },
  { label: 'hide', value: 'on' },
  { label: 'show', value: 'off' }
]

function emptyForm() {
  return {
    profile: props.tenant.profiles[0] ?? '',
    label: '',
    expiresOn: '',
    maxUses: '' as string | number,
    overrides: {
      flags: Object.fromEntries(REDACTION_FLAGS.map(f => [f, 'inherit'])) as Record<string, FlagChoice>,
      hiddenFields: '',
      grants: '',
      replaceGrants: false
    } satisfies OverridesForm
  }
}

const form = reactive(emptyForm())
const showOverrides = ref(false)
const creating = ref(false)
const created = ref<CreatedInvite | null>(null)
const createError = ref('')

async function create() {
  creating.value = true
  createError.value = ''
  try {
    created.value = await admin.createInvite(props.tenant.id, {
      profile: form.profile,
      label: form.label.trim() || undefined,
      // End of the chosen day in the admin's time zone.
      expiresAt: form.expiresOn ? new Date(`${form.expiresOn}T23:59:59`).toISOString() : undefined,
      maxUses: form.maxUses === '' ? undefined : Number(form.maxUses),
      overrides: buildOverrides(form.overrides)
    })
    Object.assign(form, emptyForm())
    showOverrides.value = false
    await load()
  } catch (e) {
    createError.value = errorMessage(e)
  } finally {
    creating.value = false
  }
}

const copied = ref('')
async function copy(text: string, what: string) {
  try {
    await navigator.clipboard.writeText(text)
    copied.value = what
    setTimeout(() => { if (copied.value === what) copied.value = '' }, 1500)
  } catch { /* clipboard blocked: text stays selectable */ }
}

// --- Actions ------------------------------------------------------------------------------

const busy = ref<string | null>(null)
const pdfResults = ref<Record<string, PdfOutcome[] | string>>({})

async function revoke(invite: AdminInvite) {
  const name = invite.label || invite.id
  if (!confirm(`Revoke invite "${name}"? Its QR invite and cached PDFs are revoked/deleted too.`)) return
  busy.value = invite.id
  try {
    await admin.revokeInvite(props.tenant.id, invite.id)
    await load()
  } catch (e) {
    error.value = errorMessage(e)
  } finally {
    busy.value = null
  }
}

async function renderPdf(invite: AdminInvite) {
  busy.value = invite.id
  try {
    pdfResults.value[invite.id] = (await admin.renderPdf(props.tenant.id, invite.id)).pdf
  } catch (e) {
    pdfResults.value[invite.id] = errorMessage(e)
  } finally {
    busy.value = null
  }
}

const statusColor = { active: 'success', revoked: 'error', expired: 'warning', exhausted: 'warning' } as const

function formatDate(value?: string) {
  return value ? new Date(value).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' }) : '–'
}

function overridesSummary(invite: AdminInvite) {
  const o = invite.overrides
  if (!o) return ''
  const parts: string[] = []
  for (const [flag, value] of Object.entries(o.flags ?? {})) parts.push(`${value ? '+' : '−'}${flag.replace(/^hide/, '')}`)
  if (o.hiddenFields?.length) parts.push(`hidden: ${o.hiddenFields.join(', ')}`)
  if (o.grants) parts.push(`grants: ${o.grants.join(', ') || '(none)'}`)
  return parts.join(' · ')
}

onMounted(load)
</script>

<template>
  <div class="space-y-6">
    <!-- Create invite -->
    <section class="rounded-lg border border-gray-200 dark:border-gray-800 bg-white dark:bg-gray-900 p-4">
      <h3 class="font-semibold mb-3">New invite</h3>
      <form class="space-y-3" @submit.prevent="create">
        <div class="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
          <label class="text-sm space-y-1">
            <span class="text-gray-500">For (label)</span>
            <UInput v-model="form.label" placeholder="ACME recruiting" class="w-full" />
          </label>
          <label class="text-sm space-y-1">
            <span class="text-gray-500">Profile</span>
            <USelect v-model="form.profile" :items="profileItems" class="w-full" />
          </label>
          <label class="text-sm space-y-1">
            <span class="text-gray-500">Expires on (optional)</span>
            <UInput v-model="form.expiresOn" type="date" class="w-full" />
          </label>
          <label class="text-sm space-y-1">
            <span class="text-gray-500">Max. redemptions (optional)</span>
            <UInput v-model="form.maxUses" type="number" min="1" placeholder="unlimited" class="w-full" />
          </label>
        </div>

        <UButton
          :icon="showOverrides ? 'i-lucide-chevron-down' : 'i-lucide-chevron-right'"
          color="neutral" variant="link" size="sm" class="px-0"
          label="Per-invite overrides" @click="showOverrides = !showOverrides"
        />
        <div v-if="showOverrides" class="grid gap-3 sm:grid-cols-2 lg:grid-cols-4 border-l-2 border-gray-200 dark:border-gray-700 pl-3">
          <label v-for="flag in REDACTION_FLAGS" :key="flag" class="text-sm space-y-1">
            <span class="text-gray-500">{{ flag }}</span>
            <USelect v-model="form.overrides.flags[flag]" :items="flagItems" class="w-full" />
          </label>
          <label class="text-sm space-y-1 sm:col-span-2">
            <span class="text-gray-500">Additional hidden fields (dot paths, comma separated)</span>
            <UInput v-model="form.overrides.hiddenFields" placeholder="details.phone, projects" class="w-full" />
          </label>
          <div class="text-sm space-y-1 sm:col-span-2">
            <UCheckbox v-model="form.overrides.replaceGrants" label="Replace the profile's grants with:" />
            <UInput v-model="form.overrides.grants" :disabled="!form.overrides.replaceGrants" placeholder="contact, private" class="w-full" />
          </div>
        </div>

        <p v-if="createError" class="text-sm text-red-600 dark:text-red-400">{{ createError }}</p>
        <UButton type="submit" icon="i-lucide-plus" label="Create invite" :loading="creating" :disabled="!form.profile" />
        <span v-if="creating" class="text-sm text-gray-500 ml-3">Rendering PDFs, this can take a few seconds…</span>
      </form>

      <!-- Result, shown once -->
      <div v-if="created" class="mt-4 rounded-md border border-green-300 dark:border-green-800 bg-green-50 dark:bg-green-950/40 p-3 text-sm space-y-2">
        <div class="flex items-center justify-between gap-2">
          <strong>Invite created for "{{ created.invite.label || created.invite.profile }}"</strong>
          <UButton icon="i-lucide-x" size="xs" color="neutral" variant="ghost" aria-label="Dismiss" @click="created = null" />
        </div>
        <div class="flex items-center gap-2 flex-wrap">
          <span class="text-gray-500 w-10">Link</span>
          <code class="break-all select-all" data-testid="invite-link">{{ created.link }}</code>
          <UButton size="xs" :icon="copied === 'link' ? 'i-lucide-check' : 'i-lucide-copy'" color="neutral" variant="outline" label="Copy" @click="copy(created.link, 'link')" />
        </div>
        <div class="flex items-center gap-2 flex-wrap">
          <span class="text-gray-500 w-10">Code</span>
          <code class="break-all select-all">{{ created.code }}</code>
          <UButton size="xs" :icon="copied === 'code' ? 'i-lucide-check' : 'i-lucide-copy'" color="neutral" variant="outline" label="Copy" @click="copy(created.code, 'code')" />
        </div>
        <div v-if="created.pdf" class="flex gap-2 flex-wrap items-center">
          <span class="text-gray-500 w-10">PDF</span>
          <UBadge
            v-for="p in created.pdf" :key="p.locale"
            :color="p.ok ? 'success' : 'error'" variant="subtle"
            :label="p.ok ? `${p.locale} ✓ ${formatBytes(p.bytes ?? 0)}` : `${p.locale} ✗ ${p.error ?? ''}`"
          />
        </div>
      </div>
    </section>

    <!-- Invite list -->
    <section class="rounded-lg border border-gray-200 dark:border-gray-800 bg-white dark:bg-gray-900">
      <div class="flex items-center gap-3 p-4 border-b border-gray-200 dark:border-gray-800">
        <h3 class="font-semibold">Invites</h3>
        <UCheckbox v-model="showInactive" :label="`Show revoked / expired (${inactiveCount})`" class="ml-auto text-sm" />
        <UButton icon="i-lucide-refresh-cw" size="sm" color="neutral" variant="ghost" aria-label="Reload invites" :loading="loading" @click="load" />
      </div>
      <p v-if="error" class="p-4 text-sm text-red-600 dark:text-red-400">{{ error }}</p>
      <p v-else-if="!loading && !rows.length" class="p-4 text-sm text-gray-500">No invites.</p>
      <div v-else class="overflow-x-auto">
        <table class="w-full text-sm">
          <thead class="text-left text-gray-500">
            <tr>
              <th class="px-4 py-2 font-medium">For</th>
              <th class="px-4 py-2 font-medium">Profile</th>
              <th class="px-4 py-2 font-medium">Status</th>
              <th class="px-4 py-2 font-medium">Uses</th>
              <th class="px-4 py-2 font-medium">Last used</th>
              <th class="px-4 py-2 font-medium">Expires</th>
              <th class="px-4 py-2 font-medium">Created</th>
              <th class="px-4 py-2" />
            </tr>
          </thead>
          <tbody>
            <tr v-for="invite in rows" :key="invite.id" class="border-t border-gray-100 dark:border-gray-800 align-top" :data-testid="`invite-${invite.id}`">
              <td class="px-4 py-2" :class="{ 'pl-10': invite.depth }">
                <template v-if="invite.source === 'pdf-qr'">
                  <UIcon name="i-lucide-qr-code" class="align-middle mr-1" />
                  <span class="text-gray-500">QR code in PDF</span>
                </template>
                <template v-else>{{ invite.label || '–' }}</template>
                <div v-if="invite.link" class="mt-1 flex items-center gap-1">
                  <code class="text-xs text-gray-500 select-all" data-testid="invite-code">{{ invite.code }}</code>
                  <UButton
                    size="xs" color="neutral" variant="ghost"
                    :icon="copied === `link-${invite.id}` ? 'i-lucide-check' : 'i-lucide-link'"
                    :aria-label="`Copy link for ${invite.label || invite.id}`" title="Copy link"
                    @click="copy(invite.link, `link-${invite.id}`)"
                  />
                  <UButton
                    size="xs" color="neutral" variant="ghost"
                    :icon="copied === `code-${invite.id}` ? 'i-lucide-check' : 'i-lucide-copy'"
                    :aria-label="`Copy code for ${invite.label || invite.id}`" title="Copy code"
                    @click="copy(invite.code!, `code-${invite.id}`)"
                  />
                </div>
                <div v-else-if="invite.source !== 'pdf-qr'" class="text-xs text-gray-400 mt-1">code not stored (created before codes were kept)</div>
                <div v-if="overridesSummary(invite)" class="text-xs text-gray-500 mt-0.5">{{ overridesSummary(invite) }}</div>
                <div v-if="pdfResults[invite.id]" class="mt-1 flex gap-1 flex-wrap">
                  <span v-if="typeof pdfResults[invite.id] === 'string'" class="text-xs text-red-600">{{ pdfResults[invite.id] }}</span>
                  <template v-else>
                    <UBadge
                      v-for="p in (pdfResults[invite.id] as PdfOutcome[])" :key="p.locale" size="sm"
                      :color="p.ok ? 'success' : 'error'" variant="subtle"
                      :label="p.ok ? `${p.locale} ✓` : `${p.locale} ✗ ${p.error ?? ''}`"
                    />
                  </template>
                </div>
              </td>
              <td class="px-4 py-2">{{ invite.profile }}</td>
              <td class="px-4 py-2">
                <UBadge :color="statusColor[inviteStatus(invite)]" variant="subtle" :label="inviteStatus(invite)" />
              </td>
              <td class="px-4 py-2 tabular-nums">{{ invite.useCount }}<span v-if="invite.maxUses != null" class="text-gray-500"> / {{ invite.maxUses }}</span></td>
              <td class="px-4 py-2 whitespace-nowrap">{{ formatDate(invite.lastUsedAt) }}</td>
              <td class="px-4 py-2 whitespace-nowrap">{{ formatDate(invite.expiresAt) }}</td>
              <td class="px-4 py-2 whitespace-nowrap">{{ formatDate(invite.createdAt) }}</td>
              <td class="px-4 py-2 whitespace-nowrap text-right">
                <template v-if="!invite.depth && inviteStatus(invite) !== 'revoked'">
                  <UButton
                    v-if="inviteStatus(invite) === 'active' || inviteStatus(invite) === 'exhausted'"
                    size="xs" icon="i-lucide-file-text" color="neutral" variant="ghost" label="Re-render PDF"
                    :loading="busy === invite.id" @click="renderPdf(invite)"
                  />
                  <UButton size="xs" icon="i-lucide-ban" color="error" variant="ghost" label="Revoke" :disabled="busy === invite.id" @click="revoke(invite)" />
                </template>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </section>
  </div>
</template>
