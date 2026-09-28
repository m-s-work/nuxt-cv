# Plan: Visitor & Session Tracking

Status: **planned** – nothing in this document is implemented yet.
Related: [`REQUIREMENTS_ACCESS_AND_TENANCY.md`](REQUIREMENTS_ACCESS_AND_TENANCY.md) (§4 invites, §10 non-goals, §11 R11.4).

The CV owner wants to know **who looked at the CV, for how long, and what they were interested in**.
This document defines the tracking model, what is recorded, how interest is derived from it, the API and
storage, privacy rules and the implementation phases.

Keywords MUST / SHOULD / MAY follow RFC 2119.

---

## 1. Goals and non-goals

**Goals**

- G1 Per invite: how many different people/devices opened it, how often, and for how long.
- G2 Per session: a timeline of what the visitor did (time, clicks, scrolling, sections read).
- G3 A cursor **heatmap** per invite (and aggregated per tenant) on top of the CV layout.
- G4 An **interest profile** per visitor and per invite: which sections, entries and technologies drew
  attention, plus a comparable interest score.

**Non-goals**

- No cross-site tracking. The IP address and the browser fingerprint recorded per session (§3.2) are only
  used inside this CV site and never shared or matched with external data sets.
- No full session replay (DOM recording). Too invasive and too heavy for the value it adds.
- No third-party analytics (Google Analytics, Hotjar, …). Everything stays first-party in the API's volume.
- No tracking of the showcase page, the PDF renderer (`cv_render`, `?print=1`) or admin previews.
- No tracking features visible to invitees (R11.4) – see §9 for the tension with privacy notices.

---

## 2. Terms

| Term | Meaning |
|---|---|
| **Visitor group** | All visitors that came in through the same **invite** (`inviteId`). A QR invite (`source: "pdf-qr"`) is its own group, linked to its parent, so "opened from the printed PDF" stays distinguishable. Public-profile visitors on a tenant host form the group `public:<profile>`. |
| **Visitor** | One browser/device, identified by a persistent first-party cookie `cv_vid` (§3). One visitor MAY belong to several groups (e.g. redeemed two invites). |
| **Session** | A contiguous timeframe in which a visitor had the CV **open** in one tab (§4). |
| **Event** | One recorded observation inside a session (page shown, click, section visible, …) (§5). |
| **Anchor** | A stable, trackable element of the CV, marked with `data-track="<kind>:<key>"` (§6). |

Hierarchy: **Tenant → Visitor group (invite) → Visitor (cookie) → Session (tab/timeframe) → Events**.

---

## 3. Visitor identification

- R3.1 On the first `GET /api/cv` that grants access, the API sets `cv_vid` if absent:
  a random 128-bit id, signed with Data Protection (same key ring as `cv_access`),
  `HttpOnly; Secure; SameSite=Lax; Path=/; Max-Age=13 months`.
- R3.2 The browser never reads `cv_vid`; the API resolves it from the cookie on every tracking request.
  JavaScript therefore cannot leak or forge it.
- R3.3 The visitor → group relation is recorded server-side from `cv_access` (which invite) at the time of the
  request. A visitor who later redeems another invite is added to that group as well; sessions are attributed
  to the invite that was active when the session started.
- R3.4 Server-side we store per visitor only: `firstSeen`, `lastSeen`, coarse device class
  (`desktop | tablet | mobile`), browser family, OS family, preferred language, time-zone offset.
  IP address and fingerprint are stored **per session**, not per visitor (§3.2).
- R3.5 Useful derived fact: **number of distinct visitors per invite**. An invite with 3+ visitors was very
  likely forwarded inside the company – a strong interest signal (§7).

### 3.2 IP address and fingerprint per session

Every session records the network origin and a browser fingerprint. Together with `cv_vid` they allow
recognising a visitor again after the cookie was deleted, spotting the same person on several devices
of one network, and seeing *where* an invite is being opened.

- R3.6 **IP address.** The API stores the client IP of the `session_start` request with the session
  (taken from `X-Forwarded-For` of the trusted reverse proxy only). If the IP changes during the session
  (mobile network, VPN), each distinct IP is added to `session_ips` with first/last seen.
- R3.7 **Derived network info** is resolved once per session from the IP with a **local** database
  (e.g. DB-IP Lite / MaxMind GeoLite2 files in the data volume – no external lookup service):
  country, region, city (coarse), ASN and AS organisation (e.g. "ACME Corp" vs. "Deutsche Telekom").
