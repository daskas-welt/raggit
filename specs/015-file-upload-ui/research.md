# Research: File Upload UI Refresh

**Feature**: `015-file-upload-ui` | **Date**: 2026-09-18 | **Plan**: [plan.md](plan.md)

All Technical Context items were known (no NEEDS CLARIFICATION remained after `/speckit.clarify`). This file records the decisions taken and the alternatives rejected, grounded in the repo's existing Fluent patterns and the `winapp find-ui` skill guidance.

## R-01 — Queue state lives in `UploadViewModel`, not the view

- **Decision**: Extend the shared `UploadViewModel` (`RAGGit.Client.Core`, namespace `RAGGit.Client.Maui.ViewModels` kept per 006 convention) with an `ObservableCollection<UploadQueueItem>` plus gating/count/severity state; the XAML view only binds.
- **Rationale**: Constitution III (library-first, client reuse) and 008 precedent (sheet reused `UploadViewModel` with zero duplicated logic). Keeps the state unit-testable without a window.
- **Alternatives considered**: View-local queue in code-behind (rejected — untestable, duplicates gating), per-file child ViewModels (rejected — overkill for a small-N sequential queue).

## R-02 — Sequential uploads, not parallel

- **Decision**: Upload queued files one at a time on the single `UploadCommand` execution; a failure/cancel marks that item and continues with the rest.
- **Rationale**: Flat memory profile, deterministic per-file progress, trivial cancellation (one CTS at a time), matches the workstation's single-document ingest model.
- **Alternatives considered**: Parallel fan-out (rejected — progress attribution complexity, memory spikes on large files, no server requirement for it).

## R-03 — Pick-time rejection via repeated `IFilePicker` picks

- **Decision**: No `IFilePicker` interface change. Each pick is validated (extension allow-list `.pdf/.docx/.xlsx/.txt/.md` + `DocumentValidation.MaxFileSizeBytes` 100 MB) before entering the queue; rejections surface as `RejectionMessage` naming the file. Multi-file in one dialog open = repeated picks.
- **Rationale**: Spec clarification (block at pick with immediate message) with zero interface churn; existing picker tests and fakes keep working.
- **Alternatives considered**: Multi-select picker API (rejected — WinUI `FileOpenPicker` multi-select + new fake surface for no spec'd benefit), post-start per-file failure for bad types (rejected — contradicts the pick-time-blocking clarification).

## R-04 — Status via `InfoBar`, per-row inline errors

- **Decision**: One summary `InfoBar` (severity bound through `StatusSeverityConverter` to a UI-framework-free `StatusSeverity` string) + one warning `InfoBar` for rejections + per-row inline error text in `StatusErrorBrush`. Both InfoBars use `LiveSetting="Polite"` for screen-reader announcements.
- **Rationale**: Matches Login/Query pages (the repo's established status pattern); resolves the 011 audit gap where upload used a raw `TextBlock`.
- **Alternatives considered**: `TeachingTip` (rejected — transient, wrong for persistent outcomes), keeping `TextBlock` (rejected — fails the Fluent-consistency goal).

## R-05 — Auto-close timing (~900 ms perceivable success)

- **Decision**: Code-behind watches `IsAllSucceeded` → shows the success `InfoBar` for ~900 ms → auto-closes; any failure/cancel stays open. `Closing` while `IsUploading` cancels the in-flight request.
- **Rationale**: Balances the auto-close clarification with perceivability (screen-reader users hear the outcome before dismiss) and the 008 rule that dismissal cancels in-flight work.
- **Alternatives considered**: Immediate close (rejected — success never perceivable), no auto-close (rejected — contradicts the Session 2026-09-18 decision).

## R-06 — No new project, no new dependency, no HTTP change

- **Decision**: All work in `RAGGit.Client.Core` (ViewModel) + `RAGGit.Client.WinUI` (XAML, one converter). `POST /api/documents` multipart Admin-only under contract `1.4.0` unchanged; legacy single-file ViewModel surface (`SelectedFileName`, `HasFile`, `UploadProgress`, status strings) preserved.
- **Rationale**: Keeps Constitution VII (simplicity) and the 013/008 single-entry-point rules intact; all 9 pre-existing upload tests pass unchanged.
- **Alternatives considered**: Dedicated queue service project (rejected — 4th-project violation with no justification), new upload endpoint (rejected — unrequested server scope).

## Reference grounding

- In-repo: `LoginPage.xaml` / `QueryPage.xaml` (`InfoBar` pattern), `LibraryPage.xaml` (`SymbolIcon` row actions, card brushes), `App.xaml` theme dictionaries, `MainWindow.xaml.cs` Mica backdrop.
- Skill: winui-dev + winui-design guidance (native controls, theme resources, 44px targets, keyboard/screen-reader behavior). `winapp find-ui` scenario lookup to be run at implement time if the CLI is available; otherwise the in-repo patterns above are the authority.
