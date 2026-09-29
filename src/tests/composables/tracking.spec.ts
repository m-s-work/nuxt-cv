import { describe, it, expect } from 'vitest'
import {
  CONSENT_TEXT_VERSION, breakpointOf, isInView, isRageClick, linkKind, looksClickable, relativePosition, scrollDepth, sha256, timelineAnchor
} from '~/utils/tracking'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'

describe('tracking helpers', () => {
  it('computes SHA-256 like the API', () => {
    expect(sha256('')).toBe('e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855')
    expect(sha256('abc')).toBe('ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad')
    expect(sha256('ä'.repeat(100))).toHaveLength(64)
  })

  it.each([[390, 'sm'], [800, 'md'], [1100, 'lg'], [1440, 'xl']])('breakpoint of %i is %s', (w, bp) => {
    expect(breakpointOf(w)).toBe(bp)
  })

  it('positions points on the 1 % grid inside an anchor', () => {
    expect(relativePosition(150, 60, { left: 100, top: 50, width: 200, height: 40 })).toEqual([25, 25])
    expect(relativePosition(0, 999, { left: 100, top: 50, width: 200, height: 40 })).toEqual([0, 100])
  })

  it('counts tall anchors as visible when they cover half the viewport', () => {
    expect(isInView(300, 500, 1000)).toBe(true)
    expect(isInView(200, 500, 1000)).toBe(false)
    expect(isInView(500, 3000, 1000)).toBe(true)
  })

  it('classifies links', () => {
    expect(linkKind('mailto:a@b.c', 'cv.test')).toBe('mailto')
    expect(linkKind('tel:+43', 'cv.test')).toBe('tel')
    expect(linkKind('https://github.com/x', 'cv.test')).toBe('github')
    expect(linkKind('https://www.linkedin.com/in/x', 'cv.test')).toBe('linkedin')
    expect(linkKind('https://example.org', 'cv.test')).toBe('project')
    expect(linkKind('/#projects', 'cv.test')).toBeNull()
    expect(linkKind('blob:https://cv.test/1', 'cv.test')).toBeNull()
  })

  it('detects rage clicks', () => {
    expect(isRageClick([{ t: 0, x: 0, y: 0 }, { t: 300, x: 5, y: 5 }, { t: 600, x: 2, y: 1 }])).toBe(true)
    expect(isRageClick([{ t: 0, x: 0, y: 0 }, { t: 300, x: 5, y: 5 }, { t: 1600, x: 2, y: 1 }])).toBe(false)
    expect(isRageClick([{ t: 0, x: 0, y: 0 }, { t: 300, x: 100, y: 5 }, { t: 600, x: 2, y: 1 }])).toBe(false)
  })

  it('finds dead-click candidates', () => {
    expect(looksClickable('SPAN', 'pointer', false)).toBe(true)
    expect(looksClickable('img', 'auto', false)).toBe(true)
    expect(looksClickable('P', 'text', false)).toBe(false)
    expect(looksClickable('A', 'pointer', true)).toBe(false)
  })

  it('maps timeline entries and scroll depth', () => {
    expect(timelineAnchor('exp-3')).toBe('experience:3')
    expect(timelineAnchor('project-7')).toBe('project:7')
    expect(timelineAnchor('x')).toBeUndefined()
    expect(scrollDepth(500, 500, 2000)).toBe(50)
  })

  it('uses the same consent text version as the API', () => {
    const api = readFileSync(resolve(__dirname, '../../../api/CvApi/Tracking/TrackingPolicy.cs'), 'utf8')
    expect(api).toContain(`TextVersion = "${CONSENT_TEXT_VERSION}"`)
  })
})
