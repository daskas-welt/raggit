# Tasks: Ingest Breadth (XLSX)

**Input**: Design documents from `/specs/003-ingest-breadth/`

**Prerequisites**: plan.md, spec.md, research.md (R1–R7), data-model.md, contracts/api.yaml (v1.2.0), quickstart.md

**Tests**: Included — Constitution VI (Test-First, NON-NEGOTIABLE): every test task below MUST be written first and observed FAILING (red) before its paired implementation task turns it green. Real-Ollama tests carry `[Trait("RequiresOllama","true")]` + probe skip (002 pattern) so CI stays green with fakes.

**Organization**: Tasks grouped by user story (US1 admin uploads xlsx, US2 edge guards — both P1); Phase 1–2 must complete before any story. FR-001..007 / SC-001..005 traceability table at the end.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: US1 / US2 / Found (foundational) / Pol (polish)
- File paths per `plan.md` Project Structure: `src/RAGGit.Core` / `src/RAGGit.Ingest` / `src/RAGGit.Retrieval` / `src/RAGGit.Workstation.Api` / `src/RAGGit.Client.Maui` (unchanged) / `tests/`

---

## Phase 1: Setup (Fixtures — no new projects, no new NuGet per Constitution VII)

**Purpose**: Deterministic xlsx sample workbooks consumed by every later test wave. Two tracks: committed binary fixtures (integration/quickstart parity) and an in-memory builder (unit tests, no mystery binaries).

- [ ] T001 [P] Create integration xlsx fixtures + checked-in generator under `tests/integration/fixtures/xlsx/`: `sample-3sheet.xlsx` (3 visible sheets, header + 10 data rows each, known sentence `refund policy: 30-day full refund with receipt` on Sheet2!B5, cell with cached value `42.50`), `sample-hidden.xlsx` (2 visible + 1 hidden sheet containing `hidden-token-xyz`), `sample-overcap.xlsx` (>100,000 visible `<c>` cells, quickstart step 3 recipe), `fake-xlsx-from-docx.xlsx` (valid docx zip renamed `.xlsx` — `word/document.xml`, no `xl/workbook.xml`), `corrupt.xlsx` (truncate `sample-3sheet.xlsx` after PK header), `empty-hidden-only.xlsx` (single hidden sheet). Generator script `tests/integration/fixtures/xlsx/generate-xlsx-fixtures.py` (openpyxl) + `tests/integration/fixtures/xlsx/README.md` documenting regeneration (no binary-only mystery files); register all six as `Content` `CopyToOutputDirectory=PreserveNewest` in `tests/integration/RAGGit.Tests.Integration.csproj`
- [ ] T002 [P] Create in-memory unit-test workbook builder `tests/unit/XlsxWorkbookBuilder.cs` (registered as plain class in `tests/unit/RAGGit.Tests.Unit.csproj`, uses already-referenced `DocumentFormat.OpenXml` — no new package): fluent builders for multi-sheet workbooks, `Hidden`/`VeryHidden` sheet state, formula cells with cached `<v>` (e.g. `=SUM(A1:A3)` cached `42.50`), SharedString vs InlineString cells, date-formatted cells on 1900 and 1904 date systems, merged-cell ranges, sparse/wide rows (1 row × 20k cells), N-cell workbooks for cap boundary tests (exactly 100_000 and 100_001), empty/blank-row sheets; returns `MemoryStream` positioned at 0

---

## Phase 2: Foundational (Blocking Prerequisites — MIME plumbing + deep-validation probe)

**Purpose**: `Xlsx` enum/JSON/content-type plumbing, `MaxSpreadsheetCells` constant, controller mime map, DB read-back mapping, and the reusable deep-PK probe helper. ⚠️ **CRITICAL**: no user story work begins until this phase is complete and green.

### Tests for Foundational (Write FIRST — must FAIL before implementation)

