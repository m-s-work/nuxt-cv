import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import { createI18n } from 'vue-i18n'
import CvConsentModal from '~/components/CvConsentModal.vue'

// Nuxt UI components replaced by plain elements; the <i18n> block of the component provides the texts.
function mountSuspended(component: typeof CvConsentModal, options: { props: Record<string, unknown> }) {
  return Promise.resolve(mount(component, {
    ...options,
    global: {
      plugins: [createI18n({ legacy: false, locale: 'en', fallbackLocale: 'en' })],
      stubs: {
        UIcon: { template: '<i />' },
        UButton: { props: ['label', 'color', 'variant', 'size', 'icon', 'block'], emits: ['click'], template: '<button :class="[color, variant, size]" @click="$emit(\'click\')">{{ label }}</button>' }
      }
    }
  }))
}

const consent = {
  required: true,
  state: null,
  policyVersion: 'abc',
  controller: 'Bob Builder',
  contact: 'privacy@bob.test',
  retention: { identifiersMonths: 13, eventsMonths: 13, summaryMonths: 25 }
}

describe('CvConsentModal', () => {
  it('shows both choices with the same style and emits them', async () => {
    const wrapper = await mountSuspended(CvConsentModal, { props: { consent, name: 'Bob' } })
    const accept = wrapper.get('[data-testid="consent-accept"]')
    const decline = wrapper.get('[data-testid="consent-decline"]')
    expect(accept.text()).toBe('Accept')
    expect(decline.text()).toBe('Continue without accepting')
    // Equal buttons (R9.21): identical classes.
    expect(accept.classes()).toEqual(decline.classes())
    expect(accept.classes()).not.toContain('truncate')
    await accept.trigger('click')
    await decline.trigger('click')
    expect(wrapper.emitted('accept')).toHaveLength(1)
    expect(wrapper.emitted('decline')).toHaveLength(1)
  })

  it('names the owner and hides the details until asked', async () => {
    const wrapper = await mountSuspended(CvConsentModal, { props: { consent, name: 'Bob' } })
    expect(wrapper.text()).toContain('Bob is glad you are taking a look')
    expect(wrapper.text()).toContain('never passed on to third parties')
    expect(wrapper.find('[data-testid="consent-details"]').exists()).toBe(false)
    await wrapper.get('[aria-controls="consent-details"]').trigger('click')
    const details = wrapper.get('[data-testid="consent-details"]').text()
    expect(details).toContain('IP address')
    expect(details).toContain('fingerprint')
    expect(details).toContain('privacy@bob.test')
    expect(details).toContain('13 months after your last visit')
  })

  it('has no close button on the first decision', async () => {
    const first = await mountSuspended(CvConsentModal, { props: { consent, name: 'Bob' } })
    expect(first.find('[aria-label="Close"]').exists()).toBe(false)
    const reopened = await mountSuspended(CvConsentModal, { props: { consent: { ...consent, state: 'accept' as const }, name: 'Bob', reopened: true } })
    expect(reopened.find('[aria-label="Close"]').exists()).toBe(true)
  })

  it('mentions browser signals', async () => {
    const wrapper = await mountSuspended(CvConsentModal, { props: { consent: { ...consent, signals: 'gpc' }, name: 'Bob' } })
    expect(wrapper.text()).toContain('Your browser asks websites not to track you')
  })
})
