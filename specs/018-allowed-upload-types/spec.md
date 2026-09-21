# Feature Specification: Allowed Upload Types

**Feature Branch**: `018-allowed-upload-types`

**Created**: 2026-09-18

**Status**: Draft

**Input**: User description: "Allow file uploads are docx, pdf, txt, xlsx, docx"

**Constitution**: v1.3.0 — no amendment needed (file-intake allow-list only; single-tenant model, offline invariant, citation grounding, and library-first structure untouched).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Admin uploads a supported document type (Priority: P1)

An admin opens the upload dialog from the Document Library header, picks a file ending in `.docx`, `.pdf`, `.txt`, or `.xlsx`, and completes the upload. The file queues normally, uploads with existing progress/outcome behavior, and becomes searchable in the library.

**Why this priority**: This is the core allow-list guarantee — every listed type must work end-to-end through queue, upload, and search. Without it the feature has no value.

**Independent Test**: For each of the 4 types, open upload, pick one valid file of that type, start upload, confirm success outcome, dialog auto-close, library refresh shows the new document, and a query for known text in the file returns a citation.

**Acceptance Scenarios**:

1. **Given** an admin with the upload dialog open, **When** they pick a valid `.docx` file, **Then** it queues with name and size, uploads successfully, and appears in the library after auto-close.
2. **Given** an admin with the upload dialog open, **When** they pick a valid `.pdf` file, **Then** it queues with name and size, uploads successfully, and appears in the library after auto-close.
3. **Given** an admin with the upload dialog open, **When** they pick a valid `.txt` file, **Then** it queues with name and size, uploads successfully, and appears in the library after auto-close.
4. **Given** an admin with the upload dialog open, **When** they pick a valid `.xlsx` file, **Then** it queues with name and size, uploads successfully, and appears in the library after auto-close.

---

### User Story 2 - Admin is blocked from uploading an unsupported type (Priority: P1)

An admin tries to queue a file whose type is not in the allow-list (e.g. images, archives, presentations, legacy `.doc`, markdown). The file never enters the queue; an immediate plain-language message names the file, states the reason, and lists the supported types.

**Why this priority**: An allow-list without enforcement is a suggestion. Blocking with a clear message prevents wasted uploads, server rejections, and support tickets.

**Independent Test**: Attempt to queue one file of each unsupported sample type (`.png`, `.zip`, `.pptx`, `.doc`, `.md`) via picker and via drag-and-drop; confirm none enter the queue and each attempt shows a message naming the file and listing the 4 supported types.

**Acceptance Scenarios**:

1. **Given** an admin with the upload dialog open, **When** they select an unsupported file in the picker, **Then** it is blocked from the queue and a message names the file, the reason (unsupported type), and the supported types.
2. **Given** an admin with the upload dialog open, **When** they drop an unsupported file onto the drop target, **Then** the same blocking message appears and nothing is queued.
3. **Given** a mixed selection containing both supported and unsupported files, **When** the selection is confirmed, **Then** exactly the supported files queue and each rejected file gets its own named reason.

---

### User Story 3 - Admin sees only supported types offered up front (Priority: P2)

An admin opening the picker or reading the dialog sees the supported types advertised before choosing, so they do not have to discover the allow-list by trial and error.

**Why this priority**: Up-front affordance reduces failed attempts and is independently demonstrable without uploading anything.

**Independent Test**: Open the upload dialog and the system picker; confirm the dialog lists the 4 supported types and the picker filter offers only those types (with an all-supported option); no upload needed.

**Acceptance Scenarios**:

1. **Given** an admin with the upload dialog open, **When** they view the file-choice area, **Then** the 4 supported types are listed in plain language before any file is chosen.
2. **Given** an admin invoking the system file picker, **When** the picker opens, **Then** its type filter defaults to the supported set so unsupported files are de-emphasized or hidden by the OS.

---

### Edge Cases

