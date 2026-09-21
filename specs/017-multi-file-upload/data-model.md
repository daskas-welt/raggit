# Data Model: Multi-File Upload (Picker + Drag-and-Drop)

**Feature**: `017-multi-file-upload` | **Date**: 2026-09-18 | **Plan**: [plan.md](plan.md)

No persisted entities change. All entities below are transient (one dialog open) and live in `RAGGit.Client.Core`; the server data model (`Document`, `Chunk`, `Query`, `User`) is untouched.

## Entities

### PickedFile (existing, reused unchanged)

- One user-chosen document: `FileName`, `Stream`, `ContentType`.
- Produced by `IFilePicker.PickAsync` / `PickMultipleAsync` (picker path) and by the view's drop conversion (drop path: `StorageFile.Name` → `FileName`, `OpenStreamForReadAsync()` → `Stream`, `StorageFile.ContentType` → `ContentType`).
- Validation (applied identically on both paths in `AddPickedFilesAsync`): non-blank name → extension in allow-list (`.pdf/.docx/.xlsx/.txt/.md`) → seekable length ≤ 100 MB (`DocumentValidation.MaxFileSizeBytes`).

### UploadQueueItem (existing, unchanged)

- `FileName`, `Stream`, `ContentType`, `SizeBytes`, `Progress`, `State` (Queued/Uploading/Succeeded/Failed/Cancelled), `ErrorMessage`, `ResultStatus`.
- No shape change; multi-file queue semantics from 015 reused (sequential upload, per-file progress, retry of Failed/Cancelled only).

### Drop intake (new transient state on `UploadViewModel`)

- `IsDragOver` (bool): true while an accepted drag hovers the target; drives the highlight affordance.
- `IsDropEnabled` (bool, derived = `!IsUploading`): false locks the target during upload per clarification; drops are declined/ignored and the hint explains the lock.
- `DropHintText` (string): contextual hint — idle ("Drag files here or use Add files"), locked ("Uploading — add more files after this run finishes").
- Rejected drop entries (folders, virtual items, invalid files) never become entities; each surfaces as a named entry in the aggregated rejection message.

## Relationships

```text
picker pass / drop ──> PickedFile(s) ──validate──> UploadQueueItem(s) ──> Queue
                                              └─reject──> RejectionMessage (named per file)
```

## Validation rules (both paths, FR-003)

1. Blank/whitespace name → reject ("choose a named document").
2. Extension not in allow-list → reject (names the file + supported list).
3. Size > 100 MB → reject (names the file + actual size).
4. Non-file drop content (folder, shortcut, virtual item) → reject with the standard invalid-file message; no resolution attempted (clarified).
5. Start requires ≥ 1 valid queued entry; queue locked (`IsUploading`) for adds/removes until the run completes (clarified); no batch cap (clarified).

## State transitions

- Queue entries: existing 015 lifecycle unchanged (Queued → Uploading → Succeeded/Failed/Cancelled; Failed/Cancelled retryable).
- Drop target: Idle → DragOver (highlight) → Idle; Idle → Locked while `IsUploading` → Idle. No transition opens the dialog implicitly or modifies the queue.