- [ ] T003 [P] [Found] Unit tests for the mime surface in `tests/unit/DocumentMimeTypeXlsxTests.cs`: `DocumentMimeType` contains `Xlsx` (enum order `Pdf, Docx, Xlsx, Txt, Md`); `Xlsx.GetContentType()` == `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`; `DocumentMimeTypeConverter` Read/Write round-trips that exact string and still throws `JsonException` on unknown values; `DocumentValidation.MaxSpreadsheetCells == 100_000` (FR-001, FR-005 constant per data-model.md)
- [ ] T004 [P] [Found] Contract tests for api.yaml 1.2.0 acceptance shape in `tests/contract/DocumentsContractTests.cs` (extends existing file, uses `tests/contract/TestApiFactory.cs` fakes): `POST /api/documents` with xlsx content-type → 201/accepted (not `unsupported type`), `.xlsx` extension fallback maps even when content-type is `application/octet-stream`; `GET /api/documents` serializes the doc's `mime` as the spreadsheetml string; `Document.mime` enum in `specs/003-ingest-breadth/contracts/api.yaml` includes the spreadsheetml value (FR-001)
- [ ] T005 [P] [Found] Unit tests for the deep PK probe in `tests/unit/XlsxDeepValidationTests.cs` (uses T001/T002 fixtures): genuine xlsx passes probe and returned stream is rewound to 0; `fake-xlsx-from-docx.xlsx` (PK magic + `[Content_Types].xml` but `word/document.xml`, no `xl/workbook.xml`) rejected with message naming `content does not match type`; `corrupt.xlsx` (truncated after PK header → `InvalidDataException`) rejected as 400-mappable error, never an unhandled 500; non-seekable stream is buffered to seekable `MemoryStream` (research.md R2); docx declared as `Docx` still passes on existing magic path (no regression)

### Implementation for Foundational

- [ ] T006 [Found] Extend `src/RAGGit.Core/Models/Document.cs`: add `Xlsx` to `DocumentMimeType` (Document.cs:9-15), add `Xlsx => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"` arm to `DocumentMimeTypeExtensions.GetContentType()` (Document.cs:67-74), add `public const int MaxSpreadsheetCells = 100_000;` to `DocumentValidation` (Document.cs:80-83); update `EnumDataType` error message text to list xlsx (depends on T003)
- [ ] T007 [Found] Add the spreadsheetml string → `DocumentMimeType.Xlsx` case to `DocumentMimeTypeConverter.Read` in `src/RAGGit.Core/Models/DocumentMimeTypeConverter.cs` (DocumentMimeTypeConverter.cs:13-24) so JSON write (via `GetContentType()`) and read are symmetric (depends on T006)
- [ ] T008 [Found] Extend `TryMapMime` in `src/RAGGit.Workstation.Api/Controllers/DocumentsController.cs` (DocumentsController.cs:120-164): declared content-type case `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` → `Xlsx` and extension fallback `.xlsx` → `Xlsx` (FR-001) (depends on T006)
- [ ] T009 [Found] Add spreadsheetml → `DocumentMimeType.Xlsx` case to `IngestService.MapDocument` in `src/RAGGit.Ingest/IngestService.cs` (IngestService.cs:359-366) so xlsx rows read back from SQLite instead of throwing `Unknown MIME type in database` (depends on T006)
- [ ] T010 [Found] Implement deep xlsx validation in `src/RAGGit.Ingest/DocumentFormatValidator.cs`: route `DocumentMimeType.Xlsx` through a new `ValidateXlsxDeepAsync(Stream)` probe (R2) — `new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true)` requiring `[Content_Types].xml` AND (`xl/workbook.xml` or fallback `xl/_rels/workbook.xml.rels`); `InvalidDataException`/`XmlException`/missing entries → typed rejection carrying `content does not match type` (truncation may say `corrupted xlsx`); `PK`-magic-only check is NOT sufficient for Xlsx; rewind `Position = 0` after probe (buffer non-seekable input like the existing path at DocumentFormatValidator.cs:55-64) (depends on T005)

**Checkpoint**: Foundation ready — `dotnet build RAGGit.sln` green; T003–T005 red→green; xlsx maps end-to-end at the boundary but extraction still yields nothing. No story work before this.

---

## Phase 3: User Story 1 - Admin Uploads an Excel Workbook (Priority: P1) 🎯 MVP

**Goal**: `.xlsx` flows validate → stream-extract (all visible sheets, header-repeat, cached values) → chunk 512/50 → embed → index → cite; a query for text on any visible sheet returns a citation for the workbook (FR-001..004, FR-007; SC-001, SC-003).

**Independent Test**: Per spec US-1 — upload `sample-3sheet.xlsx` → `GET /api/documents` shows `Ready` → `POST /api/query` "refund policy 30-day full refund" returns a citation whose `documentId` is the workbook and whose text contains the Sheet2 row; no US-2 edge cases needed.

