# Implementation Plan: Ingest Breadth (XLSX)

**Branch**: `003-ingest-breadth` | **Date**: 2026-09-11 | **Spec**: [spec.md](./spec.md) | **Constitution**: v1.1.0

**Input**: Feature specification from `/specs/003-ingest-breadth/spec.md` — add `.xlsx` as fifth ingest format end-to-end (MIME map → deep validation → streaming extract of all visible sheets → row-wise header-repeat text representation → chunk 512/50 → embed → LanceDB index → citation), enforce 100k cell cap (413) and PK deep-validation (400), hide hidden-sheet content, use cached formula values, no new NuGet beyond `DocumentFormat.OpenXml` already referenced, no OCR/pptx/html/eml/csv-first-class.

## Summary

Extend the single-tenant offline RAG library (001 + 002 real bring-up) with spreadsheet ingest breadth: `.xlsx` (`application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`) joins `pdf/docx/txt/md` through the same pipeline (validate → extract → chunk → embed → index → query with citations). Each visible sheet is serialized row-wise as `col1 | col2 | col3` with a leading `[Sheet: <name>]` line and the header row (first non-empty row) repeated before every data row so any 512-token chunk is self-describing. Hidden/veryHidden sheets are skipped entirely. Formula cells emit their cached/displayed value. A per-document cap of 100,000 cells across visible sheets is enforced before chunking (413 with actionable message, no document retained). Deep validation disambiguates `PK` zip streams by inspecting `[Content_Types].xml` / `xl/workbook.xml` and rejects renamed docx/corrupt xlsx with 400 and no partial index, reusing the 002 hardening pattern. Contract bumps `1.1.0` → `1.2.0` (MINOR, additive mime enum). No SQLite schema change; no new project; `Chunk` unchanged.

## Technical Context

**Language/Version**: C# .NET 8 (both tiers; .NET 9 compatible)

**Primary Dependencies**: ASP.NET Core 8 (Workstation API), `DocumentFormat.OpenXml` 3.x (already referenced — `SpreadsheetDocument` / `OpenXmlReader` SAX for streaming), `System.IO.Compression.ZipArchive` for deep PK validation, `UglyToad.PdfPig` (pdf), LanceDB .NET SDK embedded `lancedb.connect(VectorDb:Path)` file-backed, `SQLite` (`Microsoft.Data.Sqlite` doc metadata), `OllamaSharp` / `HttpClient` to `Ollama:Url`, `CommunityToolkit.Mvvm` (MAUI), `System.Text.Json`

**Storage**: Workstation: embedded LanceDB `connect("./data/lancedb")` table `library` with `FixedSizeList<float, VectorSize>` HNSW cosine (`m=16, ef=128`) + `rag.db` SQLite (`Documents`, `Chunks`, `Queries`, `Library` 1 row); xlsx adds no new table — sheet name is textual prefix inside `Chunk.Text`; Client: stateless

**Testing**: xUnit + FluentAssertions, `Microsoft.AspNetCore.Mvc.Testing` (contract), integration (offline, guard 400/413, no-partial-index), unit for extractor (3-sheet, hidden-sheet, formula cached, merged, date best-effort, empty, over-cap), `Microsoft.AspNetCore.Mvc.Testing` contract vs `contracts/api.yaml` 1.2.0; opt-in real suite still gated by Ollama probe (as in 002); 80% library coverage

**Target Platform**: AI Workstation: Windows 10+/Linux (Ubuntu 22.04) 16GB RAM + GPU recommended, 10GB disk; Client: Windows 11 desktop primary (`net8.0-windows10.0.19041.0`); iOS/Android TFMs buildable but not acceptance — LAN/VPN to workstation, no cloud egress

**Project Type**: Client + API (workstation service + thin MAUI client; single solution 5 projects + tests)

**Performance Goals**: XLSX extract ≤2s for 100k-cell workbook on dev laptop (streaming, not DOM); cell-cap check is O(visible cells) before tokenization; chunk/ingest path unchanged (512/50, topK=5); SC-001 multi-sheet xlsx `Ready` <30s on dev laptop; SC-002 hidden-only query 0 hits

**Constraints**: Offline invariant (no WAN at query time, LAN only), thin client zero local model weights, single-tenant per deployment, proprietary signed distribution, `100k cells` guard orthogonal to `100MB` file-size cap, no new NuGet, no OCR/pptx/html/eml/csv-first-class, `DocumentFormat.OpenXml` already referenced

