# Feature Specification: Dashboard Redesign — "Work overview"

**Feature Branch**: `027-dashboard-redesign`

**Created**: 2026-10-01

**Status**: Draft

**Input**: User request: "Redesign the DashboardPage". Agreed design: turn the decorative
dashboard (tall hero, five navigation-duplicating tiles, inline metric strip, two fixed
two-column panels) into a compact work overview.

**Constitution**: v1.3.0 — presentation-only change to the desktop client. No change to the
shared behaviour layer, the workstation service, contracts, storage, or retrieval.

## Clarifications

### Session 2026-10-01

- Q: What replaces the tall hero? → A: A compact page header (title "Dashboard" plus a
  supporting line) with the profile card, refresh control, and labeled History / My Docs /
  Admin quick actions on the right; Admin visible to administrators only.
- Q: How are library numbers shown? → A: Six clickable metric cards (Documents, Ready,
  Indexing, Failed, Questions, Mine), each with icon, count, label, and destination.
- Q: What happens to the five tiles? → A: Removed as tiles; History, My Docs, and Admin
  survive as labeled header buttons keeping their existing automation IDs.
- Q: How do the two panels behave at narrow widths? → A: Side by side when wide; stacked
  below each other when the page content width is 720 DIPs or less.
- Q: What shows when everything is empty? → A: A single whole-dashboard empty state with a
  clear "Open the library" call to action.
- Q: What happens to numbers on a failed refresh? → A: The last successfully loaded numbers
  stay on screen next to the error status (last-known-good).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Read the library at a glance (Priority: P1)

A signed-in user opens the Dashboard and immediately sees a compact header (title,
supporting line, profile, refresh, quick actions) plus six metric cards showing document,
ingestion, question, and personal counts. Each card navigates to the surface it describes.
Indexing and failed counts — already known to the app but currently invisible — are now
visible without opening another page.

**Why this priority**: The dashboard's core job is orientation; today the headline numbers
are a decorative strip and two ingestion states are hidden entirely.

**Independent Test**: Sign in with documents in mixed states and saved questions; verify
all six cards show the right counts and each navigates to its destination. Trigger a failed
refresh and verify the last-known numbers remain beside the error.

**Acceptance Scenarios**:

1. **Given** a signed-in user, **When** the Dashboard opens, **Then** a compact header
   shows the page title, a supporting line, the profile card, a refresh control, and
   labeled History, My Docs, and Admin actions (Admin for administrators only).
2. **Given** the dashboard, **When** viewed with library content, **Then** six metric
   cards each show an icon, a count, and a label for Documents, Ready, Indexing, Failed,
   Questions, and Mine.
3. **Given** a metric card, **When** activated, **Then** Documents, Ready, Indexing, and
   Failed open the Library; Questions opens Ask; Mine opens My Docs.
4. **Given** a failed refresh, **When** the error status appears, **Then** the previously
   loaded numbers remain visible next to the error (last-known-good) rather than clearing.
5. **Given** a non-administrator, **When** the dashboard opens, **Then** no Admin action
   is offered.

---

### User Story 2 - Scan recent activity in two panels (Priority: P2)

A user scans two panels — recent documents and recent questions — each with its own empty
state. At wide widths the panels sit side by side; at page content widths of 720 DIPs or
less they stack below each other so nothing is squeezed or clipped.

**Why this priority**: Recent activity is the second half of "work overview"; the current
fixed two-column layout squeezes both panels at narrow widths.

**Independent Test**: With and without recent documents/questions, verify each panel's
content and empty state at a wide width and at 800×600, confirming the stacked layout at
narrow content width.

**Acceptance Scenarios**:

1. **Given** recent documents, **When** the dashboard opens, **Then** the Recent documents
   panel lists them with identity, state, and time.
2. **Given** recent questions, **When** the dashboard opens, **Then** the Recent questions
   panel lists them with a preview and citation information.
3. **Given** no recent documents, **When** viewed, **Then** the documents panel shows its
   empty state instead of a blank list.
4. **Given** no recent questions, **When** viewed, **Then** the questions panel shows its
   empty state instead of a blank list.
5. **Given** page content at or below 720 DIPs, **When** the layout adapts, **Then** the
   panels stack below each other and both remain reachable without clipping.

---

### User Story 3 - Start from an empty library (Priority: P1)

A first-time user with an empty library and no saved questions sees one clear
whole-dashboard empty state with a call to action that opens the library, instead of a
page of zeros and blank panels.

**Why this priority**: The empty first-run moment must teach the next step; zeros and
blank panels do not.

**Independent Test**: Sign in to a fresh library with no saved questions; verify the
single empty state and that its action opens the library.

**Acceptance Scenarios**:

1. **Given** an empty library and no saved questions, **When** the dashboard opens,
   **Then** a single whole-dashboard empty state is shown with an "Open the library"
   call to action.
2. **Given** the empty state, **When** its action is activated, **Then** the library
   opens.
3. **Given** any documents or any saved questions exist, **When** the dashboard opens,
   **Then** the whole-dashboard empty state is not shown.

---

### User Story 4 - Operate the dashboard accessibly in any theme (Priority: P2)

A keyboard, touch, or screen-reader user can reach the header actions, metric cards, and
panels in a logical order. Light, Dark, and High Contrast themes keep every number, label,
and focus cue legible.

**Why this priority**: A denser overview must remain operable for every user, not only at
a wide desktop size in one theme.

