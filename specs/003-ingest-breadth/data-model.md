# Data Model: Ingest Breadth (XLSX)

**Feature**: `003-ingest-breadth` | **Date**: 2026-09-11 | **Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Delta on**: [002 data-model.md](../002-real-bringup/data-model.md) (itself delta on [001](../001-offline-mode/data-model.md))

## Overview

This is a delta on `001-offline-mode/data-model.md` as amended by `002-real-bringup/data-model.md`. No data-model version bump and **no SQLite schema change**. The xlsx addition is an ingest-time text representation plus a MIME enum extension and a per-document cell-cap constant; storage (`Documents`, `Chunks`, `Queries`, `Library` 1 row) and LanceDB collection `library` are unchanged. `Sheet` is explicitly not a stored entity.

## Entities — changes from 001/002

### Library (Singleton) — unchanged

One company library per deployment. Not a multi-tenant table — one row (`id=1`) or implicit. No `CompanyId`. No schema change from 002.

### Document — MIME enum extended, validation extended

Same fields: `Id` (Guid PK), `Filename`, `Mime`, `Size` (reject >100MB), `Hash` (SHA-256 hex unique), `Status` (`Indexing` → `Ready`|`Failed`), `CreatedBy`, `CreatedAt`. Relationship `Library 1:N Document 1:N Chunk`. SQLite table `Documents`.

**Delta in this feature**:

- `DocumentMimeType` enum gains `Xlsx`:
  ```csharp
  public enum DocumentMimeType { Pdf, Docx, Xlsx, Txt, Md }
  ```
  `DocumentMimeTypeExtensions.GetContentType()` maps `Xlsx` → `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` (the OOXML SpreadsheetML content-type). JSON converter `DocumentMimeTypeConverter` handles the same string symmetrically; `EnumDataType` validation on `Document.Mime` allows it for `POST /api/documents`.

- `DocumentValidation` gains per-document spreadsheet guard orthogonal to file size:
  ```csharp
  public static class DocumentValidation
  {
      public const long MaxFileSizeBytes = 100L * 1024 * 1024; // 100 MB — unchanged
      public const int MaxSpreadsheetCells = 100_000;           // xlsx-only, visible sheets
  }
  ```
  Enforcement is in `RAGGit.Ingest.Chunker.ExtractXlsxTextAsync` / `DocumentsController` before chunk/embed: stream → count visible-sheet `<c>` cells during SAX scan → if `> MaxSpreadsheetCells` → `413` + rollback, no Document/Chunks/vectors retained. `100k` is the spec FR-005 guard; naming both cap and actual count in the 413 message is required.

- **Empty / hidden-only guard**: If SAX scan yields zero non-empty rows across visible sheets (empty workbook or only hidden sheets or all-blank rows) → `400 { error: "no extractable content" }` with no Document retained (same no-partial-index invariant as 002 corrupted pdf/docx). This is **400**, not 413, even when `Dimension` would suggest empty ≠ over-cap.

- **No status change**: Document lifecycle remains `Indexing → Ready|Failed`; over-cap and invalid-type rejections never create a Document row, so no new status.

### Chunker / Extractor — streaming XLSX path (no Chunk change)

Existing `Chunker.ExtractTextAsync(Stream, DocumentMimeType)` gains an `Xlsx` branch:

```csharp
DocumentMimeType.Xlsx => await ExtractXlsxTextAsync(stream) // streaming
```

**`ExtractXlsxTextAsync` behavior** (pure function, no DB):

- Opens `SpreadsheetDocument.Open(stream, false)` for sheet enumeration and `SharedStringTable`/`WorkbookProperties(Date1904)` read, then SAX `OpenXmlReader` per visible `WorksheetPart` `SheetData`.
- Skips hidden sheets: `Sheet.State == Hidden|VeryHidden` (null = Visible).
- For each visible sheet, finds first non-empty row as **header row**; emits `[Sheet: <name>]` line, then header line `col1 | col2 | col3`, then for each subsequent non-empty data row emits header line again + `val1 | val2 | val3` row line (FR-003 header-repeat). Empty rows skipped.
- Per cell: cached/displayed value only — `CellFormula` ignored; `SharedString` via `SharedStringTable` index, `InlineString` via `InlineString` element; `StyleIndex → NumberFormatId → Date1904` best-effort date formatting via `DateTime.FromOADate`, fallback raw `v`.
- Joins cell texts per row with ` | `; sparse rows compact (no empty gaps emitted beyond missing trailing cols); merged cells naturally anchor-only (only anchor `<c>` has `v`).
- Ignores embedded `drawing`/`chart`/`pivotTable`/`image` parts.
- Counts cells toward `MaxSpreadsheetCells` during scan; returns `string` (concatenated lines with `Environment.NewLine`) for the caller to `ChunkText`.

