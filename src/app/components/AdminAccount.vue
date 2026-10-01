<script setup lang="ts">
/**
 * Account tab of a signed-in user (docs/REQUIREMENTS_SAAS.md §4, §5, §8): plan and usage, Pro passes (Paddle
 * overlay checkout), PDF credit, own domain, payments, data export, sign out everywhere, account deletion.
 * The plan comparison may name analytics features here: this is the signed-in dashboard, not a public page (S4.4).
 */
import { errorMessage, formatBytes, type AccountInfo, type AccountUser, type Payment } from '~/composables/useAdmin'
import {
  accountErrorText, formatMoney, passName, passPricing, proChanged, upgradeText, usagePercent, type BillingConfig, type PassPricing
} from '~/utils/account'
import { initPaddle, type PaddleEvent } from '~/utils/paddle'

const props = defineProps<{ user: AccountUser }>()
/** changed: plan or account data changed (the page reloads the user); deleted: the account is gone. */
const emit = defineEmits<{ changed: [], deleted: [] }>()
const admin = useAdmin()

const info = ref<AccountInfo | null>(null)
const billing = ref<BillingConfig | null>(null)
const payments = ref<Payment[]>([])
const error = ref('')

const user = computed(() => info.value?.user ?? props.user)
const plan = computed(() => user.value.plan)
const isPro = computed(() => plan.value.name === 'pro')
const usage = computed(() => info.value?.usage)
const handle = computed(() => user.value.tenantId ?? '')

async function load() {
  error.value = ''
  try {
    const [a, b, p] = await Promise.all([admin.account(), admin.billingConfig(), admin.payments()])
    info.value = a
    billing.value = b
    payments.value = p
    hideCredit.value = !!a.usage?.hideCredit
    notifyOnOpen.value = a.user.notifyOnOpen !== false
  } catch (e) {
    error.value = errorMessage(e)
  }
}
onMounted(load)

function formatDate(value?: string | null, withTime = false) {
  if (!value) return '–'
  return new Date(value).toLocaleString('en', withTime ? { dateStyle: 'medium', timeStyle: 'short' } : { dateStyle: 'medium' })
}

const proLabel = computed(() => plan.value.proForever
  ? 'Pro, no end date'
  : isPro.value ? `Pro until ${formatDate(plan.value.proUntil)}` : plan.value.proUntil ? `Free (Pro ended ${formatDate(plan.value.proUntil)})` : 'Free, forever')

// --- Passes & checkout --------------------------------------------------------------------------

const passes = computed<PassPricing[]>(() => passPricing(billing.value?.passes ?? []))
const canBuy = computed(() => billing.value?.provider === 'paddle' && !!billing.value.clientToken)
const buying = ref<string | null>(null)
const checkoutState = ref<'idle' | 'waiting' | 'done' | 'timeout'>('idle')
const checkoutError = ref('')

function money(minor: number, currency: string) {
  return formatMoney(minor, currency, 'en')
}

async function waitForPro(before: { proUntil?: string | null, proForever?: boolean }) {
  checkoutState.value = 'waiting'
  // The webhook usually arrives within seconds; poll for ~30 s.
  for (let i = 0; i < 15; i++) {
    await new Promise(resolve => setTimeout(resolve, 2000))
    try {
      const me = await admin.me()
      if (proChanged(before, me.plan)) {
        checkoutState.value = 'done'
        emit('changed')
        await load()
        return
      }
    } catch { /* keep polling */ }
  }
  checkoutState.value = 'timeout'
}

async function buy(pass: PassPricing) {
  if (!billing.value?.clientToken || !pass.priceId) return
  buying.value = pass.id
  checkoutError.value = ''
  checkoutState.value = 'idle'
  const before = { proUntil: plan.value.proUntil, proForever: plan.value.proForever }
  try {
    const paddle = await initPaddle(
      { clientToken: billing.value.clientToken, environment: billing.value.environment },
      (event: PaddleEvent) => {
        if (event.name === 'checkout.completed' && checkoutState.value !== 'waiting') waitForPro(before)
      }
    )
    paddle.Checkout.open({
      items: [{ priceId: pass.priceId, quantity: 1 }],
      customer: { email: user.value.email },
      customData: { userId: user.value.id }
    })
  } catch (e) {
    checkoutError.value = errorMessage(e)
  } finally {
    buying.value = null
  }
}