### Tests for User Story 1 (Write FIRST — must FAIL or skip-gracefully before implementation) ⚠️

- [ ] T011 [P] [US1] Extractor unit tests in `tests/unit/XlsxExtractorTests.cs` (all workbooks via T002 builder; no Ollama): (a) 3-sheet extract contains `[Sheet: Sheet1]`/`[Sheet: Sheet2]`/`[Sheet: Sheet3]` lines and the known sentence, rows joined ` | ` (FR-003/FR-004); (b) header row repeated before EVERY data row — assert header line count == emitted data row count per sheet and every data line is immediately preceded by the header line (FR-003); (c) formula cell emits cached `42.50`, `SUM` appears nowhere in output (FR-004, SC-003); (d) SharedString and InlineString cells both resolve to text (no raw SST indices like `12`); (e) date-formatted cells render best-effort `yyyy-MM-dd` on 1900 and 1904 systems, raw cached value on missing style (R5 assumption); (f) merged range → value on anchor cell only; (g) all-blank rows omitted; (h) extract piped through `Chunker.ChunkText(512,50)` yields >1 chunk and every chunk containing a data row also contains header text (self-describing chunks, R7) (depends on T002)
- [ ] T012 [P] [US1] Integration tests in `tests/integration/XlsxIngestTests.cs` (uses `TestApiFactory` fakes + T001 fixtures): upload `sample-3sheet.xlsx` (Admin key) → 201 with spreadsheetml `mime`; poll `GET /api/documents` → `Ready`; `Chunks` rows for that document contain `[Sheet:` prefix text; re-upload identical bytes → 200 with existing `documentId` and no second index (dedupe parity with pdf/docx, spec Edge Cases) (FR-001, SC-001) (depends on T001, T008)
- [ ] T013 [P] [US1] Opt-in real citation tests in `tests/integration/XlsxQueryCitationTests.cs`: `[Trait("RequiresOllama","true")]` + existing probe-skip helper — real LanceDB temp path + real embedder; upload `sample-3sheet.xlsx` → `Ready` <30s on dev laptop (SC-001); `POST /api/query` "refund policy 30-day full refund" → 200 `{answer, citations[]}` with ≥1 citation `documentId` == workbook and `text` containing the Sheet2 row (FR-007, Constitution V); query "42.50" → citation containing `42.50`, not formula text (SC-003); machine without Ollama → SKIP never FAIL (depends on T001)

### Implementation for User Story 1

- [ ] T014 [US1] Implement streaming extractor `ExtractXlsxTextAsync(Stream)` in `src/RAGGit.Ingest/Chunker.cs` and wire `DocumentMimeType.Xlsx => await ExtractXlsxTextAsync(stream)` into the `ExtractTextAsync` switch (Chunker.cs:23-32) — per R1/R3/R4/R5/R7: `SpreadsheetDocument.Open(stream, false)` for sheet enumeration + `SharedStringTable` (null-safe fallback when absent) + `WorkbookProperties.Date1904`; SAX `OpenXmlReader` per visible `WorksheetPart` (never `Descendants<Row>()` DOM); skip `Sheet.State == Hidden|VeryHidden` (null = Visible); first non-empty row = header; emit `[Sheet: <name>]` + header line, then header line repeated before each data line; per-cell: `CellFormula` → cached `v` only, `SharedString` → SST lookup, `InlineString` → inner text, style → best-effort date via `DateTime.FromOADate` with 1904/leap-bug offset, fallback raw `v`; join ` | `, skip empty rows, ignore drawings/charts/pivots; `Environment.NewLine`-joined string returned for existing `ChunkText(512,50)` — no `Chunk` schema change (depends on T006, T011)

**Checkpoint**: US-1 independently functional — T011/T012 green with fakes; T013 green or skipping gracefully; the spec US-1 Independent Test passes end-to-end. MVP gate.

---

## Phase 4: User Story 2 - Edge Cases and Guards for Spreadsheets (Priority: P1)

**Goal**: 100k visible-cell cap → 413 with actionable message; hidden sheets never indexed; deep-PK and empty/hidden-only → 400; every rejection retains NO Document/Chunks/vectors (FR-002/005/006; SC-002, SC-004, SC-005).

