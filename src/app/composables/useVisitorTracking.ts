/**
 * Visitor tracking (docs/VISITOR_SESSION_TRACKING.md). Runs only after the visitor accepted the consent modal and
 * never in print mode. Collects sessions (open / visible / active time), attention per anchor (`data-track`),
 * clicks, cursor samples for the heatmap and a few semantic events, and sends them in batches to `/api/events`.
 * Failures never affect the CV: everything is fire-and-forget.
 */
import {
  anchorOf, breakpointOf, collectFingerprint, isInView, isRageClick, linkKind, looksClickable, randomId, relativePosition, scrollDepth
} from '~/utils/tracking'

export interface TrackingVersions {
  appSha: string
  cvSourceSha?: string
  cvVersion?: string
}

interface StoredSession {
  sessionId: string
  tabId: string
  seq: number
  startedAt: number
  lastActivity: number
  appSha: string
  cvVersion?: string
  cvSourceSha?: string
}

type TrackEvent = { e: string, t: number, a?: string } & Record<string, unknown>

const STORAGE_KEY = 'cv-track'
const RESUME_LIMIT_MS = 30 * 60 * 1000
const FLUSH_MS = 10_000
const HEARTBEAT_MS = 15_000
const IDLE_MS = 30_000
const MAX_QUEUE = 50
const HOVER_MIN_MS = 800
const ENTRY_ANCHOR = /^(experience|study|project|other):/

// Module state: one tracker per tab.
let running = false
let apiBase = '/api'
let session: StoredSession | null = null
let previousSessionId: string | undefined
let startPayload: Record<string, unknown> | null = null
let queue: TrackEvent[] = []
let pointerSamples: Array<[string, number, number, number]> = []
let cleanup: Array<() => void> = []
let visibleAcc = 0
let activeAcc = 0
let lastInput = 0
let hiddenSince: number | null = null
let maxScroll = 0
let lastSample = { t: 0, x: -100, y: -100 }
let hover: { anchor: string, since: number } | null = null
let recentClicks: Array<{ t: number, x: number, y: number }> = []
const inView = new Map<string, number>()        // anchor → accumulated visible ms (not yet sent)
const currentlyVisible = new Set<string>()
let observed = new WeakSet<Element>()
let observer: IntersectionObserver | null = null
let context: { locale: string, versions: TrackingVersions } | null = null
let referrer: 'link' | 'direct' = 'direct'

function now() { return Date.now() }

function readStored(): StoredSession | null {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY)
    return raw ? JSON.parse(raw) as StoredSession : null
  } catch { return null }
}

function store() {
  if (!session) return
  try { sessionStorage.setItem(STORAGE_KEY, JSON.stringify(session)) } catch { /* storage blocked */ }
}

/** Queues an event of the running session; a no-op when tracking is off. */
export function trackEvent(e: string, data: Record<string, unknown> = {}) {
  if (!running || !session) return
  queue.push({ e, t: now() - session.startedAt, ...data })
  session.lastActivity = now()
  if (queue.length >= MAX_QUEUE) flush()
}

/** Anchor of the entry a lightbox group belongs to ("project-3" → "project:3"). */
export function trackLightbox(groupId: string | number | undefined) {
  const match = /^(experience|study|project|other)-(.+)$/.exec(String(groupId ?? ''))
  trackEvent('lightbox', match ? { a: `${match[1]}:${match[2]}` } : {})
}

function sameVersions(stored: StoredSession, versions: TrackingVersions) {
  return stored.appSha === versions.appSha && stored.cvVersion === versions.cvVersion && stored.cvSourceSha === versions.cvSourceSha
}

function newSession(versions: TrackingVersions, previous?: StoredSession | null): StoredSession {
  return {
    sessionId: randomId(),
    tabId: previous?.tabId ?? randomId(),
    seq: 0,
    startedAt: now(),
    lastActivity: now(),
    appSha: versions.appSha,
    cvVersion: versions.cvVersion,
    cvSourceSha: versions.cvSourceSha
  }
}

