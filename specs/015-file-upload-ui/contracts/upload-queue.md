# Contract: Upload Queue Surface

**Feature**: `015-file-upload-ui` | **Date**: 2026-09-18 | **Plan**: [plan.md](../plan.md)

No HTTP contract change: upload still `POST /api/documents` (multipart, Admin-only) under contract `1.4.0`. This contract documents the **added/changed client surface** only.

Namespace note: shared types keep the 006 convention (`RAGGit.Client.Maui.*`).

---

## ViewModel surface (`UploadViewModel`)

```csharp
namespace RAGGit.Client.Maui.ViewModels;

public sealed class UploadQueueItem : ObservableObject
{
    // string FileName { get; }
    // Stream Stream { get; }
    // string ContentType { get; }
    // long SizeBytes { get; }
    // double Progress { get; set; }          // 0–1, per-file bar
    // UploadQueueItemState State { get; set; } // Queued/Uploading/Succeeded/Failed/Cancelled
    // string? ErrorMessage { get; set; }     // inline row error when Failed
    // string StateLabel { get; }             // derived display text
    // string FileMetaText { get; }           // "name · size" derived text
}

public sealed partial class UploadViewModel : ObservableObject
{
    // ... existing members preserved (PickFileCommand now appends one queue
    // entry; SelectedFileName/HasFile/UploadProgress/StatusMessage/IsAdmin/
    // IsUploadEnabled/IsBusy/CancelUploadCommand unchanged in behavior) ...

    // New queue surface bound by UploadDialog:
    // ObservableCollection<UploadQueueItem> Queue { get; }
    // bool HasFiles { get; }                 // Queue.Count > 0
    // int TotalCount { get; }                // Queue.Count
    // int CompletedCount { get; }            // items Succeeded
    // string CompletedCountText { get; }     // "2 of 4 done"
    // string? RejectionMessage { get; }      // pick-time block reason, names file
    // bool HasRejection { get; }             // drives warning InfoBar
    // string StatusSeverity { get; }         // Informational/Success/Warning/Error
    // bool IsAllSucceeded { get; }           // drives auto-close
    // string SupportedTypesText { get; }     // "PDF, DOCX, XLSX, TXT, MD supported · up to 100 MB."
    // IRelayCommand RemoveFileCommand { get; } // per-row remove (before start)
}
```

**Rules**:
- `UploadCommand.CanExecute` = `!IsUploading && Queue.Count > 0` (FR-002).
- Pick-time validation (extension allow-list + `DocumentValidation.MaxFileSizeBytes`) runs before any queue insert; blocked files set `RejectionMessage` and never enter `Queue` (FR-002).
- Sequential run; a per-file failure/cancel marks that item and continues (FR-013); `IsAllSucceeded` is true only when the queue is non-empty and all items are `Succeeded` (FR-006).
- Legacy single-file surface preserved so all 9 pre-existing `UploadViewModelTests` pass unchanged.

---

## Dialog (`RAGGit.Client.WinUI.Views.UploadDialog : ContentDialog`)

```text
UploadDialog(ContentDialog)
├── BindingContext: UploadViewModel (DI, same resolution as before)
├── Add-files Button (SymbolIcon Add, AutomationId AddFilesButton)
│     → PickFileCommand per pick (click-to-select only; NO drag-drop target)
├── Supported-types caption ← SupportedTypesText
├── Queue ListView (virtualized, MaxHeight=260, never in a ScrollViewer)
│   └── Row: file icon + FileMetaText + StateLabel + per-file ProgressBar
│       + inline error (StatusErrorBrush) + Remove Button (AutomationId per row)
├── Summary InfoBar ← StatusMessage + StatusSeverity→InfoBarSeverity
│     (StatusSeverityConverter), LiveSetting Polite
├── Warning InfoBar ← RejectionMessage (visible when HasRejection), Polite
├── Upload Button ← UploadCommand (SymbolIcon Upload, AutomationId UploadButton)
├── Cancel Button ← CancelUploadCommand (visible when IsUploading)
├── Overall count ← CompletedCountText ("2 of 4 done")
├── Behavior: focus Add-files on open; Closing while IsUploading cancels;
│   IsAllSucceeded → ~900 ms perceivable success → auto-close;
│   failure/cancel stays open with per-file outcomes
└── Accessibility: logical tab order, visible focus, Esc, open/progress/
    outcome announcements, focus returns to the Library Upload button
```

**Rules**: click-to-select only (no drop target); `{ThemeResource}` brushes only; ≥44px targets; no `ScrollViewer`-wrapped collection.

---

## Presenter (`LibraryPage`)

```text
Header Upload Button (admin-gated): opens UploadDialog over the list
On dialog close: reload documents so new rows appear;
    on partial success only the succeeded documents appear (FR-013)
```

**Rules**: single entry point preserved (013); non-admins see no entry; 401 mid-upload follows `SessionExpiryNavigator` unchanged.