**Independent Test**: Complete header-action, metric-card, and panel flows using keyboard
and a screen reader; inspect Light/Dark/High Contrast at 800×600 and a wider desktop size.

**Acceptance Scenarios**:

1. **Given** keyboard-only use, **When** moving through the dashboard, **Then** every
   action and card is reachable in logical order with visible focus.
2. **Given** screen-reader use, **When** navigating cards and actions, **Then** each
   announces its name and destination.
3. **Given** Light, Dark, or High Contrast, **When** the dashboard renders, **Then** all
   counts, labels, focus, and status remain distinguishable without relying on color
   alone.
4. **Given** an 800×600 window, **When** the dashboard is used, **Then** header, cards,
   and panels remain reachable without clipping.

### Edge Cases

- Refresh fails on first load (no last-known numbers yet): the error status shows and no
  stale or zero-pretending numbers are presented as loaded data.
- A metric count is zero while others are non-zero: the card still shows zero and still
  navigates.
- Long display names or file names: truncated in place, never displacing actions or
  cards.
- Administrator role changes mid-session: the Admin quick action appears or disappears on
  the next load without breaking the header layout.
- At narrow content widths the six cards wrap instead of clipping; the panels stack below
  them.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The dashboard MUST present a compact header with the page title, a
  supporting line, the profile card, a refresh control, and labeled History, My Docs,
  and Admin quick actions; the Admin action MUST appear for administrators only.
- **FR-002**: The dashboard MUST present six metric cards — Documents, Ready, Indexing,
  Failed, Questions, Mine — each showing an icon, a count, and a label.
- **FR-003**: Metric-card activation MUST navigate: Documents, Ready, Indexing, and Failed
  to the Library; Questions to Ask; Mine to My Docs.
- **FR-004**: Indexing and failed ingestion counts already known to the app MUST be
  visible as metric cards; no new data source may be introduced for them.
- **FR-005**: The dashboard MUST present Recent documents and Recent questions panels,
  each with a distinct empty state when it has no content.
- **FR-006**: The two panels MUST sit side by side at wide content widths and MUST stack
  below each other when the page content width is 720 DIPs or less.
- **FR-007**: When the library is empty AND there are no saved questions, the dashboard
  MUST show a single whole-dashboard empty state with an "Open the library" call to
  action; otherwise it MUST NOT show it.
- **FR-008**: On a failed refresh, the dashboard MUST keep the last successfully loaded
  numbers visible next to the error status (last-known-good).
- **FR-009**: All 11 existing Dashboard automation IDs MUST be preserved exactly (same
  ID, same navigation/meaning); the four new metric cards MUST receive stable new IDs.
- **FR-010**: Every card and control MUST be keyboard reachable in logical order with
  visible focus, per-control accessible names, interactive targets of at least 44 DIPs,
  and legible Light/Dark/High Contrast treatments using theme tokens only.
- **FR-011**: Existing dashboard behavior, handlers, and navigation MUST remain
  unchanged; no new API endpoint, query parameter, persisted data, project, package, or
  upload behavior is in scope.

### Key Entities

- **Metric card**: An icon, a count, a label, and a navigation destination; values are
  transient view state bound from already-available dashboard numbers.
- **Content panel**: Recent documents or Recent questions; transient list content plus a
  per-panel empty state.
- **Whole-dashboard empty state**: Shown only when the library is empty and no saved
  questions exist; carries the "Open the library" call to action.
- **Layout mode**: Wide (panels side by side) or compact stacked (content width at most
  720 DIPs); transient and derived from the current content width.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user can read all six headline counts and reach any of their six
  destinations from the dashboard within 10 seconds of it loading.
- **SC-002**: 100% of metric-card activations land on the card's agreed destination.
- **SC-003**: With an empty library and no saved questions, 100% of dashboard loads show
  the single empty state whose action opens the library.
- **SC-004**: At 800×600 and a wide desktop size, every header action, card, and panel
  remains reachable with no clipped critical content; Light, Dark, and High Contrast
  walkthroughs pass.
- **SC-005**: All 11 frozen automation IDs remain present with unchanged meaning, and the
  solution builds with the unit, contract, and offline integration suites passing and no
  unrelated assertion changes.
- **SC-006**: 100% of failed refreshes after a successful load keep the last-known
  numbers visible beside the error status.

## Assumptions

- The six headline numbers (total, ready, indexing, failed, questions, mine) already
  exist in the app and only need to be shown; no behavior or data work is required.
- The minimum supported desktop window remains 800×600; panels stack when the page
  content width is at most 720 DIPs.
- Existing navigation targets (Library, Ask, History, My Docs, Admin) and their access
  rules are unchanged.
- The design-system rules from `specs/023-design-system-refinement/design-system.md`
  (theme tokens only, shared text styles, valid icon/theme keys) apply to the redesigned
  page.

## Dependencies

- Existing dashboard numbers, recent-documents/questions content, refresh/retry behavior,
  and navigation from the current client.
- Existing WPF page, shared dashboard view model, WPF-UI controls, and status/theme
  conventions.
- Existing Dashboard automation-ID baseline and cross-client theme/accessibility rules.

## Out of Scope

- Any new API endpoint or query parameter, new persisted data, new project or package.
- Any change to upload behavior or a dashboard upload command.
- Any change to the shared behaviour layer, workstation service, contracts, storage, or
  retrieval behavior.
- Changes to authentication, authorization rules, or navigation destinations.
