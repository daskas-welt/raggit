# Feature Specification: File Upload UI Refresh

**Feature Branch**: `015-file-upload-ui`

**Created**: 2026-09-18

**Status**: Draft

**Input**: User description: "use winui-dev sub agent to update the UI for file upload"

**Constitution**: v1.3.0 — no amendment needed (presentation-only refresh of the existing upload dialog/sheet; Core/API/contracts untouched).

## Clarifications

### Session 2026-09-18

- Q: Should the refreshed upload UI support drag-and-drop files in addition to click-to-select? → A: Click-to-select only, restyled (no drag-drop).
- Q: How should the upload dialog behave after a successful upload? → A: Auto-close on success and refresh the list.
- Q: Should each upload dialog handle exactly one file per open? → A: Multiple files queued in one dialog open.
- Q: How should progress be shown when multiple files upload as a queue? → A: Per-file progress bars plus overall completed count.
- Q: How should unsupported file types be handled when building the queue? → A: Block adding them with an immediate message.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Admin uploads a document with clear progress and outcome (Priority: P1)

An admin opens the upload dialog from the Document Library header, chooses a file, starts the upload, watches clear progress, and sees an unmistakable success or failure outcome. On success the dialog closes and the new document appears in the library list behind it.

**Why this priority**: This is the core job of the upload UI — pick, send, confirm. Every other improvement is secondary if this flow is confusing or ambiguous.

**Independent Test**: As admin on the Library, open upload, pick a supported file, start upload, observe progress to completion, confirm success message and that the new row appears after close. Repeat with an unsupported file and with a simulated failure to confirm clear error messaging.

**Acceptance Scenarios**:

1. **Given** an admin on the Library, **When** they open upload, **Then** they see a single clear place to choose a file with the supported types listed and the start action disabled until a file is chosen.
2. **Given** a file is selected, **When** the admin starts the upload, **Then** progress is visibly advancing and the start action cannot be pressed twice.
3. **Given** an upload completes successfully, **When** the outcome is shown, **Then** the dialog auto-closes and the library list refreshes to include the new document.
4. **Given** an upload fails, **When** the error occurs, **Then** the dialog stays open showing a plain-language reason and what to do next (pick another file / retry), with no silent loss.

---

### User Story 2 - Admin understands state at a glance and can cancel (Priority: P2)

An admin who picks the wrong file or starts a large upload can see what is selected (name and size), remove or replace it before starting, and cancel mid-upload with a clear result.

**Why this priority**: Prevents wasted waits and wrong-file uploads; directly reduces support friction for large documents.

**Independent Test**: Pick a file → verify name/size shown with a remove/replace option; start a slow upload → cancel → verify a "cancelled" outcome and that no partial document appears in the library.

**Acceptance Scenarios**:

1. **Given** a file is selected, **When** it is displayed, **Then** the user sees the file name and size plus a way to remove or pick a different file before starting.
2. **Given** an upload is in progress, **When** the user cancels, **Then** the upload stops, a "cancelled" outcome is shown, and the library is unchanged.

---

### User Story 3 - Every user can operate upload by keyboard and screen reader (Priority: P2)

A keyboard-only or screen-reader user can open the dialog, choose a file, start/cancel, and hear the outcome, with focus correctly trapped while open and returned to the Library Upload button on close.

**Why this priority**: Upload is an admin-only but must-meet accessibility surface; 011 redesign requires native keyboard and screen-reader behavior for all overlay UI.

**Independent Test**: Full keyboard walkthrough (Tab/Enter/Esc) in Light, Dark, and High Contrast themes plus a screen-reader pass verifying open/close announcements, progress announcement, and outcome announcement.

**Acceptance Scenarios**:

1. **Given** the dialog is open, **When** the user tabs through it, **Then** every control is reachable in logical order with a visible focus indicator and Esc closes it.
2. **Given** the dialog opens or closes, **When** using a screen reader, **Then** the open is announced with focus moved inside, and close returns focus to the Upload button that opened it.

---

### Edge Cases

