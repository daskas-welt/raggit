# Research: Ask-View Document Output

**Feature**: `033-ask-document-generation` | **Date**: 2026-11-16

This phase resolves the technical choices behind the specification. The assessed scope is the **library-derived tier only**: from-scratch generation is explicitly deferred by `.specify/assessments/doc-generation/decision.md`. Every decision below preserves the existing grounded-answer behaviour.

## R1 — Render from the persisted answer, never re-generate

- **Decision**: A document is rendered from the already-persisted `Query` row (`IQueryRepository.GetDetailAsync(sub, id)`), using its `Answer`, `Prompt` and `Citations`. The document endpoint never calls the LLM.
- **Rationale**: FR-003 requires the file to contain the answer the conversation showed, not a second generation whose wording could diverge. Rendering from the persisted row also makes "any finished answer" work identically for history, keeps document production fast, and means a document can be produced while the model is unavailable.
- **Alternatives considered**:
  - Re-run `QueryPipeline` with a format instruction: rejected — a second generation can change facts/names and violates FR-003.
  - Send the answer from the client and render it server-side: rejected — unnecessary trust in client input and harder to guarantee citation integrity.
  - Render in the client: rejected — spreads four format libraries into the thin desktop and duplicates format logic.
- **Consequence**: `QueryResponse` and the streamed `answer` event must carry the persisted query id so the client can request the document. This is an additive wire change (contract 1.6.0 → 1.7.0).

## R2 — A dedicated `RAGGit.Documents` library

- **Decision**: Add `src/RAGGit.Documents` (library) that owns format detection, rendering, table shaping, file naming and citation mapping. The API references it; the client references only the shared `RAGGit.Core` types.
- **Rationale**: Constitution III requires feature logic in an independently testable library. Document assembly is a distinct responsibility from retrieval/orchestration (`RAGGit.Retrieval`) and from upload extraction (`RAGGit.Ingest`), and it must not live in the API controller. A focused library keeps OpenXml/SkiaSharp out of the client and lets unit tests validate every format.
- **Alternatives considered**:
  - Add to `RAGGit.Retrieval`: rejected — couples query orchestration to word-processing/PDF libraries.
  - Add to `RAGGit.Ingest`: rejected — ingest is read-only extraction; generation is the opposite direction.
  - Add to `RAGGit.Workstation.Api`: rejected — violates library-first and is not independently testable.
- **Complexity**: This is an extra project, recorded in `plan.md` Complexity Tracking as required by Constitution VII.

## R3 — DOCX and XLSX use the existing OpenXml SDK

- **Decision**: Use `DocumentFormat.OpenXml` 3.5.1 (already present in `RAGGit.Ingest`) for both Word and Excel output. No new package for these formats.
- **Rationale**: The SDK can create both packages natively, supports Greek text, preserves typed spreadsheet cells and is already restored in the solution. Reusing the version avoids a second OpenXml graph.
- **Alternatives considered**: `docx`/`ClosedXML`/`NPOI` — extra dependencies for no capability this feature needs.

## R4 — PDF uses SkiaSharp with an embedded Greek-capable font

- **Decision**: Use `SkiaSharp` 4.156.1 (MIT) and its PDF backend (`SKDocument.CreatePdf`) with `DejaVuSans.ttf` (regular and bold) embedded in `RAGGit.Documents`. No system-font lookup.
- **Rationale**: PDF is a required format and the existing `PdfPig` dependency can only read PDFs. SkiaSharp is MIT, has no revenue-gated licence, ships Win32/Linux native assets, and can draw the embedded DejaVu typeface directly. This keeps Greek glyphs deterministic on Windows and Linux workstations.
- **Alternatives considered**:
  - `PDFsharp-MigraDoc` 6.5 — the current line is AGPL or paid-commercial, incompatible with this MIT project's proprietary deployment. Rejected.
  - `PDFsharpCore`/`MigraDocCore` — MIT, but pulls a vulnerable `SixLabors.ImageSharp` line; pinning a fixed ImageSharp major risks API incompatibility and the 4.x line requires a paid licence. Rejected.
  - `QuestPDF` — excellent API, but its Community licence is revenue-gated; a deployment could require a paid licence. Rejected.
  - `iText` — AGPL/commercial; rejected.
  - HTML-to-PDF via a browser — an external process/asset; rejected as disproportionate.
- **Consequence**: The font assets and their licence file are added to the repository; SkiaSharp and its native assets are restored at build only (no runtime egress).

## R5 — Spreadsheet shape is derived deterministically from the answer