- Duplicate entry in the request (`docx` listed twice): treated as a single allow-list entry; the effective set is 4 unique types.
- Uppercase/mixed-case extensions (`DOCX`, `Pdf`, `TXT`, `XlSx`): accepted identically to lowercase.
- Renamed content (e.g. a `.png` renamed to `.pdf`, a `.docx` renamed to `.xlsx`): rejected with a "content does not match type" message; no partial document retained.
- Legacy `.doc` (OLE compound document, not OOXML): rejected as unsupported with a message directing the user to save as `.docx`.
- Markdown `.md`: rejected for new uploads under this allow-list even though prior ingestion work accepted it; existing `.md` documents already in the library remain searchable.
- Empty (0-byte) file with an allowed extension: rejected with a "no extractable content" style message, not queued.
- Oversized file with an allowed extension: queued-or-blocked per existing size policy, with the size reason named (type check passes, size check still applies).
- Files with no extension or double extensions (`report.pdf.exe`, `data.`): rejected as unsupported.
- Queue with zero valid files after rejections: start action stays disabled with a hint that a supported file is required.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST accept new uploads with exactly these extensions: `.docx`, `.pdf`, `.txt`, `.xlsx` (case-insensitive), and MUST reject every other extension for new uploads.
- **FR-002**: The upload dialog MUST display the supported types before selection and the system file-picker filter MUST default to the supported set.
- **FR-003**: Files with unsupported types MUST be blocked from entering the upload queue with an immediate plain-language message that names the file, states the type is unsupported, and lists the supported types.
- **FR-004**: Validation MUST apply identically to picker-selected files and drag-and-dropped files; neither path may bypass the allow-list.
- **FR-005**: The workstation service MUST enforce the same allow-list authoritatively and reject disallowed uploads even if the client check is bypassed, with a plain-language reason naming the file and the supported types.
- **FR-006**: Files whose extension is allowed but whose content does not match the declared type MUST be rejected with a "content does not match type" message and MUST NOT create a document or index entries.
- **FR-007**: Existing size limits and per-format guards (e.g. xlsx cell cap, no-extractable-content rule) MUST continue to apply within the allowed set; passing the type check MUST NOT skip them.
- **FR-008**: Documents already in the library from previously accepted types outside this list (e.g. `.md`) MUST remain stored, searchable, and citable; this feature gates new uploads only and MUST NOT delete or hide existing documents.
- **FR-009**: Admin-only gating, single Library-header entry point, session handling, progress/cancel/outcome/auto-close/refresh behavior, keyboard operability, and theme rendering MUST remain unchanged.

### Key Entities *(include if feature involves data)*

- **Allowed file type**: One of the 4 permitted kinds — extension (`.docx`, `.pdf`, `.txt`, `.xlsx`), content signature used for deep validation, human-readable label shown in the dialog.
- **Upload candidate**: A user-offered file before queueing — name, size, declared extension; carries a validation verdict (accepted or rejected with reason).
- **Validation outcome**: Result of the allow-list check — accepted, or rejected with a named file, a reason (unsupported type vs. content mismatch vs. empty), and the supported-type list.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: First-attempt admins successfully queue and upload one file of each of the 4 supported types (4/4 succeed) and find each new document in the library within 1 minute per file.
- **SC-002**: 100% of unsupported-type trials (picker and drag-and-drop, including `.doc`, `.md`, `.png`, `.zip`, `.pptx`) are blocked before queueing with a message that names the file and lists the supported types.
- **SC-003**: Zero documents are created from renamed/mismatched content (e.g. image renamed to `.pdf`, docx renamed to `.xlsx`) across 10 mismatch trials, and each trial shows the content-mismatch reason.
- **SC-004**: Support burden drops: trial admins encounter zero "which files can I upload?" questions after seeing the dialog, verified by 90% unaided correct naming of the 4 types in a 10-person walkthrough.

## Assumptions

- The duplicated `docx` in the request is a typo; the effective allow-list is 4 unique types: `docx`, `pdf`, `txt`, `xlsx`.
- Legacy OLE `.doc` is excluded (not equivalent to `.docx`); users convert to `.docx` before upload. If `.doc` support is wanted, it is a separate ingestion feature.
- `.md` is excluded from new uploads under this exclusive allow-list even though `003-ingest-breadth` previously accepted it; existing `.md` documents stay intact and searchable per FR-008.
- `.csv` remains out of scope as a first-class format (may upload as `.txt` only if the user renames it; no format sniffing beyond the 4 types).
- File-size caps and the 100k-cell xlsx cap are unchanged; this feature does not set new limits.
- Extension comparison is case-insensitive; content deep-validation reuses the existing `002`/`003` hardening pattern (magic bytes / container inspection).
- Upload stays admin-only on the desktop client over LAN to the single-tenant workstation; no API, contract-version, RBAC, or offline-invariant change beyond enforcing the allow-list.

## Dependencies

- `015-file-upload-ui`: dialog, queue, picker filter surface, progress/outcome/auto-close baseline this allow-list is displayed and enforced in.
- `017-multi-file-upload`: multi-select picker and drag-and-drop intake paths that must both apply identical allow-list validation.
- `003-ingest-breadth`: xlsx/pipeline validation pattern reused for content-mismatch rejection; `.md` history noted for FR-008.
- `002-real-bringup`: corrupted-document 400 hardening pattern reused for mismatched content.
- `006-client-architecture`: upload state, admin gating, and session handling reused unchanged.

## Out of Scope

- Adding new formats (`.doc`, `.pptx`, `.csv` as first-class, `.html`, `.eml`, OCR for scanned PDFs) — separate ingestion features.
- Changing size caps, chunking (512/50), topK, or eval thresholds.
- Server API shape, contract versioning beyond allow-list enforcement, RBAC, or offline behavior changes.
- Bulk server-side re-validation or migration of the existing library.
