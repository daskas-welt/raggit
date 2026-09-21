# Data Model: Allowed Upload Types

**Feature**: `018-allowed-upload-types` | **Date**: 2026-09-18

## Summary

No persisted schema change. The feature narrows the **transient intake gate** (which extensions may enter the upload queue / `POST /api/documents`) from 5 types to 4. Stored `Document` rows — including existing `Md` rows — are untouched.

## Persisted Entities (UNCHANGED)

### Document (`RAGGit.Core/Models/Document.cs`)

Unchanged fields: `Id`, `Filename`, `Mime`, `Size`, `Hash`, `Status`, `CreatedBy`, `CreatedByName`, `CreatedAt`.

- `DocumentMimeType` enum **keeps** `Md` (`Pdf, Docx, Xlsx, Txt, Md`) — required so existing rows deserialize via `DocumentMimeTypeConverter` and `SqliteDocumentRepository`. Read path (`GetContentType()`, `Chunker` plain-text branch, `DocumentFormatValidator` UTF-8 branch, `DocumentDisplay`) unchanged.
- Only the *intake* mapping (`DocumentsController.TryMapMime`) stops producing `Md` for new uploads.

### Chunk

Unchanged. Existing `.md` chunks remain queryable and citable (FR-008).

## New / Changed Configuration

### `DocumentValidation` (`RAGGit.Core/Models/Document.cs`)

| Member | Type | Value / Rule |
|--------|------|--------------|
| `AllowedExtensions` (new) | `IReadOnlySet<string>` (case-insensitive) | `{ ".pdf", ".docx", ".xlsx", ".txt" }` — single source of truth for client + server |
| `SupportedTypesLabel` (new) | `string` | `"PDF, DOCX, XLSX, TXT"` — feeds `SupportedTypesText` and rejection messages |
| `MaxFileSizeBytes` | `const long` | Unchanged (`100 MB`) |
| `MaxSpreadsheetCells` | `const int` | Unchanged (`100_000`) |

## Transient Entities (intake only)

### Upload candidate (existing shape, narrowed verdict)

Produced by `IFilePicker.PickMultipleAsync` (picker) and the drop handler (`StorageFile` → `PickedFile`), validated in `UploadViewModel.AddPickedFilesAsync`:

| Field | Rule (after change) |
|-------|---------------------|
| `FileName` | Non-blank (unchanged) |
| Extension (`Path.GetExtension`, case-insensitive) | MUST be in `DocumentValidation.AllowedExtensions`; `.md`, `.doc`, and all others rejected |
| `SizeBytes` | ≤ `MaxFileSizeBytes` (unchanged; evaluated only after type passes) |
| Content sniff | Unchanged (`DocumentFormatValidator`; mismatch → "content does not match type", no document retained) |

### Validation outcome (existing `RejectionMessage` surface, updated catalog)

| Verdict | Message (must name the file) |
|---------|------------------------------|
| Accepted | Queued; `"{N} file(s) ready. Press Upload to start."` |
| Unsupported type | `"'{file}' isn't supported. Choose PDF, DOCX, XLSX, TXT files."` (from `SupportedTypesLabel`) |
| Content mismatch | `"content does not match type"` + file name (existing pattern, unchanged) |
| Empty | `"no extractable content"` style + file name (existing pattern, unchanged) |

## Validation Rules

1. Extension comparison is case-insensitive (`.DOCX` ≡ `.docx`).
2. Type check precedes size check (a disallowed type reports type, never size).
3. Picker and drop paths share `AddPickedFilesAsync` — identical verdicts by construction (FR-004).
4. Server re-validates authoritatively in `TryMapMime` — client bypass still yields 400 (FR-005).
5. Zero valid files after rejections → start action stays disabled (unchanged gating).

## State Transitions

None. Queue item lifecycle (`Queued → Uploading → Succeeded/Failed/Cancelled`) and document lifecycle (`Indexing → Ready|Failed`) are unchanged.