- R3.8 **Fingerprint.** On session start the client computes a fingerprint from stable browser traits:
  user agent / client hints, platform, languages, time zone, screen size and colour depth, device memory,
  hardware concurrency, touch support, installed-font probe (fixed list), canvas and WebGL renderer hashes,
  audio-context hash. Only the resulting **SHA-256 hash** (`fp`) and a small list of component hashes
  (`fpParts`, to compute similarity when single traits change) are sent – never the raw values.
- R3.9 The fingerprint is sent inside the `session_start` event; the server additionally derives a
  server-side hash from request headers (`User-Agent`, `Accept-Language`, client hints) so a session without
  JavaScript fingerprint still gets `fpServer`.
- R3.10 **Linking.** The report groups sessions into a *probable person* when they share `cv_vid`, or share
  `fp` (exact) / ≥ 90 % of `fpParts` **and** the same IP /24 (IPv4) or /48 (IPv6) within 30 days.
  Linking is shown as "probably the same visitor" and never overrides the cookie-based visitor id.
- R3.11 IP and fingerprint are owner-only data (R9.5), fall under the same consent gating as `cv_vid`
  (R9.4) and are shortened after the retention period (R9.2).

---

## 4. Session definition

A session is the time a visitor had the CV open in one tab.

- R4.1 The client creates a random `sessionId` on page load and keeps it in `sessionStorage`
  (survives reloads of the same tab, not new tabs).
- R4.2 **Start**: first `session_start` event after the CV is rendered (splash screen finished, `access` granted).
- R4.3 **Heartbeat**: while the tab is visible the client sends a heartbeat every 15 s (piggy-backed on the event batch).
- R4.4 **End**: `pagehide` / tab close (sent with `navigator.sendBeacon`), or no heartbeat for 30 min.
  A tab hidden for more than 30 min that becomes visible again starts a **new** session.
- R4.5 Three durations are computed per session:

  | Duration | Definition |
  |---|---|
  | **Open time** | `end - start` (tab existed). |
  | **Visible time** | Sum of intervals with `document.visibilityState === "visible"`. |
  | **Active time** | Visible time in which there was input (mouse move, scroll, key, touch) in the last 30 s. The most honest "reading time". |

- R4.6 Concurrent tabs of the same visitor are separate sessions; reports MAY merge overlapping sessions of a
  visitor into one **visit**.

---

## 5. What is recorded (events)

The client buffers events and flushes them in batches (§8). Every event carries `t` (ms since session start).

| Event | Payload | Purpose |
|---|---|---|
| `session_start` | locale, viewport w×h, device pixel ratio, color scheme, referrer kind (`direct`/`qr`/`link`), local hour, `fp`, `fpParts` (§3.2), `appSha`, `cvVersion` (§6.4) | Context of the visit; IP is taken server-side from the request |
| `heartbeat` | visible, active | Durations (R4.5) |
| `visibility` | `visible` / `hidden` | Tab switches, visible time |
| `section_view` | anchor, visible ms (≥ 50 % in viewport), max visible ratio | Dwell time per section / entry |
| `scroll` | max depth % (sampled, only on new maximum) | Scroll depth |
| `click` | anchor (nearest `data-track` ancestor), target kind (`link`, `button`, `badge`, `image`, `text`), relative x/y in anchor | Clicks + click heatmap |
| `pointer` | batch of cursor samples (§6) | Cursor heatmap |
| `hover` | anchor, ms (≥ 800 ms on one entry) | Lingering on an entry |
| `tech_filter` | tech name, on/off | What they are looking for |
| `timeline` | action (zoom, select entry), anchor | Timeline interest |
| `lightbox` | image anchor, ms open | Screenshot / project interest |
| `expand` | anchor (details opened) | Wants more detail |
| `link_out` | kind (`github`, `linkedin`, `project`, `other`), anchor | Leaves to verify |
| `contact` | kind (`mailto`, `tel`, `copy_email`, `copy_phone`) | Strongest intent signal |
| `copy` | anchor, character count (not the text) | Takes notes / pastes into ATS |
| `select` | anchor, character count | Reads closely |
| `pdf` | locale, outcome | Downloads to share/keep |
| `print` | – (`beforeprint`) | Prints the CV |
| `locale_switch` | from, to | Language preference |
| `theme_switch` | dark/light | UX only |
| `rage_click` | anchor, count (≥ 3 clicks in 1 s within 30 px) | UX problem (something looks clickable but is not) |
| `dead_click` | anchor | UX problem |
| `version` | new `appSha` / `cvVersion` (R6.11) | Keep heatmap samples version-correct |
| `session_end` | reason (`pagehide`, `timeout`) | Close of the session |

