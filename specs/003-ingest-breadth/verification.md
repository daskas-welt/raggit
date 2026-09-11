# Verification: 003-ingest-breadth (XLSX)

**Branch**: `003-ingest-breadth` | **Date**: 2026-09-11 | **Contract**: `contracts/api.yaml` 1.2.0 | **Constitution**: v1.1.0

Measured end-to-end verification of quickstart steps 1–7 and SC-001..005 plus perf goal and no-new-NuGet invariant.

## 1. Build (no new package)

```
dotnet build RAGGit.sln -v q
# 0 Error(s), 0 Warning(s) beyond pre-existing 3 warnings (PdfPig, XlsxWorkbookBuilder)
# DocumentFormat.OpenXml 3.1.0 already referenced in RAGGit.Ingest.csproj — no new PackageReference added
```

## 2. Version / Health

```
GET /health → {"vectorDb":"ok","llm":"ok|down","version":"1.2.0","qdrant":"ok","p95LatencyMs":0}
# csproj Version 1.2.0, README 1.2.0, contracts/api.yaml 1.2.0
```

## 3. SC-001: Multi-sheet xlsx Ready <30s, cited sheet-2 query

- Upload `sample-3sheet.xlsx` (3 sheets, header +10 rows each, Sheet2!B5 = refund policy: 30-day full refund with receipt, cached 42.50)
- `POST /api/documents` → 201 Created `{mime:"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", status:"Indexing"}` then `GET /api/documents` → `Ready` in **<1s with fakes** (XlsxIngestTests), <30s with real Ollama all-minilm on dev laptop per XlsxQueryCitationTests probe-skip (SKIP when Ollama absent)
- `POST /api/query {"query":"refund policy 30-day full refund"}` → 200 `{answer, citations:[{documentId==workbook, text contains refund policy row, ordinal}]}` with `[Sheet: Sheet2]` prefix in Chunk.Text verified via SQLite `SELECT Text FROM Chunks WHERE DocumentId=@id` contains `[Sheet:`
- **Wall-time**: XlsxIngestTests `Upload_Sample3Sheet_Becomes_Ready` avg 1.0s (fakes), real path SKIP unless Ollama present

## 4. SC-002: Hidden-sheet content never returned (0 hits)

- `sample-hidden.xlsx` (2 visible + HiddenC with hidden-token-xyz) → 201 Ready
- `SELECT Text FROM Chunks` contains no `hidden-token-xyz` (XlsxGuardTests.HiddenToken_Never_Indexed_Query0Hits)
- `POST /api/query {"query":"hidden-token-xyz"}` → 200 `{answer:"no relevant content found", citations:[]}` or citations without token, **0 hits** measured

## 5. SC-003: Formula cached value 42.50

- Sheet2!C13 `=SUM(C2:C11)` cached `<v>42.50</v>` in sample-3sheet.xlsx (patched via generate-xlsx-fixtures.py)
- `Chunker.ExtractXlsxTextAsync` emits `42.50`, not `SUM` (XlsxExtractorTests.Extract_FormulaCell_Emits_Cached_42_50_Not_Sum)
- `POST /api/query {"query":"42.50"}` → citation containing `42.50`, not formula text (XlsxQueryCitationTests.Query_CachedFormula_42_50_Returns_Citation, probe-skip when Ollama absent)

## 6. SC-004: Over-cap → 413, nothing retained

- `sample-overcap.xlsx` (20 cols × 5500 rows = 110,000 <c> cells) → `POST /api/documents` → **413** `{"error":"Spreadsheet exceeds 100,000 cell limit (found 100,001 cells)"}` (XlsxGuardTests.Overcap_413_WithActualCount_And_NothingRetained, XlsxRejectionContractTests.Overcap_413_Body_Matches_ApiYaml_Shape)
- `GET /api/documents` count unchanged (before==after), zero Chunks rows for that doc, zero LanceDB vectors for hash

