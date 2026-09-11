# Feature Specification: Ingest Breadth (XLSX)

**Feature Branch**: `003-ingest-breadth`

**Created**: 2026-09-11

**Status**: Draft

**Input**: User description: "Ingest breadth: add xlsx support (pdf docx xlsx txt md), all visible sheets with header-row repeated, cached formula values, 100k cell cap, hidden sheets skipped; install only (no OCR, no pptx/html/eml/csv)."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Admin Uploads an Excel Workbook (Priority: P1)

An admin uploads a multi-sheet `.xlsx` workbook to the single-tenant library. The system segments each visible sheet into chunks and embeds them so a later natural-language query over cell values and headers returns a cited answer from that workbook.

**Why this priority**: Spreadsheets are the largest real-world gap after PDF/docx; making them searchable adds value independently of OCR/pptx. Without this there is no way to RAG-query tabular financial/operational docs.

**Independent Test**: Upload a 3-sheet `.xlsx` (each sheet has a header row + 10 data rows with a known "refund policy" sentence on Sheet 2). Verify `GET /api/documents` shows the document as `Ready`, then query that sentence → `POST /api/query` returns a citation whose `documentId` is the workbook and whose text contains the spreadsheet row. Do not need US-2 edge cases.

**Acceptance Scenarios**:

1. **Given** an admin is authenticated as `Admin` and the workstation is reachable, **When** they upload a valid `.xlsx` (<100MB, ≤100k cells), **Then** the document reaches `Ready` and queries for text inside any visible sheet return it among the top-5 with a citation.
2. **Given** the uploaded workbook has 3 visible sheets, **When** an employee queries text that exists on sheet 2, **Then** at least one returned citation corresponds to sheet 2's content.
3. **Given** a workbook's cell is a formula whose cached value is "42.50", **When** an employee queries "42.50", **Then** the system returns a citation containing that cached value, not the formula text.

---

### User Story 2 - Edge Cases and Guards for Spreadsheets (Priority: P1)

An admin uploads workbooks that exercise edge cases; the system applies the same no-partial-index invariant that PDF/docx already enforce, and enforces the per-document cell cap.

**Why this priority**: Guards are load-bearing for FR-011 scale (5k docs / 1M chunks) and for FR-006 (no partial writes). Without them an uploaded 200k-cell workbook or a renamed docx could corrupt the library.

**Independent Test**: Upload a hidden-sheet-only workbook → 400 "no extractable content". Upload a workbook with 110k cells → 413 with actionable "cell cap exceeded" message. Rename a valid docx to `.xlsx` and upload → 400 "content does not match type". Corrupt an xlsx (truncate after PK header) → 400.

**Acceptance Scenarios**:

1. **Given** a workbook exceeds 100,000 cells across visible sheets, **When** it is uploaded, **Then** the upload is rejected with 413 + message that names the 100k cap and actual cell count, and no document is retained.
2. **Given** a workbook where all sheets are hidden, **When** it is uploaded, **Then** it is rejected with 400 "no extractable content" and no document/chunks are persisted.
3. **Given** a `.xlsx`-declared file that is really a docx (PK zip but no `xl/workbook.xml`), **When** it is uploaded, **Then** it is rejected with 400 "content does not match type" and no partial index is created.
4. **Given** a workbook with a hidden sheet containing the token "hidden-token-xyz", **When** an employee queries that token, **Then** zero hits are returned (hidden sheet content never entered the index).

---

### Edge Cases

