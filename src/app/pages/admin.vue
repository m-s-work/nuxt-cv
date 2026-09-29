<script setup lang="ts">
import '~/assets/css/admin.css'
import { changeSummary, errorMessage, findRevision, pinStatus, shortSha, type AdminTenant, type CvRevisions } from '~/composables/useAdmin'

// Owner tool: never indexed, never linked from the public pages.
useSeoMeta({ title: 'CV admin', robots: 'noindex, nofollow' })

const admin = useAdmin()

const keyInput = ref('')
const tenants = ref<AdminTenant[]>([])
const selectedId = ref<string>('')
const loading = ref(false)
const loginError = ref('')
const tab = ref('invites')

const selected = computed(() => tenants.value.find(t => t.id === selectedId.value))
const tenantItems = computed(() => tenants.value.map(t => ({ label: `${t.name || t.id} (${t.id})`, value: t.id })))

const tabs = [
  { label: 'Invites', value: 'invites', icon: 'i-lucide-ticket' },
  { label: 'Files', value: 'files', icon: 'i-lucide-folder' },
  { label: 'Preview', value: 'preview', icon: 'i-lucide-eye' },
  { label: 'Design', value: 'design', icon: 'i-lucide-palette' }
]

async function loadTenants() {
  loading.value = true
  loginError.value = ''
  try {
    tenants.value = await admin.tenants()
    if (!tenants.value.some(t => t.id === selectedId.value)) selectedId.value = tenants.value[0]?.id ?? ''
    else await loadRevisions()
  } catch (error: unknown) {
    const status = (error as { statusCode?: number }).statusCode
    loginError.value = status === 401
      ? 'Admin key rejected.'
      : status === 404
        ? 'The admin API is disabled (no Admin__ApiKey configured on the server).'
        : `Could not reach the admin API (${errorMessage(error)}).`
    if (status === 401 || status === 404) admin.setKey('')
  } finally {
    loading.value = false
  }
}

async function login() {
  admin.setKey(keyInput.value.trim())
  keyInput.value = ''
  await loadTenants()
}

function logout() {
  admin.setKey('')
  tenants.value = []
  selectedId.value = ''
}

/** A tenant created or changed via the file editor: reload the list and select it. */
async function onTenantChanged(id?: string) {
  await loadTenants()
  if (id && tenants.value.some(t => t.id === id)) selectedId.value = id
  await loadRevisions()
}

// CV revisions (git commits registered by tools/cv-sync.sh), used for pins and their warnings.
const revisions = ref<CvRevisions | null>(null)
async function loadRevisions() {
  const id = selectedId.value
  if (!id) { revisions.value = null; return }
  try {
    const result = await admin.revisions(id)
    if (selectedId.value === id) revisions.value = result
  } catch {
    revisions.value = null
  }
}
watch(selectedId, loadRevisions)

const currentRevision = computed(() => revisions.value?.revisions.find(r => r.sha === revisions.value?.current))
const profilePins = computed(() => Object.entries(selected.value?.pins ?? {})
  .map(([profile, sha]) => ({ profile, sha, status: pinStatus(sha, revisions.value), changes: changeSummary(findRevision(sha, revisions.value)) })))
const pinColor = { current: 'neutral', outdated: 'warning', missing: 'error' } as const

// Profile pinned to a revision that is not stored: fetch it from the CV's git repo.
const fetching = ref<string | null>(null)
const fetchError = ref('')
async function fetchPin(pin: string) {
  if (!selectedId.value) return
  fetching.value = pin
  fetchError.value = ''
  try {
    await admin.fetchRevision(selectedId.value, pin)
    await loadRevisions()
  } catch (e) {
    fetchError.value = `${pin}: ${errorMessage(e)}`
  } finally {
    fetching.value = null
  }
}

// New tenant: only needs an id; the files tab then offers a tenant.json template.
const newTenantId = ref('')
const newTenantError = ref('')
const creatingTenant = ref<string | null>(null)
function startNewTenant() {
  const id = newTenantId.value.trim()
  if (!/^[a-z0-9][a-z0-9-]{0,62}$/.test(id)) {
    newTenantError.value = 'Lowercase letters, digits and dashes only.'
    return
  }
  newTenantError.value = ''
  creatingTenant.value = id
  newTenantId.value = ''
  tab.value = 'files'
}

