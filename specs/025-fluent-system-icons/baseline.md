# Baseline: Fluent System Icons Across the Client (T001/T002)

**Feature**: `025-fluent-system-icons` | **Date**: 2026-09-30 | **Plan**: [plan.md](plan.md)

The pre-change inventory the icon pass is measured against (SC-001 baseline). Recorded before any edit.

## Gates (T001, 2026-09-30)

| Check | Result |
|-------|--------|
| `dotnet tool restore` | ✅ csharpier 1.3.0 restored |
| `dotnet csharpier check .` | ✅ clean (242 files) |
| `dotnet build RAGGit.sln -c Release -p:Platform=x64` | ✅ 0 errors, 5 pre-existing nullable warnings (`RAGGit.Ingest`, tests — not the client) |

## Action controls lacking a glyph (the SC-001 baseline)

| Page / component | Control | Target glyph |
|------------------|---------|--------------|
| `Views/Pages/LoginPage.xaml` | Sign in | `ArrowEnterLeft24` |
| `Views/Pages/QueryDetailPage.xaml` | Ask again | `ArrowRepeatAll24` |
| `Views/Pages/QueryDetailPage.xaml` | Back | `ArrowLeft24` |
| `Views/Pages/QueryDetailPage.xaml` | Copy prompt / answer / citation (×3) | `Copy24` |
| `Views/Pages/LibraryPage.xaml` | Retry | `ArrowClockwise24` |
| `Views/Pages/HistoryPage.xaml` | Refresh | `ArrowClockwise24` |
| `Views/Pages/HistoryPage.xaml` | View (per row) | `Eye24` |
| `Views/Pages/HistoryPage.xaml` | Ask again (per row) | `ArrowRepeatAll24` |
| `Views/Pages/HistoryPage.xaml` | Load more | `ArrowDown24` |
| `Views/Pages/DocumentsMinePage.xaml` | Refresh | `ArrowClockwise24` |
| `Views/Pages/DocumentsMinePage.xaml` | Load more | `ArrowDown24` |
| `Views/Pages/AdminUsersPage.xaml` | Refresh | `ArrowClockwise24` |
| `Views/Pages/AdminUsersPage.xaml` | role (per row) | `PersonEdit24` |
| `Views/Pages/AdminUsersPage.xaml` | active toggle (per row) | `Checkmark24` / `Dismiss24` (state, US2) |
| `Views/Pages/AdminUsersPage.xaml` | Create user | `PersonAdd24` |
| `Views/Pages/AdminUsersPage.xaml` | Reset password | `KeyReset24` |
| `Views/Pages/SettingsPage.xaml` | Sign out | `SignOut24` |
| `Views/Dialogs/UploadDialog.xaml` | Upload / Cancel / Close | `ArrowUpload24` / `Dismiss24` / `Dismiss24` |
| `Components/ChatControl.xaml` | "Sources" disclosure | `TextQuote24` |

## State surfaces reading by words or colour alone

| Surface | States | Target glyph |
|---------|--------|--------------|
| `AdminUsersPage.xaml` active toggle | active / inactive | `Dismiss24` / `Checkmark24` |
| `AdminUsersPage.xaml` lock cell | locked / not locked | `LockClosed24` / `LockOpen24` |
| `AdminUsersPage.xaml` status bar | Informational / Success / Warning / Error | `Info24` / `CheckmarkCircle24` / `Warning24` / `ErrorCircle24` |
| `SettingsPage.xaml` connection value | reachable / unavailable | `PlugConnected24` / `PlugDisconnected24` |
| `LibraryPage` / `HistoryPage` / `DocumentsMinePage` / `QueryDetailPage` footer error text | error | `ErrorCircle24` |

## Already glyph-bearing — no change (verify only)

Navigation (`MainWindowViewModel`), Dashboard tiles/header actions, Library header Refresh/Upload, Library
row Download/Delete, Chat Send/Copy, Upload dialog drop zone/Browse/Remove/empty state, document status
chip (`Components/StatusChip.xaml`), empty states (`EmptyStateGlyph`), pager (`PaginationFooterControl`),
Upload/Query InfoBars (severity glyph supplied by the control).

## Explicitly out of scope (FR-009, clarified 2026-09-30)

The Ask page's repeated "did you mean?" person suggestion chips stay text-only; table column headers,
numeric page buttons, the pager ellipsis and free-text fields take no icon.