function buildStart(locale: string, versions: TrackingVersions): Record<string, unknown> {
  let fingerprint: { fp?: string, fpParts?: string[] } = {}
  try { fingerprint = collectFingerprint() } catch { /* canvas/webgl blocked */ }
  return {
    locale,
    viewportW: window.innerWidth,
    viewportH: window.innerHeight,
    colorScheme: document.documentElement.classList.contains('dark') || matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light',
    referrer,
    localHour: new Date().getHours(),
    tzOffset: new Date().getTimezoneOffset(),
    language: navigator.language,
    appSha: versions.appSha,
    cvSourceSha: versions.cvSourceSha,
    cvVersion: versions.cvVersion,
    ...fingerprint
  }
}

/** Sends the queue. Uses sendBeacon when the page goes away. */
function flush(beacon = false) {
  if (!running || !session) return
  emitSectionViews()
  if (pointerSamples.length) {
    queue.push({ e: 'pointer', t: now() - session.startedAt, s: pointerSamples.splice(0, 2000) })
  }
  if (!queue.length && !startPayload) return
  const body = JSON.stringify({
    sessionId: session.sessionId,
    seq: session.seq++,
    tabId: session.tabId,
    previousSessionId,
    bp: breakpointOf(window.innerWidth),
    start: startPayload ?? undefined,
    events: queue.splice(0, queue.length)
  })
  store()
  const url = `${apiBase}/events`
  try {
    if (beacon && navigator.sendBeacon?.(url, new Blob([body], { type: 'application/json' }))) {
      startPayload = null
      return
    }
    const start = startPayload
    fetch(url, { method: 'POST', body, credentials: 'include', keepalive: true, headers: { 'Content-Type': 'application/json' } })
      .then((r) => { if (r.ok && startPayload === start) startPayload = null })
      .catch(() => { /* ignored */ })
  } catch { /* ignored */ }
}

function emitSectionViews() {
  for (const [anchor, ms] of inView) {
    if (ms >= 500) trackEventRaw('section_view', { a: anchor, ms })
    inView.set(anchor, 0)
  }
  for (const [anchor, ms] of inView) if (ms === 0 && !currentlyVisible.has(anchor)) inView.delete(anchor)
}

function trackEventRaw(e: string, data: Record<string, unknown>) {
  if (session) queue.push({ e, t: now() - session.startedAt, ...data })
}

function observeAnchors() {
  if (!observer) return
  document.querySelectorAll('[data-track]').forEach((el) => {
    const anchor = (el as HTMLElement).dataset.track ?? ''
    if (anchor.startsWith('tech:') || anchor.startsWith('contact:') || observed.has(el)) return
    observed.add(el)
    observer!.observe(el)
  })
}

function onIntersect(entries: IntersectionObserverEntry[]) {
  for (const entry of entries) {
    const anchor = (entry.target as HTMLElement).dataset.track
    if (!anchor) continue
    const visible = entry.isIntersecting && isInView(entry.intersectionRect.height, entry.boundingClientRect.height, window.innerHeight)
    if (visible) currentlyVisible.add(anchor)
    else currentlyVisible.delete(anchor)
  }
}

/** Every second: visible / active time and attention of the anchors in view. */
function tick() {
  if (!session || document.visibilityState !== 'visible') return
  visibleAcc += 1000
  if (now() - lastInput < IDLE_MS) activeAcc += 1000
  for (const anchor of currentlyVisible) inView.set(anchor, (inView.get(anchor) ?? 0) + 1000)
}

function heartbeat() {
  if (!session) return
  trackEventRaw('heartbeat', { vis: visibleAcc, act: activeAcc })
  visibleAcc = 0
  activeAcc = 0
  observeAnchors()
}

function onInput() {
  lastInput = now()
  if (session) session.lastActivity = lastInput
}

function onPointerMove(event: PointerEvent) {
  onInput()
  if (event.pointerType !== 'mouse' || !session) return
  const t = now()
  if (t - lastSample.t < 100 || Math.hypot(event.clientX - lastSample.x, event.clientY - lastSample.y) < 8) return
  const target = anchorOf(event.target as Element)
  const dt = Math.min(t - lastSample.t, 2000)
  lastSample = { t, x: event.clientX, y: event.clientY }
  const anchor = target?.dataset.track
  if (target && anchor) {
    const [x, y] = relativePosition(event.clientX, event.clientY, target.getBoundingClientRect())
    pointerSamples.push([anchor, x, y, dt])
  }
  // Lingering on an entry (R5 "hover").
  const entry = (event.target as Element)?.closest?.('[data-track^="experience:"],[data-track^="study:"],[data-track^="project:"],[data-track^="other:"]') as HTMLElement | null
  const entryAnchor = entry?.dataset.track
  if (hover?.anchor !== entryAnchor) {
    if (hover && t - hover.since >= HOVER_MIN_MS) trackEvent('hover', { a: hover.anchor, ms: t - hover.since })
    hover = entryAnchor && ENTRY_ANCHOR.test(entryAnchor) ? { anchor: entryAnchor, since: t } : null
  }
}

