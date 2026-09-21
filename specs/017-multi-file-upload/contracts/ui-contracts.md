# UI Contracts: Multi-File Upload (Picker + Drag-and-Drop)

**Feature**: `017-multi-file-upload` | **Date**: 2026-09-18 | **Plan**: [plan.md](../plan.md)

No server API, wire-contract, or RBAC change. `POST /api/documents` (multipart, Admin-only, contract 1.4.0) is reused as-is. The contracts below are the client-side seams between the shared ViewModel (`RAGGit.Client.Core`), the platform picker/drop mechanics (`RAGGit.Client.WinUI`), and the dialog view.

## C-01 — `IFilePicker.PickMultipleAsync` (additive)

```csharp
// RAGGit.Client.Core/Services/IFilePicker.cs (namespace RAGGit.Client.Maui.Services)
Task<IReadOnlyList<PickedFile>> PickMultipleAsync();  // default impl: one PickAsync() call
Task<PickedFile?> PickAsync();                        // unchanged, still abstract
```

- Returns an empty list (never null) when the user cancels.
- `PickedFile` shape unchanged (`FileName`, `Stream`, `ContentType`).
- The default `PickMultipleAsync` delegates to a single `PickAsync()` so existing fakes keep compiling untouched; `DummyFilePicker` overrides with an empty list.
- `WinUIFilePicker` implements via `FileOpenPicker.PickMultipleFilesAsync()` with the existing filter list and `OwnerHwnd` init; `PickAsync` keeps `PickSingleFileAsync`.

## C-02 — `UploadViewModel.AddPickedFilesAsync` (new intake seam)

```csharp
Task AddPickedFilesAsync(IEnumerable<PickedFile> files, CancellationToken ct = default);
```

- Applies the FR-003 validation loop per file; queues valid files as `UploadQueueItem`s; aggregates one rejection message naming every rejected file (valid files still queued).
- Called by `PickFileCommand` (after `PickMultipleAsync`) and by the view drop handler (after StorageItems conversion).
- No-ops on empty input. Never throws for per-file validation failures (rejections are messages, not exceptions).

## C-03 — Queue-lock + drop-state bindings

| ViewModel member | Type | View binding / enforcement |
|------------------|------|----------------------------|
| `IsDropEnabled` (derived `!IsUploading`) | bool | Guarded in `DropArea_DragOver` (decline) and `DropArea_Drop` (ignore); `Border` exposes no `IsEnabled`, so the lock is behavioral + hint text |
| `IsDragOver` | bool | Drop-area highlight overlay `Visibility` |
| `DropHintText` | string | Hint text under the drop area (locked vs idle wording) |
| `PickFileCommand.CanExecute` | — | `!IsUploading`; `ExecuteAsync` additionally no-ops while uploading |

## C-04 — View drop mechanics (`UploadDialog`, code-behind only)

- `DragOver`: accept (`AcceptedOperation = Copy`, raise `IsDragOver`) only when `e.DataView` contains `StandardDataFormats.StorageItems`; otherwise decline.
- `Drop`: `GetStorageItemsAsync()` → `StorageFile` items become `PickedFile`s via `OpenStreamForReadAsync()`; `StorageFolder`/other formats become named rejections (forwarded through `AddPickedFilesAsync` as invalid entries, no resolution).
- No other element accepts drops; drops with the dialog closed change nothing.