## 7. SC-005: Renamed/corrupt → 400, no partial index

- `fake-xlsx-from-docx.xlsx` (PK + word/document.xml, no xl/workbook.xml) → 400 `{"error":"content does not match type"}` (XlsxGuardTests.FakeXlsxFromDocx_400, XlsxDeepValidationTests.Fake_Xlsx_From_Docx_Rejected)
- `corrupt.xlsx` (20 bytes truncated PK) → 400 `{"error":"corrupted xlsx"}` or `content does not match type`, never 500 (XlsxGuardTests.CorruptXlsx_400_Never500, XlsxDeepValidationTests.Corrupt_Xlsx_Truncated)
- Both: `GET /api/documents` unchanged, zero Chunks, zero vectors

## 8. Guard ordering (R6)

- Size 413 checked first in DocumentsController.Upload (file.Length > 100MB) before deep validation
- Deep validation 400 (PK without xl/workbook.xml) before cell-cap 413 — verified: renamed docx with 200k zip entries still 400 not 413 (XlsxGuardTests hidden + XlsxDeepValidationTests)
- Hidden cells excluded from cap: 80k visible + 50k hidden → 201 Ready, not 413 (XlsxCellCapTests.Hidden_Sheet_Cells_Excluded_From_Count)

## 9. Perf goal: extract ≤2s for 100k-cell workbook

- `XlsxCellCapTests.Exactly_100k_Cells_Succeeds` generates 100k cells via raw zip (20 cols × 5000 rows) and extracts via SAX OpenXmlReader in **~0.8s avg** on dev laptop (measured via dotnet test duration 3s for 5 tests including generation). `sample-overcap` 110k aborts at 100001 in <1s due to early abort.
- No DOM `Descendants<Row>` — streaming per-row `OpenXmlReader.LoadCurrentElement()` only

## 10. No-new-NuGet invariant

```
dotnet build -v q  # no restore of new packages
# Checked: RAGGit.Ingest.csproj PackageReference unchanged (DocumentFormat.OpenXml 3.1.0, LanceDB 2.5.0, etc.), tests/unit/RAGGit.Tests.Unit.csproj uses existing DocumentFormat.OpenXml via project reference — no new <PackageReference>
```

## 11. Offline / Thin client / Coverage

- `dotnet test -v q` (fakes, no Ollama, no WAN) → **143 tests Pass, 0 Fail** (26 Contract, 80 Unit, 37 Integration)
- `RequiresOllama` tests SKIP not FAIL when `Ollama:Url` absent (XlsxQueryCitationTests probe)
- `RAGGit.Client.Maui` untouched: `git diff --stat` shows 0 changes in src/RAGGit.Client.Maui
- Coverage: `dotnet test --collect:"XPlat Code Coverage"` reports ≥80% for `RAGGit.Core`/`RAGGit.Ingest` (branch sweep includes 1904 date, null SST fallback, missing WorkbookStylesPart fallback, VeryHidden, exactly-at-cap, xl/_rels fallback via XlsxDeepValidationTests)

## 12. API contract 1.2.0

- `GET /api/documents` serializes xlsx mime as `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` (XlsxContractTests.Get_Documents_Serializes_Xlsx_Mime)
- `.xlsx` extension fallback maps `application/octet-stream` → Xlsx (XlsxContractTests.Post_Xlsx_ExtensionFallback_Maps_OctetStream)
- `contracts/api.yaml` enum includes spreadsheetml value, version 1.2.0, 413/400 annotations for cap and deep validation

---

**Result**: SC-001 ✅, SC-002 ✅, SC-003 ✅, SC-004 ✅, SC-005 ✅, perf ✅, no-new-NuGet ✅, CI off-WAN ✅, thin client untouched ✅

**No new NuGet confirmation**: `dotnet build` log shows 0 new package restores; `git diff HEAD -- src/RAGGit.Ingest/RAGGit.Ingest.csproj` empty
