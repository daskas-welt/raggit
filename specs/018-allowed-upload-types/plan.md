# Implementation Plan: Allowed Upload Types

**Branch**: `018-allowed-upload-types` | **Date**: 2026-09-18 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/018-allowed-upload-types/spec.md`

## Summary

Restrict new uploads to exactly four types (`.docx`, `.pdf`, `.txt`, `.xlsx`), dropping `.md` from the intake allow-list. Single source of truth added as `DocumentValidation.AllowedExtensions` in `RAGGit.Core`; the shared `UploadViewModel` and `WinUIFilePicker` consume it client-side, and `DocumentsController.TryMapMime` enforces it server-side with a plain-language 400. `DocumentMimeType.Md` and the read path (converter, repository, chunker, validator) stay untouched so existing `.md` documents remain searchable per FR-008. No new project, dependency, endpoint, or persisted schema change.

## Technical Context

**Language/Version**: C# .NET 8 (workstation API + shared libraries); WinUI 3 client targets `net8.0-windows10.0.17763.0`

**Primary Dependencies**: CommunityToolkit.Mvvm (`ObservableObject`, `RelayCommand`), WinUI 3 `FileOpenPicker`, existing `DocumentsApiClient.UploadAsync`, `DocumentValidation` (`RAGGit.Core`)

**Storage**: N/A — no persisted schema change; transient queue validation only (SQLite `Documents` rows, including existing `Md` rows, untouched)

**Testing**: xUnit — `tests/unit/UploadViewModelTests.cs` (allow-list/rejection/message tests), contract tests for `POST /api/documents` `.md` → 400; existing validator/display/mime-order tests stay green unchanged

**Target Platform**: Windows 10 1809+ / 11 desktop (WinUI 3, Windows App SDK, MSIX) + LAN-reachable AI Workstation

**Project Type**: desktop-app (WinUI 3 client + shared .NET library) + workstation web-service

**Performance Goals**: 4/4 supported types queue-and-upload within 1 minute per file (SC-001); type check is O(1) string comparison — no perf impact

**Constraints**: Offline invariant (LAN-only, no cloud egress); admin-only upload entry; Light/Dark/High Contrast themes; keyboard + screen-reader operability; no new project, dependency, endpoint, or breaking persisted-data change

**Scale/Scope**: 4-entry allow-list; 3 client touch points + 1 server gate; existing size/cell-count guards unchanged

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Single-Tenant On-Prem | ✅ PASS | No data-model or API-shape change; singleton Library untouched; no tenant parameter added |
| II. Workstation-Owned AI | ✅ PASS | Client stays thin (extension check + filter only); no local models/vectors added |
| III. .NET Library-First & Client Reuse | ✅ PASS | Allow-list defined once in `RAGGit.Core.DocumentValidation`; shared `UploadViewModel` (`RAGGit.Client.Core`) + WinUI shell consume it; server reuses same constant |
| IV. Offline Invariant | ✅ PASS | Upload transport unchanged (LAN `POST /api/documents`); string-comparison gate needs no egress |
| V. Citation-Grounded RAG | N/A | Upload intake only; no answers generated; existing `.md` chunks stay citable |
| VI. Test-First | ✅ PASS | Unit tests for accept/reject/message wording; contract test for server 400 on `.md`; full suite must stay green; TDD Red-Green-Refactor required |
| VII. Simplicity & Proprietary Stewardship | ✅ PASS | No new project, dependency, endpoint, or contract-shape change; behavior restriction only |

## Project Structure

### Documentation (this feature)

```text
specs/018-allowed-upload-types/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
│   └── upload-allowlist.md
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/
├── RAGGit.Core/
│   └── Models/Document.cs            # EXTEND: DocumentValidation gains AllowedExtensions + supported-types label
├── RAGGit.Client.Core/
│   └── ViewModels/UploadViewModel.cs # EXTEND: consume shared allow-list; drop .md from text/messages/ContentTypeFor
├── RAGGit.Client.WinUI/
│   ├── Services/WinUIFilePicker.cs   # EXTEND: remove ".md" from FileTypeFilter
│   └── Views/UploadDialog.xaml(.cs)  # UNCHANGED (binds SupportedTypesText; no hardcoded type list)
├── RAGGit.Workstation.Api/
│   └── Controllers/DocumentsController.cs # EXTEND: TryMapMime rejects text/markdown + .md (400 unsupported)
├── RAGGit.Ingest/                    # UNCHANGED (Chunker/DocumentFormatValidator keep Md read path)
└── RAGGit.Core/
    ├── Models/DocumentMimeTypeConverter.cs  # UNCHANGED (existing Md rows must still deserialize)
    └── Data/SqliteDocumentRepository.cs     # UNCHANGED (existing Md rows must still load)

tests/
├── unit/UploadViewModelTests.cs      # EXTEND: allow-list accept/reject/message/Uppercase tests
└── contract/DocumentsContractTests.cs (or new allow-list contract test) # EXTEND: .md upload → 400
```

**Structure Decision**: Existing layout per Constitution III/VII — one shared constant in `RAGGit.Core`, behavior in the shared client ViewModel, thin WinUI shell change, single server gate. No new projects or layers.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | No violations; table intentionally empty |

## Post-Design Constitution Re-check (Phase 1 complete)

All gates re-evaluated after research + data-model + contracts + quickstart: **all still PASS**. Design adds no projects, dependencies, endpoints, or persisted data; `Md` retention is a read-path preservation (not new scope); test plan covers VI; themes/keyboard/screen-reader constraints unchanged (no new UI surfaces — only text/filter changes).
