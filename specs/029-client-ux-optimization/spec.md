# Feature Specification: Client UX Optimization

**Feature Branch**: `029-client-ux-optimization`

**Created**: 2026-10-05

**Status**: Draft

**Input**: User description: "Optimize UI/UX"

**Constitution**: v1.3.0 — no amendment needed (presentation-only; shared client behavior layer, workstation API, and contracts untouched).

## Context

The desktop client already completed a consistency-and-feedback polish pass (feature 028: shared headers, one status idiom, in-control busy, copy confirmations, sign-out confirmation, reliable "Ask again", role-aware empty states, responsive table columns). This feature is the next pass: it optimizes the *experience* of the surfaces that 028 left structurally unchanged — how quickly a person can find and act on a record, how much work a repeated task takes, and how gracefully the app behaves under real content volume and real window sizes. It does not redo 028's consistency work, and it adds no new product capability.

The gaps this feature closes were observed in the client as it stands after 028:

- The People directory is the only data surface with search, filtering, and sorting. The Library table, History, and My Documents offer none — finding one document or one past question means scrolling or paging through everything.
- The Dashboard shows six metrics, but only the failure count carries any urgency signal, and a metric card gives no hint of what activating it does. A person scanning for "is anything wrong?" has to read every number.
- The Library table keeps its header row pinned, but the People directory's column headers scroll away with its rows, so sort affordances disappear mid-list.
- The People directory and the Library table force horizontal scrolling of their primary content at the minimum supported window size; 028's graceful-collapse treatment covered only the Library's two secondary columns.
- The most common admin chores — resetting a password, changing a role, enabling or disabling an account — each require locating the person first, with no shortcut from search.
- Destructive and confirming actions are not consistently placed: in some dialogs the confirming action sits beside the dismissing one with no fixed order, so the "safe" click position moves between dialogs.
- Nothing in the app communicates what is happening during the wait that matters most: after sign-in, and while an answer is being prepared, the user sees only a spinner.

## Clarifications

### Session 2026-10-05

- Q: When an administrator searches the People directory, where should each matching person's account actions appear? → A: In the existing details panel, which follows the highlighted match — the first match is highlighted automatically as the administrator types, so the panel shows that person's actions with no separate selection step.
- Q: When someone types in the Library, History, or My Documents search, how quickly must the filtered results appear? → A: Within a tenth of a second of the text changing.
- Q: In every confirmation and task dialog, which action should sit in the easiest-to-reach position at the end of the dialog? → A: The dismissing action goes last — the confirming action comes first and the dismissing action (Cancel, Close, Keep) is always the final button, so the reflex click is the safe one.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Find any record without scrolling (Priority: P1)

A person looking for a specific document, a past question, or one of their own uploads can find it by typing part of its name or content, instead of paging or scrolling through the whole list. Today only the People directory can be searched; the Library, History, and My Documents cannot, so the time to find something grows linearly with how much the company has stored.

**Why this priority**: Findability is the difference between a tool that scales with the library and one that only works while the library is small. It is the single change that most reduces effort on the three most-visited data surfaces.

**Independent Test**: With 50+ documents and 50+ saved questions, locate a specific document in the Library, a specific past question in History, and a specific upload in My Documents using only the new search — no paging or scrolling.

**Acceptance Scenarios**:

1. **Given** the Library contains more documents than fit on one page, **When** the user types part of a filename, **Then** the table shows only matching documents within a tenth of a second, and the match count is stated.
2. **Given** a search that matches nothing, **When** the results render, **Then** an empty state explains that nothing matched and offers to clear the search, and the full list returns when cleared.
3. **Given** History or My Documents, **When** the user searches, **Then** matching entries appear and non-matching entries do not, using the same search presentation as the Library.
4. **Given** an active search, **When** the underlying list refreshes or the user navigates away and back, **Then** the search text and its results are preserved for the session.

---

### User Story 2 - See what needs attention at a glance (Priority: P1)

A person opening the Dashboard can tell within a glance whether anything needs action — documents that failed to index, a question still processing — without reading every metric. Metric cards that lead somewhere signal that they can be activated and where they lead. Today every metric looks identical regardless of urgency, and the cards that navigate give no visual cue that they do anything.

**Why this priority**: The Dashboard is the landing surface after every sign-in. If problems are visually silent there, they are discovered late, in the Library, by someone who thought to go looking.

**Independent Test**: Load the Dashboard once with a failed document and once with none; confirm the failure metric is visually distinct only when non-zero, and that every metric card shows where it leads.