**Scale/Scope**: 5k documents / ~1M chunks max per library (singleton), chunk 512 tokens / 50 overlap, retrieval topK=5, 50 screens max; per-doc 100k cell cap is the xlsx scale guard (mirrors 100MB)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] **I. Single-Tenant On-Prem**: Data model remains singleton Library (no Company entity), API has no tenant parameter (`/api/documents` not `/companies/{cid}/...`), one-tenant-per-workstation — xlsx is just an added mime, complies.
- [x] **II. Workstation-Owned AI**: Vectors and LLMs live only on workstation (LanceDB path, Ollama); MAUI client remains `HttpClient`-only, no `IVectorStore`/`IEmbedder` in `RAGGit.Client.Maui` — xlsx extraction runs server-side in `RAGGit.Ingest`, complies.
- [x] **III. .NET Library-First & Client Reuse**: Libraries `RAGGit.Core`/`RAGGit.Ingest`/`RAGGit.Retrieval` reused by both API and client; xlsx extractor lives in `RAGGit.Ingest.Chunker` (same static class as pdf/docx), no new project, TFMs `net8.0-windows10.0.19041.0` / `net8.0-ios` / `net8.0-android` (`net8.0` fallback for CI), complies.
- [x] **IV. Offline Invariant (NON-NEGOTIABLE)**: Xlsx extraction is fully local (`DocumentFormat.OpenXml` + `ZipArchive`), no cloud pull; `POST /api/query` offline still works for xlsx chunks; no `ollama pull` at query time, complies.
- [x] **V. Citation-Grounded RAG**: `POST /api/query` still `{answer, citations[]}` + `no relevant content found` branch; xlsx chunks carry `documentId`/`chunkId`/`ordinal`/`text` like every other format (FR-007), measured in SC-001..003, complies.
- [x] **VI. Test-First (NON-NEGOTIABLE)**: TDD gates unit + contract (`contracts/api.yaml` v1.2.0) + integration (400/413, hidden skip, no-partial-index) + opt-in real; xlsx extractor unit tests fail before green, complies.
- [x] **VII. Simplicity & Proprietary Stewardship**: Still 5 projects + tests within limit; no new project; no new NuGet (uses existing `DocumentFormat.OpenXml`); versioning `1.2.0` MINOR for additive mime enum (`application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`) per spec Assumptions; signing MSIX + private mobile unchanged, complies.

*Re-check after Phase 1 2026-09-11: No new violations introduced by xlsx streaming, deep validation, 100k cap, or contract 1.2.0 mime addition.*

## Project Structure

### Documentation (this feature)

```text
specs/003-ingest-breadth/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
│   └── api.yaml         # OpenAPI 1.2.0: 002 + Document.mime xlsx + 413 cell-cap / 400 deep-validation annotations
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
RAGGit.sln
├── src/
│   ├── RAGGit.Core/                 # Shared models (Document, Chunk, DocumentMimeType incl. Xlsx, DocumentValidation.MaxSpreadsheetCells)
│   ├── RAGGit.Ingest/               # Chunker.ExtractTextAsync streaming xlsx + Validator deep xlsx + 100k cap guard
│   ├── RAGGit.Retrieval/            # Embed query → LanceDB search topK=5 → prompt → local LLM (unchanged)
│   ├── RAGGit.Workstation.Api/      # ASP.NET Core 8 API: POST/GET /api/documents, DELETE /api/documents/{id}, POST /api/query, GET /api/auth/me, /health (+ mime map + 400/413 xlsx paths)
│   └── RAGGit.Client.Maui/          # .NET MAUI .NET 8: unchanged (thin, no xlsx logic)
├── tests/
│   ├── unit/                        # RAGGit.Ingest xlsx extractor + cap + validator + chunk representation
│   ├── contract/                    # OpenAPI contract tests (Api.Tests) against api.yaml 1.2.0
│   └── integration/                 # Offline + guard 400/413 + hidden skip + no-partial-index + query citation for xlsx
├── data/                            # .gitignored: ./data/lancedb, rag.db
└── models/                          # .gitignored: GGUF + ONNX — not used for xlsx
```

**Structure Decision**: Client + API — same 5-project split as `001`/`002` (Rationale: workstation API and MAUI client share `RAGGit.Core`/`Ingest`/`Retrieval` per Constitution III but client must stay thin per II). No new project; xlsx work touches `RAGGit.Core` (mime enum + validation constant) and `RAGGit.Ingest` (Chunker + Validator) only; `Chunk` and LanceDB schema unchanged.

## Phase 0 Research Summary

Seven decisions (R1-R7). Full details in [research.md](./research.md).