**Independent Test**: Per spec US-2 — hidden-only workbook → 400 "no extractable content"; 110k-cell workbook → 413 naming cap + actual count; renamed docx → 400 "content does not match type"; truncated xlsx → 400; in every case `GET /api/documents` count unchanged.

### Tests for User Story 2 (Write FIRST — must FAIL) ⚠️

- [ ] T015 [P] [US2] Cell-cap unit tests in `tests/unit/XlsxCellCapTests.cs` (T002 builder): exactly 100_000 visible cells → extract succeeds; 100_001 → typed `SpreadsheetCellCapExceededException` (or equivalent result) whose message names the 100,000 cap AND the actual count; hidden-sheet cells excluded from the count (80k visible + 50k hidden → passes, R6); wide sparse row (1 × 20k `<c>` tags) counts toward the cap — no bypass (FR-005) (depends on T002, T006)
- [ ] T016 [P] [US2] Guard integration tests in `tests/integration/XlsxGuardTests.cs` (fakes): (a) `sample-overcap.xlsx` → 413 with `error` matching `exceeds 100,000 cell limit` and containing the actual count, `GET /api/documents` count unchanged, zero `Chunks` rows, zero LanceDB vectors for that hash (SC-004); (b) `empty-hidden-only.xlsx` → 400 `no extractable content`, nothing retained; (c) all-blank-row workbook → 400 same; (d) `fake-xlsx-from-docx.xlsx` → 400 `content does not match type`, no partial index (SC-005); (e) `corrupt.xlsx` → 400 (never 500); (f) ordering: >100MB file → 413 size error first; a renamed docx with >100k zip entries → 400 type error, NOT 413 cap (R6 ordering); (g) upload `sample-hidden.xlsx` → `Ready`, then assert no `Chunk.Text` (SQLite or fake vector store) contains `hidden-token-xyz`, and querying that token yields `no relevant content found` with zero citations (SC-002, FR-002) (depends on T001, T010, T014)
- [ ] T017 [P] [US2] Contract tests for rejection bodies in `tests/contract/DocumentsContractTests.cs`: 413 response `{error}` example matches api.yaml 1.2.0 shape `Spreadsheet exceeds 100,000 cell limit (found {n} cells)`; 400 responses carry `{error}` for `content does not match type` and `no extractable content` variants; both annotated per `specs/003-ingest-breadth/contracts/api.yaml` (FR-005, FR-006)

### Implementation for User Story 2

- [ ] T018 [US2] Enforce the cap during the SAX scan in `ExtractXlsxTextAsync` in `src/RAGGit.Ingest/Chunker.cs`: count physical `<c>` cells across visible sheets only (hidden never counted, R6), abort as soon as count > `DocumentValidation.MaxSpreadsheetCells` (pre-tokenization, pre-chunk) throwing the typed cap exception naming cap + actual count; add the exception type in `src/RAGGit.Ingest/` (consumed by controller mapping) (depends on T015)
- [ ] T019 [US2] Map rejections with NO partial index in `src/RAGGit.Workstation.Api/Controllers/DocumentsController.cs` + `src/RAGGit.Ingest/IngestService.cs`: deep-validation rejection from T010 → 400 `content does not match type` (corruption → 400 too, never 500); cap exception → 413 with the actionable message; zero non-empty visible rows → 400 `no extractable content`; ensure these paths NEVER persist a Document — reorder `IngestService.IngestAsync` so xlsx validation + extraction happen BEFORE `InsertDocumentAsync` (IngestService.cs:82) or delete any staged row (current `Failed`-status path at IngestService.cs:119-124 is wrong for 400/413 rejections per data-model.md "rejections never create a Document row"); preserve hash-dedupe 200 behavior and existing pdf/docx 400 hardening (002) (depends on T010, T018, T016)
- [ ] T020 [US2] Manual guard-ordering + hidden-exclusion demo per `specs/003-ingest-breadth/quickstart.md` step 6 on dev laptop: >100MB → 413 first; renamed docx with huge zip → 400 not 413; 80k visible + 50k hidden → 201 `Ready`; hidden token query → 0 hits; capture outputs for `verification.md` (depends on T019)

**Checkpoint**: US-1 AND US-2 both independently functional — library integrity holds under every xlsx edge case (FR-011 scale guard in place).

---

## Phase 5: Polish & Cross-Cutting Concerns