**Chunk** is unchanged: `Id` (Guid PK = LanceDB row `id`), `DocumentId` (FK indexed), `Ordinal` (0-based unique per Document), `Text` (512 tokens max, 50 overlap, now contains spreadsheet row text for xlsx docs), `TokenCount`. SQLite table `Chunks`. Token counting remains whitespace-split approximation; `|` is kept as token (`Split` only on whitespace).

### Chunk — unchanged

`Id`, `DocumentId`, `Ordinal`, `Text`, `TokenCount` — same as 001/002. No new column for sheet name; sheet name is textual prefix `[Sheet: name]` inside `Text`.

### Embedding (in LanceDB) — unchanged

Vector for a Chunk; owned by LanceDB table `library` with HNSW `m=16, efConstruction=128`, cosine, scalar filter on `documentId`. Same as 002 (dimension guard 384|768 enforced there; xlsx does not change it).

- `Id` (Guid = `Chunk.Id`)
- `Vector` (float[], enforced dim 384 or 768)
- `Columns`: `{ documentId, text, ordinal }`
- `ModelName` (metadata string, e.g., `all-minilm:384` or `nomic-embed-text:768`)

Xlsx chunks are vectors like any other format; no sheet column.

### Query — unchanged

`Id`, `UserId`, `Prompt`, `RetrievedChunkIds` (Guid[] JSON, topK=5), `Answer` (nullable), `CitationIds` (Guid[] subset), `LatencyMs`, `CreatedAt`. SQLite table `Queries`. Citations for xlsx chunks carry `documentId`/`chunkId`/`text`/`ordinal` like every other format (Constitution V / FR-007).

### ClientSession — unchanged (client-memory only)

Held by the thin MAUI client to gate UI; not persisted. No xlsx-specific field; discovered via `GET /api/auth/me` as in 002.

### Sheet — not a stored entity (explicit)

Per spec Key Entities: `Sheet` is **not** persisted. Sheet name is embedded textually as `[Sheet: <name>]` inside each chunk's `Text`. No `Sheets` table, no FK. Retrieval relies on text match; citation shows sheet context via that prefix.

## Relationships Overview — unchanged

```text
Library (1) ──< Document (N) ──< Chunk (N) ── Embedding (1:1 via Chunk.Id in LanceDB library)
                                     │
                                     └─> Query.RetrievedChunkIds (references Chunks)
Query.CitationIds ⊆ RetrievedChunkIds
ClientSession (memory) ──(GET /api/auth/me)──> Role (gates Document upload/delete)
Sheet — not stored; name inside Chunk.Text as "[Sheet: name]"
```

## State Transitions — unchanged

- `Document.Status`: `Indexing` → `Ready` | `Failed` (Failed remains for retrieval error message). Over-cap / deep-validation / empty rejections never create a Document, so no `Failed` row for them — they are HTTP 400/413 at upload time.
- `DELETE /api/documents/{id}` removes `Document` + `Chunk` rows + LanceDB rows `filter: documentId==id` (same for xlsx docs).

## Validation — delta

- `Filename` not empty, `Mime` in allowed list **now includes `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`**, `Size ≤100MB`, `Hash` unique, `Prompt` not empty, `RetrievedChunkIds` length ≤5 — same as 001/002 with mime extension.
- **New**: `MaxSpreadsheetCells = 100_000` across visible sheets; if exceeded → `413` (not 400) with message naming cap and actual count; atomic rollback, no partial index.
- **New**: Deep PK validation for xlsx: `ZipArchive` must contain `[Content_Types].xml` + `xl/workbook.xml`; otherwise `400 "content does not match type"`; corruption/truncation also `400`; no partial index.
- **New**: Empty/hidden-only workbook (zero non-empty rows after filtering) → `400 "no extractable content"`; also no partial index.
- **Reused**: `VectorDb:VectorSize` ∈ {384, 768} enforced as in 002 (orthogonal).
- **Reused**: Corrupted pdf/docx → 400 with rollback as in 002.

## Storage Mapping — unchanged except constants

- SQLite `rag.db`: `Documents`, `Chunks`, `Queries`, `Library` (1 row) — no migration, no new column.
- LanceDB file `data/lancedb` (VectorDb:Path) table `library`: row `id=Chunk.Id`, `vector`, columns `{ documentId, text, ordinal }` — xlsx chunks stored identically. Sheet name lives inside `text`.
- No new file or table for sheets or cell counts; counts are transient during upload.

