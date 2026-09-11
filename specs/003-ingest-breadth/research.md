# Research: Ingest Breadth (XLSX)

**Feature**: `003-ingest-breadth` | **Date**: 2026-09-11 | **Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Constitution**: v1.1.0

## Summary

Phase 0 resolves 7 decisions for adding `.xlsx` as the fifth ingest format without a new NuGet package (`DocumentFormat.OpenXml` already referenced). All decisions stay within constitution v1.1.0 (I single-tenant, II workstation-owned AI, IV offline invariant, VII MINOR `1.2.0` bump for additive mime enum). The tiling mirrors 002's hardening pattern (deep validation → no partial index) and adds a streaming extractor so 100k cells never load as a DOM.

## Decisions

### R1 — XLSX streaming extraction API (OpenXmlReader vs DOM)

**Decision**: Use `DocumentFormat.OpenXml.Packaging.SpreadsheetDocument.Open(stream, false)` for workbook/sheet enumeration (light DOM), then SAX `OpenXmlReader` over each `xl/worksheets/sheet*.xml` part for cell streaming; resolve `SharedStringTable` via indexed lookup (`SharedStringTablePart`). Keep `WorkbookProperties.Date1904` and `WorkbookPart.Workbook.Sheets` for sheet list. Never call `Worksheet.Descendants<Row>()` DOM on large sheets.

**Rationale**: `SheetData` DOM for 100k cells allocates ~100k `Cell` + `Row` objects (~80MB) and can OOM on wide rows; SAX `OpenXmlReader` is O(1) memory and already in `DocumentFormat.OpenXml`. Workbooks are `.zip` OPC, so streaming reads benefit from `ZipArchive` leaveOpen.

**Alternatives**:
- Full DOM `SpreadsheetDocument.WorkbookPart.WorksheetParts.SelectMany(wp => wp.Worksheet.Descendants<Row>())` — rejected: O(n) allocations, tested to exceed LOH on 100k cells.
- Third-party `ExcelDataReader` / `ClosedXML` — rejected: would add a new NuGet package, violating Assumptions ("no new NuGet — DocumentFormat.OpenXml already referenced").

**Open verification**:
- Exact `OpenXmlReader` construction: `OpenXmlReader.Create(worksheetPart)` vs `new OpenXmlReader(worksheetPart)` in `DocumentFormat.OpenXml` 3.x — requires compile probe.
- `SharedStringTable` access: `WorkbookPart.SharedStringTablePart.SharedStringTable` may be null (workbook with only inlineStr) — handle null fallback.
- `leaveOpen` interplay with `SpreadsheetDocument.Open`: `SpreadsheetDocument` takes ownership of stream; `ZipArchive` validation probe must use `leaveOpen:true` and reset `Position=0` after probe when `CanSeek`.
- Whether `OpenXmlReader` exposes `Cell.DataType` and `Cell.CellValue` without loading `SheetData` DOM — to be confirmed with small fixture commit.

---

### R2 — Deep PK / ZIP validation (xlsx vs docx disambiguation)

**Decision**: After MIME map and size guard, validate that the byte stream is a genuine `xlsx` before extraction by opening `new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true)` and requiring **both** `[Content_Types].xml` and `xl/workbook.xml` entries (accept `xl/_rels/workbook.xml.rels` as fallback signal). Missing entries or `InvalidDataException` / `XmlException` on parse → `400 "content does not match type"` (or `400 "corrupted xlsx"` on truncation) with no `Documents`/`Chunks` rows and no LanceDB upsert (same rollback as 002 corrupted pdf/docx). `leaveOpen:true` + `stream.Position = 0` when seekable restores the stream for the extractor.

**Rationale**: `xlsx` and `docx` share `PK` zip magic (`0x50 0x4B 0x03 0x04`); `MimeMap` by extension/content-type alone would accept a renamed docx as xlsx and later emit nonsense. Deep inspection mirrors 002's PDF magic hardening (FR-006). `[Content_Types].xml` distinguishes zip-at-all vs non-zip, `xl/workbook.xml` distinguishes xlsx vs docx (`word/document.xml` for docx).

**Alternatives**:
- Magic-byte `PK` prefix only — rejected: does not distinguish xlsx vs docx.
- Opening `SpreadsheetDocument.Open` and catching `OpenXmlPackageException` as sole validator — rejected: coarser error mapping and conflates 400 type-mismatch vs corrupted; ZipArchive probe gives precise entry-level messages.
- Full OOXML content-type parsing (`application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` in `[Content_Types].xml`) — deferred: entry existence is sufficient for 003; full override parse could be added if false positive observed.