**Acceptance Scenarios**:

1. **Given** one or more documents failed to index, **When** the Dashboard loads, **Then** the failure metric is visually distinguished as needing attention, and it is not distinguished when the count is zero.
2. **Given** any Dashboard metric card, **When** the user looks at it or focuses it, **Then** it is clear the card can be activated and which page it opens.
3. **Given** the Dashboard is still loading, **When** the user sees it, **Then** placeholder content of the final layout is shown rather than a blank area or a bare spinner, and it is replaced by real content when the load completes.
4. **Given** a load failure, **When** the Dashboard recovers via retry, **Then** the attention signals reflect the freshly loaded data, not stale placeholders.

---

### User Story 3 - Work the dense tables at any window size (Priority: P2)

A person working in the People directory or the Library table at the minimum supported window size can still read, sort, and act on rows without horizontal scrolling, and column headers stay visible while scrolling through long lists. Today the People directory's headers scroll away with its rows, and both tables force horizontal scrolling of their primary content below a comfortable width.

**Why this priority**: These are the two surfaces where real work happens in bulk. Losing the header mid-scroll and being forced to scroll sideways are the two defects that most slow sustained use, but they affect power users rather than every session.

**Independent Test**: Open each table with 100+ rows at the minimum supported window size with the navigation pane open; scroll to the bottom and confirm headers remain visible and no horizontal scrollbar appears for primary content.

**Acceptance Scenarios**:

