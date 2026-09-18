# Implementation Plan: File Upload UI Refresh

**Branch**: `015-file-upload-ui` | **Date**: 2026-09-18 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/015-file-upload-ui/spec.md` (+ normative `## Clarifications / Session 2026-09-18`)

**Note**: This plan was produced after a working implementation existed (built via the winui-dev agent and verified: WinUI build 0 warnings/0 errors, 18/18 upload unit tests green). It records the approach as decided so `/speckit.tasks` and `/speckit.converge` have a baseline.

## Summary

Refresh the WinUI upload surface (`Views/UploadDialog`) from a single-file form with a raw status `TextBlock` to a Fluent multi-file queue UI: click-to-select queue building with pick-time type/size rejection, per-file progress bars plus an overall completed count, `InfoBar` status surfaces, `SymbolIcon` actions, auto-close on full success with library refresh, stay-open with per-file outcomes on any failure/cancel. Queue state lives in the existing `UploadViewModel` (shared behavior layer, `RAGGit.Client.Maui.ViewModels` namespace kept); no server, contract, or RBAC change.

## Technical Context

**Language/Version**: C# / .NET 10 (`net10.0-windows10.0.17763.0` for the WinUI client)

**Primary Dependencies**: Windows App SDK 2.4.0 (WinUI 3), CommunityToolkit.Mvvm (ObservableObject/RelayCommand), `XamlControlsResources` Fluent styles

**Storage**: N/A (client is stateless apart from credential-locker token cache; queue is in-memory ViewModel state only)

**Testing**: xUnit (`dotnet test tests/unit/RAGGit.Tests.Unit.csproj`); 9 pre-existing + 9 new upload ViewModel tests

**Target Platform**: Windows 10 1809+ / 11 desktop, single-project MSIX

**Project Type**: desktop-app (WinUI 3 client) + shared .NET behavior library (`RAGGit.Client.Core`)

**Performance Goals**: SC-001 — small-file pick-to-success with list refresh in under 1 minute, unaided, 90% first-attempt trials; sequential per-file uploads keep memory flat (no parallel fan-out)

**Constraints**: Offline-invariant untouched (no query-time cloud egress; client stays thin HttpClient-only); Light/Dark/HighContrast legibility with zero hard-coded colors; ≥44px targets; full keyboard + screen-reader operability

**Scale/Scope**: One dialog, one queue (small-N files, each ≤100 MB workstation-enforced limit); no pagination or virtualization-at-scale concerns beyond a `MaxHeight`-bounded virtualized list

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Single-Tenant On-Prem**: PASS — no tenant concept touched; upload posts to the single-company workstation as before.
- **II. Workstation-Owned AI**: PASS — client stays thin; queue holds only file streams + progress, no model weights or vector DB on desktop.
- **III. .NET Library-First & Client Reuse**: PASS — queue state added to the existing shared `UploadViewModel` in `RAGGit.Client.Core`; WinUI view binds to it. No new project, no duplicated RAG logic.
- **IV. Offline Invariant**: PASS — no network path changed; upload still targets the LAN workstation API.
- **V. Citation-Grounded RAG**: N/A — upload carries no answer/citation surface.
- **VI. Test-First**: PASS — 9 new `UploadViewModelTests` (queue, gating, per-file progress, rejection, partial success) written red-first; full upload filter 18/18 green; 2 unrelated full-suite failures proven pre-existing via `git stash`.
- **VII. Simplicity & Proprietary Stewardship**: PASS — no new project (still `src/` + `RAGGit.Client.WinUI/` + `RAGGit.Workstation.Api/` + `tests/`); single dialog, sequential uploads, no new dependencies.

Post-Phase-1 re-check: no new violations introduced (see research.md R-06 for the rejected parallel-upload alternative). Complexity Tracking stays empty.

## Project Structure

### Documentation (this feature)

```text
specs/015-file-upload-ui/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
│   └── upload-queue.md  # ViewModel↔View binding + dialog behavior contract (no HTTP change)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/
├── RAGGit.Client.Core/
│   └── ViewModels/
│       └── UploadViewModel.cs        # queue state (UploadQueueItem, progress, gating, cancel)
├── RAGGit.Client.WinUI/
│   ├── Views/
│   │   ├── UploadDialog.xaml(.cs)    # queue list, InfoBars, icons, a11y, auto-close
│   │   └── LibraryPage.xaml.cs       # post-dialog refresh (successes only on partial)
│   └── Converters/
│       └── DocumentConverters.cs     # StatusSeverityConverter (added)
└── [Workstation API, Ingest, Retrieval — untouched]

tests/
└── unit/
    └── UploadViewModelTests.cs       # 9 existing + 9 new queue tests
```

**Structure Decision**: Single-solution layout preserved; all upload work lands in the existing shared ViewModel plus the existing WinUI dialog. No new projects, no API/contract version bump (contract stays `1.4.0`, `POST /api/documents` multipart Admin-only unchanged).

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — (none) | — | — |
