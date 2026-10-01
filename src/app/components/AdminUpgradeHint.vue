<script setup lang="ts">
/**
 * Upgrade hint for a plan limit (402 plan_limit), the storage quota (413 quota_exceeded) or a Pro-only feature
 * (docs/REQUIREMENTS_SAAS.md §4 S4.1). The button switches to the Account tab (provided by the admin page in user
 * mode); the super-admin sees the hint without it.
 */
import { OPEN_ACCOUNT_TAB, upgradeText, type UpgradeReason } from '~/utils/account'

const props = defineProps<{ reason: UpgradeReason, text?: string, locked?: boolean }>()
const openAccount = inject(OPEN_ACCOUNT_TAB, null)
const admin = useAdmin()
const canUpgrade = computed(() => !!openAccount && admin.mode.value === 'session')
const message = computed(() => props.text ?? upgradeText(props.reason))
</script>

<template>
  <div
    class="rounded-md border border-primary/40 bg-primary/5 text-sm flex gap-3"
    :class="locked ? 'px-3 py-8 flex-col items-center text-center' : 'p-3 items-start flex-wrap'"
    data-testid="upgrade-hint" role="status"
  >
    <UIcon :name="locked ? 'i-lucide-lock' : 'i-lucide-sparkles'" class="text-primary shrink-0" :class="locked ? 'size-8' : 'size-5 mt-0.5'" />
    <p :class="locked ? 'max-w-md' : 'flex-1 min-w-48'">{{ message }}</p>
    <UButton v-if="canUpgrade" size="sm" icon="i-lucide-circle-arrow-up" label="Get Pro" @click="openAccount?.()" />
  </div>
</template>