| # | Topic | Decision | Why |
|---|-------|----------|-----|
| R1 | XLSX streaming extraction API | Use `DocumentFormat.OpenXml.Packaging.SpreadsheetDocument.Open(stream, false)` + SAX `OpenXmlReader` over `xl/worksheets/sheet*.xml` with `SharedStringTable` lookup; DOM `WorkbookPart` kept only for sheet list + `WorkbookProperties.Date1904`; never load whole `SheetData` DOM for large sheets | O(1) memory for 100k cells; `DocumentFormat.OpenXml` already referenced so no new NuGet; SAX avoids OOM on wide sheets |
| R2 | Deep PK / ZIP validation for xlsx vs docx | Validate by opening `ZipArchive(stream, ZipArchiveMode.Read, leaveOpen:true)` and requiring `[Content_Types].xml` plus `xl/workbook.xml` (or `xl/_rels/workbook.xml.rels` fallback); reject `PK` streams lacking both with 400 "content does not match type" | `xlsx` and `docx` share `PK` magic; magic-byte alone insufficient per FR-006; ZipArchive probe is the same hardening pattern as 002 corrupted-pdf |
| R3 | Sheet visibility filtering | Read `Sheet.State` (`Visible` vs `Hidden`/`VeryHidden`); skip any sheet where `State != Visible` (null = Visible); empty/whitespace-only rows skipped; hidden sheet cells never counted toward cap or emitted | FR-002 explicit; matches Excel semantics; hidden content must not enter index (SC-002) |
| R4 | Cell value & formula cached value | For each `Cell`: if `CellFormula` present, use `CellValue`/`InnerText` cached display value, not formula text; resolve `Cell.DataType == SharedString` via `SharedStringTable` index and `InlineString` via `InlineString` element; join cells per row with ` \| ` | FR-004; `v` is cached value already formatted by Excel on save; InlineString + SharedString both occur in real workbooks |
| R5 | Date system & custom number formats | Read `WorkbookProperties.Date1904` (default 1900 system); resolve `StyleIndex → CellFormat → NumberFormatId` + `NumberingFormats` table; best-effort apply built-in + custom date formats via `DateTime.FromOADate` with 1904 offset; fallback to raw cached `v` when format missing/unparseable | Assumptions: best-effort date, not strict; avoids mis-rendering `44561` as raw double when cell is date-formatted |
| R6 | Cell-cap guard placement & HTTP mapping | Count cells across **visible sheets only** during streaming (pre-tokenization, pre-chunk); if `>100_000` → reject with `413 Payload Too Large` + message `Spreadsheet exceeds 100,000 cell limit (found {n} cells)` and no Document/Chunks/vectors retained; ordering: file-size `413 >100MB` checked first, then deep-validation `400` for non-xlsx PK before cap, then `400 "no extractable content"` for zero non-empty rows | FR-005 + FR-006; 413 before chunking prevents 1M-token blowup (FR-011 scale); 400 for type mismatch precedes cap so renamed docx gets 400 not 413; transaction rolls back on every rejection |
| R7 | Row-wise text representation & chunking | Per visible sheet emit `[Sheet: <name>]` header line, then header row (first non-empty row) repeated before each data row: `h1 \| h2 \| h3` then `h1 \| h2 \| h3` + `val1 \| val2 \| val3` for each data row; empty rows omitted, merged-cell anchor-only semantics inherited from OpenXml, embedded drawings/charts ignored; resulting text fed to existing `ChunkText(512/50)` — no Chunk schema change | FR-003: header-repeat makes every chunk self-describing for retrieval without sheet-level metadata; textual sheet name satisfies citation without new entity |

Open verification items (kept in `research.md`): `OpenXmlReader` vs `SpreadsheetDocument` DOM streaming exact API (requires compile probe), `Date1904` property path, custom `NumberFormat` id range handling, `ZipArchive` `leaveOpen:true` and stream seekability after validation, 413 vs 400 ordering for over-cap renamed docx, and whether to emit `Sheet: name` only once or repeat per chunk boundary.

## Phase 1 Design

### Data Model — delta on 001/002