- No file selected: start action is disabled with a hint explaining a file must be chosen first.
- Unsupported type or oversized file: cannot be added to the queue; an immediate plain-language message names the problem and the start action stays disabled until at least one valid file is queued.
- Dismiss during upload (Esc, backdrop, close control): in-flight upload is cancelled; no orphaned partial document appears.
- Session expiry or loss of connection mid-upload: existing session-expired path applies; message explains connectivity rather than a generic failure.
- Narrow window (≤720px): dialog content reflows or scrolls; nothing is clipped with no way to reach it.
- Repeated upload without re-picking: resends full content or prompts to re-pick — never sends zero bytes silently.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The Library header Upload button MUST remain the single upload entry point for admins; no other navigation entry for upload may be added.
- **FR-002**: The upload dialog MUST offer click-to-select file picking only (restyled; no drag-and-drop target), MUST show supported file types before selection, MUST block adding unsupported types or oversized files to the queue with an immediate plain-language message, and MUST disable the start action until at least one valid file is queued.
- **FR-003**: The dialog MUST list each queued file with its name and size and MUST offer a way to remove or replace entries before starting.
- **FR-004**: During upload the dialog MUST show a per-file progress bar for each queued file plus an overall completed count (e.g. 2 of 4 done), MUST prevent duplicate starts, and MUST offer cancel.
- **FR-005**: Upload outcome (success, failure with reason and next step, cancelled) MUST be shown in a dedicated status surface distinct from the file picker and progress areas.
- **FR-006**: When all queued uploads succeed, the dialog MUST show a brief success outcome, then auto-close and refresh the library list to include the new documents with their usual status indicators.
- **FR-007**: Failed upload MUST keep the dialog open with the failure reason and recovery action visible; the user's file choice MUST be preserved where possible.
- **FR-008**: Cancellation or dismissal mid-upload MUST cancel the in-flight operation and leave the library unchanged.
- **FR-009**: All interactive elements MUST meet ≥44px touch targets, full keyboard operability (Tab/Enter/Esc), visible focus, and screen-reader announcements for open, progress milestones, outcome, and close with focus return.
- **FR-010**: All surfaces MUST render legibly in Light, Dark, and High Contrast themes with no hard-coded colors that break theming.
- **FR-011**: Non-admin users MUST see no upload entry point and MUST NOT be able to open the dialog.
- **FR-012**: Upload logic MUST support a multi-file queue in one dialog open; existing admin gating and session handling MUST be reused and extended only as needed for queue state.
- **FR-013**: If any queued file fails or is cancelled, the dialog MUST stay open showing per-file outcomes, and the library MUST refresh to include only the documents that succeeded.

### Key Entities

- **Upload dialog**: Modal presentation of the upload form — file choice area, queued-file list with per-file summary, progress area, outcome status area.
- **Selected file**: User-chosen document — name, size, type; drives start-action enablement.
- **Upload outcome**: Result of an attempt — success, failure (reason + next step), or cancelled.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Admins complete pick-to-success (including list refresh) in under 1 minute for a small test file, unaided, on first attempt in 90% of trials.
- **SC-002**: Zero duplicate-start uploads and zero orphaned partial documents across 20 open/upload/cancel/dismiss trials.
- **SC-003**: 100% of failure and cancellation trials show the correct plain-language outcome with a visible next step.
- **SC-004**: Full keyboard walkthrough plus Light/Dark/High Contrast visual check passes with zero unreachable controls and zero illegible surfaces.

## Assumptions

- Existing upload fundamentals (file pick, start gating, progress 0–1, status message, cancel, admin gating, session handling) are reused and extended only as needed for the multi-file queue; layout, visual hierarchy, and status presentation are refreshed.
- Supported types remain PDF, DOCX, TXT, XLSX unless ingestion breadth changes them; size limits are whatever the workstation API enforces and are surfaced as plain-language messages.
- The Library header Upload button (admin gating + post-close refresh) is kept as-is as the single entry point per 013-remove-upload-nav.
- Server API, contracts, and RBAC are untouched; non-admin invisibility is presentation of the existing server-side admin-only rule.
- Visual language follows the 011 redesign (native Windows 11 look, theme-aware surfaces, native status surfaces, icon buttons) — details resolved at plan time with reference grounding before UI changes.

## Dependencies

- `008-upload-sheet`: single-entry-point upload flow the dialog preserves.
- `011-ui-redesign`: native theme/icon/status conventions this refresh must follow.
- `013-remove-upload-nav`: Library header as the sole upload entry — no nav item is reintroduced.
- `006-client-architecture`: existing upload state, DI registration, admin gating, session-expiry navigation reused and extended only for queue state.
