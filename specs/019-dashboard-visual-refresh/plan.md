# Implementation Plan: Dashboard Visual Refresh

**Branch**: `019-dashboard-visual-refresh` | **Date**: 2026-09-22 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/019-dashboard-visual-refresh/spec.md`

## Summary

Refresh the authenticated WinUI shell and landing experience to use the supplied reference's visual hierarchy: a distinct blue navigation rail, compact summary tiles, a welcoming context header, and disciplined content panels. Add a dashboard landing page that composes existing document and history data, then apply the shared surface language to existing pages. Preserve all current workflows, permissions, API contracts, citations, status behavior, virtualization, and offline operation.

## Technical Context

<!--
  Technical context is fixed by the existing client architecture.
  for the project. The structure here is presented in advisory capacity to guide
  the iteration process.
-->

**Language/Version**: C# / XAML, .NET 10 (`net10.0-windows10.0.17763.0` for the WinUI client)

**Primary Dependencies**: Windows App SDK 2.4.0 (WinUI 3), CommunityToolkit.Mvvm, WinUI `NavigationView`/`ListView`/`InfoBar`, existing `RAGGit.Client.Core` API clients and ViewModels

**Storage**: N/A for the feature; dashboard state is transient and composes existing workstation-backed client data

**Testing**: xUnit shared-client tests, WinUI XAML compilation, existing unit/contract/integration/offline suites, manual keyboard/Narrator/theme/responsive walkthroughs

**Target Platform**: Windows 10 1809+ / Windows 11 desktop, single-project MSIX; thin client connected to the workstation over LAN/VPN

**Project Type**: WinUI 3 desktop app with shared .NET client behavior library

**Performance Goals**: Dashboard becomes usable within the existing authenticated startup flow; 100-document collections remain smooth; no duplicate equivalent data loads beyond the dashboard's required summaries; no visible jank during resize or navigation

**Constraints**: Presentation-only; no new server endpoints or fields; no query-time WAN; preserve existing permission gates, citations, status surfaces, stable Automation IDs, virtualization, and upload entry point; semantic Light/Dark/High Contrast resources only; minimum 44px interactive targets; no `ScrollViewer` around collection controls

**Scale/Scope**: Authenticated shell plus new dashboard landing surface and visual alignment of existing Library, Ask, History, My Docs, Admin, and Query Detail surfaces; narrow-window breakpoint at 720px; representative collections of 100+ rows

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Single-Tenant On-Prem**: PASS — no tenant parameter, partition, or cross-company behavior is added.
- **II. Workstation-Owned AI**: PASS — the desktop remains a thin client; no model, vector store, or AI logic moves to the client.
- **III. .NET Library-First & Client Reuse**: PASS — dashboard aggregation belongs in the existing `RAGGit.Client.Core` ViewModel layer and reuses existing API clients; no new project.
- **IV. Offline Invariant**: PASS — no query-time network path changes; dashboard reads existing workstation data through current LAN clients and does not add WAN dependencies.
- **V. Citation-Grounded RAG**: PASS — answer/citation presentation is preserved and remains more prominent than dashboard decoration; no generation behavior changes.
- **VI. Test-First**: PASS — add unit coverage for dashboard presentation aggregation and run existing unit, contract, integration, offline, and WinUI build gates; manual UI acceptance covers rendered behavior because no UI automation project exists.
- **VII. Simplicity & Proprietary Stewardship**: PASS — one new page/ViewModel and semantic resources in existing projects; no dependency, project, storage, or packaging changes.

**Pre-Phase-0 gate**: PASS. No constitutional violation requires complexity tracking.

## Project Structure

### Documentation (this feature)

```text
specs/019-dashboard-visual-refresh/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)
<!--
  The source tree below records the concrete layout for this feature.
  This is the selected existing project structure.
-->

```text
src/
├── RAGGit.Client.Core/
│   └── ViewModels/
│       └── DashboardViewModel.cs
├── RAGGit.Client.WinUI/
│   ├── App.xaml                         # semantic navigation/dashboard resources
│   ├── MainWindow.xaml(.cs)             # shell, landing route, responsive nav
│   └── Views/
│       ├── DashboardPage.xaml(.cs)
│       ├── LibraryPage.xaml              # shared visual alignment only
│       ├── QueryPage.xaml                # shared visual alignment only
│       ├── HistoryPage.xaml              # shared visual alignment only
│       ├── DocumentsMinePage.xaml        # shared visual alignment only
│       ├── AdminUsersPage.xaml           # shared visual alignment only
│       └── QueryDetailPage.xaml          # shared visual alignment only
└── RAGGit.Workstation.Api, RAGGit.Ingest, RAGGit.Retrieval  # untouched

tests/
└── unit/
    └── DashboardViewModelTests.cs
```

**Structure Decision**: Preserve the existing single-solution structure. Put presentation aggregation in `RAGGit.Client.Core`, the new dashboard and shell/theme work in `RAGGit.Client.WinUI`, and keep workstation/API projects untouched. Existing page XAML is changed only where needed to adopt shared surface/layout resources.

## Complexity Tracking

No constitutional violations require additional complexity justification.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — (none) | — | — |

**Post-Phase-1 gate**: PASS — design artifacts add no server contract, project, dependency, persistence, or AI behavior change.