- R5.1 Event payloads MUST NOT contain CV text, form input or copied text – only anchors, kinds and numbers.
  Anchors are resolved to labels **server-side** against the master CV when the owner looks at reports.
- R5.2 Events for anchors that were not delivered to the visitor cannot occur (redaction is server-side), so
  tracking never reveals hidden data to anyone but the owner.

---

## 6. Anchors and cursor heatmap

The CV layout is responsive, so raw page coordinates are not comparable between visitors.
Positions are therefore stored **relative to anchors**.

### 6.1 Anchors

- R6.1 Components mark trackable elements with `data-track="<kind>:<key>"`, e.g.
  `section:experiences`, `experience:<id>`, `project:<id>`, `skill:<name>`, `tech:<name>`, `contact:email`.
- R6.2 `<key>` MUST be stable across CV edits: an explicit `id` field in the master CV where present,
  otherwise a short hash of identifying fields (e.g. company + start date). Array indices MUST NOT be used.
- R6.3 Section-level anchors are required; entry-level anchors are added per component
  (`CvExperiences`, `CvProjects`, `CvSkills`, `CvPreferredTechs`, `CvTimeline`, `CvStudies`, `CvDetails`, …).

### 6.2 Cursor sampling

- R6.4 Pointer positions are sampled at most every 100 ms and only when the cursor moved ≥ 8 px.
  Idle cursors produce no samples; touch devices send taps only (no cursor).
- R6.5 Each sample is stored as `(anchor, xRel, yRel, dt)` where `xRel`/`yRel` ∈ [0,1] are relative to the
  innermost anchor's bounding box, plus the **layout breakpoint** (`sm`/`md`/`lg`/`xl`).
- R6.6 Samples are quantised to a 1 % grid before sending; ~1–3 KB per active minute.

### 6.3 Aggregation and rendering

- R6.7 The API aggregates samples into
  `heat_cells(tenant, group, breakpoint, appSha, cvVersion, anchor, cellX, cellY, weight)`
  where weight = dwell ms. Raw samples MAY be deleted after aggregation (retention §9).
- R6.8 To render a heatmap, the owner view loads the CV in the matching breakpoint **and version** (§6.4),
  looks up every anchor's bounding box in the live DOM and paints the cells into it (canvas overlay).
- R6.9 Heatmap types: **move** (cursor dwell), **click**, **attention** (section_view dwell, one colour per
  section/entry – works for mobile visitors too).

### 6.4 Versions (git SHA and CV version)

Relative coordinates only mean something against the layout and content the visitor actually saw.
A changed component (new padding, reordered block) or an edited entry (longer text) moves the hot spots
inside an anchor. Every session is therefore stamped with the versions it was rendered with.

