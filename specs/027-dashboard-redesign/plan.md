# Implementation Plan: Dashboard Redesign — "Work overview"

**Branch**: `027-dashboard-redesign` | **Date**: 2026-10-01 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/027-dashboard-redesign/spec.md`

## Summary

Rebuild `DashboardPage` as a compact work overview: a small header (title, supporting
line, profile card, refresh, labeled History / My Docs / Admin actions) replaces the tall
gradient hero; six clickable metric cards (Documents, Ready, Indexing, Failed, Questions,
Mine) replace the five navigation tiles and the inline metric strip; the two content
panels become real `ui:Card` controls that stack at content widths ≤720 DIPs; and a
whole-dashboard empty state covers the empty-library/empty-history case. All values bind
to the existing `DashboardViewModel` properties — no view-model, service, API, contract,
or storage change. The 11 frozen Dashboard automation IDs are preserved; four new stable
IDs cover the new metric cards.

## Technical Context

**Language/Version**: C# / XAML, .NET 10 (`net10.0-windows10.0.17763.0` client;
`net10.0` Core/API)

**Primary Dependencies**: WPF-UI `4.3.0`, CommunityToolkit.Mvvm — already referenced; no
new package.

**Storage**: N/A — no persisted data; all dashboard values are transient view state.

**Testing**: No new test project. Presentation is verified by the 023 static audits and
the `winapp ui` walkthrough in [quickstart.md](quickstart.md), plus the existing
unit/contract/offline-integration regression suites.

**Target Platform**: Windows 10 1809+ / Windows 11 desktop; workstation API remains
local/LAN-only.

**Project Type**: WPF desktop client presentation change only.

**Performance Goals**: No new data fetch and no extra request per card; the existing
single load populates all six cards and both panels as today.

**Constraints**: Preserve the 11 frozen Dashboard automation IDs with unchanged meaning;
Fluent (`ui:*`) controls only; theme tokens only (no colour literals, no `Opacity=`); no
ad-hoc `FontSize` on `ui:TextBlock`; valid `SymbolRegular`/`ThemeResource` keys; shared
styles (`PageTitleText`, `PageSubtitleText`, `SecondaryText`, `EmptyStateGlyph`); ≥44-DIP
targets; 800×600 usability; existing handlers/navigation unchanged; no `Client.Core`,
API, contract, storage, or retrieval change.

**Scale/Scope**: One page (`DashboardPage.xaml` + code-behind) and this feature's docs.
No new project or package.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Single-Tenant On-Prem**: PASS — no tenant or data partitioning changes.
- **II. Workstation-Owned AI**: PASS — no client AI/model/vector/network destination
  change; the client remains HTTP-only to the configured workstation.
- **III. .NET Library-First & Client Reuse**: PASS — uses the existing page and view
  model; no new project and no duplicated RAG logic.
- **IV. Offline Invariant**: PASS — no WAN path added; refresh behavior is unchanged.
- **V. Citation-Grounded RAG**: PASS — query, retrieval, answer, and citation behavior
  are untouched.
- **VI. Test-First (NON-NEGOTIABLE)**: PASS — presentation-only change with no behavior
  to unit-test first; verification is the 023 static audits, the UI walkthrough, and the
  unchanged existing suites (same basis as sibling client features 019–026).
- **VII. Simplicity & Proprietary Stewardship**: PASS — no project/package/artifact
  format changes; no new persisted state.

**Pre-Phase-0 gate**: PASS. No constitutional violation requires an exception.

## Project Structure

### Documentation (this feature)

```text
specs/027-dashboard-redesign/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── ui-contracts.md
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source Code (repository root)

```text
src/RAGGit.Client.WPF/
└── Views/Pages/
    ├── DashboardPage.xaml      # compact header, six metric cards, two ui:Card
    │                           # panels, whole-dashboard empty state
    └── DashboardPage.xaml.cs   # unchanged handlers + ≤720-DIP stacking only

src/RAGGit.Client.Core/
└── ViewModels/DashboardViewModel.cs   # UNCHANGED — values already exist

tests/   # UNCHANGED — existing suites only, no new tests
```

**Structure Decision**: All work happens in the existing WPF page. Metric values
(`TotalDocuments`, `ReadyDocuments`, `IndexingDocuments`, `FailedDocuments`,
`TotalQueries`, `MyDocuments`), panel content, and status already exist on the shared
view model, so the page only re-binds them. Responsive stacking is page-owned
code-behind, following the sibling-feature pattern (view-only layout state stays in the
WPF layer, never in the shared view model).

## Complexity Tracking

No constitutional violations or new projects/packages require an exception.

| Item | Why not needed | Simpler course (chosen) |
|------|----------------|--------------------------|
| New project/package | Presentation-only re-binding of existing values | Edit the existing page only |
| View-model change | Indexing/Failed/summary values already exist | Bind existing properties; keep last-known-good via unchanged load behavior |
| New API/contract | No new data is required | No endpoint, parameter, or contract change |
| New tests | No behavior change to pin | 023 audits + walkthrough + existing suites |
| New dialog/window | No new flow is introduced | Keep the single-page surface |

**Post-Phase-1 gate**: PASS — no new project, dependency, schema, contract, or WAN
behavior; the change is bounded to `DashboardPage.xaml(.cs)`.