const activeTenantId = computed(() => creatingTenant.value ?? selectedId.value)
watch(selectedId, () => { creatingTenant.value = null })

onMounted(() => {
  if (admin.key.value) loadTenants()
})
</script>

<template>
  <UApp>
    <div class="min-h-screen bg-gray-50 dark:bg-gray-950 text-gray-900 dark:text-gray-100">
      <header class="border-b border-gray-200 dark:border-gray-800 bg-white dark:bg-gray-900">
        <div class="max-w-6xl mx-auto px-4 py-3 flex items-center gap-3 flex-wrap">
          <UIcon name="i-lucide-shield" class="size-5 text-primary" />
          <h1 class="font-semibold">CV admin</h1>
          <div v-if="admin.key.value && tenants.length" class="flex items-center gap-2 ml-auto flex-wrap">
            <USelect v-model="selectedId" :items="tenantItems" class="min-w-56" aria-label="Tenant" />
            <UButton icon="i-lucide-refresh-cw" color="neutral" variant="ghost" aria-label="Reload" :loading="loading" @click="loadTenants" />
            <UButton icon="i-lucide-log-out" color="neutral" variant="ghost" label="Log out" @click="logout" />
          </div>
        </div>
      </header>

      <main class="max-w-6xl mx-auto px-4 py-6">
        <!-- Login -->
        <form v-if="!admin.key.value || (!tenants.length && loginError)" class="max-w-sm mx-auto mt-16 space-y-4" @submit.prevent="login">
          <h2 class="text-lg font-semibold">Sign in</h2>
          <p class="text-sm text-gray-500">Enter the admin key (<code>Admin__ApiKey</code>). It is kept for this browser tab only.</p>
          <UInput v-model="keyInput" type="password" placeholder="Admin key" autocomplete="current-password" class="w-full" autofocus />
          <p v-if="loginError" class="text-sm text-red-600 dark:text-red-400">{{ loginError }}</p>
          <UButton type="submit" label="Sign in" block :loading="loading" :disabled="!keyInput.trim()" />
        </form>

        <div v-else-if="loading && !tenants.length" class="text-center text-gray-500 mt-16">Loading…</div>

        <template v-else>
          <section class="grid gap-4 md:grid-cols-[1fr_auto] items-start mb-6">
            <div v-if="selected && !creatingTenant" class="rounded-lg border border-gray-200 dark:border-gray-800 bg-white dark:bg-gray-900 p-4">
              <div class="flex items-baseline gap-2 flex-wrap">
                <h2 class="text-xl font-semibold">{{ selected.name || selected.id }}</h2>
                <code class="text-sm text-gray-500">{{ selected.id }}</code>
              </div>
              <dl class="mt-3 grid grid-cols-[auto_1fr] gap-x-4 gap-y-1 text-sm">
                <dt class="text-gray-500">Hosts</dt>
                <dd>{{ selected.hosts.length ? selected.hosts.join(', ') : '– (shared host only)' }}</dd>
                <dt class="text-gray-500">Profiles</dt>
                <dd class="flex gap-1 flex-wrap">
                  <UBadge v-for="p in selected.profiles" :key="p" :label="p" color="neutral" variant="subtle" />
                </dd>
                <dt class="text-gray-500">Public profile</dt>
                <dd>{{ selected.publicProfile ?? '– (no public access)' }}</dd>
                <dt class="text-gray-500">Locales</dt>
                <dd>{{ selected.locales.length ? selected.locales.join(', ') : '– (no CV file yet)' }} <span class="text-gray-500">(default {{ selected.defaultLocale }})</span></dd>
                <dt class="text-gray-500">CV version</dt>
                <dd data-testid="cv-version">
                  <template v-if="currentRevision">
                    <code>{{ shortSha(currentRevision.sha) }}</code>
                    <span v-if="currentRevision.message" class="text-gray-500"> · {{ currentRevision.message }}</span>
                    <UBadge
                      v-if="revisions?.modified" class="ml-2" size="sm" color="warning" variant="subtle" label="changed since (not registered)"
                      :title="changeSummary(currentRevision)"
                    />
                  </template>
                  <span v-else class="text-gray-500">– (no revision registered; deploy with tools/cv-sync.sh)</span>
                </dd>
                <template v-if="profilePins.length">
                  <dt class="text-gray-500">Pinned profiles</dt>
                  <dd class="flex gap-1 flex-wrap">
                    <UBadge
                      v-for="pin in profilePins" :key="pin.profile" :color="pinColor[pin.status]" variant="subtle" icon="i-lucide-pin"
                      :title="pin.changes ? `Changed since: ${pin.changes}` : undefined"
                      :label="`${pin.profile} → ${shortSha(pin.sha)}${pin.status === 'current' ? '' : pin.status === 'outdated' ? ' (outdated)' : ' (not stored)'}`"
                    />
                  </dd>
                </template>
              </dl>
              <p v-if="profilePins.some(p => p.status !== 'current')" class="mt-3 text-sm text-amber-600 dark:text-amber-400" data-testid="profile-pin-warning">
                <UIcon name="i-lucide-triangle-alert" class="align-middle" />
                A profile is pinned to a CV version that is outdated or not stored. Update its <code>revision</code> in <code>tenant.json</code>.
              </p>
              <div v-if="profilePins.some(p => p.status === 'missing')" class="mt-1 flex gap-2 flex-wrap items-center">
                <UButton
                  v-for="pin in profilePins.filter(p => p.status === 'missing')" :key="pin.profile"
                  size="xs" color="neutral" variant="outline" icon="i-lucide-git-branch"
                  :label="`Fetch ${shortSha(pin.sha)} from git`" :loading="fetching === pin.sha" :disabled="!revisions?.source"
                  @click="fetchPin(pin.sha)"
                />
                <span v-if="!revisions?.source" class="text-xs text-gray-500">No git repository known yet (deploy once with a current tools/cv-sync.sh).</span>
                <span v-if="fetchError" class="text-xs text-red-600">{{ fetchError }}</span>
              </div>
            </div>
            <div v-else-if="creatingTenant" class="rounded-lg border border-dashed border-primary p-4 text-sm">
              Creating tenant <code>{{ creatingTenant }}</code>: save a <code>tenant.json</code> below to create it.
              <UButton size="xs" color="neutral" variant="link" label="Cancel" @click="creatingTenant = null" />
            </div>
            <div v-else class="text-gray-500">No tenants yet. Create one:</div>

            <form class="flex gap-2 items-start" @submit.prevent="startNewTenant">
              <div>
                <UInput v-model="newTenantId" placeholder="new-tenant-id" size="sm" aria-label="New tenant id" />
                <p v-if="newTenantError" class="text-xs text-red-600 mt-1">{{ newTenantError }}</p>
              </div>
              <UButton type="submit" size="sm" icon="i-lucide-plus" label="New tenant" color="neutral" variant="outline" />
            </form>
          </section>

          <template v-if="activeTenantId">
            <UTabs v-model="tab" :items="tabs" :content="false" class="mb-4" />
            <AdminInvites v-if="tab === 'invites' && selected && !creatingTenant" :key="`i-${selected.id}`" :tenant="selected" :revisions="revisions" @revisions-changed="loadRevisions" />
            <AdminFiles v-else-if="tab === 'files'" :key="`f-${activeTenantId}`" :tenant-id="activeTenantId" :is-new="!!creatingTenant" @changed="onTenantChanged(activeTenantId)" />
            <AdminPreview v-else-if="tab === 'preview' && selected && !creatingTenant" :key="`p-${selected.id}`" :tenant="selected" :revisions="revisions" />
            <div v-else-if="tab === 'design' && selected && !creatingTenant" class="space-y-4">
              <AdminFavicon :key="`fav-${selected.id}`" :tenant="selected" @changed="onTenantChanged(selected.id)" />
              <AdminTemplates :key="`d-${selected.id}`" :tenant="selected" @changed="onTenantChanged(selected.id)" />
            </div>
            <p v-else class="text-sm text-gray-500">Save a <code>tenant.json</code> first.</p>
          </template>
        </template>
      </main>
    </div>
  </UApp>
</template>