**Open verification**:
- `ZipArchive` with `leaveOpen:true` requires `stream.CanSeek` to reset; non-seekable `IFormFile` stream must be buffered to `MemoryStream` first — confirm `DocumentsController` already copies to `MemoryStream` (as in 002) so seek is guaranteed.
- Truncated zip (`PK` header then EOF) throws `InvalidDataException` — confirm it maps to 400 not 500.
- Whether `xl/workbook.xml` is always present for Strict vs Transitional xlsx — some generators emit `xl/workbook.xml` vs `xl/workbook.xml.rels` variant; include fallback to `xl/workbook.xml` OR `xl/worksheets/sheet1.xml` presence as second check if needed.

---

### R3 — Sheet visibility filtering (hidden vs veryHidden)

**Decision**: Enumerate `WorkbookPart.Workbook.Sheets.Elements<Sheet>()` and for each `Sheet` resolve its `WorksheetPart` via `Sheet.Id → WorkbookPart.GetPartById(Sheet.Id)`. Read `Sheet.State` (`EnumValue<SheetStateValues>`): `null` or `Visible` → include; `Hidden` or `VeryHidden` → skip. Skip also sheets where `WorksheetPart.Worksheet.SheetProperties` indicates `Hidden` (belt-and-braces). Empty-sheet (no non-empty rows) produces no text and does not count as extractable content; hidden-only content never enters extractor nor cell count.

**Rationale**: FR-002 ("all visible, hidden skipped") and SC-002 (0 hits for hidden-only token). Excel persists `state="hidden"` or `state="veryHidden"` on `<sheet>`; inline detection via `SheetPart` alone misses the `veryHidden` variant.

**Alternatives**:
- Skip by `Sheet.Name` prefix (`Hidden*`) — rejected: name is user-controlled, not a signal.
- Skip only `Hidden`, keep `VeryHidden` — rejected: `VeryHidden` is the stronger hide (VBA-only unhide) and must also be skipped.

**Open verification**:
- Exact API: `Sheet.State?.Value == SheetStateValues.Hidden` vs `Sheet.State?.HasValue` — requires compile probe against `DocumentFormat.OpenXml.Spreadsheet.SheetStateValues`.
- Whether `SpreadsheetDocument` exposes hidden via `SheetState` on `<sheet>` or via `Worksheet.SheetProperties.TabColor` etc. — sheet-level `State` is authoritative per ECMA-376.
- Confirm that a hidden-sheet-only workbook correctly returns 400 "no extractable content" after filtering (no visible rows emitted).

---

### R4 — Cell value & cached formula value resolution

**Decision**: For each `Cell` read `Cell.CellValue?.Text` / `Cell.InnerText` (cached `v`) and `Cell.DataType?.Value`:
- `DataType == SharedString` → `SharedStringTable.ElementAt(int.Parse(v)).InnerText` (concatenate `Text` elements for rich shared strings).
- `DataType == InlineString` → `Cell.InlineString.InnerText`.
- `CellFormula` present → **ignore formula text**, emit cached `v` (or resolved shared string if `v` is SST index per `DataType`).
- Otherwise raw `v` (numbers, booleans use `v` as-is).
- Row text is `string.Join(" | ", cellTexts)` with cells ordered by `CellReference` column (e.g., `A2`, `C2` leaves gap as empty). Empty rows (all cells empty/whitespace) skipped.

**Rationale**: FR-003/FR-004 + SC-003: cached value is what Excel displays; formula text (`=SUM(...)`) is not searchable and would pollute retrieval. Shared strings are the dominant text storage in xlsx; inlineStr occurs in programmatically generated sheets.

**Alternatives**:
- Emit `CellFormula.Text` when `CellFormula` exists — rejected: violates FR-004, leaks `=SUM` into index.
- Use `Cell.InnerText` directly without `DataType` dispatch — rejected: would emit SST index (`"12"`) rather than string `"refund policy"` for SharedString cells.

**Open verification**:
- Formula cells that store `DataType == SharedString` — some Excel versions emit SST index as cached `v` even with a formula; verify dispatch order is `formula? use v with DataType dispatch : DataType dispatch`.
- Rich shared strings (multiple `<t>` under `<si>`) need concatenation — verify `InnerText` flattens correctly or explicit `si.Elements<Text>().Select(t=>t.Text)`.
- `CellReference` parsing for column order and sparse rows — confirm gaps are needed for header alignment or if compact join (skip empty cells) is acceptable per FR-003 (`col1 | col2 | col3` compact); spec suggests compact join, so gaps render as empty strings skipped rather than `""`.

