# Implementation Plan: Design-System Refinement

**Branch**: `023-design-system-refinement` | **Date**: 2026-09-29 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/023-design-system-refinement/spec.md`

## Summary

Own and formalise the desktop client's design system. This feature establishes the icon size scale
and one-glyph-per-concept rule, the colour-token taxonomy and the removal of opacity-based emphasis,
then makes the rules **reusable and enforced**: shared typography styles, a page-header pattern
(title + supporting line), empty-state iconography, table row separation, active-destination emphasis
in the navigation pane, and static checks that keep the rules from drifting.

Presentation-only: no behavior, API, contract, or `RAGGit.Client.Core` change; every existing
automation identifier is preserved.

## Technical Context

**Language/Version**: C# / XAML, .NET 10 (`net10.0-windows10.0.17763.0`)

**Primary Dependencies**: WPF-UI `4.3.0` (already referenced). Verified for this feature:
`App.xaml` supports app-level `Style` resources; `NavigationViewItem.Icon` is an `IconElement` and
`SymbolIcon : FontIcon : IconElement` with a settable `Filled` bool; `ThemeResource` enum members
`DividerStrokeColorDefaultBrush`, `SubtleFillColorSecondaryBrush`, `TextFillColorDisabledBrush`,
`TextFillColorTertiaryBrush`, `SystemFillColorCautionBrush`, `CardStrokeColorDefaultBrush` all exist;
`SymbolRegular` includes `Document24`, `FolderOpen24`, `Search24`, `History24`, `People24`,
`Library24`, `DocumentText24` (`Inbox24`/`ClipboardText24` do **not** exist).

**Storage**: N/A — presentation and documentation only.

**Testing**: `dotnet build RAGGit.sln -c Release -p:Platform=x64`; the existing Unit/Contract/offline
suites; `dotnet csharpier check .`; the static contract + design checks in [quickstart.md](quickstart.md);
`winapp ui` screenshots for the affected surfaces.

**Target Platform**: Windows 10 1809+ / Windows 11 desktop (single-tenant, offline-first)

**Project Type**: Desktop application (WPF client)

**Performance Goals**: No measurable regression; empty-state imagery and separators must not cause
clipping at 800×600.

**Constraints**: Presentation-only; zero hard-coded colour values; **no `Opacity=` as a colour
substitute**; icons must be valid `SymbolRegular`/`SymbolFilled` members; existing automation IDs
preserved; `RAGGit.Client.Core`, APIs and contracts read-only.

**Scale/Scope**: `App.xaml` (shared styles), `Views/MainWindow.xaml(.cs)` + `ViewModels/MainWindowViewModel.cs`
(active emphasis), and the pages (`DashboardPage`, `LibraryPage`, `HistoryPage`, `DocumentsMinePage`,
`AdminUsersPage`, `QueryPage`, `QueryDetailPage`, `SettingsPage`).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Single-Tenant On-Prem**: PASS — no tenant, data, or deployment change.
- **II. Workstation-Owned AI**: PASS — no model, vector, or network behaviour.
- **III. .NET Library-First & Client Reuse**: PASS — presentation layer only; no new project.
- **IV. Offline Invariant**: PASS — no request path touched; shared styles and glyphs are all local.
- **V. Citation-Grounded RAG**: PASS — no retrieval or citation semantics touched.
- **VI. Test-First**: PASS — XAML compiles as a build gate; visual/typographic standards are proven
  by the static checks + screenshot review (no new library surface to unit-test).
- **VII. Simplicity & Proprietary Stewardship**: PASS — one project, no dependency change; the design
  system is expressed as shared styles rather than hand-rolled per-page attributes.

**Pre-Phase-0 gate**: PASS. No violation requires Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/023-design-system-refinement/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output (design-system model)
├── quickstart.md        # Phase 1 output (validation guide)
├── contracts/           # Phase 1 output (D1–D6 design contracts)
│   └── ui-contracts.md
└── tasks.md             # Phase 2 output (/speckit.tasks — not created here)
```

### Source Code (repository root)

```text
src/RAGGit.Client.WPF/
├── App.xaml                                   # shared styles (page header, subtitle, section, secondary, meta, empty-state glyph)
├── Views/
│   ├── MainWindow.xaml / .cs                  # active-destination emphasis wiring
│   └── Pages/
│       ├── DashboardPage.xaml                 # header + empty states
│       ├── LibraryPage.xaml                   # header + table separation + empty state
│       ├── HistoryPage.xaml                   # header + empty state
│       ├── DocumentsMinePage.xaml             # header + empty state
│       ├── AdminUsersPage.xaml                # header + table separation + empty state
│       ├── QueryPage.xaml                     # header + subtitle
│       ├── QueryDetailPage.xaml               # section headings
│       └── SettingsPage.xaml                  # header + section headings
└── ViewModels/MainWindowViewModel.cs          # explicit SymbolIcon per nav item (enables filled state)

tests/  └── existing suites (unchanged; regression gate only)
```

**Structure Decision**: Keep the single-solution layout and the presentation layer only. Design rules
live in `specs/023-design-system-refinement/` and are enforced by the static checks in quickstart.md.

## Complexity Tracking

No constitutional violations require additional complexity justification.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — (none) | — | — |

**Post-Phase-1 gate**: PASS — presentation only; reuses library tokens/controls and adds no project,
dependency, storage, or contract change.
