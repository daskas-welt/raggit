# Implementation Plan: Multi-File Upload (Picker + Drag-and-Drop)

**Branch**: `017-multi-file-upload` | **Date**: 2026-09-18 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/017-multi-file-upload/spec.md`

## Summary

Extend the `015-file-upload-ui` dialog with two intake paths that feed the same validated queue: multi-select in one system picker pass (`FileOpenPicker.PickMultipleFilesAsync` via an additive `IFilePicker.PickMultipleAsync`) and an OS drag-and-drop target (`DragOver`/`Drop` with `StorageItems`). Queue stays locked during upload (clarified), no batch cap (clarified), virtual items rejected like other invalid files (clarified). No server, contract, or RBAC changes.

## Technical Context

**Language/Version**: C# .NET 10 (solution) / net8.0-windows10.0.17763.0 WinUI target; `RAGGit.Client.Core` stays UI-framework free

**Primary Dependencies**: WinUI 3 (`FileOpenPicker`, `DragOver`/`Drop` + `StandardDataFormats.StorageItems`), CommunityToolkit.Mvvm (`ObservableObject`, `RelayCommand`), existing `DocumentsApiClient.UploadAsync`, `DocumentValidation.MaxFileSizeBytes` (100 MB)

**Storage**: N/A (transient queue only; no new persisted data)

**Testing**: xUnit (`tests/unit/UploadViewModelTests.cs` extended with multi-add/rejection/lock tests; existing picker fakes extended)

**Target Platform**: Windows 10 1809+ / 11 desktop (WinUI 3, Windows App SDK, MSIX)

**Project Type**: desktop-app (WinUI 3 client + shared .NET library)

**Performance Goals**: 5 files queued via one picker pass or one drop in under 30 s (SC-001); per-file sequential upload unchanged

**Constraints**: Offline invariant (LAN-only uploads, no cloud egress); admin-only entry; Light/Dark/High Contrast themes; ≥44px targets; full keyboard + screen-reader operability; no new project, dependency, endpoint, or contract change

**Scale/Scope**: Uncapped queue (clarified); existing scrollable list + sequential upload model reused

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Single-Tenant On-Prem | ✅ PASS | No data-model or API change; singleton Library untouched |
| II. Workstation-Owned AI | ✅ PASS | Client stays thin (HttpClient only); no local models/vectors |
| III. .NET Library-First & Client Reuse | ✅ PASS | Intake/validation/queue-lock logic in shared `UploadViewModel` (`RAGGit.Client.Core`); XAML view only binds; `IFilePicker` extended in Core |
| IV. Offline Invariant | ✅ PASS | Upload transport unchanged (LAN `POST /api/documents`); no new egress |
| V. Citation-Grounded RAG | N/A | Upload path only; no answers generated |
| VI. Test-First | ✅ PASS | VM-level unit tests for multi-add, mixed rejection, queue lock; full existing suite must stay green; no contract change so contract coverage unaffected |
| VII. Simplicity & Proprietary Stewardship | ✅ PASS | No new project, dependency, endpoint, or breaking API change |

## Project Structure

### Documentation (this feature)

```text
specs/017-multi-file-upload/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/
├── RAGGit.Client.Core/
│   ├── Services/IFilePicker.cs          # EXTEND: additive PickMultipleAsync + PickedFile reuse
│   └── ViewModels/UploadViewModel.cs    # EXTEND: multi-add intake, queue lock, drop-state
├── RAGGit.Client.WinUI/
│   ├── Services/WinUIFilePicker.cs      # EXTEND: PickMultipleFilesAsync impl
│   └── Views/UploadDialog.xaml(.cs)     # EXTEND: drop target area, bindings, Drop/DragOver
└── RAGGit.Core/Models/Document.cs       # UNCHANGED (DocumentValidation limits reused)

tests/
├── unit/UploadViewModelTests.cs         # EXTEND: multi-add, mixed rejection, lock, virtual reject
└── (contract/integration unchanged — no server change)
```

**Structure Decision**: Existing two-project client layout (`RAGGit.Client.Core` shared behavior + `RAGGit.Client.WinUI` shell) per Constitution III/VII; all intake logic in the shared ViewModel, WinUI layer owns only picker/drop mechanics and visuals.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | No violations; table intentionally empty |

## Post-Design Constitution Re-check (Phase 1 complete)

All gates re-evaluated after research + data-model + contracts + quickstart: **all still PASS**. Design adds no projects, dependencies, endpoints, or persisted data; test plan covers VI; themes/keyboard/screen-reader constraints carried into contracts and quickstart.