**Purpose**: Version/docs sync to contract 1.2.0, measured SC sign-off, coverage gate.

- [ ] T021 [P] [Pol] Contract 1.2.0 + docs sync (Constitution VII MINOR): confirm shipped API surface matches `specs/003-ingest-breadth/contracts/api.yaml` 1.2.0 (xlsx mime enum, 413 cell-cap, 400 deep-validation annotations); bump repository version references so `/health` reports `1.2.0` (`src/RAGGit.Workstation.Api/Controllers/HealthController.cs` + `src/RAGGit.Workstation.Api/Program.cs`/`appsettings*` if versioned there); update `README.md` supported-formats list to `pdf/docx/xlsx/txt/md` with the 100k-cell cap and hidden-sheet-skip notes; keep `specs/001-offline-mode/quickstart.md` format references consistent (002 T031 pattern)
- [ ] T022 [Pol] Run full `specs/003-ingest-breadth/quickstart.md` steps 1–7 on the dev laptop and create `specs/003-ingest-breadth/verification.md` with measured results: SC-001 (upload→`Ready` wall-time <30s, cited sheet-2 query), SC-002 (hidden token 0 hits), SC-003 (`42.50` cached-value citation), SC-004 (413 over-cap, nothing retained), SC-005 (renamed/corrupt 400, no partial index), plus plan.md perf goal (extract ≤2s for a 100k-cell workbook) and the no-new-NuGet confirmation from `dotnet build` (depends on T012, T013, T016, T020)
- [ ] T023 [P] [Pol] Coverage + branch sweep per Constitution VI (≥80% libraries): `dotnet test` with coverage for `RAGGit.Core`/`RAGGit.Ingest`; add targeted unit tests in `tests/unit/` for uncovered xlsx branches — 1904 date path, null `SharedStringTablePart` fallback, missing `WorkbookStylesPart` fallback, `VeryHidden` variant, exactly-at-cap boundary, `xl/_rels/workbook.xml.rels` probe fallback (R2 open item)
- [ ] T024 [P] [Pol] CI/offline invariant proof: plain `dotnet test` (fakes, no Ollama, no WAN) fully green with the whole xlsx suite; `RequiresOllama` tests SKIP not FAIL; no task introduced any non-configured egress (Constitution IV/VI); confirm `RAGGit.Client.Maui` truly untouched (thin client, Constitution II)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 Setup**: No deps — start immediately (T001 ∥ T002)
- **Phase 2 Foundational**: Depends on Phase 1 (T005/T001 fixtures for probe tests) — ⚠️ BLOCKS all user stories (mime plumbing + deep-validation probe)
- **Phase 3 US1 (P1)**: Depends on Phase 2 — extractor + end-to-end happy path; MVP gate
- **Phase 4 US2 (P1)**: Depends on Phase 2 (T010 probe) AND on US1's extractor (T014) because the cap count lives inside the SAX scan; hidden-token/0-hits guard reuses T014's visibility filter. Independently testable once T014 lands.
- **Phase 5 Polish**: Depends on both stories (T022 needs all measurement paths green)

### Red→Green Pairs (Constitution VI — tests MUST fail first)

| Test (red) | Implementation (green) |
|---|---|
| T003 | T006, T007 |
| T004 | T006, T008 |
| T005 | T010 |
| T011 | T014 |
| T012 | T014 (+ T008/T009 plumbing) |
| T013 | T014 (real path; skips without Ollama) |
| T015 | T018 |
| T016 | T018, T019 |
| T017 | T019 |

### Within Each Story

- Fixtures/builders before tests; tests before implementation; extractor (model-level) before controller mapping (endpoint-level); story checkpoint validated via spec Independent Test before the next phase.

### Parallel Opportunities

- T001 ∥ T002 (Setup). Red wave T003 ∥ T004 ∥ T005; green wave T006 → {T007 ∥ T008 ∥ T009} and T010 independent. US1 red wave T011 ∥ T012 ∥ T013. US2 red wave T015 ∥ T016 ∥ T017. Polish T021 ∥ T023 ∥ T024. US1 and US2 test-waves may be drafted in parallel by different developers after Phase 2, but US2 implementation tasks T018/T019 land after T014.

### FR/SC Traceability

