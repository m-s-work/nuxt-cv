<script setup lang="ts">
/**
 * Super-admin user management (docs/REQUIREMENTS_SAAS.md §6): search users, set the plan manually, block / unblock,
 * delete; all payments incl. unmatched webhook payments. Admin key only (the API answers 404 to users).
 */
import { errorMessage, type AdminUser, type Payment } from '~/composables/useAdmin'
import { endOfDayIso, formatMoney } from '~/utils/account'

const admin = useAdmin()

const view = ref<'users' | 'payments'>('users')
const views = [
  { label: 'Users', value: 'users', icon: 'i-lucide-users' },
  { label: 'Payments', value: 'payments', icon: 'i-lucide-receipt' }
]

const q = ref('')
const users = ref<AdminUser[]>([])
const loading = ref(false)
const error = ref('')

async function search() {
  loading.value = true
  error.value = ''
  try {
    users.value = await admin.users(q.value.trim())
  } catch (e) {
    error.value = errorMessage(e)
  } finally {
    loading.value = false
  }
}

let timer: ReturnType<typeof setTimeout> | undefined
watch(q, () => {
  clearTimeout(timer)
  timer = setTimeout(search, 300)
})

function formatDate(value?: string | null, withTime = false) {
  if (!value) return '–'
  return new Date(value).toLocaleString('en', withTime ? { dateStyle: 'medium', timeStyle: 'short' } : { dateStyle: 'medium' })
}

function planLabel(u: AdminUser) {
  if (u.proForever) return 'Pro ∞'
  return u.plan === 'pro' ? 'Pro' : 'Free'
}

// --- Detail ------------------------------------------------------------------------------------

const selected = ref<AdminUser | null>(null)
const selectedPayments = ref<Payment[]>([])
const busy = ref(false)
const detailError = ref('')
const note = ref('')
const customUntil = ref('')
const deleteConfirm = ref('')

async function open(u: AdminUser) {
  if (selected.value?.id === u.id) { selected.value = null; return }
  selected.value = u
  selectedPayments.value = []
  detailError.value = ''
  note.value = ''
  customUntil.value = u.proUntil ? u.proUntil.slice(0, 10) : ''
  deleteConfirm.value = ''
  try {
    const detail = await admin.user(u.id)
    selected.value = detail.user
    selectedPayments.value = detail.payments
  } catch (e) {
    detailError.value = errorMessage(e)
  }
}

function replace(updated: AdminUser) {
  selected.value = updated
  users.value = users.value.map(u => u.id === updated.id ? updated : u)
}

async function act(fn: () => Promise<unknown>) {
  if (!selected.value) return
  busy.value = true
  detailError.value = ''
  try {
    await fn()
    const detail = await admin.user(selected.value.id)
    replace(detail.user)
    selectedPayments.value = detail.payments
    note.value = ''
  } catch (e) {
    detailError.value = errorMessage(e)
  } finally {
    busy.value = false
  }
}

const noteOrUndefined = () => note.value.trim() || undefined

function addDays(days: number) {
  const u = selected.value!
  return act(() => admin.setPlan(u.id, { addDays: days, proForever: false, note: noteOrUndefined() }))
}
function proForever() {
  const u = selected.value!
  return act(() => admin.setPlan(u.id, { proUntil: u.proUntil ?? null, proForever: true, note: noteOrUndefined() }))
}
function backToFree() {
  const u = selected.value!
  if (!confirm(`Set ${u.email} back to Free? Their Pro time ends now.`)) return
  return act(() => admin.setPlan(u.id, { proUntil: null, proForever: false, note: noteOrUndefined() }))
}
function setUntil() {
  const u = selected.value!
  const until = endOfDayIso(customUntil.value)
  if (!until) { detailError.value = 'Choose a date.'; return }
  return act(() => admin.setPlan(u.id, { proUntil: until, proForever: false, note: noteOrUndefined() }))
}
function toggleBlock() {
  const u = selected.value!
  const block = !u.blockedAt
  if (block && !confirm(`Block ${u.email}? They are signed out everywhere and their CV is hidden.`)) return
  return act(() => admin.blockUser(u.id, block))
}
async function remove() {
  const u = selected.value!
  busy.value = true
  detailError.value = ''
  try {
    await admin.deleteUser(u.id)
    users.value = users.value.filter(x => x.id !== u.id)
    selected.value = null
  } catch (e) {
    detailError.value = errorMessage(e)
  } finally {
    busy.value = false
  }
}
const deleteTarget = computed(() => selected.value?.tenantId ?? selected.value?.email ?? '')

