# Research: Allowed Upload Types

**Feature**: `018-allowed-upload-types` | **Date**: 2026-09-18
**Method**: Codebase inspection (no external research needed — all unknowns resolved from repo evidence)

## R-01 — Single source of truth for the allow-list

- **Decision**: Add `AllowedExtensions` ( `{ ".pdf", ".docx", ".xlsx", ".txt" }` ) plus a display label to `DocumentValidation` in `src/RAGGit.Core/Models/Document.cs`; `UploadViewModel` and `DocumentsController.TryMapMime` consume it.
- **Rationale**: Constitution III (library-first, no duplicated logic). Both sides already depend on `RAGGit.Core` (`UploadViewModel` reads `DocumentValidation.MaxFileSizeBytes`; controller reads it for request-size limits), so the constant is reachable with zero new references. One edit changes the list everywhere.
- **Alternatives considered**: (a) Leave two parallel lists (client array + server switch) — rejected: they already drifted conceptually and a future type addition would need two coordinated edits. (b) Config file / appsettings allow-list — rejected: over-engineering for a fixed 4-entry product decision; spec fixes the set.

## R-02 — Keep `DocumentMimeType.Md` for stored documents; gate only new uploads

- **Decision**: `DocumentMimeType` enum keeps `Md`; `DocumentMimeTypeConverter`, `SqliteDocumentRepository` (`"text/markdown" => Md`), `Document.GetContentType()`, `Chunker` (`Txt or Md → plain text`), and `DocumentFormatValidator` (`Txt or Md` magic branch) are UNCHANGED. Only `DocumentsController.TryMapMime` stops mapping `text/markdown` / `.md` (returns false → existing 400 path).
- **Rationale**: FR-008 — existing `.md` rows must remain loadable, searchable, and citable. Removing the enum member would break deserialization of stored rows and fail `DocumentMimeTypeXlsxTests` (asserts `values[4] == Md`), `DocumentFormatValidatorTests` (`Md` inline data), and `DocumentDisplayTests` (`Md → "md"`).
- **Alternatives considered**: (a) Remove `Md` everywhere + data migration — rejected: violates FR-008 and Simplicity (VII); bulk re-validation is explicitly out of scope. (b) Keep server accepting `.md` — rejected: contradicts the exclusive allow-list (FR-001/FR-005).

## R-03 — Client changes are text + filter only

- **Decision**: In `UploadViewModel`: delete `".md"` from `AllowedExtensions` (or replace array with the shared constant), update `SupportedTypesText` to `"PDF, DOCX, XLSX, TXT supported · up to 100 MB."`, update the rejection string to `"Choose PDF, DOCX, XLSX, or TXT files."`, remove the `".md" => "text/markdown"` arm of `ContentTypeFor` (unreachable once blocked — keeping it would suggest `.md` is routable). In `WinUIFilePicker.CreatePicker`: remove `FileTypeFilter.Add(".md")`, keep `"*"` fallback (defensive: `AddPickedFilesAsync` still validates). `UploadDialog.xaml` unchanged (binds `SupportedTypesText`).
- **Rationale**: Smallest diff that satisfies FR-002/FR-003/FR-004; drop path flows through the same `AddPickedFilesAsync` seam so one change covers picker + drag-and-drop.
- **Alternatives considered**: Removing `"*"` from the picker filter — rejected: OS filter is advisory only; server + VM validation remain authoritative, and `"*"` preserves the existing tolerant-pick-then-explain behavior for edge cases (double extensions, extensionless files).

## R-04 — Server rejection shape and message

- **Decision**: `TryMapMime` returns false for `text/markdown` / `.md`; the controller's existing unsupported-type branch returns 400 with a message naming the file and listing supported types (same pattern as the over-cap/corrupt 400s from `002`/`003`). `Document.Filename`/`Mime` model messages updated only where they name the allowed set (`"Allowed: pdf, docx, xlsx, txt, md."` → `"Allowed: pdf, docx, xlsx, txt."`).
- **Rationale**: Reuses the established 400 hardening pattern; no new status code, endpoint, or OpenAPI shape — behavior restriction only.
- **Alternatives considered**: 415 Unsupported Media Type — rejected: inconsistent with the codebase's existing 400-for-type-problems convention (`002` corrupt → 400, `003` mismatch → 400).

## R-05 — Legacy `.doc` guidance

- **Decision**: `.doc` falls out of the extension check naturally; the generic unsupported-type message covers it. No special `.doc → save as .docx` hint string in code beyond what the spec's plain-language message already requires (message lists the 4 supported types so the remedy is self-evident).
- **Rationale**: Avoids one-off per-extension messaging branches; keeps the rejection catalog to three reasons (unsupported type / content mismatch / empty) per the data model.
- **Alternatives considered**: Dedicated `.doc` hint — rejected: special-casing invites N more (`ppt`, `xls`…); the supported-type list in the message is the guidance.

## R-06 — Test impact

- **Decision**: Existing tests asserting structure (mime order, validator `Md` data, display `Md`) stay green untouched. `UploadViewModelTests` gains: accept-each-of-4, reject `.md`/`.doc`/`.png`/`.zip`/`.pptx` via picker + drop seam, mixed-batch partial accept, uppercase-extension accept, message-contents assertions. Contract suite gains: `POST /api/documents` with `.md` bytes → 400 naming the file.
- **Rationale**: Constitution VI — Red-Green-Refactor; behavior change lands with failing-first tests at both seams (VM unit + server contract).
- **Alternatives considered**: None — standard gate coverage.

## Resolved Unknowns

No `NEEDS CLARIFICATION` items remain. All Technical Context fields were filled from constitution + direct code inspection (`UploadViewModel.cs:137-151/404-412/743-752`, `WinUIFilePicker.cs:51-56`, `DocumentsController.cs:246-296`, `Document.cs:9-16/99-103`).