- Empty workbook (no sheets / all empty rows) → 400 "no extractable content".
- Merged cells → value lives on the anchor cell; otherwise empty.
- Wide sheet (one row, 20k cells) → cell cap triggers before chunking.
- Date-formatted cell — value best-effort applies the cell's number format; fallback is raw cached value.
- Embedded images/charts/pivots inside xlsx → ignored.
- Duplicate upload (same bytes hash as existing doc) → existing documentId returned (200, not re-indexed), same dedupe as pdf/docx.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST accept `.xlsx` (`application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`) end-to-end: MIME map → format validation → text extraction → chunk (512/50) → embed → index → cite.
- **FR-002**: System MUST extract **all visible sheets** and skip hidden sheets.
- **FR-003**: System MUST produce a text representation per visible sheet that is **row-wise**: each serialized row is `col1 | col2 | col3` with a leading sheet-name line `[Sheet: <name>]`, and the **header row (first non-empty row) is repeated before each data row** so any chunk is self-describing.
- **FR-004**: System MUST use the **cached/displayed value** for formula cells; cell text MUST be joined with ` | `; empty rows skipped.
- **FR-005**: System MUST enforce a **per-document cell cap of 100,000 cells across visible sheets**; over-cap uploads MUST be rejected with 413 and an actionable message and MUST NOT create a document or index.
- **FR-006**: System MUST **deep-validate** `.xlsx` despite shared `PK` zip magic; validation MUST inspect `[Content_Types].xml` / `xl/workbook.xml` (or equivalent) and reject non-xlsx `PK` streams with 400 "content does not match type" and no partial index.
- **FR-007**: Citations for xlsx chunks MUST carry `documentId`/`chunkId`/cell-text like every other format (Constitution V).

*Unclear aspects resolved via clarify (max 3 markers used — 0 remain):*

- Q1 text representation — row-wise with sheet-name header + header-row repeated (FR-003).
- Q2 sheets/values — all visible, cached values, hidden skipped, xlsx-only (FR-002, FR-004).
- Q3 large-spreadsheet guard — 100k cells → 413 (FR-005).

### Key Entities *(include if feature involves data)*

- **Sheet**: Not a stored entity; sheet name is embedded textually as `[Sheet: <name>]` inside each chunk's `text`.
- **Document** (`DocumentMimeType` gains `Xlsx`): unchanged otherwise; `Filename`, `Mime`, `Size`, `Hash`, `Status`, `CreatedBy`, `CreatedAt`.
- **Chunk**: unchanged: `Id`, `DocumentId`, `Ordinal`, `Text`, `TokenCount`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An admin uploads a multi-sheet `.xlsx` and it becomes searchable as `Ready`; a query for text from any visible sheet returns a citation whose documentId is that workbook.
- **SC-002**: Content on a hidden sheet is never returned (0 hits for a hidden-only token).
- **SC-003**: A formula cell is retrievable by its displayed cached value, not the formula text.
- **SC-004**: An over-cap workbook (>100k cells) is rejected with 413 and no document is retained.
- **SC-005**: A renamed/corrupt `.xlsx` (including a docx renamed to `.xlsx`) is rejected with 400 and no partial index.

## Assumptions

- `.xlsx` is added to the existing set `pdf`/`docx`/`txt`/`md`; this extends `001 FR-010` via a MINOR contract bump `1.1.0` → `1.2.0`.
- No new NuGet package — `DocumentFormat.OpenXml` already referenced.
- Date-formatted cells: best-effort apply the cell's number format; fallback is raw cached value.
- Shared-string and inline-string cells both resolved.
- Merged cells: value on anchor cell only.
- Embedded images/charts/pivots ignored.
- Empty workbook / only-hidden sheets → 400 "no extractable content".
- `100k cells` is a per-document guard, orthogonal to the 100MB file-size cap.
- OCR, pptx, html, eml, csv as a separate first-class format remain out of scope for this feature.
- Implemented after `002-real-bringup` to avoid Ingest churn.

## Dependencies

- `002-real-bringup` (`specs/002-real-bringup/spec.md`) — real bring-up, dimension guard, corrupted-doc 400 hardening; `003` reuses that hardening pattern for xlsx.
- `001-offline-mode` shipped as `v1.1.0`; contract `Document.mime` enum is versioned per feature dir.

## Out of Scope

- `pptx`/`html`/`eml`, `.csv` as a first-class format (`.csv` may still upload as `txt` if desired), OCR for scanned PDFs/spreadsheets, embedded images, pivot caches.