- **Decision**: `DocumentTableBuilder` turns the persisted answer into one of two shapes, without an LLM:
  1. **Record shape** — when a section contains repeated `label: value` lines (or `label — value` / `label – value`), the union of labels becomes the header row and each repeated block becomes one data row. Missing labels stay empty; no value is inferred.
  2. **Section shape** — otherwise, the answer is split into titled sections and each non-empty line becomes a row in a two-column `Section | Detail` sheet. This is the FR-011 fallback for prose.
  - Dates and amounts that use a documented format are written as date/number cells with formats. Documented dates are `yyyy-MM-dd`, `dd/MM/yyyy`, `d/M/yyyy`, `dd-MM-yyyy`, `d.M.yyyy`, and Greek month-name dates; ambiguous numeric dates are interpreted day-first. Documented amounts/quantities are decimal numbers with `.` or `,` separators, currency amounts in `€`, `$` or `£`, percentages, and parenthesised negatives. Values outside these formats stay text and are never guessed.
- **Rationale**: FR-003 forbids a second generation, FR-008/009 require a real table with no invented values, and FR-010 wants typed dates/numbers. A deterministic parser can satisfy all three; a model-based extractor would be a second generation.
- **Alternatives considered**:
  - A second LLM "structure this as JSON" call: rejected — violates FR-003 and risks changed facts.
  - One column of prose: rejected — violates FR-008/011.
  - One row per citation: rejected — not the answer's content and not the requested subject.
- **Risk**: free-form prose may produce a section sheet rather than a record table. That is the specified fallback, not a failure; tests cover both shapes.

## R6 — Format detection is shared and conservative

- **Decision**: Add `DocumentRequestDetector` to `RAGGit.Core` (no package dependencies). It returns a `DocumentFormatDetection` (format, unsupported token, ambiguous flag): exactly one supported format **and** a document-creation intent word yields a format; no format yields nothing; a known unsupported token yields an unsupported result; more than one distinct supported format yields the ambiguous flag. Supported tokens: Word/docx, Excel/xlsx/spreadsheet, PDF, text/txt plus Greek equivalents. Intent words: create, make, write, generate, export, save, produce, put/list/table (for spreadsheets), and Greek equivalents.
- **Rationale**: FR-002 wants ordinary wording, FR-007 forbids producing a file when no format was named, and a naive token match would turn "what does the text say?" into a .txt file. The shared detector keeps client auto-production and the contract tests on one rule; the server still validates the requested enum independently.
- **Alternatives considered**:
  - Server-side inference only: adds a response field and makes the client's save menu logic diverge.
  - Broad keyword matching without intent: produces unasked files (violates FR-007).
  - Model-based intent detection: unnecessary model work for a deterministic decision.
- **Unsupported formats**: tokens such as PowerPoint/pptx/presentation/image/email are recognized as unsupported and the client shows the four supported formats while still answering (FR-024). They are not passed to the endpoint.

## R7 — Saving uses an OS dialog, not a temp-file launcher

- **Decision**: Add `IFileSaveService` in `RAGGit.Client.Core`, implemented in WPF with `Microsoft.Win32.SaveFileDialog` and a sibling-temp-file write followed by `File.Move(..., overwrite: true)`; the temp file is deleted on failure. It returns the chosen path (or null on cancel), and exposes an `OpenAsync(path)` for the "Open" action. The existing `ILauncherService` (temp + open) remains for library document downloads and is not reused for generated documents.
- **Rationale**: FR-012/013 require the employee's chosen location, overwrite confirmation and no partial file. The current launcher writes to a temp directory and opens it — useful for previewing library files, but it does not let the employee choose a location and would leave generated files behind. The temp-then-move write means a disk-full or cancellation failure never leaves a truncated target file.
- **Alternatives considered**:
  - Reuse `ILauncherService`: rejected — conflates preview with save and violates FR-013.
  - Save server-side: rejected — the workstation must not retain generated files (FR-014), and the employee's disk is the destination.

## R8 — Query identity is additive, with no schema change

- **Decision**: `SaveQueryAsync` returns the generated `Guid`; `QueryResponse.QueryId` and `QueryStreamEvent.QueryId` carry it. `QueryDetail.Id` already exists for history. No database schema change.
- **Rationale**: The document endpoint needs a stable, owned identifier for the answer, and the client needs it on both live and history messages. The `Queries` row already has the id; only the response shape changes. Keeping the pipeline pure (no persistence) preserves the 032 architecture.
- **Alternatives considered**:
  - Add a `QueryId` column: unnecessary; the id already exists.
  - Cache the answer server-side by a token: adds state and expiry for no benefit.
  - Re-query on save: violates FR-003.

## R9 — Language follows the question

- **Decision**: Add `TextScript.IsGreek` to `RAGGit.Core` (the same Greek Unicode-block check), and have `AnswerLanguage.IsGreek` delegate to it without changing behaviour. `RAGGit.Documents` uses `TextScript` to select Greek or English labels (`Sources`/`Πηγές`, `Title`/`Τίτλος`, `Page`/`Σελίδα`) and keeps the answer text verbatim. The answer itself already follows the question language.
- **Rationale**: FR-005 requires the document to match the question language; labels are the only new text the renderer introduces. A Core helper is usable by both Retrieval and Documents without making Documents depend on Retrieval.
- **Alternatives considered**: English-only labels — violates FR-005 for Greek requests; duplicating the script loop in Documents — avoidable duplication; referencing `RAGGit.Retrieval` from `RAGGit.Documents` — wrong dependency direction.