const comparison = [
  { feature: 'CV, profiles, redaction, PDF in all templates, languages', free: true, pro: true },
  { feature: 'Active invites', free: '3', pro: 'unlimited' },
  { feature: 'Visitor statistics: visits, time on CV, devices, reach per invite', free: true, pro: true },
  { feature: 'Heatmaps, attention per section, technology intent, session timeline', free: false, pro: true },
  { feature: 'Remove the "Created with" credit from PDFs', free: false, pro: true },
  { feature: 'Own domain', free: false, pro: true },
  { feature: 'Storage for images and files', free: '20 MB', pro: '200 MB' }
]

// --- PDF credit ----------------------------------------------------------------------------------

const hideCredit = ref(false)
const creditBusy = ref(false)
const creditError = ref('')
async function setHideCredit(value: boolean) {
  creditBusy.value = true
  creditError.value = ''
  try {
    await admin.updateAccount({ hideCredit: value })
    hideCredit.value = value
  } catch (e) {
    hideCredit.value = !value
    const code = (e as { data?: { error?: string, feature?: string } }).data
    creditError.value = code?.error === 'plan_limit' ? upgradeText({ feature: 'hideCredit' }) : errorMessage(e)
  } finally {
    creditBusy.value = false
  }
}

// --- Notifications -------------------------------------------------------------------------------

const notifyOnOpen = ref(props.user.notifyOnOpen !== false)
const notifyBusy = ref(false)
const notifyError = ref('')
async function setNotify(value: boolean) {
  notifyBusy.value = true
  notifyError.value = ''
  try {
    await admin.updateAccount({ notifyOnOpen: value })
    notifyOnOpen.value = value
  } catch (e) {
    notifyOnOpen.value = !value
    notifyError.value = errorMessage(e)
  } finally {
    notifyBusy.value = false
  }
}

// --- Own domain ------------------------------------------------------------------------------------

const domain = ref('')
const domainBusy = ref(false)
const domainError = ref('')
const domainTarget = ref('')
const sharedHost = import.meta.client ? window.location.hostname : ''

async function saveDomain() {
  domainBusy.value = true
  domainError.value = ''
  try {
    await admin.setDomain(domain.value.trim().toLowerCase())
    domain.value = ''
    domainTarget.value = ''
    emit('changed')
    await load()
  } catch (e) {
    const data = (e as { data?: { error?: string, target?: string } }).data
    if (data?.target) domainTarget.value = data.target
    domainError.value = data?.error === 'plan_limit' ? upgradeText({ feature: 'customDomain' }) : data?.error ? accountErrorText(data.error) : errorMessage(e)
  } finally {
    domainBusy.value = false
  }
}

async function removeDomain() {
  if (!confirm(`Remove ${user.value.customDomain}? Visitors of that domain will no longer reach your CV.`)) return
  domainBusy.value = true
  domainError.value = ''
  try {
    await admin.removeDomain()
    emit('changed')
    await load()
  } catch (e) {
    domainError.value = errorMessage(e)
  } finally {
    domainBusy.value = false
  }
}

// --- Privacy -----------------------------------------------------------------------------------------

const sessionsDone = ref(false)
const sessionsError = ref('')
async function revokeSessions() {
  if (!confirm('Sign out on all other devices and browsers? This browser stays signed in.')) return
  sessionsError.value = ''
  try {
    await admin.revokeSessions()
    sessionsDone.value = true
  } catch (e) {
    sessionsError.value = errorMessage(e)
  }
}

const deleteOpen = ref(false)
const deleteConfirm = ref('')
const deleting = ref(false)
const deleteError = ref('')
const confirmTarget = computed(() => user.value.tenantId ?? user.value.email)
async function deleteAccount() {
  deleting.value = true
  deleteError.value = ''
  try {
    await admin.deleteAccount(deleteConfirm.value.trim())
    emit('deleted')
  } catch (e) {
    const code = (e as { data?: { error?: string } }).data?.error
    deleteError.value = code ? accountErrorText(code) : errorMessage(e)
  } finally {
    deleting.value = false
  }
}

const statusColor: Record<string, 'success' | 'warning' | 'error' | 'neutral' | 'info'> = {
  completed: 'success', manual: 'info', unmatched: 'warning', refunded: 'neutral', chargeback: 'error'
}
</script>