---

### R5 — Date system & custom number formats (1900 vs 1904, NumberFormatId)

**Decision**: Read `WorkbookProperties.Date1904?.Value` (default false = 1900 system; true = 1904 system with 1462-day offset). For each cell with `StyleIndex != null`, resolve `CellFormats[StyleIndex] → NumberFormatId`, then `NumberingFormats` custom format strings. Apply best-effort date detection: `NumberFormatId` in built-in date ranges (14-22, 27-36, 45-47, 50-58) or custom `formatCode` containing `y`/`m`/`d`/`h` → treat `double.Parse(v, CultureInfo.InvariantCulture)` as OADate via `DateTime.FromOADate(v + (is1904 ? 1462 : 0))` adjusted for Excel's 1900 leap-year bug (`>=60` offset). Format with `yyyy-MM-dd` or respect `formatCode`'s short form; on parse failure or missing style table, fallback to raw cached `v`.

**Rationale**: Assumptions say "date-formatted cells: best-effort apply the cell's number format; fallback is raw cached value". Without style lookup, `44561` (2022-01-01) would be indexed as `"44561"` and never match a query for `"2022-01-01"`.

**Alternatives**:
- Always index raw OADate double — rejected: fails SC date expectation and FR-003 best-effort.
- Strict formatCode parsing (tokenize `[$-409]yyyy\-mm\-dd`) — deferred: best-effort heuristic is sufficient for 003; strict parser reserved for follow-up.
- Require `WorkbookStylesPart` presence — fallback: if `StylesPart` null, raw `v` is emitted (no crash).

**Open verification**:
- Exact `WorkbookPart.WorkbookProperties.Date1904` property path vs `WorkbookPart.Workbook.WorkbookProperties.Date1904` — requires SDK probe.
- Custom `NumberFormat` ids >=164 vs built-in <164 — confirm custom table lookup.
- 1904 offset constant (1462 vs 1461) and leap-year bug correction — verify against `DateTime.FromOADate` docs (bug retained for compatibility).
- Locale in custom formatCode — fallback to invariant when `yyyy/mm/dd` vs `dd/mm/yyyy` ambiguous.

---

### R6 — Cell-cap guard placement & HTTP status mapping (413 vs 400 ordering)

