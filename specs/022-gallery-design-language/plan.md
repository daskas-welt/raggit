# Implementation Plan: Gallery-Style Design Language

**Branch**: `022-gallery-design-language` | **Date**: 2026-09-29 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/022-gallery-design-language/spec.md`

## Summary

Adopt the WPF-UI Gallery's design language on three surfaces of the desktop client
(`src/RAGGit.Client.WPF`): the **shell**, the **Dashboard**, and the **Settings** page. The client
already references WPF-UI 4.3.0 and uses a `ui:FluentWindow` + `ui:NavigationView` shell, but the
chrome is unstyled (no backdrop, no pane toggle/search/grouping, no transitions), the Dashboard
header is plain text with button-shaped metric tiles, and Settings is a stack of bare controls.

The plan (1) polishes the window chrome and navigation pane (Mica backdrop, centered startup, pane
toggle, transition, grouped sections via `NavigationViewItemHeader`, live `AutoSuggestBox` search,
footer Settings); (2) re-skins the Dashboard with a **theme-accent-gradient hero banner** and
navigational **`ui:CardAction` tiles** (icon + title + one-line description); and (3) re-skins
Settings into **`ui:CardControl` rows** (icon + title + description + control) under section
headings. Presentation-only: no behavior, API, contract, or `RAGGit.Client.Core` change, and every
existing automation identifier is preserved.

## Technical Context

**Language/Version**: C# / XAML, .NET 10 (`net10.0-windows10.0.17763.0`)

**Primary Dependencies**: WPF-UI `4.3.0` and `WPF-UI.DependencyInjection` `4.3.0` (already
referenced); WPF (`System.Windows`). Controls confirmed present in 4.3.0: `CardAction` (derives from
`System.Windows.Controls.Primitives.ButtonBase` → exposes `Click`), `CardControl`, `CardExpander`,
`Anchor`, `HyperlinkButton`, `AutoSuggestBox`, `BreadcrumbBar`, `NavigationViewItemHeader`,
`SymbolIcon`, `TextBlock` (`Appearance`, `FontTypography`). `FluentWindow`: `WindowBackdropType`,
`WindowCornerPreference`, `ExtendsContentIntoTitleBar`. `NavigationView`: `IsPaneToggleVisible`,
`OpenPaneLength`, `Transition`, `IsTopSeparatorVisible`, `IsFooterSeparatorVisible`, `FrameMargin`,
`Header`, `AutoSuggestBox`, `ContentOverlay`.

**Storage**: N/A — presentation state only (no new persistence).

**Testing**: `dotnet build RAGGit.sln -c Release -p:Platform=x64`; `dotnet test` filters
(Unit/Contract/offline subset); `dotnet csharpier check .`; the static contract checks in
[quickstart.md](quickstart.md); and `winapp ui` UI-automation walkthrough for the shell/Dashboard/
Settings surfaces.

**Target Platform**: Windows 10 1809+ / Windows 11 desktop (single-tenant, offline-first)

**Project Type**: Desktop application (WPF client shell + pages)

**Performance Goals**: No measurable regression; Dashboard tiles reflow rather than clip; no new
scroll-owning wrapper around a virtualizing collection.

**Constraints**: Presentation-only; zero hard-coded color literals; every icon name MUST be a valid
`SymbolRegular` member (runtime-validated, not compile-validated); all existing automation IDs
preserved; `RAGGit.Client.Core`, APIs, and contracts read-only; must reflow at 800×600.

**Scale/Scope**: `App.xaml`, `Views/MainWindow.xaml(.cs)`, `ViewModels/MainWindowViewModel.cs`,
`Views/Pages/DashboardPage.xaml(.cs)`, `Views/Pages/SettingsPage.xaml(.cs)` — 3 surfaces, 6 files.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Single-Tenant On-Prem**: PASS — no tenant, data, or deployment change.
- **II. Workstation-Owned AI**: PASS — no model, vector, or network behavior; client stays thin.
- **III. .NET Library-First & Client Reuse**: PASS — the WPF client (constitutional fallback)
  reuses `RAGGit.Client.Core`; no new project or library; no RAG logic duplicated.
- **IV. Offline Invariant**: PASS — no request path touched; hero/tiles use no network assets.
- **V. Citation-Grounded RAG**: PASS — no retrieval or citation semantics touched.
- **VI. Test-First**: PASS — XAML compiles as a build gate; existing suites remain the regression
  gate with zero assertion changes. This feature adds no library surface to unit-test; UI standards
  are proven by the contract checks + UI-automation walkthrough (see [quickstart.md](quickstart.md)).
- **VII. Simplicity & Proprietary Stewardship**: PASS — one project, no dependency change beyond
  the already-referenced package; reuses the library's controls/theming instead of hand-rolled styles.

**Pre-Phase-0 gate**: PASS. No violation requires Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/022-gallery-design-language/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
│   └── ui-contracts.md
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created here)
```

### Source Code (repository root)

```text
src/
└── RAGGit.Client.WPF/
    ├── App.xaml                                # (unchanged) theme dictionaries
    ├── Views/
    │   ├── MainWindow.xaml / .cs               # shell: backdrop, NavigationView props, search wiring
    │   └── Pages/
    │       ├── DashboardPage.xaml / .cs        # hero banner + CardAction tiles
    │       └── SettingsPage.xaml / .cs         # CardControl rows + section headings
    ├── ViewModels/
    │   └── MainWindowViewModel.cs              # nav grouping + search filter (WPF project, not Core)
    └── Components/                             # (unchanged; reused as-is)

tests/
└── existing unit / contract / integration suites (unchanged; regression gate only)
```

**Structure Decision**: Preserve the existing single-solution layout and the presentation layer
only. No files are added under `src/` beyond optional small support types (e.g. a local `ICommand`)
if needed; no server, API, or `Core` project is modified. `MainWindowViewModel` lives in the WPF
project, so navigation grouping/search stay within the presentation layer.

## Complexity Tracking

No constitutional violations require additional complexity justification.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — (none) | — | — |

**Post-Phase-1 gate**: PASS — the design touches only presentation surfaces, converts literal
colors to existing library resources, uses library controls, and adds no project, dependency,
storage, or contract change.