- **No SQLite schema change** — `Documents`, `Chunks`, `Queries`, `Library` unchanged (see [data-model.md](./data-model.md)).
- **DocumentMimeType gains `Xlsx`**: enum `Pdf, Docx, Txt, Md → Pdf, Docx, Xlsx, Txt, Md`; `GetContentType()` maps to `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`; JSON converter handles the same string; `EnumDataType` validation allows it in `POST /api/documents`.
- **DocumentValidation gains `MaxSpreadsheetCells = 100_000`**: per-document guard orthogonal to `MaxFileSizeBytes = 100MB`; checked pre-chunk. Empty/only-hidden workbook → 400 "no extractable content" (no Document retained) rather than 413.
- **Extractor**: streaming `Chunker.ExtractXlsxTextAsync(stream)` → counts cells (visible only), skips hidden sheets, resolves shared/inline strings, uses cached formula values, best-effort dates, joins with ` | `, skips empty rows, emits `[Sheet: name]` + header-repeat representation.
- **Sheet**: Not a stored entity; sheet name is embedded textually as `[Sheet: <name>]` inside each chunk's `text` (per spec Key Entities).
- **Chunk**: unchanged: `Id`/`DocumentId`/`Ordinal`/`Text`/`TokenCount`; `Text` now contains spreadsheet row text for xlsx docs.
- **LanceDB `library`**: unchanged; xlsx chunks indexed identically to pdf/docx.

See [data-model.md](./data-model.md).

### Contracts — OpenAPI 1.2.0

Copy of `specs/002-real-bringup/contracts/api.yaml` with `info.version: 1.2.0` (MINOR per Constitution VII — additive, non-breaking) plus:

- `Document.mime` enum gains `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`.
- `POST /api/documents` `413` description extended: `File >100MB or spreadsheet exceeds 100,000 cells (FR-005)` with actionable message naming the 100k cap and actual cell count.
- `POST /api/documents` `400` description extended: deep-validation for `PK` streams that are not `xlsx` (renamed docx/corrupt/truncated) + `no extractable content` for empty/hidden-only workbooks, no partial index (FR-006; same invariant as 002 corrupted pdf/docx).
- `GET /api/auth/me` already in 002 (unchanged), `GET /api/documents`, `DELETE /api/documents/{id}`, `POST /api/query`, `/health` unchanged.

See [contracts/api.yaml](./contracts/api.yaml).

### Tests — outline

- **Unit** (`tests/unit`, `RAGGit.Ingest`):
  - 3-sheet workbook (each sheet header + 10 rows, known sentence on sheet 2) → extract contains `[Sheet: Sheet2]` + that sentence + header repeated; chunk count >1 and every data row chunk contains header text.
  - Hidden-sheet workbook (2 visible + 1 hidden with token `hidden-token-xyz`) → extract never contains hidden token; hidden sheet cells excluded from cell count.
  - Hidden-only workbook → `Extract` returns empty → service returns 400 "no extractable content".
  - Formula cell ( cached `42.50` vs formula `=SUM(A1:A3)` ) → search for `42.50` hits, `SUM` does not.
  - SharedString + InlineString mix → both resolved.
  - Date-formatted cell (1900 system + 1904 system `45.5` → 1904 date) → best-effort formatted date appears; fallback raw on missing style.
  - Merged cells anchor-only → value only on anchor col.
  - Empty workbook / single empty sheet → 400.
  - 100_001 cells visible → `MaxSpreadsheetCells` validator returns 413; 100_000 exactly → passes.
  - Renamed docx (`PK` with `word/document.xml`) presented as `Xlsx` mime → deep validator returns 400 "content does not match type".
  - Truncated xlsx (truncate after `PK` header) → 400.
- **Contract** (`tests/contract`): `contracts/api.yaml` 1.2.0 shape — `Document.mime` allows xlsx, POST 413/400 annotations, GET list returns xlsx docs as `Ready`.
- **Integration** (`tests/integration`): upload xlsx → `GET /api/documents` shows `Ready`, `POST /api/query` for sheet-2 sentence returns citation whose `documentId` is workbook; over-cap xlsx → 413 and `GET` does not list it; hidden-only → 400; renamed docx → 400; dedupe same-hash xlsx returns 200 existing.

### Quickstart — outline

- [quickstart.md](./quickstart.md): real steps — `dotnet run --project src/RAGGit.Workstation.Api`, `/health`, `curl` upload xlsx samples (3-sheet, hidden-sheet, over-cap, renamed docx), `GET /api/documents` verify `Ready`, `POST /api/query` for formula value `42.50` and hidden-token 0 hits, guard demos (100k cap 413 vs 400 ordering), opt-in `dotnet test` note, MAUI client unchanged.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |

No violations. Still 5 projects + tests; no new project, no new NuGet package (`DocumentFormat.OpenXml` already referenced), no cloud fallback, no per-person identity change in this feature, versioning `1.2.0` MINOR additive only. All seven constitution gates pass both pre-Phase 0 and post-Phase 1.
