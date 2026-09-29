# Implementation Plan: WPF-UI Modernization

**Branch**: `021-wpfui-modernization` | **Date**: 2026-09-29 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/021-wpfui-modernization/spec.md`

## Summary

Audit and modernize the desktop client (`src/RAGGit.Client.WPF`) onto its already-referenced UI library, WPF-UI 4.3.0 (`https://github.com/lepoco/wpfui`, docs `https://wpfui.lepo.co/`). The client already uses WPF-UI for its shell (`FluentWindow`, `NavigationView`, `TitleBar`, `ContentDialogHost`, `SnackbarPresenter`) and 35 `ui:Button` + 29 `ui:Card` + 7 `ui:SymbolIcon` usages, but retains stragglers: 9 raw buttons, 12 `ListView`, 9 `ListBox`, 8 `ProgressBar`, 2 `ComboBox`, 2 `RadioButton`, 1 `CheckBox`, 2 `Expander`, 127 plain `TextBlock`, 5 hard-coded XAML colors plus 4 hard-coded brushes in `StatusChip.xaml.cs`, and 4 literal text-glyph pager buttons (`« ‹ › »`).

The plan converts every control that has a `ui:` equivalent, styles the rest through the library's implicit styles, replaces all color literals with the library's semantic theme brushes, converts the pager and status/icon surfaces to the bundled Fluent icon set (`SymbolRegular`), applies typography from the library, and makes the app follow the system theme (Light/Dark/High Contrast) at startup. A control-mapping catalog records each family's disposition and every justified exception. Presentation-only: no behavior, API, contract, ViewModel, or data change.

## Technical Context

**Language/Version**: C# / XAML, .NET 10 (`net10.0-windows10.0.17763.0`)

**Primary Dependencies**: WPF-UI `4.3.0` and `WPF-UI.DependencyInjection` `4.3.0` (already referenced), `CommunityToolkit.Mvvm` (via `RAGGit.Client.Core`), WPF (`System.Windows`)

**Storage**: N/A — presentation state only (no new persistence)

**Testing**: `dotnet build RAGGit.Server.slnf -c Release`; `dotnet test --filter "FullyQualifiedName~Tests.Unit"` (and Contract/Integration/offline subsets); `dotnet csharpier check .`; manual Light/Dark/High Contrast + keyboard/Narrator UI walkthrough on Windows

**Target Platform**: Windows 10 1809+ / Windows 11 desktop (single-tenant, offline-first)

**Project Type**: Desktop application (WPF client shell + pages + shared controls)

**Performance Goals**: No measurable regression; lists stay virtualized; no new scroll-owning wrappers around virtualizing collections

**Constraints**: Presentation-only; zero hard-coded colors; no font installed by the user (Windows 10 floor); existing Automation IDs preserved; `RAGGit.Client.Core`, APIs, and contracts read-only

**Scale/Scope**: `App.xaml(.cs)`, `MainWindow.xaml(.cs)`, 8 pages, `Views/Dialogs/UploadDialog`, 3 shared components (`ChatControl`, `PaginationFooterControl`, `StatusChip`), 1 converter file, and the `ViewModels` icon declarations that feed navigation

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Single-Tenant On-Prem**: PASS — no tenant, data, or deployment change.
- **II. Workstation-Owned AI**: PASS — no model, vector, or network behavior; client stays thin.
- **III. .NET Library-First & Client Reuse**: PASS — the WPF client (constitutional fallback) reuses `RAGGit.Client.Core`; no new project or library. No RAG logic duplicated.
- **IV. Offline Invariant**: PASS — no request path touched; zero WAN impact.
- **V. Citation-Grounded RAG**: PASS — citation rendering is restyled only; citation semantics untouched.
- **VI. Test-First**: PASS — XAML compiles as a build gate; existing unit/contract/integration/offline suites remain the regression gate with zero assertion changes. UI standards are validated by the mapping catalog + manual theme/keyboard checks (no new library surface to unit-test).
- **VII. Simplicity & Proprietary Stewardship**: PASS — one project, no dependency change beyond the already-referenced package; no packaging/storage change. Reuses the library's theming instead of hand-rolled styles.

**Pre-Phase-0 gate**: PASS. No violation requires Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/021-wpfui-modernization/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output (control-mapping catalog)
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output (UI contracts)
│   └── ui-contracts.md
└── tasks.md             # created by /speckit.tasks
```

### Source Code (repository root)

```text
src/
└── RAGGit.Client.WPF/
    ├── App.xaml / App.xaml.cs                 # theme dictionary + system-theme application
    ├── Views/
    │   ├── MainWindow.xaml / .cs              # shell; SystemThemeWatcher retained
    │   ├── Pages/*.xaml (+ .cs)               # Dashboard, Library, Query, QueryDetail,
    │   │                                      # History, DocumentsMine, AdminUsers, Login, Settings
    │   └── Dialogs/UploadDialog.xaml (+ .cs)
    ├── Components/
    │   ├── ChatControl.xaml (+ .cs)           # accent bubble literals
    │   ├── PaginationFooterControl.xaml (+ .cs)# literal glyphs -> Fluent icons
    │   └── StatusChip.xaml (+ .cs)            # code-behind brush literals -> semantic brushes
    ├── ViewModels/MainWindowViewModel.cs      # nav icons (SymbolRegular, already correct)
    └── Converters/                            # status/visibility converters (contract preserved)

tests/
└── existing unit / contract / integration suites (unchanged; regression gate only)
```

**Structure Decision**: Preserve the existing single-solution layout and the presentation layer only. No files are added under `src/` beyond optional small support types (e.g. a status-brush converter) if needed; no server, API, or `Core` project is modified.

## Complexity Tracking

No constitutional violations require additional complexity justification.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — (none) | — | — |

**Post-Phase-1 gate**: PASS — the design touches only presentation surfaces, converts literals to existing library resources, and adds no project, dependency, storage, or contract change.
