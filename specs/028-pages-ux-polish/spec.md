# Feature Specification: All-Pages UX/UI Polish

**Feature Branch**: `028-pages-ux-polish`

**Created**: 2026-10-02

**Status**: Draft

**Input**: User description: "Update/improve UX/UI for all pages" — applied as a consistency-and-feedback polish pass over every page and dialog of the desktop client, grounded in a full-client UI audit performed 2026-10-02 (headers, status/error, busy, feedback, sizing, and responsiveness compared page-by-page).

**Constitution**: v1.3.0 — no amendment needed (presentation-only; shared client behavior layer, workstation API, and contracts untouched).

## Clarifications

### Session 2026-10-02

- Q: Should the Dashboard adopt the same shared header treatment as every other page, or remain a distinctive landing-page layout? → A: The Dashboard adopts the shared header card (title, supporting line, actions area) while the feature-027 metric/panel content below the header stays untouched; the profile card moves out of the header row into the content area, with its exact placement decided at planning.
- Q: Should the app use one pagination style everywhere, or keep the two it has today? → A: Keep per-surface paradigms — numbered pagination (with rows-per-page) for the Library admin table, "Load more" for the History/My Documents feeds — documented as an accepted difference, not a consistency defect.
- Q: Should the three task dialogs (Upload, Create Person, Reset Password) become in-window dialogs like the app's confirmation prompts, or stay as separate windows? → A: Create Person and Reset Password convert to the in-window dialog pattern; the Upload dialog stays a separate window with its chrome (title bar, icon, window flags, button widths) aligned to the application standard.
- Q: Which busy indication should become the app-wide standard when an operation is in flight? → A: In-control busy — the control that started the operation shows the busy state while page content stays visible; initial page content loads keep the shared centered loading treatment.

### Session 2026-10-05

Resolutions of the four spec-level ambiguities surfaced by the requirements-quality review (`checklists/ux.md` CHK011, CHK016, CHK019, CHK026):

- Q: Does FR-001's "every content page" include the Login page? → A: No — Login is the one explicit exemption (centered sign-in card; shared busy/status idioms only, per Story 5). FR-001 now defines "content page" for the header requirement accordingly, matching Contracts U1.3.
- Q: Does FR-003's retry requirement cover the saved-answer detail page? → A: No — explicitly exempt: its failure recovery is its navigation affordances (Back / "Ask again"), not an in-place retry of a parameterized detail load. FR-003 and Contracts U2.3 now name the exemption.
- Q: Does SC-005's "100% of interactive controls" target every control, including inline chat controls? → A: No — scoped to row-level action controls on data surfaces; inline icon controls inside content items (chat copy buttons, send button) keep their pre-existing compact targets per the established inline-icon precedent. SC-005 now matches FR-014 and Contracts U6.1.
- Q: Does clarification Q1's "027 content below the header stays untouched" waive FR-019's role-aware empty states on the Dashboard? → A: No — "untouched" fixes layout and structure ownership only; FR-019 governs empty-state guidance copy on every page, including the Dashboard's. FR-019 now states this precedence explicitly.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Every page reads as one app (Priority: P1)

A user moving between Dashboard, Library, Ask, the saved-answer page, History, My Documents, Admin, Settings, and Login experiences one page anatomy everywhere: the same header treatment (page title, supporting line, actions area), the same status/error presentation, the same busy indication, and the same refresh affordance. Today these drift page-by-page — four different header variants, four different status/error idioms, three different busy patterns, and a retry button that exists on only two of the pages that need one — which makes the app feel assembled from parts rather than designed.

**Why this priority**: Consistency debt is the most visible UI problem in the current client; it touches every user on every visit, and unifying it raises the perceived quality of the entire product in one pass.

