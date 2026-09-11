# XLSX Fixtures for 003-ingest-breadth

Deterministic `.xlsx` workbooks for integration + contract tests. No binary-only mystery — regenerate with `generate-xlsx-fixtures.py`.

## Files

- `sample-3sheet.xlsx` — 3 visible sheets (`Sheet1`, `Sheet2`, `Sheet3`), each header `Name | Dept | Refund Policy` + 10 data rows. `Sheet2!B5` = `refund policy: 30-day full refund with receipt`. `Sheet2!C13` is `=SUM(C2:C11)` with cached `<v>42.50</v>` (phone-home for SC-003). All 3 sheets visible.
- `sample-hidden.xlsx` — 2 visible (`VisibleA`, `VisibleB`) + 1 hidden (`HiddenC` with `hidden-token-xyz`). Hidden sheet must never enter index (SC-002).
- `sample-overcap.xlsx` — 1 sheet `S1` with 20 cols × 5500 rows = **110,000** `<c>` cells (>100k cap). Triggers `413 Spreadsheet exceeds 100,000 cell limit (found 110000 cells)`.
- `fake-xlsx-from-docx.xlsx` — ZIP with `PK` + `[Content_Types].xml` + `word/document.xml`, no `xl/workbook.xml`. Upload as `.xlsx` must return `400 content does not match type`.
- `corrupt.xlsx` — first 20 bytes of `sample-3sheet.xlsx` truncated after `PK` header (`InvalidDataException`) → 400, never 500.
- `empty-hidden-only.xlsx` — single sheet `HiddenOnly` patched to `state="hidden"` (only-visible-hidden edge → 400 `no extractable content`).

## Regeneration

```powershell
pip install openpyxl
python tests/integration/fixtures/xlsx/generate-xlsx-fixtures.py
```

Requires `openpyxl` only. All workbooks deterministic on every run (no timestamps that affect content). `generate-xlsx-fixtures.py` handles the `hidden-only` workaround (openpyxl forbids saving hidden-only, so the script patches `xl/workbook.xml` after save) and injects the cached formula value for `Sheet2!C13`.

## Usage in tests

```csharp
var path = Path.Combine("fixtures", "xlsx", "sample-3sheet.xlsx");
await using var fs = File.OpenRead(path);
var form = new MultipartFormDataContent { { new StreamContent(fs), "file", "sample-3sheet.xlsx" } };
form.Headers.Add("X-Api-Key", adminKey);
await client.PostAsync("/api/documents", form);
```

All six `.xlsx` files are registered as `Content` `CopyToOutputDirectory=PreserveNewest` in `tests/integration/RAGGit.Tests.Integration.csproj`.
