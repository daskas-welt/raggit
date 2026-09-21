# Contracts: Upload Allow-List

**Feature**: `018-allowed-upload-types` | **Date**: 2026-09-18

No new endpoint and no OpenAPI shape change. This document records the behavior contracts both seams must honor.

## C-01 — Client ViewModel surface (`UploadViewModel`)

```csharp
// string SupportedTypesText { get; }  // "PDF, DOCX, XLSX, TXT supported · up to 100 MB."
// Task AddPickedFilesAsync(IEnumerable<PickedFile> files, CancellationToken ct = default)
```

- `SupportedTypesText` lists exactly the 4 supported types before selection (FR-002).
- `AddPickedFilesAsync` blocks unsupported extensions with an immediate per-file message: `"'{file}' isn't supported. Choose PDF, DOCX, XLSX, TXT files."` (built from `DocumentValidation.SupportedTypesLabel`, the single source of truth). Blocked files never enter `Queue` (FR-003).
- Identical validation for picker and drop intake — both flow through `AddPickedFilesAsync` (FR-004).
- Start action enablement unchanged: disabled until ≥ 1 valid queued entry.

## C-02 — Picker filter (`WinUIFilePicker.CreatePicker`)

- `FileTypeFilter` contains `.pdf`, `.docx`, `.xlsx`, `.txt` (plus the existing `"*"` advisory fallback). `.md` is removed.
- Filter is advisory only; `AddPickedFilesAsync` remains the enforcing seam (defense for `*`-picked or dropped files).

## C-03 — Server upload gate (`POST /api/documents`)

- `TryMapMime` maps only: `application/pdf → Pdf`, `application/vnd.openxmlformats-officedocument.wordprocessingml.document → Docx`, `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet → Xlsx`, `text/plain → Txt` (plus `.pdf/.docx/.xlsx/.txt` extension fallback).
- `text/markdown` content-type and `.md` extension return `false` → existing unsupported-type branch → **400** with a plain-language body naming the file and listing the supported types (FR-005).
- Mismatched content (allowed extension, wrong bytes) → **400** `"content does not match type"`, no document retained (FR-006).
- Oversize / cell-cap / empty guards unchanged (FR-007).
- `GET /api/documents`, query, and delete behavior for pre-existing `.md` documents unchanged (FR-008).

## Message catalog (normative wording)

| Situation | Message pattern |
|-----------|-----------------|
| Client unsupported type | `'{file}' isn't supported. Choose PDF, DOCX, XLSX, TXT files.` |
| Dialog hint | `PDF, DOCX, XLSX, TXT supported · up to 100 MB.` |
| Server unsupported type | 400 naming the file + supported types (same pattern as existing 400s) |
| Server content mismatch | 400 `content does not match type` + file name |