// --- Payments ------------------------------------------------------------------------------------

const payments = ref<Payment[]>([])
const paymentsError = ref('')
const onlyUnmatched = ref(false)
const shownPayments = computed(() => onlyUnmatched.value ? payments.value.filter(p => p.status === 'unmatched') : payments.value)
const unmatchedCount = computed(() => payments.value.filter(p => p.status === 'unmatched').length)

async function loadPayments() {
  paymentsError.value = ''
  try {
    payments.value = await admin.allPayments()
  } catch (e) {
    paymentsError.value = errorMessage(e)
  }
}
watch(view, v => { if (v === 'payments') loadPayments() })

const statusColor: Record<string, 'success' | 'warning' | 'error' | 'neutral' | 'info'> = {
  completed: 'success', manual: 'info', unmatched: 'warning', refunded: 'neutral', chargeback: 'error'
}

onMounted(() => {
  search()
  loadPayments()
})
</script>

<template>
  <div class="space-y-4" data-testid="admin-users">
    <div class="flex items-center gap-2 flex-wrap">
      <UTabs v-model="view" :items="views" :content="false" size="sm" variant="link" />
      <UBadge v-if="unmatchedCount" :label="`${unmatchedCount} unmatched payment${unmatchedCount === 1 ? '' : 's'}`" color="warning" variant="subtle" class="cursor-pointer" @click="view = 'payments'; onlyUnmatched = true" />
    </div>

    <template v-if="view === 'users'">
      <div class="flex gap-2 items-center">
        <UInput v-model="q" icon="i-lucide-search" placeholder="Search e-mail, name or handle" class="w-full max-w-md" aria-label="Search users" />
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="ghost" aria-label="Reload" :loading="loading" @click="search" />
        <span class="text-sm text-gray-500 ml-auto">{{ users.length }} user{{ users.length === 1 ? '' : 's' }}</span>
      </div>
      <p v-if="error" class="text-sm text-red-600 dark:text-red-400">{{ error }}</p>

      <div class="rounded-lg border border-gray-200 dark:border-gray-800 bg-white dark:bg-gray-900 overflow-x-auto">
        <table class="w-full text-sm text-left">
          <thead class="text-xs text-gray-500 border-b border-gray-200 dark:border-gray-800">
            <tr>
              <th class="p-2 font-normal">E-mail</th><th class="p-2 font-normal">Name</th><th class="p-2 font-normal">Handle</th>
              <th class="p-2 font-normal">Sign-in</th><th class="p-2 font-normal">Plan</th><th class="p-2 font-normal">Pro until</th>
              <th class="p-2 font-normal">Created</th><th class="p-2 font-normal">Last login</th><th class="p-2 font-normal">Status</th>
            </tr>
          </thead>
          <tbody>
            <template v-for="u in users" :key="u.id">
              <tr
                class="border-t border-gray-100 dark:border-gray-800 cursor-pointer hover:bg-gray-50 dark:hover:bg-gray-800/50"
                :class="{ 'bg-primary/5': selected?.id === u.id }" :data-testid="`user-${u.id}`" @click="open(u)"
              >
                <td class="p-2 font-medium">{{ u.email }}</td>
                <td class="p-2">{{ u.name || '–' }}</td>
                <td class="p-2"><code v-if="u.tenantId">{{ u.tenantId }}</code><span v-else class="text-gray-400">–</span></td>
                <td class="p-2 text-xs">{{ u.providers.join(', ') }}</td>
                <td class="p-2"><UBadge :label="planLabel(u)" :color="u.plan === 'pro' ? 'primary' : 'neutral'" variant="subtle" size="sm" /></td>
                <td class="p-2 whitespace-nowrap">{{ u.proForever ? 'forever' : formatDate(u.proUntil) }}</td>
                <td class="p-2 whitespace-nowrap">{{ formatDate(u.createdAt) }}</td>
                <td class="p-2 whitespace-nowrap">{{ formatDate(u.lastLoginAt, true) }}</td>
                <td class="p-2"><UBadge v-if="u.blockedAt" label="blocked" color="error" variant="subtle" size="sm" /></td>
              </tr>
              <tr v-if="selected?.id === u.id">
                <td colspan="9" class="p-4 bg-gray-50 dark:bg-gray-950 border-t border-gray-100 dark:border-gray-800">
                  <div class="grid gap-6 lg:grid-cols-2" data-testid="user-detail">
                    <div class="space-y-3">
                      <dl class="grid grid-cols-[auto_1fr] gap-x-4 gap-y-1 text-sm">
                        <dt class="text-gray-500">Id</dt><dd><code class="text-xs">{{ selected.id }}</code></dd>
                        <dt class="text-gray-500">Plan</dt><dd>{{ planLabel(selected) }}{{ selected.proForever ? '' : selected.proUntil ? ` · until ${formatDate(selected.proUntil, true)}` : '' }}</dd>
                        <dt class="text-gray-500">Plan note</dt><dd>{{ selected.planNote || '–' }}</dd>
                        <dt class="text-gray-500">Own domain</dt><dd>{{ selected.customDomain || '–' }}</dd>
                        <dt v-if="selected.blockedAt" class="text-gray-500">Blocked</dt><dd v-if="selected.blockedAt">{{ formatDate(selected.blockedAt, true) }}</dd>
                      </dl>

                      <div class="space-y-2">
                        <h4 class="text-sm font-semibold">Set plan</h4>
                        <UInput v-model="note" size="sm" placeholder="Note, e.g. paid via invoice 2026-17" class="w-full" aria-label="Plan note" />
                        <div class="flex gap-1 flex-wrap">
                          <UButton size="xs" color="neutral" variant="outline" icon="i-lucide-calendar-plus" label="+7 days" :disabled="busy" @click="addDays(7)" />
                          <UButton size="xs" color="neutral" variant="outline" icon="i-lucide-calendar-plus" label="+30 days" :disabled="busy" @click="addDays(30)" />
                          <UButton size="xs" color="neutral" variant="outline" icon="i-lucide-calendar-plus" label="+365 days" :disabled="busy" @click="addDays(365)" />
                          <UButton size="xs" color="primary" variant="outline" icon="i-lucide-infinity" label="Pro forever" :disabled="busy" @click="proForever" />
                          <UButton size="xs" color="warning" variant="outline" icon="i-lucide-rotate-ccw" label="Back to Free" :disabled="busy" @click="backToFree" />
                        </div>
                        <form class="flex gap-2 items-center" @submit.prevent="setUntil">
                          <label class="text-xs text-gray-500" for="pro-until">Pro until</label>
                          <UInput id="pro-until" v-model="customUntil" type="date" size="sm" />
                          <UButton type="submit" size="xs" label="Set" :disabled="busy || !customUntil" />
                        </form>
                      </div>

                      <div class="flex gap-2 flex-wrap pt-2 border-t border-gray-200 dark:border-gray-800">
                        <UButton
                          size="xs" :color="selected.blockedAt ? 'neutral' : 'warning'" variant="outline"
                          :icon="selected.blockedAt ? 'i-lucide-user-check' : 'i-lucide-user-x'" :label="selected.blockedAt ? 'Unblock' : 'Block'"
                          :disabled="busy" @click="toggleBlock"
                        />
                      </div>
                      <form class="flex gap-2 items-center flex-wrap" @submit.prevent="remove">
                        <UInput v-model="deleteConfirm" size="sm" :placeholder="`Type ${deleteTarget} to delete`" class="w-64" :aria-label="`Type ${deleteTarget} to delete`" />
                        <UButton
                          type="submit" size="xs" color="error" icon="i-lucide-trash-2" label="Delete user and CV"
                          :disabled="busy || deleteConfirm.trim().toLowerCase() !== deleteTarget.toLowerCase()"
                        />
                      </form>
                      <p v-if="detailError" class="text-sm text-red-600 dark:text-red-400">{{ detailError }}</p>
                    </div>

                    <div>
                      <h4 class="text-sm font-semibold mb-1">Payments</h4>
                      <p v-if="!selectedPayments.length" class="text-sm text-gray-500">None.</p>
                      <table v-else class="w-full text-xs text-left">
                        <tbody>
                          <tr v-for="p in selectedPayments" :key="p.id" class="border-t border-gray-200 dark:border-gray-800">
                            <td class="py-1 pr-2 whitespace-nowrap">{{ formatDate(p.createdAt) }}</td>
                            <td class="py-1 pr-2">{{ p.provider }} · {{ p.pass ?? '–' }}</td>
                            <td class="py-1 pr-2">{{ p.days ? `${p.days} d` : '' }}</td>
                            <td class="py-1 pr-2">{{ p.amount ? formatMoney(p.amount, p.currency ?? 'EUR') : '' }}</td>
                            <td class="py-1 pr-2"><UBadge :label="p.status" :color="statusColor[p.status] ?? 'neutral'" variant="subtle" size="sm" /></td>
                            <td class="py-1 text-gray-500">{{ p.note }}</td>
                          </tr>
                        </tbody>
                      </table>
                    </div>
                  </div>
                </td>
              </tr>
            </template>
            <tr v-if="!users.length && !loading">
              <td colspan="9" class="p-4 text-center text-gray-500">No users{{ q ? ' found' : ' yet' }}.</td>
            </tr>
          </tbody>
        </table>
      </div>
    </template>

    <template v-else>
      <div class="flex items-center gap-3">
        <UCheckbox v-model="onlyUnmatched" label="Only unmatched" />
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="ghost" aria-label="Reload" @click="loadPayments" />
      </div>
      <p class="text-xs text-gray-500">Unmatched: a Paddle payment whose user or price is unknown. Find the buyer and set their plan manually.</p>
      <p v-if="paymentsError" class="text-sm text-red-600 dark:text-red-400">{{ paymentsError }}</p>
      <div class="rounded-lg border border-gray-200 dark:border-gray-800 bg-white dark:bg-gray-900 overflow-x-auto" data-testid="admin-payments">
        <table class="w-full text-sm text-left">
          <thead class="text-xs text-gray-500 border-b border-gray-200 dark:border-gray-800">
            <tr>
              <th class="p-2 font-normal">Date</th><th class="p-2 font-normal">Provider</th><th class="p-2 font-normal">E-mail</th>
              <th class="p-2 font-normal">Pass</th><th class="p-2 font-normal">Days</th><th class="p-2 font-normal">Amount</th>
              <th class="p-2 font-normal">Status</th><th class="p-2 font-normal">Reference / note</th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="p in shownPayments" :key="p.id" class="border-t border-gray-100 dark:border-gray-800"
              :class="{ 'bg-amber-50 dark:bg-amber-950/40': p.status === 'unmatched' }"
            >
              <td class="p-2 whitespace-nowrap">{{ formatDate(p.createdAt, true) }}</td>
              <td class="p-2">{{ p.provider }}</td>
              <td class="p-2">{{ p.email || '–' }}</td>
              <td class="p-2">{{ p.pass ?? '–' }}</td>
              <td class="p-2">{{ p.days || '–' }}</td>
              <td class="p-2 whitespace-nowrap">{{ p.amount ? formatMoney(p.amount, p.currency ?? 'EUR') : '–' }}</td>
              <td class="p-2"><UBadge :label="p.status" :color="statusColor[p.status] ?? 'neutral'" variant="subtle" size="sm" /></td>
              <td class="p-2 text-xs text-gray-500 break-all">{{ p.externalId && p.provider !== 'manual' ? p.externalId : '' }} {{ p.note }}</td>
            </tr>
            <tr v-if="!shownPayments.length">
              <td colspan="8" class="p-4 text-center text-gray-500">No payments.</td>
            </tr>
          </tbody>
        </table>
      </div>
    </template>
  </div>
</template>
