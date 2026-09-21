# Feature Specification: Multi-File Upload (Picker + Drag-and-Drop)

**Feature Branch**: `017-multi-file-upload`

**Created**: 2026-09-18

**Status**: Draft

**Input**: User description: "Allow end-user to select multiple files for upload + allow drag and drop"

**Constitution**: v1.3.0 — no amendment needed (client presentation + file-intake behavior only; ingestion pipeline, API contracts, RBAC, and offline invariant untouched).

## Clarifications

### Session 2026-09-18

- Q: Can the admin add more files to the queue while an upload is already in progress? → A: Lock queue during upload (picker and drop target disabled once upload starts; extra files wait for the next dialog open).
- Q: Should the upload queue enforce a maximum number of files per batch? → A: No cap (no explicit limit; the queue stays scrollable regardless of count).
- Q: How should the drop target handle non-file drops such as shortcuts, Outlook attachments, or archive contents? → A: Reject like other invalid files (standard invalid-file message, no resolution attempted).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Admin selects multiple files at once in the picker (Priority: P1)

An admin opens the upload dialog from the Document Library header, chooses several files in a single system file-picker pass, and sees every valid file land in the upload queue with name and size. Invalid files are reported immediately and never enter the queue.

**Why this priority**: Multi-select is the core of the request — it turns N open-picker cycles into one, which is the dominant time saver for bulk library loads.

**Independent Test**: Open upload, multi-select 3 supported files in one picker pass, confirm all 3 appear queued with names/sizes, start upload, confirm all 3 succeed and appear in the library after auto-close.

**Acceptance Scenarios**:

1. **Given** an admin with the upload dialog open, **When** they invoke file selection and choose multiple supported files in one pass, **Then** every valid file is added to the queue with its name and size shown.
2. **Given** a multi-select pass containing a mix of supported and unsupported files, **When** the selection is confirmed, **Then** the supported files are queued and each rejected file is reported with a plain-language reason naming the file.
3. **Given** files queued from one or more picker passes, **When** the admin starts the upload, **Then** the existing per-file progress, overall completed count, outcome, auto-close, and library-refresh behavior applies unchanged.

---

### User Story 2 - Admin drags files from the desktop onto the dialog (Priority: P1)

An admin drags one or more files from File Explorer (or the desktop) and drops them onto a clearly marked drop area in the upload dialog; the files enter the same queue with the same validation, progress, and outcome behavior as picked files.

**Why this priority**: Drag-and-drop is the second half of the explicit request and the fastest path for users who already have files visible in Explorer. It is independently testable and demonstrable.

**Independent Test**: Drag 2 supported files from Explorer onto the dialog's drop area, confirm both queue correctly, upload, and confirm library refresh — without ever opening the system picker.

**Acceptance Scenarios**:

1. **Given** an admin with the upload dialog open, **When** they drag files over the dialog, **Then** a visible drop affordance appears indicating where files can be released.
2. **Given** files released over the drop area, **When** the drop completes, **Then** each valid file is queued with name and size, and each invalid file is reported with a plain-language reason naming the file.
3. **Given** files added by drag-and-drop, **When** the admin starts the upload, **Then** behavior (progress, cancel, outcomes, auto-close, refresh) is identical to picker-added files.

---

### User Story 3 - Admin manages a mixed-intake queue before uploading (Priority: P2)

An admin combines picker passes and drag-and-drop passes into one queue, reviews the combined list, removes unwanted entries, and uploads the rest in one start.

**Why this priority**: Mixed intake is the natural consequence of offering two entry paths; queue review/removal prevents wrong-file uploads in bulk flows.

**Independent Test**: Add 2 files via picker, add 2 more via drag-and-drop, remove 1, start upload, confirm the remaining 3 succeed and the library shows exactly those 3 new documents.

**Acceptance Scenarios**:

1. **Given** files already queued from one intake path, **When** the admin adds more files via the other path, **Then** the queue shows the combined list with per-file name, size, and a remove option for each entry.
2. **Given** a queued entry the admin does not want, **When** they remove it, **Then** it leaves the queue and the start action enablement reflects the remaining valid entries.
3. **Given** duplicate files added across passes (same content), **When** the queue is built, **Then** each entry is handled the same way the system already handles duplicates (no new duplicate semantics introduced).

---

### Edge Cases