1. **Given** a long People directory list, **When** the user scrolls down, **Then** the column headers — including the sort affordance and current sort direction — remain visible.
2. **Given** the minimum supported window size, **When** the Library table or People directory is shown, **Then** primary content (name, status, and the row's actions) is usable without horizontal scrolling, with lower-priority columns collapsing and restoring as the window grows.
3. **Given** a collapsed column, **When** the user needs that information, **Then** it is still reachable (for example by widening the window or, for the People directory, in the selected person's details) — collapsing hides, it does not delete.
4. **Given** a sorted column, **When** columns collapse and restore, **Then** the sort order and its visible indicator are unchanged.

---

### User Story 4 - Finish frequent admin tasks in fewer steps (Priority: P2)

An administrator who already knows who they mean — "reset Dana's password", "disable this account" — can get there straight from the people search: the first matching person is highlighted automatically as they type, and the details panel shows that person's actions immediately, so there is no separate step of finding the row and selecting it. The confirming action and the dismissing action occupy the same positions in every dialog, so the safe click never moves.

**Why this priority**: These are the highest-frequency admin chores and each currently costs a full select-then-act round trip. Dialog consistency prevents the class of mistake where a confirming click in one dialog is a dismissing click in another.

**Independent Test**: Reset a password and disable an account by typing the person's name and using only the details panel that follows the highlighted match — never clicking a row; then open every confirmation dialog and confirm the primary and dismissing actions occupy consistent positions.

**Acceptance Scenarios**:

1. **Given** the People directory search, **When** the administrator types a name, **Then** the first matching person is highlighted automatically and the details panel shows that person's frequent account actions immediately, and activating one opens the same confirmation used everywhere else.
2. **Given** any confirmation or task dialog, **When** it renders, **Then** the confirming action comes first and the dismissing action is the final button, matching every other dialog.
3. **Given** a destructive confirming action, **When** it renders, **Then** it is visually distinguished from a non-destructive confirming action, consistently across dialogs.
4. **Given** an account action taken from the details panel while a search is active, **When** it completes or is cancelled, **Then** the directory reflects the outcome, the search text is preserved, and the panel still shows the person acted on.

---

### User Story 5 - Know what the app is doing during a wait (Priority: P3)

A person signing in, and a person waiting for an answer, sees a plain-language description of what is happening and that progress is being made, rather than an unadorned spinner. Long answers are presented so the opening is readable immediately while the rest remains reachable.

**Why this priority**: These waits are inherent (workstation round-trips) and cannot be removed; explaining them costs little and removes the "is it stuck?" uncertainty. It is polish on top of flows that already complete correctly.

**Independent Test**: Sign in against a slow workstation and ask a question that produces a long answer; confirm each wait shows descriptive progress text, and the long answer's opening is visible without scrolling.

**Acceptance Scenarios**:

1. **Given** a sign-in in progress, **When** the user waits, **Then** the page states what is happening in plain language, not only a spinner.
2. **Given** an answer being prepared, **When** the user waits, **Then** the conversation shows a plain-language progress state and the conversation so far stays visible and readable.
3. **Given** an answer longer than the visible area, **When** it arrives, **Then** its opening is visible without manual scrolling and the remainder is reachable by scrolling.
4. **Given** a wait that fails, **When** the failure renders, **Then** the progress state is replaced by the shared error presentation — the two never show together.

---

### Edge Cases

- Search text containing punctuation, accents, or very long strings: treated as literal text, never as an error, and matched case-insensitively.
- Searching while a load is in flight, and refreshing while a search is active: the search reapplies to the freshly loaded data; no flicker of unfiltered content.
- A search that matches more rows than one page holds: paging operates over the filtered set, and the match count reflects the filtered total.
- Clearing a search restores the prior sort order and page position as closely as the data allows.
- Attention signals on the Dashboard when the relevant count is zero, and when the load failed: no false urgency, and no urgency signal rendered over an error state.
- Minimum supported window size with the navigation pane open: collapsed-column tables still show name, status, and row actions without clipping.
- A sorted column that is itself collapsed at narrow widths: the sort persists silently and its indicator returns when the column restores.
- Rapid typing in search: results correspond to the latest text, never an earlier keystroke.
- Account action taken from search on a person who no longer exists (removed by another session): the shared error presentation explains it and the stale row disappears on the next refresh.
- Theme switched to high contrast while attention signals, progress states, or collapsed tables are visible: all remain legible without relying on color alone.
- Keyboard-only use: search fields, per-row actions surfaced from search, and dialog actions are all reachable and operable by keyboard, in a sensible tab order.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The Library, History, and My Documents surfaces MUST each offer text search that filters their displayed records as the user types, matching case-insensitively on the record's primary text (filename for documents, question and answer text for history), with the filtered results visible within a tenth of a second of the text changing.
- **FR-002**: An active search MUST state how many records match, and a search that matches nothing MUST present an empty state that explains no records matched and offers to clear the search.
- **FR-003**: Search text MUST be preserved across refreshes and across leaving and returning to the surface for the duration of the session, and cleared results MUST restore the unfiltered list.
- **FR-004**: Paging on a searched surface MUST operate over the filtered set, with the match count reflecting the filtered total.
- **FR-005**: The Dashboard MUST visually distinguish a metric whose count indicates something needs attention (documents that failed to index) from the same metric at zero, using more than color alone so the distinction survives high contrast.
- **FR-006**: Every Dashboard metric card that navigates MUST indicate that it can be activated and which destination it opens, both visually and through its accessible name.
- **FR-007**: The Dashboard MUST show placeholder content matching its final layout while its first load is in flight, replaced by real content on completion, and MUST NOT show attention signals over a load-failure state.
- **FR-008**: The People directory's column headers MUST remain visible while its rows scroll, and MUST continue to show the current sort column and direction.
- **FR-009**: At the minimum supported window size, the Library table and the People directory MUST keep their primary content — name, status, and row actions — usable without horizontal scrolling, by collapsing lower-priority columns.
- **FR-010**: A column collapsed for width MUST be restorable by widening the window, and the information it carried MUST remain reachable; collapsing MUST NOT change sort order or discard data.
- **FR-011**: As the administrator types in the People directory search, the first matching person MUST be highlighted automatically and the details panel MUST show that person's frequent account actions (reset password, change role, enable/disable) without any separate row selection; activating one MUST use the same confirmation flow as the existing directory actions. The administrator can still move the highlight to a different match to act on that person instead.
- **FR-012**: In every confirmation and task dialog the confirming action MUST come first and the dismissing action (Cancel, Close, or Keep) MUST be the final button, and a destructive confirming action MUST be visually distinguished from a non-destructive one, consistently across all dialogs.
- **FR-013**: Sign-in and answer preparation MUST each communicate a plain-language description of what is happening while in progress, and that progress state MUST be removed when the operation completes or fails — never shown together with the error.
- **FR-014**: An answer longer than the visible area MUST arrive with its opening visible without manual scrolling, with the remainder reachable by scrolling.
- **FR-015**: All new interactive elements (search fields, clear-search actions, per-person actions surfaced from search) MUST receive stable automation identifiers, and every pre-existing automation identifier MUST be preserved.
- **FR-016**: All new or changed visual treatments MUST remain legible and unclipped in light, dark, and high-contrast themes, and MUST use the established design-system scale for spacing, sizing, and color — no new ad-hoc values.
- **FR-017**: Changes MUST remain presentation-layer: the shared client behavior layer, workstation API, and contracts are read-only, except for the minimum wiring a new affordance needs (search text, filtering, and attention state). No data model, persistence, or retrieval behavior changes.

### Key Entities *(include if feature involves data)*

- **Search State**: Per-surface text the user has typed, the count of records it matches, and whether a no-match empty state is showing. Lives for the session; not persisted.
- **Attention State**: Whether a Dashboard metric represents a condition needing action, derived from counts the Dashboard already loads. No new data.
- **Column Priority**: Which columns of a table are primary (always shown) versus collapsible at narrow widths. A presentation rule, not stored data.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: With 50 or more records, a person can locate a specific document, past question, or upload using search alone — no paging or scrolling — on the Library, History, and My Documents surfaces.
- **SC-002**: Search results reflect the typed text within a tenth of a second of it changing, and the stated match count equals the number of records shown.
- **SC-003**: On the Dashboard, a non-zero failure count is visually distinguishable from a zero count within a single glance, and the distinction remains perceivable in high contrast without relying on color alone.
- **SC-004**: 100% of Dashboard metric cards that navigate expose their destination in both their visible treatment and their accessible name (baseline: destination stated only in a tooltip and accessible name).
- **SC-005**: At the minimum supported window size with the navigation pane open, neither the Library table nor the People directory requires horizontal scrolling to reach a row's name, status, and actions (baseline: both require it).
- **SC-006**: While scrolling a People directory of 100 or more rows, the column headers and the current sort indicator remain visible at all times (baseline: they scroll away).
- **SC-007**: Resetting a password and enabling or disabling an account can each be completed by typing the person's name and using the details panel alone, with no row click and no more steps than the existing select-then-act flow.
- **SC-008**: In 100% of confirmation and task dialogs, the confirming action comes first and the dismissing action is the final button, and every destructive confirming action shares one visual treatment.
- **SC-009**: During sign-in and during answer preparation, the user sees a plain-language description of the in-progress work, and that description is gone once the operation completes or fails.
- **SC-010**: All pre-existing automation identifiers remain present, the solution builds, and all existing suites pass with zero assertion changes.

## Assumptions

- Scope is the desktop client only: the Library, History, My Documents, Dashboard, and People directory surfaces, plus the app's confirmation and task dialogs. Server surfaces, the retrieval pipeline, and answer quality are out of scope.
- Search filters records the client has already loaded. It does not introduce workstation-side search, new endpoints, or changes to what the workstation returns; a surface that loads incrementally searches what is loaded and its existing "load more" extends the searchable set.
- "Needs attention" is scoped to conditions the Dashboard already computes — documents that failed to index. No new health checks, alerts, or notifications are introduced.
- "Frequent account actions" are the ones the People directory already supports: reset password, change role, and enable/disable. No new account operations are added, and each keeps its existing confirmation. They surface through the directory's existing details panel, which follows the automatically highlighted first match while a search is active, rather than through new per-row controls.
- The dialog action order is a chosen convention, not the status quo: the confirming action comes first and the dismissing action is always the final button, matching the Windows dialog convention. Dialogs that currently place the dismissing action first (Create Person, Reset Password) are brought into line; the Upload dialog already follows it.
- Collapsed columns at narrow widths follow the precedent already set for the Library's secondary columns: primary content is name, status, and row actions.
- Plain-language progress text is short, English, and consistent in tone with the app's existing micro-copy. No localization.
- The minimum supported window size and the design-system scale are the ones established by earlier features; this feature adopts them and does not redefine them.
- Placeholder content during loading is a presentation of the existing loading state, not a new loading mechanism, and it must never display fabricated numbers.

## Dependencies

- The shared styles, design-system scale, and empty-state pattern established by the earlier redesign features.
- The shared status and confirmation presentations, and the frozen automation identifiers, delivered by the preceding polish feature.
- The existing people-search, confirmation, and account-action flows in the People directory, which the new shortcuts reuse rather than replace.
- The Dashboard's existing metric counts and navigation targets.

## Out of Scope

- Any new product capability: no saved searches, no search history, no export, no bulk actions, no notifications or alerting.
- Workstation-side or full-text search, and any change to the workstation API, contracts, data model, or retrieval behavior.
- Re-theming, new visual assets, or changes to the component library.
- Redesigning surfaces the preceding polish feature already brought to standard, except where this feature's requirements touch them.
- Localization or internationalization.
- Performance tuning of data loading beyond the tenth-of-a-second bound on search filtering over already-loaded records.