<template>
  <div class="space-y-6" data-testid="admin-account">
    <p v-if="error" class="text-sm text-red-600 dark:text-red-400">{{ error }}</p>

    <!-- Plan & usage -->
    <section class="rounded-lg border border-gray-200 dark:border-gray-800 bg-white dark:bg-gray-900 p-5" data-testid="plan-card">
      <div class="flex items-center gap-3 flex-wrap">
        <UIcon :name="isPro ? 'i-lucide-crown' : 'i-lucide-user-round'" class="size-6 text-primary" />
        <h2 class="text-xl font-semibold">{{ isPro ? 'Pro' : 'Free' }} plan</h2>
        <UBadge :label="proLabel" :color="isPro ? 'primary' : 'neutral'" variant="subtle" />
        <span class="ml-auto text-sm text-gray-500">{{ user.email }} · signed in via {{ (user.logins ?? []).join(', ') || 'e-mail' }}</span>
      </div>
      <div v-if="usage" class="mt-4 grid gap-4 sm:grid-cols-2">
        <div>
          <div class="flex text-sm mb-1">
            <span>Active invites</span>
            <span class="ml-auto font-medium" data-testid="usage-invites">
              {{ usage.activeInvites }} / {{ plan.limits.activeInvites ?? '∞' }}
            </span>
          </div>
          <UProgress v-if="plan.limits.activeInvites" :model-value="usagePercent(usage.activeInvites, plan.limits.activeInvites)" size="sm"
            :color="usage.activeInvites >= plan.limits.activeInvites ? 'warning' : 'primary'" />
          <p v-else class="text-xs text-gray-500">Unlimited with Pro.</p>
        </div>
        <div>
          <div class="flex text-sm mb-1">
            <span>Storage</span>
            <span class="ml-auto font-medium" data-testid="usage-storage">{{ formatBytes(usage.assetBytes) }} / {{ formatBytes(plan.limits.assetBytes) }}</span>
          </div>
          <UProgress :model-value="usagePercent(usage.assetBytes, plan.limits.assetBytes)" size="sm"
            :color="usagePercent(usage.assetBytes, plan.limits.assetBytes) >= 90 ? 'warning' : 'primary'" />
        </div>
      </div>
    </section>

    <!-- Pro passes -->
    <section class="rounded-lg border border-gray-200 dark:border-gray-800 bg-white dark:bg-gray-900 p-5 space-y-4" data-testid="passes">
      <div>
        <h3 class="text-lg font-semibold">{{ isPro ? 'Extend Pro' : 'Get Pro' }}</h3>
        <p class="text-sm text-gray-500">
          Prepaid passes – one-time payment, no subscription, nothing to cancel. Buying while Pro is active extends it.
        </p>
      </div>
      <div class="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <div
          v-for="pass in passes" :key="pass.id"
          class="relative rounded-lg border p-4 flex flex-col gap-1"
          :class="pass.cheapest ? 'border-primary ring-2 ring-primary/30 bg-primary/5' : 'border-gray-200 dark:border-gray-800'"
          :data-testid="`pass-${pass.id}`"
        >
          <UBadge v-if="pass.cheapest" label="Best value" color="primary" size="sm" class="absolute -top-2.5 right-3" />
          <div class="text-sm font-medium text-gray-600 dark:text-gray-300">{{ passName(pass.days) }}</div>
          <div class="text-3xl font-bold tracking-tight" data-testid="pass-per-week">
            {{ money(pass.perWeek, pass.currency) }}<span class="text-sm font-normal text-gray-500"> / week</span>
          </div>
          <div class="text-xs text-gray-500">
            {{ money(pass.amount, pass.currency) }} total · {{ pass.days }} days
            <span v-if="pass.savingPercent > 0" class="text-green-700 dark:text-green-400 font-medium"> · save {{ pass.savingPercent }} % vs weekly</span>
          </div>
          <UButton
            v-if="canBuy" class="mt-3" block :label="isPro ? 'Extend' : 'Buy'" :color="pass.cheapest ? 'primary' : 'neutral'"
            :variant="pass.cheapest ? 'solid' : 'outline'" :disabled="!pass.priceId" :loading="buying === pass.id" @click="buy(pass)"
          />
        </div>
      </div>
      <UAlert
        v-if="billing && !canBuy" color="neutral" variant="subtle" icon="i-lucide-credit-card"
        description="Online payment is not set up yet – contact the operator to get Pro." data-testid="billing-off"
      />
      <p v-else-if="canBuy" class="text-xs text-gray-500">Payment is handled by Paddle (merchant of record); the invoice comes by e-mail. Prices include VAT where applicable.</p>
      <UAlert v-if="checkoutState === 'waiting'" color="info" variant="subtle" icon="i-lucide-loader-circle" description="Payment received – activating Pro…" />
      <UAlert v-else-if="checkoutState === 'done'" color="success" variant="subtle" icon="i-lucide-circle-check" :description="`Thank you! ${proLabel}.`" data-testid="checkout-done" />
      <UAlert v-else-if="checkoutState === 'timeout'" color="warning" variant="subtle" description="The payment is still being processed. Pro will be active in a few minutes – reload this page later." />
      <p v-if="checkoutError" class="text-sm text-red-600 dark:text-red-400">{{ checkoutError }}</p>

      <details class="text-sm">
        <summary class="cursor-pointer font-medium">Compare Free and Pro</summary>
        <table class="mt-2 w-full text-left">
          <thead class="text-xs text-gray-500">
            <tr><th class="py-1 font-normal" /><th class="py-1 px-3 font-medium">Free</th><th class="py-1 px-3 font-medium">Pro</th></tr>
          </thead>
          <tbody>
            <tr v-for="row in comparison" :key="row.feature" class="border-t border-gray-100 dark:border-gray-800">
              <td class="py-1.5">{{ row.feature }}</td>
              <td v-for="value in [row.free, row.pro]" :key="String(value)" class="py-1.5 px-3">
                <UIcon v-if="value === true" name="i-lucide-check" class="text-green-600" />
                <UIcon v-else-if="value === false" name="i-lucide-minus" class="text-gray-400" />
                <span v-else>{{ value }}</span>
              </td>
            </tr>
          </tbody>
        </table>
      </details>
    </section>

    <div class="grid gap-6 lg:grid-cols-2">
      <!-- Notifications & PDF credit -->
      <section class="rounded-lg border border-gray-200 dark:border-gray-800 bg-white dark:bg-gray-900 p-5 space-y-2">
        <h3 class="font-semibold">Notifications</h3>
        <USwitch
          :model-value="notifyOnOpen" :disabled="notifyBusy" :loading="notifyBusy"
          label="E-mail me when an invite is opened for the first time" data-testid="notify-on-open"
          @update:model-value="setNotify"
        />
        <p class="text-xs text-gray-500">Sent to {{ user.email }}.</p>
        <p v-if="notifyError" class="text-sm text-red-600 dark:text-red-400">{{ notifyError }}</p>
        <h3 class="font-semibold pt-3">PDF credit</h3>
        <USwitch
          :model-value="hideCredit" :disabled="!plan.limits.hideCredit || creditBusy" :loading="creditBusy"
          label="Remove the &quot;Created with&quot; credit from PDFs" data-testid="hide-credit"
          @update:model-value="setHideCredit"
        />
        <p v-if="!plan.limits.hideCredit" class="text-xs text-gray-500">Available with Pro.</p>
        <p v-if="creditError" class="text-sm text-red-600 dark:text-red-400">{{ creditError }}</p>
      </section>

      <!-- Own domain -->
      <section class="rounded-lg border border-gray-200 dark:border-gray-800 bg-white dark:bg-gray-900 p-5 space-y-3" data-testid="domain">
        <h3 class="font-semibold">Own domain</h3>
        <div v-if="user.customDomain" class="flex items-center gap-2 text-sm flex-wrap">
          <UIcon name="i-lucide-globe" class="text-primary" />
          <code>{{ user.customDomain }}</code>
          <UBadge v-if="!isPro" label="inactive without Pro" color="warning" variant="subtle" size="sm" />
          <UButton size="xs" color="error" variant="ghost" icon="i-lucide-trash-2" label="Remove" :loading="domainBusy" class="ml-auto" @click="removeDomain" />
        </div>
        <template v-if="plan.limits.customDomain">
          <form class="flex gap-2" @submit.prevent="saveDomain">
            <UInput v-model="domain" placeholder="cv.example.com" class="flex-1" aria-label="Domain" autocomplete="off" spellcheck="false" />
            <UButton type="submit" :label="user.customDomain ? 'Replace' : 'Connect'" :loading="domainBusy" :disabled="!domain.trim()" />
          </form>
          <div class="text-xs text-gray-500 space-y-1">
            <p>
              First point your domain here: a <b>CNAME</b> record to <code>{{ domainTarget || sharedHost }}</code>
              (for a root domain without CNAME support, <b>A/AAAA</b> records with the same IP addresses as <code>{{ domainTarget || sharedHost }}</code>).
              Then connect it above; we check the DNS record.
            </p>
            <p>After connecting, the operator activates HTTPS for the domain; this can take a little while.</p>
          </div>
        </template>
        <p v-else class="text-sm text-gray-500">Show your CV on your own domain (e.g. cv.your-name.com) with Pro.</p>
        <p v-if="domainError" class="text-sm text-red-600 dark:text-red-400" data-testid="domain-error">{{ domainError }}</p>
      </section>
    </div>

    <!-- Payments -->
    <section class="rounded-lg border border-gray-200 dark:border-gray-800 bg-white dark:bg-gray-900 p-5">
      <h3 class="font-semibold mb-2">Payments</h3>
      <p v-if="!payments.length" class="text-sm text-gray-500">No payments yet.</p>
      <table v-else class="w-full text-sm text-left">
        <thead class="text-xs text-gray-500">
          <tr><th class="py-1 font-normal">Date</th><th class="py-1 font-normal">Pass</th><th class="py-1 font-normal">Days</th><th class="py-1 font-normal">Amount</th><th class="py-1 font-normal">Status</th></tr>
        </thead>
        <tbody>
          <tr v-for="p in payments" :key="p.id" class="border-t border-gray-100 dark:border-gray-800">
            <td class="py-1.5">{{ formatDate(p.createdAt) }}</td>
            <td class="py-1.5">{{ p.pass === 'manual' ? 'set by the operator' : p.pass ?? '–' }}</td>
            <td class="py-1.5">{{ p.days || '–' }}</td>
            <td class="py-1.5">{{ p.amount ? money(p.amount, p.currency ?? 'EUR') : '–' }}</td>
            <td class="py-1.5"><UBadge :label="p.status" :color="statusColor[p.status] ?? 'neutral'" variant="subtle" size="sm" /></td>
          </tr>
        </tbody>
      </table>
      <p class="text-xs text-gray-500 mt-2">Invoices are sent by Paddle by e-mail.</p>
    </section>

    <!-- Privacy -->
    <section class="rounded-lg border border-gray-200 dark:border-gray-800 bg-white dark:bg-gray-900 p-5 space-y-4">
      <h3 class="font-semibold">Your data</h3>
      <div class="flex gap-2 flex-wrap">
        <UButton :href="admin.exportUrl()" external download icon="i-lucide-download" color="neutral" variant="outline" label="Download my data" />
        <UButton icon="i-lucide-log-out" color="neutral" variant="outline" label="Sign out on all devices" @click="revokeSessions" />
      </div>
      <p class="text-xs text-gray-500">The download is a ZIP with your account data, payments and all files of your CV.</p>
      <p v-if="sessionsDone" class="text-sm text-green-700 dark:text-green-400">All other sessions have been signed out.</p>
      <p v-if="sessionsError" class="text-sm text-red-600 dark:text-red-400">{{ sessionsError }}</p>

      <div class="border-t border-gray-200 dark:border-gray-800 pt-4">
        <UButton v-if="!deleteOpen" icon="i-lucide-trash-2" color="error" variant="outline" label="Delete account…" @click="deleteOpen = true" />
        <form v-else class="rounded-md border border-red-300 dark:border-red-800 p-4 space-y-3" data-testid="delete-account" @submit.prevent="deleteAccount">
          <p class="text-sm">
            This permanently deletes your account, your CV <code v-if="handle">{{ handle }}</code> with all files, invites, PDFs and visitor
            statistics. Invite links stop working. Payment records are kept anonymised for bookkeeping. This cannot be undone.
          </p>
          <UFormField :label="`Type ${confirmTarget} to confirm`">
            <UInput v-model="deleteConfirm" :placeholder="confirmTarget" class="w-full" autocomplete="off" />
          </UFormField>
          <p v-if="deleteError" class="text-sm text-red-600 dark:text-red-400">{{ deleteError }}</p>
          <div class="flex gap-2">
            <UButton type="submit" color="error" label="Delete my account" :loading="deleting"
              :disabled="deleteConfirm.trim().toLowerCase() !== confirmTarget.toLowerCase()" />
            <UButton color="neutral" variant="ghost" label="Cancel" @click="deleteOpen = false; deleteConfirm = ''" />
          </div>
        </form>
      </div>
    </section>
  </div>
</template>
