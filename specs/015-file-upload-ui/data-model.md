# Data Model: File Upload Queue

**Feature**: `015-file-upload-ui` | **Date**: 2026-09-18 | **Plan**: [plan.md](plan.md)

In-memory ViewModel state only. No persistence, no schema migration, no server change.

## UploadQueueItem

One entry per file added to the dialog queue.

| Field | Type | Meaning | Validation |
|-------|------|---------|------------|
| FileName | string | Display name | Non-empty; extension in allow-list at pick time |
| Stream | Stream | File content | Seekable preferred (rewound for retry); disposed on remove/replace |
| ContentType | string | MIME type from picker | Non-empty at upload time |
| SizeBytes | long | File size | ≤ `DocumentValidation.MaxFileSizeBytes` (100 MB) at pick time |
| Progress | double 0–1 | Per-file upload progress | Updated via `IProgress<double>` during that item's upload |
| State | enum | `Queued` / `Uploading` / `Succeeded` / `Failed` / `Cancelled` | See transitions |
| ErrorMessage | string? | Per-file failure reason | Set when entering `Failed`; shown inline in the row |
| StateLabel | string (derived) | Human-readable state | e.g. "Queued", "Uploading…", "Uploaded", "Failed", "Cancelled" |
| FileMetaText | string (derived) | "name · size" summary | e.g. "report.pdf · 2.4 MB" |

## Queue (on `UploadViewModel`)

| Field | Type | Meaning |
|-------|------|---------|
| Queue | `ObservableCollection<UploadQueueItem>` | The dialog's file list; drives the `ListView` |
| CompletedCount | int | Items in `Succeeded` state |
| TotalCount | int | `Queue.Count` |
| CompletedCountText | string (derived) | "2 of 4 done" overall indicator |
| HasFiles | bool (derived) | `Queue.Count > 0`; gates the start action |
| RejectionMessage | string? | Last pick-time block reason (names the file); clears on next valid pick |
| HasRejection | bool (derived) | Drives the warning `InfoBar` |
| StatusMessage | string? (existing) | Summary outcome line |
| StatusSeverity | string (existing shape) | `Informational` / `Success` / `Warning` / `Error` → `InfoBarSeverity` via `StatusSeverityConverter`; kept as string so Core stays UI-framework-free |
| IsAllSucceeded | bool | True when the queue is non-empty and every item is `Succeeded`; drives auto-close |
| IsUploading | bool (existing) | True while the sequential run is active; blocks duplicate starts |

## State transitions (per item)

```text
Queued → Uploading → Succeeded
                  → Failed (ErrorMessage set; run continues with next item)
                  → Cancelled (run continues with next item)
Any → (removed) on user Remove before start; Failed items keep their streams
      so retry rewinds and resends full content (never silent zero bytes)
```

Run-level: start requires `!IsUploading` plus at least one `Queued`/`Failed`/`Cancelled` item. A retry without re-picking rewinds and re-queues only `Failed`/`Cancelled` items (preserves the no-silent-zero-bytes guarantee); `Succeeded` items are left alone so a retry never creates duplicates. A press with nothing retryable is a no-op (empty queue keeps the "No file selected." message).

## Validation rules (from FR-002 / Session 2026-09-18)

- Extension allow-list: `.pdf`, `.docx`, `.xlsx`, `.txt`, `.md` (spec's "PDF, DOCX, TXT, XLSX" plus MD as already supported by ingestion).
- Size cap: `DocumentValidation.MaxFileSizeBytes` (100 MB, workstation-enforced value surfaced client-side).
- Blocked files never enter `Queue`; `RejectionMessage` names the file and the reason; start stays disabled until at least one valid entry is queued.
- Admin gating (`IsAdmin`) and session-expiry behavior unchanged.
