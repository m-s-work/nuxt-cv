import { describe, it, expect } from 'vitest'
import { legalOperator, localizedCountry, splitAddress } from '~/utils/legal'

describe('splitAddress', () => {
  it.each([
    ['Hauptstraße 1 | 1010 Wien', ['Hauptstraße 1', '1010 Wien']],
    ['Hauptstraße 1\\n1010 Wien', ['Hauptstraße 1', '1010 Wien']],
    ['Hauptstraße 1\n1010 Wien\n', ['Hauptstraße 1', '1010 Wien']],
    ['Hauptstraße 1, 1010 Wien', ['Hauptstraße 1, 1010 Wien']],
    ['', []]
  ])('%j', (input, expected) => {
    expect(splitAddress(input)).toEqual(expected)
  })
})

describe('localizedCountry', () => {
  it('translates common countries for German pages only', () => {
    expect(localizedCountry('Austria', 'de')).toBe('Österreich')
    expect(localizedCountry('Austria', 'en')).toBe('Austria')
    expect(localizedCountry('Narnia', 'de')).toBe('Narnia')
  })
})

describe('legalOperator', () => {
  it('reads the runtime config', () => {
    const op = legalOperator({
      legalName: ' Max Muster ', legalAddress: 'Weg 1|1010 Wien', legalEmail: 'hi@example.org', legalVatId: '', legalCountry: ''
    }, 'de')
    expect(op).toEqual({
      name: 'Max Muster', address: ['Weg 1', '1010 Wien'], email: 'hi@example.org', vatId: '', country: 'Österreich', configured: true
    })
  })
  it('is not configured without name, address or e-mail', () => {
    expect(legalOperator({}, 'en')).toMatchObject({ configured: false, country: 'Austria' })
    expect(legalOperator({ legalName: 'X', legalEmail: 'x@example.org' }, 'en').configured).toBe(false)
  })
})