function onClick(event: MouseEvent) {
  onInput()
  const el = event.target as Element
  const target = anchorOf(el)
  const anchor = target?.dataset.track
  const kind = el.closest('a[href]') ? 'link' : el.closest('button') ? 'button' : el.closest('img') ? 'image'
    : anchor?.startsWith('tech:') ? 'badge' : 'text'
  if (target && anchor) {
    const [x, y] = relativePosition(event.clientX, event.clientY, target.getBoundingClientRect())
    trackEvent('click', { a: anchor, x, y, k: kind })
  }
  const link = el.closest('a[href]') as HTMLAnchorElement | null
  if (link) {
    // Website links go through /api/go/…: classified by the host they lead to.
    const lk = linkKind(link.dataset.linkHost ? `https://${link.dataset.linkHost}/` : link.getAttribute('href') ?? '', location.host)
    if (lk === 'mailto' || lk === 'tel') trackEvent('contact', { a: anchor, kind: lk })
    else if (lk) trackEvent('link_out', { a: anchor, kind: lk })
  }
  if (looksClickable(el.tagName, getComputedStyle(el).cursor, !!el.closest('a[href],button,input,select,textarea,label,[role="button"],[tabindex]:not([tabindex="-1"])')))
    watchForDeadClick(anchor, target, event)
  recentClicks.push({ t: now(), x: event.clientX, y: event.clientY })
  recentClicks = recentClicks.slice(-3)
  if (isRageClick(recentClicks)) {
    trackEvent('rage_click', { a: anchor, count: 3 })
    recentClicks = []
  }
}

/**
 * Dead click (R5 "dead_click"): something that looks clickable was clicked and within a second nothing happened –
 * no DOM change, no navigation, no scrolling. Points at elements that promise an action they do not have.
 */
function watchForDeadClick(anchor: string | undefined, target: HTMLElement | null, event: MouseEvent) {
  const href = location.href
  const scrollY = window.scrollY
  let changed = false
  const observer = new MutationObserver(() => { changed = true; observer.disconnect() })
  observer.observe(document.body, { subtree: true, childList: true, attributes: true, characterData: true })
  const position = target ? relativePosition(event.clientX, event.clientY, target.getBoundingClientRect()) : undefined
  setTimeout(() => {
    observer.disconnect()
    if (changed || location.href !== href || Math.abs(window.scrollY - scrollY) > 2) return
    trackEvent('dead_click', { a: anchor, ...(position ? { x: position[0], y: position[1] } : {}) })
  }, 1000)
}

function onScroll() {
  onInput()
  const depth = scrollDepth(window.scrollY, window.innerHeight, document.documentElement.scrollHeight)
  if (depth >= maxScroll + 5 || (depth === 100 && maxScroll < 100)) {
    maxScroll = depth
    trackEvent('scroll', { d: depth })
  }
}

function onCopy() {
  const selection = document.getSelection()
  const n = selection?.toString().length ?? 0
  if (!n) return
  const anchor = anchorOf(selection?.anchorNode?.parentElement)?.dataset.track
  trackEvent('copy', { a: anchor, n })
  if (anchor === 'contact:email' || anchor === 'contact:phone') trackEvent('contact', { a: anchor, kind: anchor === 'contact:email' ? 'copy_email' : 'copy_phone' })
}

let selectTimer: ReturnType<typeof setTimeout> | undefined
function onSelectionChange() {
  clearTimeout(selectTimer)
  selectTimer = setTimeout(() => {
    const selection = document.getSelection()
    const n = selection?.toString().length ?? 0
    if (n >= 20) trackEvent('select', { a: anchorOf(selection?.anchorNode?.parentElement)?.dataset.track, n })
  }, 1000)
}

