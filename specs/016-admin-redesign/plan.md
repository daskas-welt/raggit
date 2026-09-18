# Implementation Plan: Admin Page Redesign

**Branch**: `016-admin-redesign` | **Date**: 2026-09-18 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/016-admin-redesign/spec.md`

**Note**: This plan was produced after a working implementation existed (built via the winui-dev agent and verified: WinUI compiles 0 warnings/0 errors, admin tests 15/15 green). It records the approach as decided so `/speckit.tasks` and `/speckit.converge` have a baseline.

## Summary

Redesign the WinUI Admin (People Management) page from full-bleed stretched cards into a constrained native settings surface: readable column layout, virtualized user list with labeled row actions, `InfoBar` status with severity, icon Refresh, inline-validated forms — reusing the existing admin ViewModel/commands and extending presentation state only (same pattern as 015). No server, contract, or RBAC change.

## Technical Context

**Language/Version**: C# / .NET 10 (`net10.0-windows10.0.17763.0` for the WinUI client)

**Primary Dependencies**: Windows App SDK 2.4.0 (WinUI 3), CommunityToolkit.Mvvm (ObservableObject/RelayCommand), `XamlControlsResources` Fluent styles

**Storage**: N/A (page holds transient form/status state only; user accounts live server-side)

**Testing**: xUnit (`dotnet test tests/unit/RAGGit.Tests.Unit.csproj`); 3 pre-existing admin tests + 7 new status tests

**Target Platform**: Windows 10 1809+ / 11 desktop, single-project MSIX

**Project Type**: desktop-app (WinUI 3 client) + shared .NET behavior library (`RAGGit.Client.Core`)

**Performance Goals**: SC-003 — 100-user list scrolls without jank; SC-001 — each core admin task completable unaided in under 2 minutes, 90% first-attempt success

**Constraints**: Admin-only surface (existing gating + server enforcement unchanged); Light/Dark/HighContrast legibility with zero hard-coded colors; ≥44px targets; full keyboard + screen-reader operability; `UpdateSourceTrigger=PropertyChanged`; no `ScrollViewer`-wrapped collection controls

**Scale/Scope**: One page, three sections (list, create, reset); 100+ user rows virtualized; narrow-window (≤720px) reflow

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Single-Tenant On-Prem**: PASS — no tenant concept touched; same workstation endpoints as before.
- **II. Workstation-Owned AI**: PASS — client stays thin; page holds only form/status state.
- **III. .NET Library-First & Client Reuse**: PASS — status/presentation state added to the existing shared admin ViewModel in `RAGGit.Client.Core`; WinUI view binds to it. No new project.
- **IV. Offline Invariant**: PASS — no network path changed; same LAN workstation API calls.
- **V. Citation-Grounded RAG**: N/A — no answer/citation surface.
- **VI. Test-First**: PASS — 7 new `AdminUsersStatusTests` red-first; 3 pre-existing admin tests pass with zero assertion changes; 2 unrelated full-suite failures proven pre-existing via `git stash`.
- **VII. Simplicity & Proprietary Stewardship**: PASS — no new project, no new dependencies; forms downgraded from always-open `Expander`s to plain `Border` cards (simpler, fewer moving parts).

Post-Phase-1 re-check: no new violations introduced. Complexity Tracking stays empty.

## Project Structure

### Documentation (this feature)

```text
specs/016-admin-redesign/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
│   └── admin-page.md    # ViewModel↔View binding + page behavior contract (no HTTP change)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/
├── RAGGit.Client.Core/
│   └── ViewModels/
│       └── AdminUsersViewModel.cs        # status/severity presentation state + confirmations
├── RAGGit.Client.WinUI/
│   ├── Views/
│   │   └── AdminUsersPage.xaml(.cs)      # column layout, virtualized list, InfoBar, row actions, forms
│   └── Converters/
│       └── ViewConverters.cs             # row-label converters (role/active/lock/selection)
└── [Workstation API, Ingest, Retrieval — untouched]

tests/
└── unit/
    └── AdminUsersStatusTests.cs          # 7 new status/confirmation tests
```

**Structure Decision**: Existing single-solution layout preserved; all work lands in the existing shared admin ViewModel plus the existing WinUI page. No API/contract version bump (user-management endpoints unchanged).

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — (none) | — | — |
