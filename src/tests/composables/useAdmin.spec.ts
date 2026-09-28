import { describe, it, expect } from 'vitest'
import { buildOverrides, changeSummary, groupInvites, inviteStatus, parseJsonc, pinStatus, splitList, type AdminInvite } from '~/composables/useAdmin'

const invite = (patch: Partial<AdminInvite> = {}): AdminInvite => ({
  id: 'a', tenant: 'demo', profile: 'full', label: '', createdAt: '2026-01-01T00:00:00Z', useCount: 0, ...patch
})

describe('inviteStatus', () => {
  const now = new Date('2026-06-01T00:00:00Z')

  it('is active by default', () => {
    expect(inviteStatus(invite(), now)).toBe('active')
  })

  it('prefers revoked over expired and exhausted', () => {
    expect(inviteStatus(invite({ revokedAt: '2026-02-01T00:00:00Z', expiresAt: '2026-03-01T00:00:00Z', maxUses: 1, useCount: 1 }), now)).toBe('revoked')
  })

  it('detects expiry and exhausted usage', () => {
    expect(inviteStatus(invite({ expiresAt: '2026-05-31T00:00:00Z' }), now)).toBe('expired')
    expect(inviteStatus(invite({ maxUses: 2, useCount: 2 }), now)).toBe('exhausted')
    expect(inviteStatus(invite({ maxUses: 2, useCount: 1 }), now)).toBe('active')
  })
})

describe('buildOverrides', () => {
  const empty = { flags: {}, hiddenFields: '', grants: '', replaceGrants: false }

  it('returns undefined when nothing is overridden', () => {
    expect(buildOverrides({ ...empty, flags: { hidePhoto: 'inherit' } })).toBeUndefined()
  })

  it('maps flag choices, hidden fields and replaced grants', () => {
    expect(buildOverrides({
      flags: { hidePhoto: 'on', hideCompanies: 'off', hideMedia: 'inherit' },
      hiddenFields: 'details.phone, projects\n',
      grants: 'contact',
      replaceGrants: true
    })).toEqual({
      flags: { hidePhoto: true, hideCompanies: false },
      hiddenFields: ['details.phone', 'projects'],
      grants: ['contact']
    })
  })

  it('can replace grants with an empty list', () => {
    expect(buildOverrides({ ...empty, replaceGrants: true })).toEqual({ grants: [] })
  })
})

describe('groupInvites', () => {
  it('puts QR invites right after their parent', () => {
    const rows = groupInvites([
      invite({ id: 'p1' }),
      invite({ id: 'p2' }),
      invite({ id: 'q1', parentId: 'p1', source: 'pdf-qr' }),
      invite({ id: 'orphan', parentId: 'gone' })
    ])
    expect(rows.map(r => [r.id, r.depth])).toEqual([['p1', 0], ['q1', 1], ['p2', 0], ['orphan', 0]])
  })
})

describe('splitList', () => {
  it('trims and drops empty entries', () => {
    expect(splitList(' a, ,b\nc ')).toEqual(['a', 'b', 'c'])
  })
})

describe('parseJsonc', () => {
  it('accepts comments and trailing commas like the API', () => {
    expect(parseJsonc(`{
      // line comment
      "a": "http://x // not a comment", /* block */
      "b": [1, 2,],
      "c": "quote \\" , ]",
    }`)).toEqual({ a: 'http://x // not a comment', b: [1, 2], c: 'quote " , ]' })
  })

  it('rejects invalid JSON', () => {
    expect(() => parseJsonc('{ broken')).toThrow()
    expect(() => parseJsonc('{ /* open')).toThrow()
  })
})

describe('pinStatus', () => {
  const revisions = {
    current: 'bbbbbbb2',
    modified: false,
    revisions: [
      { sha: 'bbbbbbb2', registeredAt: '', outdated: false },
      { sha: 'aaaaaaa1', registeredAt: '', outdated: true }
    ]
  }

  it('matches full SHAs and prefixes', () => {
    expect(pinStatus('bbbbbbb2', revisions)).toBe('current')
    expect(pinStatus('AAAAAAA', revisions)).toBe('outdated')
  })

  it('matches fetched tags', () => {
    expect(pinStatus('sent-acme', { ...revisions, revisions: [{ sha: 'aaaaaaa1', registeredAt: '', outdated: true, refs: ['sent-acme'] }] })).toBe('outdated')
  })

  it('reports unknown revisions as missing', () => {
    expect(pinStatus('ccccccc', revisions)).toBe('missing')
    expect(pinStatus('ccccccc', null)).toBe('missing')
  })
})

describe('changeSummary', () => {
  it('lists changed files and marks added/removed ones', () => {
    expect(changeSummary({
      sha: 'a', registeredAt: '', outdated: true,
      changes: [{ path: 'cv.en.json', change: 'modified' }, { path: 'assets/new.jpg', change: 'added' }, { path: 'assets/old.jpg', change: 'removed' }]
    })).toBe('cv.en.json, assets/new.jpg (new), assets/old.jpg (removed)')
    expect(changeSummary(undefined)).toBe('')
  })
})
