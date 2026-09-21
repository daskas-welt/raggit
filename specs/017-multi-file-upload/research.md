# Research: Multi-File Upload (Picker + Drag-and-Drop)

**Feature**: `017-multi-file-upload` | **Date**: 2026-09-18 | **Plan**: [plan.md](plan.md)

All Technical Context items were known (no NEEDS CLARIFICATION remained after `/speckit.clarify`). This file records the decisions taken and the alternatives rejected, grounded in the existing `UploadViewModel` queue design (015) and WinUI 3 platform APIs.

## R-01 — Additive `IFilePicker.PickMultipleAsync`, keep `PickAsync`

- **Decision**: Extend `IFilePicker` (`RAGGit.Client.Core`, namespace `RAGGit.Client.Maui.Services` kept per 006/015 convention) with `Task<IReadOnlyList<PickedFile>> PickMultipleAsync()`, reusing the existing `PickedFile` shape (FileName/Stream/ContentType). Keep `PickAsync()` with a default interface implementation delegating to `PickMultipleAsync().FirstOrDefault()` so existing callers and fakes keep compiling.
- **Rationale**: Multi-select in one pass is spec P1; additive change means the 9+ existing picker tests/fakes keep working and `DummyFilePicker` needs only a trivial override (empty list).
- **Alternatives considered**: Replace `PickAsync` outright (rejected — breaks existing fakes/tests for no benefit), new `IMultiFilePicker` interface (rejected — second abstraction over the same capability; DI would need two registrations).

## R-02 — `WinUIFilePicker` uses `PickMultipleFilesAsync`, same filter list

- **Decision**: Implement `PickMultipleAsync` with `FileOpenPicker.PickMultipleFilesAsync()`, reusing the current filter list (`.pdf/.docx/.xlsx/.txt/.md` + `*`) and `OwnerHwnd` init; open each returned `StorageFile` via `OpenStreamForReadAsync()` into `PickedFile`s. Cancelled/empty pick returns an empty list (never null).
- **Rationale**: One-pass multi-select is the OS-native path; same filters keep picker-level and VM-level validation consistent.
- **Alternatives considered**: Repeated `PickSingleFileAsync` calls (rejected — N dialog opens defeats the feature's purpose), filtering to supported extensions only at picker level (rejected — keep `*` so VM-level rejection messages name the file, per 015 precedent).

## R-03 — Single VM intake entry `AddPickedFilesAsync` shared by picker and drop

- **Decision**: Add `UploadViewModel.AddPickedFilesAsync(IEnumerable<PickedFile>)` containing the full validation loop (name → extension allow-list → 100 MB cap), per-file queueing, and aggregated rejection reporting (each rejected file named; valid files still queued). `PickFileCommand` becomes pick-then-delegate; the view's drop handler converts `StorageItems` to `PickedFile`s and calls the same method.
- **Rationale**: Guarantees FR-003 (identical validation on both paths) with one testable seam; the existing `PickFileAsync` body logic moves almost verbatim into the loop.
- **Alternatives considered**: Separate drop-validation path in code-behind (rejected — duplication, untestable), validation in the view (rejected — Constitution III, framework-free Core).

## R-04 — Drop mechanics live in `UploadDialog` code-behind, state in the VM

- **Decision**: View owns `AllowDrop` + `DragOver` (accept only when `DataView` contains `StorageItems`, else decline; set `AcceptedOperation = Copy` and raise VM `IsDragOver`) + `Drop` (read `GetStorageItemsAsync`, keep `StorageFile` items, open read streams, forward as `PickedFile`s; `StorageFolder`/non-file formats forwarded as named rejections without resolution). VM owns `IsDragOver`, `IsDropEnabled` (= `!IsUploading`), and the "add more after this run" hint.
- **Rationale**: Only the view can touch `DataPackageView`/`StorageFile`; everything decidable (validation, state, messaging) stays in the VM where unit tests reach it. Matches 015's view-binds/VM-decides split.
- **Alternatives considered**: Full drop handling in VM via abstraction (rejected — over-abstraction for one dialog; the conversion shim is ~15 lines), drop onto the whole dialog including buttons (rejected — drop target is a dedicated area per spec FR-002/FR-004).

## R-05 — Queue lock during upload via existing gating + drop binding

- **Decision**: `PickFileCommand` `CanExecute` gains `&& !IsUploading` (plus an `ExecuteAsync` no-op guard), and the drop handlers decline/ignore while `IsUploading` (WinUI `Border` exposes no `IsEnabled`; the lock is behavioral plus the clarified hint text). `RemoveFileCommand` already blocks during upload (`CanRemove`); unchanged.
- **Rationale**: Direct implementation of the Session 2026-09-18 clarification with existing primitives; declined drags show the OS not-allowed cursor and the hint explains the lock.
- **Alternatives considered**: Allow queue edits mid-upload for not-yet-started items (rejected — contradicts the clarification), separate "locked" visual state (rejected — disabled controls + hint already communicate it).

## R-06 — No new project, dependency, endpoint, or contract change

- **Decision**: All work in `RAGGit.Client.Core` (picker interface + ViewModel) + `RAGGit.Client.WinUI` (picker impl + dialog XAML/code-behind + theme resources). `POST /api/documents` multipart Admin-only unchanged; `UploadQueueItem` shape unchanged; no new NuGet packages.
- **Rationale**: Constitution VII (simplicity) and the 013/008 single-entry-point rules intact; all 9 pre-existing upload tests pass unchanged.
- **Alternatives considered**: Dedicated queue service project (rejected — 4th-project violation with no justification), new upload endpoint (rejected — unrequested server scope).

## Reference grounding

- In-repo: `LoginPage.xaml` / `QueryPage.xaml` (`InfoBar` pattern), `LibraryPage.xaml` (`SymbolIcon` row actions, card brushes), `App.xaml` theme dictionaries, `MainWindow.xaml.cs` Mica backdrop.
- Skill: winui-dev + winui-design guidance (native controls, theme resources, 44px targets, keyboard/screen-reader behavior). `winapp find-ui` scenario lookup to be run at implement time if the CLI is available; otherwise the in-repo patterns above are the authority.