- Empty drop (no files, e.g. dropped text or a URL): nothing is queued; a brief plain-language hint explains that files are required.
- Dropped folders: folders are not traversed; a plain-language message states that individual files must be dropped (no partial or recursive ingestion).
- Non-file drops (shortcuts, Outlook attachments, archive contents, or any virtual item the OS does not present as a real file): rejected with the standard invalid-file message; no resolution is attempted.
- Unsupported type or oversized file via either path: blocked from the queue with an immediate message naming the file and the reason; start stays disabled until at least one valid file is queued.
- Very large batch: the queue remains scrollable and operable; per-file outcomes still reported individually on partial failure.
- Drop outside the drop area or while the dialog is closed: no upload state changes; the dialog never opens implicitly from an external drop.
- Picker or drop attempted after upload has started: intake is disabled for the duration of the run; a hint explains that more files can be added after the current run completes.
- Session expiry or connection loss mid-upload: existing session-expired/connectivity path applies unchanged.
- Keyboard/screen-reader users: both intake paths have keyboard-reachable equivalents (picker is natively keyboard operable; drop area exposes an equivalent add action); outcomes announced as today.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The upload dialog MUST allow selecting multiple files in a single system file-picker pass, adding every valid file to the existing upload queue.
- **FR-002**: The upload dialog MUST provide a visible drag-and-drop target that accepts file drops from the operating system shell and adds every valid dropped file to the same queue.
- **FR-003**: Both intake paths MUST apply the same validation as the existing queue (supported types, size limits): invalid files MUST be blocked with an immediate plain-language message naming the file and the reason.
- **FR-004**: While a drag is over the dialog, the drop target MUST show a clear visual affordance (highlight + hint text); releasing outside the target MUST NOT queue anything.
- **FR-005**: The queue MUST support entries accumulated across multiple picker and drop passes in one dialog open, each entry showing name and size with a per-entry remove option before starting.
- **FR-006**: Upload execution for multi-file queues MUST reuse the existing behavior: per-file progress, overall completed count, duplicate-start prevention, cancel, per-file outcomes, auto-close on full success, stay-open on failure/cancel, and library refresh showing exactly the documents that succeeded. Once upload starts, the queue MUST be locked: the picker action and drop target are disabled until the run completes, and additional files wait for the next dialog open.
- **FR-007**: Dropped folders MUST NOT be traversed or ingested; the dialog MUST explain that individual files are required.
- **FR-008**: Admin-only gating, single Library-header entry point, session handling, and server API/contracts/RBAC MUST remain unchanged; this feature adds intake paths, not new endpoints or permissions.
- **FR-009**: Both intake paths MUST remain fully keyboard operable with visible focus, screen-reader announcements for queue changes and outcomes, and legible rendering in Light, Dark, and High Contrast themes.

### Key Entities

- **Drop target**: Dialog area that accepts OS file drops — visual idle/drag-over states, hint text; feeds the upload queue.
- **Queued file**: One intake entry regardless of source (picker pass or drop) — name, size, type, source-agnostic validation state; drives start enablement and per-file progress/outcome.
- **Upload queue**: Ordered collection of queued files accumulated across passes in one dialog open; existing per-file progress, outcome, and refresh semantics apply.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Admins queue 5 files via a single picker pass or a single drop in under 30 seconds, unaided, on first attempt in 90% of trials.
- **SC-002**: 100% of mixed valid/invalid intake trials (picker and drop) queue exactly the valid files and show a correctly named reason for each rejected file.
- **SC-003**: Zero orphaned partial documents and zero duplicate-start uploads across 20 multi-file open/queue/upload/cancel/dismiss trials.
- **SC-004**: Full keyboard walkthrough of both intake paths plus Light/Dark/High Contrast visual check passes with zero unreachable controls and zero illegible surfaces.

## Assumptions

- The `015-file-upload-ui` dialog (queue list, per-file progress + completed count, outcome surface, auto-close, cancel, accessibility baseline) exists and is reused; this feature adds the multi-select picker capability and the drop target only.
- Supported types remain PDF, DOCX, TXT, XLSX (plus whatever ingestion breadth currently accepts); size limits are whatever the workstation API enforces, surfaced as plain-language messages.
- No new per-batch file-count cap is introduced (clarified 2026-09-18); queue operability for large batches relies on the existing scrollable list.
- Duplicate-content handling reuses existing server dedupe semantics; no client-side hash comparison is added.
- Upload remains admin-only; "end-user" in the request means the admin operating the desktop client.
- Server API, contracts, RBAC, and the offline invariant are untouched; all uploads still travel over LAN to the workstation.

## Dependencies

- `015-file-upload-ui`: dialog, queue, progress, outcome, auto-close, and accessibility baseline this feature extends.
- `008-upload-sheet`: single-entry-point upload flow preserved.
- `011-ui-redesign`: theme/icon/status conventions for the new drop-target visuals.
- `006-client-architecture`: existing upload state, DI registration, admin gating, session-expiry navigation reused.