## R10 — Deterministic limits and safe failure

- **Decision**: Enforce `MaxDocumentBytes = 25 MiB` and `MaxTableRows = 5_000` in `RAGGit.Documents`. The endpoint returns `422` with a user-safe `{ "error": "document too large" }` (or `"no document available"` for a refusal/uncited answer). The renderer checks the cancellation token between sections/rows and never writes a temp file.
- **Rationale**: FR-022/FR-025 require a clear message and no partial file; a cap prevents memory/layout blow-ups on huge answers. 25 MiB is well above a normal report and below the 100 MiB upload cap.
- **Alternatives considered**: No cap — an accidental 50k-row sheet could exhaust the workstation; a smaller cap — would reject legitimate reports.

## R11 — Document eligibility and source integrity

- **Decision**: A document is eligible only when the persisted answer is not exactly `no relevant content found` and has at least one citation. The endpoint returns `422` otherwise; the client hides the save action. Every citation GUID present in the answer is mapped to a numbered source entry; the source list carries document name, ordinal, chunk id and excerpt.
- **Rationale**: FR-018 forbids a file for the refusal; FR-019 requires every factual statement to be supported by the listed sources. An answer with zero citations cannot provide that list. Numbered references keep the document readable while preserving the exact chunk ids in the appendix.
- **Alternatives considered**: Render uncited answers with a warning — weakens the product's trust promise and conflicts with FR-019; keep raw GUIDs in the body — readable but ugly; numbering with a full appendix is the better balance.

## R12 — Endpoint and ownership

- **Decision**: Add `POST /api/queries/{id:guid}/documents` with body `{ "format": "docx" | "xlsx" | "pdf" | "txt" }`. It is authorized for `Employee,Admin`, resolves the caller's `sub`, calls `GetDetailAsync(sub, id)`, and returns `404` for not found/not owned/legacy (never `403`, matching the history detail rule). A successful response is the file bytes with `Content-Disposition` (UTF-8 filename) and the format's content type.
- **Rationale**: The endpoint is a read-only projection of an owned query; it needs no new persistence, no LLM and no cross-user access. Reusing `GetDetailAsync` inherits the existing per-person isolation. Logging records the query id, format, byte count and duration only — never the answer text or citation excerpts.
- **Alternatives considered**:
  - `GET` with a format query string: workable, but the file is a generated artifact and `POST` communicates that better; either is acceptable. `POST` also keeps the format in a typed JSON body.
  - A separate `DocumentsController`: it would need the same repository and auth; keeping it in the query surface makes ownership explicit. A dedicated `QueryDocumentsController` with the same route is also acceptable; this plan uses a dedicated controller for size.

## R13 — Testing strategy

- **Decision**: Unit tests for detection, filenames, table shaping, citation mapping, each renderer (DOCX/XLSX/PDF/TXT) and ViewModel save lifecycle; contract tests for auth/ownership/format/content type/filename/422 and the additive `queryId`; integration tests that ask, generate, reopen the file with OpenXml/PdfPig, and assert Greek, typed cells and sources; client screen tests for the new XAML and automation IDs; CSharpier for C# and XAML.
- **Rationale**: Constitution VI requires TDD and the 033 success criteria include file validity, source support, cross-record isolation and offline operation. Reading the produced files back is the only way to prove FR-008/010/019.
- **Alternatives considered**: Snapshot-only tests — do not prove the file opens; manual-only checks — cannot enforce the trust criteria.

## Open clarifications

None. The design resolves every technical unknown. Two product-level assumptions are carried into the plan:

- From-scratch generation (no library sources) is out of scope for this plan, per the assessment decision.
- An answer with zero citations is not document-eligible; this follows FR-019 and is reflected in the endpoint and UI.

## Sources

- `specs/033-ask-document-generation/spec.md`
- `.specify/assessments/doc-generation/decision.md`
- `.specify/memory/constitution.md`
- `src/RAGGit.Retrieval/QueryPipeline.cs`, `GenerationService.cs`, `QueryOutcome.cs`
- `src/RAGGit.Workstation.Api/Controllers/QueryController.cs`
- `src/RAGGit.Core/Models/QueryRequest.cs`, `QueryHistoryDtos.cs`, `QueryStreamEvent.cs`
- `src/RAGGit.Client.Core/ViewModels/QueryViewModel.cs`, `Services/QueryApiClient.cs`, `Services/ILauncherService.cs`
- `src/RAGGit.Ingest/RAGGit.Ingest.csproj` (OpenXml 3.5.1, PdfPig 0.1.16)
- NuGet package search: `SkiaSharp` 4.156.1, `PDFsharp-MigraDoc` 6.5.0, `QuestPDF` 2026.9.2 (retrieved 2026-11-16)