**Decision**: Count cells during streaming: increment for each non-empty cell or cell with `CellValue` across **visible sheets only** (hidden sheets excluded, empty cells not counted or counted as empty? Spec says "100k cells across visible sheets" — interpret as total addressable cells = `Dimension` or counted non-empty? Take **total cells in visited rows × columns per visible sheet's `Dimension`** is overcount; instead count **visited cells with a `Cell` element** (including empty `v` but present `<c>` tag) during SAX scan; wide sparse rows still hit cap). If count `> 100_000` during scan, abort and return `413 Payload Too Large` with JSON `{ error: "Spreadsheet exceeds 100,000 cell limit (found {actual} cells) — 100,000 is the per-document cap." }` and delete any staged `Document`/`Chunks` and skip LanceDB upsert. Ordering: `413` size `>100MB` checked first (already in `DocumentsController`), then deep-validation `400 "content does not match type"` for non-xlsx `PK`, then cell-cap `413`, then `400 "no extractable content"` for zero-row visible output.

**Rationale**: FR-005 (scale guard for FR-011 5k docs / 1M chunks) is orthogonal to file-size 413; guard preempts tokenization and embed of 100k+ cells (≈200k tokens) that would bloat LanceDB. 400 for type mismatch must precede 413 so a 200k-cell renamed docx is diagnosed as wrong type, not over-cap.

**Alternatives**:
- 400 for over-cap — rejected: spec FR-005 explicitly says 413 + actionable message naming the 100k cap.
- Post-chunk token-count guard — rejected: too late; embedding already queued.
- Count only non-empty cells — considered but spec says "cells across visible sheets" suggests include empty-addressed cells; chosen interpretation counts `<c>` elements (physical cells) which is the natural SAX count and prevents wide-sheet bypass (one row × 20k `<c>` tags = 20k toward cap).

**Open verification**:
- `ZipArchive` `leaveOpen:true` + `stream.Position=0` after validation probe must not consume cap-counting stream copy — ensure `MemoryStream` duplication or single reset.
- Exact 413 response body schema for contract tests (must match existing 413 shape in `api.yaml` — add extended description, keep same JSON `error` property).
- Atomic rollback: `DocumentsController` must `await db.Database.BeginTransactionAsync()` around `Documents.Add` + `Chunks.AddRange` + `LanceDbLocalClient.UpsertAsync` and rollback on 400/413 — same pattern as 002 corrupted-pdf.
- Whether to surface actual count vs rounded — spec SC-004 expects actionable message naming both cap and actual count.

---

### R7 — Row-wise text representation & chunk integration

**Decision**: For each visible sheet, build a string:
```
[Sheet: <name>]
h1 | h2 | h3          ← header row (first non-empty row)
h1 | h2 | h3          ← header repeat
val11 | val12 | val13 ← data row 1 with header repeated before it? Actually spec says header-row repeated before each data row, so serializer emits: sheet line, then for each data row emit header line + data line; but to avoid doubling header tokens per chunk, emit header line once then before each data row emit header again (so each row on screen is header + row). Simpler wire: [Sheet], header, then for each data row: header-line + row-line (two lines).
Merged cells: value lives on anchor cell only (OpenXml emits `<mergeCells>` but only anchor `<c>` has `v`), so other cells in merge are empty → compact join handles.
Drawings/charts/pivots (`drawing`, `chart`, `pivotTable`) ignored — not part of `SheetData`.
Empty rows: skip (`Where(rowCells.All(c=>string.IsNullOrWhiteSpace(c)))`).
Feed resulting text `string.Join(Environment.NewLine, lines)` into existing `Chunker.ChunkText(512,50)` — no Chunk schema change; sheet name is literal text inside chunk.
```

**Rationale**: FR-003 row-wise with header-repeat makes any 512-token window self-describing for the retriever; retriever topK=5 without sheet metadata still gets headers. Embedding sheet name as `[Sheet: name]` literal avoids new LanceDB column.

**Alternatives**:
- One JSON doc per row with sheet/name columns — rejected: would require Chunk schema change and structured citation.
- Emit header only once at top — rejected: would make downstream chunks header-less, violating FR-003 and hurting retrieval for later rows.
- Collapse sheet-per-chunk boundary — deferred: current approach is one concatenated document (all sheets) chunked together; per-sheet chunking considered but rejected as it complicates ordinal ordering — ordinal is global per Document as today.

**Open verification**:
- Whether `[Sheet: name]` should be `\n`-delimited vs ` | `-joined — choose newline (clear chunk boundary).
- Exact header-repeat newline count to keep token counting stable — verify `Environment.NewLine` vs `\n` doesn't affect whitespace tokenization (`Split([' ', '\t', '\n', '\r'])` already handles both).
- `ChunkText` whitespace tokenization on `|` — `|` is kept with tokens (`"val1 | val2"` → tokens include `"|"` as token separator? Split only on whitespace keeps `"|"` as token, which is fine; no change needed).
- Whether to deduplicate header when data row length < header length (ragged rows) — emit `h1 | h2 | h3` full header then `val1 | val2` ragged data (header pads trailing empties).

## Alternatives Considered (cross-cutting)

- Auto-reject vs auto-truncate over-cap workbooks — truncate rejected (violates no-partial-index invariant; FR-005 mandates full rejection).
- New extractor NuGet (`ClosedXML`, `NPOI`) — rejected (violates "no new NuGet" assumption; `DocumentFormat.OpenXml` already referenced suffices).
- New `Sheet` entity / LanceDB `sheet` column — rejected (spec Key Entities: sheet is not a stored entity, name embedded in chunk text).

## References

- `src/RAGGit.Ingest/Chunker.cs:18-32` current `ExtractTextAsync` switch (Pdf/Docx/Txt/Md) — to add `Xlsx` branch
- `src/RAGGit.Core/Models/Document.cs:9-15` `DocumentMimeType` enum (to add `Xlsx`) and `DocumentValidation.MaxFileSizeBytes`
- `src/RAGGit.Core/Models/DocumentMimeTypeConverter.cs:13-23` JSON MIME mappings (to add xlsx content-type)
- `src/RAGGit.Workstation.Api/Controllers/DocumentsController.cs` mime map + size/validation guards + transaction pattern (to clone for xlsx)
- `specs/002-real-bringup/research.md:R1-R7` guard and offline patterns reused
- `.specify/memory/constitution.md v1.1.0` principles IV, V, VII
- ECMA-376 Office Open XML — SpreadsheetML Sheet state, Shared Strings, Date system