function onVisibility() {
  if (!session || !context) return
  if (document.visibilityState === 'hidden') {
    hiddenSince = now()
    trackEvent('visibility', { state: 'hidden' })
    flush(true)
    return
  }
  const wasHidden = hiddenSince
  hiddenSince = null
  // Back after more than 30 minutes: a new session, linked to the old one (R4.4, R4.7).
  if (wasHidden !== null && now() - wasHidden > RESUME_LIMIT_MS) {
    split('resume_timeout', context.versions)
    return
  }
  trackEvent('visibility', { state: 'visible' })
}

function listen<K extends keyof WindowEventMap>(target: Window | Document, type: K | string, handler: (e: never) => void, options?: AddEventListenerOptions) {
  target.addEventListener(type, handler as EventListener, options)
  cleanup.push(() => target.removeEventListener(type, handler as EventListener, options))
}

function begin(locale: string, versions: TrackingVersions) {
  const stored = readStored()
  if (stored && sameVersions(stored, versions) && now() - stored.lastActivity < RESUME_LIMIT_MS) {
    session = stored                           // reload of the same tab: continue the session
    startPayload = null
    previousSessionId = undefined
  } else {
    session = newSession(versions, stored)
    previousSessionId = stored?.sessionId      // same tab, other versions or too old: linked session
    startPayload = buildStart(locale, versions)
  }
  store()
}

/** Ends the current session and starts a linked one (version change or resume after 30 min, R4.7). */
function split(reason: 'version_change' | 'resume_timeout', versions: TrackingVersions) {
  if (!session || !context) return
  trackEventRaw('session_end', { reason })
  flush()
  const previous = session
  session = newSession(versions, previous)
  previousSessionId = previous.sessionId
  startPayload = buildStart(context.locale, versions)
  maxScroll = 0
  store()
  flush()
}

/**
 * Starts tracking (after consent). Calling it again with other versions (locale switch, re-fetched CV) splits the
 * session; with the same versions it does nothing.
 */
export function startTracking(options: { apiBase: string, locale: string, versions: TrackingVersions, viaLink?: boolean }) {
  if (!import.meta.client) return
  apiBase = options.apiBase
  referrer = options.viaLink ? 'link' : 'direct'
  if (running && session && context) {
    context.locale = options.locale
    if (!sameVersions(session, options.versions)) {
      context.versions = options.versions
      split('version_change', options.versions)
    }
    return
  }
  context = { locale: options.locale, versions: options.versions }
  running = true
  lastInput = now()
  begin(options.locale, options.versions)

  observer = new IntersectionObserver(onIntersect, { threshold: [0, 0.1, 0.25, 0.5, 0.75, 1] })
  observed = new WeakSet()
  observeAnchors()
  const ticker = setInterval(tick, 1000)
  const beat = setInterval(heartbeat, HEARTBEAT_MS)
  const flusher = setInterval(() => flush(), FLUSH_MS)
  cleanup.push(() => { clearInterval(ticker); clearInterval(beat); clearInterval(flusher); observer?.disconnect(); observer = null })

  listen(document, 'pointermove', onPointerMove, { passive: true })
  listen(document, 'click', onClick, { capture: true, passive: true })
  listen(window, 'scroll', onScroll, { passive: true })
  listen(document, 'keydown', onInput, { passive: true })
  listen(document, 'touchstart', onInput, { passive: true })
  listen(document, 'copy', onCopy)
  listen(document, 'selectionchange', onSelectionChange)
  listen(document, 'visibilitychange', onVisibility)
  listen(window, 'beforeprint', () => trackEvent('print'))
  listen(window, 'pagehide', () => { heartbeat(); flush(true) })
  flush()
}

/** Stops tracking immediately (withdrawal) and forgets the tab's session. */
export function stopTracking() {
  if (!running) return
  running = false
  cleanup.forEach(fn => fn())
  cleanup = []
  queue = []
  pointerSamples = []
  inView.clear()
  currentlyVisible.clear()
  session = null
  context = null
  try { sessionStorage.removeItem(STORAGE_KEY) } catch { /* ignored */ }
}

export function isTracking() {
  return running
}

let appShaPromise: Promise<string> | null = null

/** Git commit of the frontend build (/version.json, written by the web image), "dev" locally. */
export function loadAppSha(): Promise<string> {
  appShaPromise ??= fetch('/version.json', { cache: 'no-store' })
    .then(r => (r.ok ? r.json() : null))
    .then((v: { commit?: string } | null) => (v?.commit && v.commit !== 'unknown' ? v.commit : 'dev'))
    .catch(() => 'dev')
  return appShaPromise
}