| Version | Source | Changes when |
|---|---|---|
| `appSha` | Git commit SHA of the SPA build, injected at build time (`NUXT_PUBLIC_GIT_SHA`, from Coolify's `SOURCE_COMMIT` build arg, fallback `git rev-parse HEAD`, else `dev`) and exposed as `runtimeConfig.public.gitSha` | Frontend code / layout changes |
| `apiSha` | Git commit SHA of the API build (`GIT_SHA` build arg → assembly metadata), recorded server-side | Redaction or API behaviour changes |
| `cvVersion` | SHA-256 (first 16 hex chars) of the **redacted CV JSON** the visitor received, computed by the API and returned in `/api/cv` as `cvVersion` | Master CV edited, profile/invite overrides changed, other locale |
| `layoutVersion` | Existing `Pdf__LayoutVersion` / `CV_PDF_LAYOUT_VERSION` | Print layout changes (PDF events only) |

- R6.10 `session_start` carries `appSha` and `cvVersion` (as seen by the client); the server adds `apiSha`
  and verifies `cvVersion` against its own computation (mismatch → stored anyway, flagged).
- R6.11 If `appSha` or `cvVersion` changes during a session (deploy or CV edit while the tab is open and the
  CV is re-fetched, e.g. locale switch), the client sends a `version` event and following samples are
  aggregated under the new version.
- R6.12 **CV snapshots.** The CV lives in the data volume, not in git, so its history is kept by the API:
  every distinct redacted CV is stored once as `cv_snapshots(tenant, cvVersion, locale, json, firstSeen)`
  (deduplicated by hash, stored when first delivered). The heatmap view renders that snapshot, so a heatmap
  of an old CV version shows the text the visitor actually read.
- R6.13 **App versions.** Old SPA builds are not kept. The heatmap view renders with the current app and
  shows a warning when `appSha` of the selected cells differs; the owner can filter by `appSha`
  (`git log` of that SHA explains what changed). Heatmaps across versions MAY be merged, but only per anchor
  (section-level attention stays comparable; cursor/click cells are only exact within one `appSha`).
- R6.14 Reports list the versions per invite (first/last seen per `appSha` / `cvVersion`), so "they read the
  CV before I added project X" is visible.

---

## 7. Measuring interest (ideas)

Raw time and clicks are noisy. The following signals turn them into "what did they care about".

### 7.1 Per section / entry

- **Reading ratio** – active dwell time ÷ expected reading time (word count ÷ 230 wpm).
  `< 0.2` skimmed, `0.2–0.8` scanned, `> 0.8` read. Normalises long vs short entries.
- **Revisits** – scrolling back to an entry within a session, or opening it again in a later session.
- **Hover lingering** – cursor resting ≥ 800 ms on an entry (people point at what they read).
- **Detail seeking** – expand, lightbox, timeline select, outbound project/GitHub link on that entry.
- **Text selection / copy** – on an entry: they are taking notes or pasting into an ATS.

### 7.2 Per visitor

- **Technology intent** – tech filter toggles and clicks on tech badges reveal *what they are hiring for*
  (e.g. repeatedly filtered `Kubernetes`, `C#`). Probably the single most actionable signal.
- **Contact intent** – `mailto`/`tel` clicks, copied e-mail/phone.
- **Keep intent** – PDF download, print.
- **Return behaviour** – number of sessions, days between them; returning after days means a later
  interview round or comparison.
- **Coverage** – share of sections seen at all; "only looked at the header" vs "read everything".
- **Context** – local time of day and device (desktop during office hours = work review; mobile evening =
  personal interest), locale switch (native language of the reader).
- **Tab switching** – frequent visibility changes while on one section may indicate comparing against
  a job ad or other candidates (interpret carefully).

### 7.3 Per network (from IP, §3.2)

- **Organisation** – the AS organisation shows whether the CV was opened from a company network
  ("ACME Corp") or a home/mobile provider. Opened from the inviting company's own network = reviewed at work.
- **Location** – city/country of each session; several cities for one invite = forwarded to another office.
- **Same network, several devices** – sessions with different `cv_vid`/fingerprints from one company network
  = several colleagues looked at it (complements "spread", §7.4).
- **Unexpected organisation** – an invite opened from a network of a *different* company than the one it
  was issued to (e.g. recruiting agency → client) shows where the CV travelled.

### 7.4 Per visitor group (invite)

- **Spread** – distinct visitors per invite (forwarded internally), QR-invite scans (printed PDF passed around), distinct probable persons
  and organisations (§3.2, §7.3).
- **Time to first open** after invite creation, and time between first and last visit.
- **Consensus** – sections that several visitors of the same invite read → what that company cares about.

### 7.5 Interest score

A simple, explainable 0–100 score per visitor and per group, e.g.

```
score = 25·min(activeMinutes/5, 1)
      + 20·coverage
      + 15·min(sessions-1, 3)/3
      + 15·detailSeeking      (0..1, capped count of expand/lightbox/link_out)
      + 15·contactOrKeep      (1 if contact, pdf or print happened)
      + 10·min(visitors-1, 3)/3   (group only: spread)
```

Weights are configuration, not code. The report MUST always show the underlying numbers next to the score.

### 7.6 UX signals (for the owner as product maker)

Rage clicks, dead clicks and sections with zero attention show layout problems, independent of any single visitor.

---

## 8. API

### 8.1 Ingest

| Method & path | Auth | Purpose |
|---|---|---|
| `POST /api/events` | `cv_access` or public profile + `cv_vid` | Batch of events of one session. `204`. |

```jsonc
{ "sessionId": "b1…", "seq": 7, "bp": "lg", "appSha": "71676ff…", "cvVersion": "9f2c…",
  "events": [ { "t": 15230, "e": "section_view", "a": "experience:acme-2021", "ms": 8400, "r": 0.93 },
              { "t": 15310, "e": "pointer", "s": [["experience:acme-2021", 42, 17, 100], …] } ] }
```

- R8.1 Access is checked like for `/api/cv` (R3.4 of the requirements); without valid access the request is
  dropped (`204`, nothing stored), so the endpoint reveals nothing.
- R8.2 Tenant, group and visitor come **only** from the cookies/host, never from the payload.
- R8.3 Batches are flushed every 10 s, at 50 events, and on `visibilitychange → hidden` / `pagehide` via
  `navigator.sendBeacon`. `seq` makes retries idempotent.
- R8.4 Limits: body ≤ 64 KB, ≤ 1 request/s per session, unknown event types ignored, rate-limited per IP.
- R8.6 The API MUST only trust `X-Forwarded-For` from the configured reverse proxy (Coolify/Traefik);
  otherwise the socket address is used, so visitors cannot spoof the stored IP.
- R8.5 No tracking when `?print=1`, with the `cv_render` ticket, for admin previews, or when the browser
  sends `DNT: 1` / `Sec-GPC: 1` (client does not start the tracker; server discards anyway).

### 8.2 Owner reports (admin key)

| Method & path | Purpose |
|---|---|
| `GET /api/admin/tenants/{tenant}/analytics/invites` | Per invite: visitors, sessions, active time, last visit, score. |
| `GET /api/admin/tenants/{tenant}/analytics/invites/{id}` | Visitors of the invite, section/entry ranking, tech intent, score breakdown. |
| `GET /api/admin/tenants/{tenant}/analytics/visitors/{vid}` | Sessions of a visitor. |
| `GET /api/admin/tenants/{tenant}/analytics/sessions/{sid}` | Event timeline of a session, incl. IPs, network info and fingerprint. |
| `GET /api/admin/tenants/{tenant}/analytics/persons` | Probable persons (sessions linked by cookie, fingerprint and network, R3.10). |
| `GET /api/admin/tenants/{tenant}/analytics/heatmap?group=&bp=&appSha=&cvVersion=&type=move\|click\|attention` | Aggregated heat cells for rendering (R6.8), filterable by version (§6.4). |
| `GET /api/admin/tenants/{tenant}/analytics/cv-snapshots/{cvVersion}` | Redacted CV as the visitor saw it (R6.12). |
| `DELETE /api/admin/tenants/{tenant}/analytics/visitors/{vid}` | Erase a visitor (data subject request). |

Anchor labels in responses are resolved from the master CV (unredacted – the owner may see everything).

---

## 9. Privacy and legal

Tracking identifiable business contacts (recruiters, hiring managers) is processing of personal data.
This section is a planning basis, **not legal advice**; it MUST be reviewed before going live.

- R9.1 **Data minimisation**: no CV text or typed/copied text in events. IP and fingerprint are stored per
  session only (§3.2), fingerprints only as hashes, geo lookups only locally.
- R9.2 **Retention**: raw events 90 days; full IP addresses and fingerprints 90 days, then the IP is
  truncated (/24 resp. /48) and the fingerprint removed (network info and "probable person" links stay);
  aggregated heat cells and session summaries 13 months; CV snapshots as long as a session or heat cell
  references them. Expired data is deleted by a daily job. Revoking an invite MAY optionally purge its tracking data.
- R9.3 **Opt-out**: `DNT`/`GPC` honoured (R8.5); visitor erasure endpoint (§8.2).
- R9.4 **Consent / notice (open decision).** Under ePrivacy rules (e.g. Austrian TKG 2021 §165, German TDDDG §25)
  a non-essential cookie such as `cv_vid`, **browser fingerprinting** (it reads information from the device just
  like a cookie) and client-side behaviour recording usually require **consent**; stored IP addresses are
  personal data under GDPR;
  GDPR Art. 13 requires information about the processing. This conflicts with R11.4 ("no tracking features
  visible to invitees"). Proposed resolution:
  - R11.4 is about **not advertising** analytics as a product feature on the showcase / CV. A neutral, legally
    required **privacy notice** (linked in the footer) is not a feature advertisement and is allowed.
  - Option A (recommended): short consent prompt on first visit ("usage statistics to improve this CV");
    without consent only server-side counters (`useCount`, `lastUsedAt`, request-level visit time) are kept,
    no `cv_vid`, no fingerprint, no stored IP, no client tracker.
  - Option B: no consent prompt, only a privacy notice, and restrict to cookieless, non-behavioural
    session counts. Heatmaps and per-visitor profiles would not be possible.
  The owner has to choose; the implementation MUST support Option A's gating either way.
- R9.5 Tracking data is owner-only and never part of `/api/cv` or any invitee-visible response.

---

## 10. Storage (SQLite `app.db`)

```
visitors        (id, tenant, first_seen, last_seen, device, browser, os, lang, tz_offset)
visitor_groups  (visitor_id, invite_id | public_profile, first_seen)
sessions        (id, visitor_id, tenant, invite_id, started_at, ended_at, open_ms, visible_ms, active_ms,
                 locale, breakpoint, end_reason,
                 ip, ip_country, ip_region, ip_city, asn, as_org,          -- §3.2, IP truncated after 90 days
                 fp, fp_parts_json, fp_server,                             -- removed after 90 days
                 app_sha, api_sha, cv_version, version_mismatch)           -- §6.4
session_ips     (session_id, ip, first_seen, last_seen)                    -- IP changes within a session
persons         (id, tenant, first_seen, last_seen)                        -- probable person (R3.10)
person_links    (person_id, session_id, reason: cookie|fp|fp_similar+net)
events          (session_id, seq, t, type, anchor, payload_json)          -- raw, 90 days
section_stats   (session_id, anchor, visible_ms, active_ms, views, hovers) -- summary, 13 months
heat_cells      (tenant, group, breakpoint, app_sha, cv_version, type, anchor, cx, cy, weight) -- aggregate, 13 months
cv_snapshots    (tenant, cv_version, locale, json, first_seen)            -- redacted CV per version, kept while referenced
```

Expected volume: ~5–20 KB per session raw; a CV with a few hundred sessions per year stays in the low MB range,
so SQLite in the existing volume is sufficient. Summaries are computed when a session ends (or times out).

---

## 11. Frontend implementation notes

- A single composable `useVisitorTracking()` started from `pages/index.vue` after `useCv()` reports access;
  no-op in print mode, without consent, or with DNT/GPC.
- Listeners: `IntersectionObserver` (section_view), passive `pointermove`/`scroll`, delegated `click`,
  `visibilitychange`, `pagehide`, `beforeprint`, `copy`, `selectionchange` (debounced).
- Components only add `data-track` attributes and call `track(event)` for semantic actions
  (tech filter, timeline, lightbox, PDF, locale switch) – no tracking logic inside components.
- Tracker failures MUST never affect the CV (fire-and-forget, errors swallowed).
- The tracker adds < 5 KB gzipped; no external libraries needed (heatmap rendering lives in the owner view only).

---

## 12. Phases

| Phase | Scope | Result |
|---|---|---|
| **P1 Sessions & time** | `cv_vid`, `/api/events`, sessions with open/visible/active time, IP + local geo/ASN lookup + fingerprint per session, version stamping (`appSha`, `apiSha`, `cvVersion`, CV snapshots), `visitors`/`sessions` tables, invite report | "Who opened it, how often, how long" |
| **P2 Sections & clicks** | Anchors in all components, `section_view`, `scroll`, `click`, semantic events (tech filter, PDF, contact, …), section ranking | "What did they look at" |
| **P3 Heatmap** | Pointer sampling, version-keyed `heat_cells`, heatmap endpoint, overlay renderer on CV snapshots | Cursor / click / attention heatmaps |
| **P4 Interest** | Reading ratio, tech intent, spread, network/organisation signals, probable-person linking, interest score, session timeline | Comparable interest per visitor and invite |
| **P5 Owner UI** | Small owner-only dashboard (separate from the invitee SPA, admin key) | Reports without curl |
| **P0 (before P1 goes live)** | Decide R9.4 (consent option), privacy notice, retention job | Legally deployable |

Each phase updates `REQUIREMENTS_ACCESS_AND_TENANCY.md` (§8 API, §9 data layout, §10 non-goals) and adds
API tests (`api/CvApi.Tests`) plus frontend tests for the composable.

---

## 13. Open questions

1. Consent model (R9.4 Option A vs B)?
2. Should revoking an invite delete its tracking data, or keep it for the owner's history?
3. Should the owner be notified (e-mail / webhook) on events like "invite opened for the first time" or
   "contact clicked"?
4. Track the showcase page anonymously (conversion: showcase → invite code entered)?
5. Where does the owner UI (P5) live – separate static app, or a route of the API?
6. Which local geo/ASN database (DB-IP Lite: CC BY, no account; MaxMind GeoLite2: licence key) and how is it updated?