**Independent Test**: Walk every page and dialog; confirm header, status/error, busy, and refresh treatments are structurally identical, and that every asynchronously loaded primary content page pairs its failures with a retry action (the saved-answer page recovers via Back / "Ask again" — FR-003's exemption).

**Acceptance Scenarios**:

1. **Given** any two content pages, **When** their headers are compared, **Then** both present a title, a supporting line, and an actions area in the same structure and visual treatment.
2. **Given** a load failure on any asynchronously loaded primary content page, **When** the failure renders, **Then** it uses the shared status presentation and offers a retry action (the saved-answer page recovers via Back / "Ask again" instead — FR-003's exemption).
3. **Given** any in-flight operation, **When** the busy state shows, **Then** it follows the same pattern on every page (no page-specific busy variant).
4. **Given** the refresh affordance, **When** compared across the pages that offer it, **Then** it is presented the same way everywhere.
5. **Given** the Settings page, **When** it opens, **Then** it carries the same header treatment as every other content page (baseline: it is the only content page without one).

---

### User Story 2 - Every action acknowledges itself (Priority: P1)

A user who copies an answer or citation, downloads a document, signs out, or completes a multi-step dialog always knows what happened: copy actions confirm briefly without interrupting, downloads show in-flight, success, and failure, sign-out asks before ending the session, and every confirmation appears in the app's own styled dialog — never an operating-system message box. Today every copy affordance in the client is silent, document downloads give no feedback at all, sign-out fires immediately despite being styled as destructive, and one dialog still confirms through a native system message box.

**Why this priority**: Silent actions are the largest perceived-reliability problem — users cannot distinguish success from failure and repeat actions or assume the app is broken. Feedback is the single highest-value UX improvement after consistency.

**Independent Test**: Perform each action (copy, download, sign-out, each confirmation dialog) and observe a clear, consistent acknowledgment for every one.

**Acceptance Scenarios**:

1. **Given** any copy affordance, **When** activated, **Then** a brief non-blocking confirmation appears and dismisses itself.
2. **Given** a document download, **When** it starts and when it finishes or fails, **Then** each state is communicated to the user.
3. **Given** sign-out, **When** activated, **Then** the user is asked to confirm before the session ends.
4. **Given** any confirmation prompt, **When** it renders, **Then** it uses the application's in-window dialog pattern (no native system message boxes anywhere in the client).
5. **Given** a transient confirmation, **When** it appears, **Then** it does not block or displace page content.

---

### User Story 3 - The Ask conversation feels like a finished chat (Priority: P2)

A user asking questions experiences a modern conversation: the newest message stays visible without manual scrolling, an in-flight question shows a thinking state inside the conversation rather than a spinner covering the whole page, the conversation can be cleared, and "Ask again" from History or a saved answer reliably places the original question on the Ask page. Today the page hides behind a full-page spinner while answering, long conversations require manual scrolling after every exchange, there is no way to clear a conversation, and "Ask again" frequently lands on an empty Ask page because it depends on the Ask page already being open.

**Why this priority**: Ask is the product's primary path, but its visuals were already modernized by earlier features — what remains are flow gaps (auto-scroll, busy placement, ask-again reliability, clear/reset) that degrade the core experience without blocking it.

**Independent Test**: Hold a long conversation (10+ exchanges) and confirm the newest message is always visible without scrolling; re-run saved questions from both History and the saved-answer page.

**Acceptance Scenarios**:

1. **Given** an ongoing conversation, **When** a message is sent or arrives, **Then** the newest message is visible without manual scrolling.
2. **Given** a question in flight, **When** the answer is being prepared, **Then** the busy state renders within the conversation area and the existing conversation stays visible.
3. **Given** the Ask page, **When** the user activates clear/reset, **Then** the conversation empties and input is ready for a new question.
4. **Given** a saved question in History or on the saved-answer page, **When** "Ask again" is activated, **Then** the Ask page opens with the original question populated — every time, not only when it was already open.
5. **Given** copy actions inside the conversation, **When** used, **Then** they confirm like every other copy affordance in the app.

---

### User Story 4 - Data surfaces work at any window size (Priority: P2)

A user working in a small window can still use the tables and lists: row-action buttons meet the app's standard interactive target size, long error text wraps instead of being silently cut off, table content degrades gracefully instead of permanently forcing horizontal scrolling, and empty states guide users toward actions they actually have — today the Library empty state tells non-admins to upload, although only admins can.

**Why this priority**: These defects affect sustained, power use (dense surfaces, small windows, long errors) rather than the first-run path, and each fix is small; together they remove the feeling that some screens were built for a different window.

**Independent Test**: Resize to the minimum supported window and work each data surface; measure row-action targets; force long error messages and check readability.

**Acceptance Scenarios**:

1. **Given** any row-level action control, **When** measured, **Then** it meets the application's standard interactive target size.
2. **Given** a long error or status message, **When** it renders on any page, **Then** it wraps and remains fully readable (no silent truncation).
3. **Given** the minimum supported window size, **When** a table-bearing page is shown, **Then** its primary content remains usable, with columns compressing or collapsing gracefully rather than always forcing horizontal scrolling.
4. **Given** an empty state, **When** shown to a user who lacks the referenced capability, **Then** its guidance references an action that user can actually take.

---

### User Story 5 - Login and Settings reach parity (Priority: P3)

A user signing in can press Enter in either credential field to submit, and sees the shared busy and error patterns rather than login-specific ones. A user in Settings finds the standard page header, can re-check the workstation connection on demand (today the status is computed once and goes stale), and can copy the displayed workstation address and signed-in identity.

**Why this priority**: Login and Settings are single-visit surfaces — important for polish and parity, but with the smallest user exposure of the five stories.

**Independent Test**: Sign in using only the keyboard; open Settings, confirm the header, refresh the connection status, and copy both displayed values.

**Acceptance Scenarios**:

1. **Given** the Login page, **When** Enter is pressed in either credential field, **Then** sign-in submits.
2. **Given** a busy sign-in or a sign-in failure, **When** it renders, **Then** it uses the shared busy and status patterns rather than login-specific variants.
3. **Given** the Settings page, **When** it opens, **Then** it carries the standard header treatment.
4. **Given** a stale connection status in Settings, **When** the user activates the re-check action, **Then** the status refreshes.
5. **Given** a read-only identity value in Settings, **When** the user activates copy, **Then** the value is copied and confirmed.

---

### Edge Cases

- High-contrast theme: every new or changed treatment (headers, status, busy, confirmations, row separation) remains legible — no reliance on subtle color alone.
- Minimum supported window size with the navigation pane open: no clipped controls or forced scrolling of page chrome.
- Long values (usernames, workstation addresses, long error text): wrap or truncate gracefully without displacing controls.
- Theme switched while a transient confirmation or busy state is visible: it restyles correctly.
- Busy and empty/error states coinciding: the busy state wins; empty/error content must not render over an active busy indicator (existing gating preserved).
- Rapid repeated actions (double-click delete or send): the busy state visibly prevents duplicate dispatch.
- Keyboard-only users: every new affordance (retry, clear conversation, copy, re-check connection) is reachable and operable by keyboard.
- Very long conversations: auto-scrolling must not degrade responsiveness.
- Answers containing very long text or many citations: the newest message still ends up fully visible.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Every content page MUST present one shared header treatment — page title, supporting line, and actions area — structurally identical across all pages, including the Dashboard (whose header adopts the shared treatment while its existing metric/panel content below stays untouched, and whose profile card moves out of the header row into the content area). The Login page is the one explicit exemption: it keeps its centered sign-in card and adopts only the shared busy and status idioms (Story 5); for this requirement, "content page" means every page above except Login.
- **FR-002**: Persistent status and error messages MUST use one shared severity-styled presentation on every page and dialog (replacing the current four variants).
- **FR-003**: Every page whose content loads asynchronously MUST pair a load failure with a retry affordance, with one explicit exemption: the saved-answer detail page, whose failure recovery is its navigation affordances (Back to return to where the question came from, "Ask again" to re-run the question from a populated Ask page) rather than an in-place retry of a parameterized detail load.
- **FR-004**: Busy indication MUST follow one consistent rule on all pages: an action-initiated operation shows its busy state in the control that started it (page content stays visible), and an initial page content load uses the shared centered loading treatment — no page-specific busy variants.
- **FR-005**: The refresh affordance MUST be presented identically on every page that offers it.
- **FR-006**: Every copy affordance MUST confirm success with a brief, non-blocking, self-dismissing notification.
- **FR-007**: Document downloads MUST communicate in-flight, success, and failure states.
- **FR-008**: Session-ending and abort-mid-process actions (sign-out, cancel during an upload) MUST request confirmation.
- **FR-009**: All confirmations MUST use the application's in-window dialog pattern; operating-system-native message boxes MUST NOT appear anywhere in the client.
- **FR-010**: The conversation surface MUST keep the newest message visible without manual scrolling after every exchange.
- **FR-011**: While a question is in flight, the busy state MUST be communicated within the conversation area, and the existing conversation MUST remain visible.
- **FR-012**: Re-running a saved question (from History or the saved-answer page) MUST reliably populate the Ask page with the original question, regardless of whether the Ask page was previously open.
- **FR-013**: The user MUST be able to clear or reset the current conversation from the Ask page.
- **FR-014**: Row-level action controls on data surfaces MUST meet the application's standard interactive target size.
- **FR-015**: Error and status text MUST wrap rather than truncate on every page.
- **FR-016**: Table-bearing pages MUST degrade gracefully at the minimum supported window size (columns compress or collapse) rather than permanently forcing horizontal scrolling of primary content.
- **FR-017**: The Login page MUST submit on Enter from either credential field.
- **FR-018**: The Settings page MUST adopt the standard header treatment, offer an on-demand connection re-check, and make displayed identity values copyable.
- **FR-019**: Empty-state guidance MUST reference only actions available to the current user (role-aware) — on every page's empty states, including the Dashboard's; a "content below stays untouched" statement (clarification Q1 / FR-001) fixes layout and structure ownership only and never waives this copy-level requirement.
- **FR-020**: Spacing, padding, and sizing values on affected surfaces MUST resolve to the documented design-system scale; ad-hoc per-page values MUST be eliminated.
- **FR-021**: All user-visible micro-copy MUST be single-language, plain, and consistent in tone.
- **FR-022**: Existing automation identifiers on affected surfaces MUST be preserved; new interactive elements MUST receive stable identifiers.
- **FR-023**: Changes MUST remain presentation-layer — the shared client behavior layer, workstation API, and all contracts are read-only (no behavior change beyond visible feedback and affordances).
- **FR-024**: All affected surfaces MUST remain legible and unclipped in light, dark, and high-contrast themes and at the minimum supported window size.
- **FR-025**: The Create Person and Reset Password dialogs MUST use the application's in-window dialog pattern; the Upload dialog MUST remain a separate window whose chrome (title bar, icon, window flags, button sizes) matches the application standard.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of content pages use the single shared header treatment (baseline: 4 variants, with Settings lacking a header entirely).
- **SC-002**: Exactly one status/error presentation idiom exists app-wide (baseline: 4), and zero operating-system-native message boxes (baseline: 1).
- **SC-003**: 100% of copy affordances show a transient confirmation (baseline: 0%).
- **SC-004**: 100% of asynchronously loaded primary content pages pair load failure with a retry affordance (baseline: 2 of 7; the saved-answer detail page is exempt per FR-003).
- **SC-005**: 100% of row-level action controls on data surfaces meet the application's standard interactive target size (baseline: Library row download/delete buttons at 36×32). Inline icon controls that live inside content items rather than data-surface rows (chat message copy buttons, the send button) keep their pre-existing compact targets and are outside this criterion's scope.
- **SC-006**: In a 10+ exchange conversation, the newest message is visible without manual scrolling after every exchange.
- **SC-007**: "Ask again" populates the Ask page with the original question on every attempt, from both History and the saved-answer page (baseline: intermittently empty).
- **SC-008**: Long error messages remain fully readable on every page (baseline: silently truncated on two pages).
- **SC-009**: At the minimum supported window size, no page clips its primary content or controls.
- **SC-010**: 100% of empty-state guidance references actions available to the current user (baseline: one empty state points non-admins to an admin-only action).
- **SC-011**: 100% of spacing, padding, and sizing values on affected surfaces resolve to the documented design-system scale (baseline: five card paddings, three dialog button widths, and two empty-state margins drifting).
- **SC-012**: All pre-existing automation identifiers remain present, the solution builds, and all existing suites pass with zero assertion changes.

## Assumptions

- "All pages" means the desktop client's content pages (Dashboard, Library, Ask, saved-answer detail, History, My Documents, Admin, Settings, Login), its three dialogs, and the app shell; server surfaces are excluded.
- This is presentation-layer polish: no new product features or workflows; the shared client behavior layer, workstation API, and contracts are read-only (existing bindings/commands are extended only where a new affordance needs wiring).
- The design system established by earlier features (type steps, color-token roles, icon size scale, empty-state pattern) is the baseline to adopt everywhere — this feature does not redefine it.
- The existing component library remains; no new UI library, fonts, or network-fetched assets are introduced.
- Micro-copy is limited to short strings (confirmations, empty-state hints, supporting lines), in a single language (English), plain product tone.
- The two paging paradigms are an accepted per-surface difference, not a consistency defect: the Library table keeps numbered pagination with rows-per-page, and the History/My Documents feeds keep incremental "Load more".
- "Standard interactive target size" is the application's existing 44-unit standard already used by most controls.
- The transient-notification mechanism already present in the app shell (currently unused) is the intended vehicle for non-blocking confirmations.
- Target remains Windows desktop, single-tenant, offline-first; the offline invariant is unaffected by this feature.

## Dependencies

- The shared styles and design-system documentation established by the earlier redesign features.
- The frozen automation identifiers on affected surfaces (no renames).
- The shared client behavior layer's existing bindings, commands, and busy-state gating.
- The app shell's existing notification presenter.

## Out of Scope

- New product features or workflows (no new query, upload, or admin capability).
- Any change to the workstation API, contracts, data model, or persistence.
- Navigation or information-architecture changes (the page set and navigation structure stay as-is).
- Localization/internationalization (beyond removing accidental non-English copy).
- New fonts, illustrations, or assets requiring network access or user installation.
- Re-theming or replacing the component library.