| Requirement | Tasks |
|---|---|
| FR-001 xlsx end-to-end (MIME map → validate → extract → chunk → embed → index → cite) | T003, T004, T006, T007, T008, T009, T012, T014 |
| FR-002 all visible sheets, hidden skipped | T011(b), T014, T015 (cap exclusion), T016(g) |
| FR-003 row-wise ` \| ` + `[Sheet: name]` + header-repeat | T011(a)(b)(h), T014 |
| FR-004 cached formula values, ` \| ` join, empty rows skipped | T011(a)(c)(g), T014 |
| FR-005 100k cell cap → 413 actionable, nothing retained | T003, T006, T015, T016(a)(f), T017, T018, T019 |
| FR-006 deep PK validation → 400, no partial index | T005, T010, T016(d)(e)(f), T017, T019 |
| FR-007 xlsx citations carry documentId/chunkId/text | T013, T022 |
| SC-001 multi-sheet upload searchable as `Ready` | T012, T013, T022 |
| SC-002 hidden-sheet content never returned | T016(g), T022 |
| SC-003 formula cell retrievable by cached value | T011(c), T013, T022 |
| SC-004 over-cap → 413, nothing retained | T015, T016(a), T022 |
| SC-005 renamed/corrupt xlsx → 400, no partial index | T005, T010, T016(d)(e), T022 |

---

## Parallel Example: Foundational red wave (Constitution VI)

```bash
# Write all three failing test files together (different files, no deps):
Task: "T003 tests/unit/DocumentMimeTypeXlsxTests.cs — enum/GetContentType/converter/MaxSpreadsheetCells"
Task: "T004 tests/contract/DocumentsContractTests.cs — 1.2.0 xlsx acceptance + mime round-trip"
Task: "T005 tests/unit/XlsxDeepValidationTests.cs — ZipArchive probe: docx-as-xlsx 400, truncated 400, rewind"
# Observe RED, then implement T006 → T007/T008/T009 and T010.
```

---

## Implementation Strategy

### MVP First (US1 Only)

1. Phase 1 + Phase 2 → mime plumbing + deep-validation probe green (xlsx accepted at the boundary)
2. Phase 3 US1 → streaming extractor + header-repeat + chunk/cite path → **spec US-1 Independent Test passes** → demo-able MVP
3. Phase 4 US2 → guards make it safe for the 5k-doc library
4. Phase 5 → version/docs/coverage sign-off

### Incremental Delivery

- US1 delivers searchable workbooks without US2; US2 adds guards without changing US1's happy path; each checkpoint maps to a spec Independent Test.

---

## Notes

- **Test-first is non-negotiable** (Constitution VI): never start T006/T007/T008/T009/T010/T014/T018/T019 before their paired red test is committed and observed failing.
- **No new NuGet** (spec Assumption / plan Constraints): `DocumentFormat.OpenXml` is already referenced in `src/RAGGit.Ingest/RAGGit.Ingest.csproj`; T002's builder uses it from `tests/unit` via the project reference — do not add ClosedXML/ExcelDataReader.
- **No-partial-index invariant** (FR-005/FR-006, data-model.md): rejections must never leave `Documents`/`Chunks` rows or LanceDB vectors — T019 explicitly fixes the pre-insert ordering in `IngestService.IngestAsync`; a `Failed` status row is NOT an acceptable outcome for 400/413.
- **Guard ordering** (R6): size 413 → deep-validation 400 → cell-cap 413 → no-extractable-content 400; T016(f) locks the ordering contract so a renamed docx is diagnosed as wrong-type, not over-cap.
- **Hidden sheets are invisible to the cap too** (R3/R6): count only visible-sheet `<c>` elements; T015 asserts the 80k+50k-hidden pass case.
- **Streaming only** (R1): never materialize `SheetData` DOM; the 100k-cell/≤2s perf goal in T022 is the check.
- **Single-tenant / offline / thin client** (Constitution I/II/IV): xlsx logic lives entirely in `RAGGit.Core`/`RAGGit.Ingest`/`RAGGit.Workstation.Api`; `RAGGit.Client.Maui` and `RAGGit.Retrieval` untouched; no egress introduced.
- **Out of scope** (spec): OCR, pptx/html/eml, `.csv` as first-class, `.xls` (BIFF) — reject anything not OOXML SpreadsheetML; do not fix the tokenizer todo here.
- Commit after each task or logical red→green pair; stop at any checkpoint to validate the story independently per spec Independent Tests.

