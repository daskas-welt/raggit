# Tasks: Allowed Upload Types

**Input**: Design documents from `/specs/018-allowed-upload-types/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Included per Constitution VI (Test-First, NON-NEGOTIABLE) — write each test task FIRST and ensure it FAILS before its implementation task.

**Organization**: Tasks grouped by user story; each story phase is an independently testable increment.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)
- Exact file paths in every description

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Confirm green baseline before changing the allow-list

- [X] T001 Verify green baseline via `dotnet build RAGGit.sln` and `dotnet test RAGGit.sln` in RAGGit.sln

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Single source of truth for the allow-list that all stories consume

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T002 Add `AllowedExtensions` (`{ ".pdf", ".docx", ".xlsx", ".txt" }`, case-insensitive) and `SupportedTypesLabel` (`"PDF, DOCX, XLSX, TXT"`) to `DocumentValidation` in src/RAGGit.Core/Models/Document.cs
- [X] T003 Update `Document.Mime` validation message to `"Allowed: pdf, docx, xlsx, txt."` (drop `md`) in src/RAGGit.Core/Models/Document.cs

**Checkpoint**: Foundation ready — `RAGGit.Core` builds; user story implementation can now begin

---

## Phase 3: User Story 1 - Admin uploads a supported document type (Priority: P1) — MVP

**Goal**: Each of `.docx`, `.pdf`, `.txt`, `.xlsx` queues, uploads, and becomes searchable end to end

**Independent Test**: QS-1 — upload one valid file per type; 4/4 succeed, appear in the library within 1 minute each, and return citations on query

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T004 [P] [US1] Unit tests accepting each of the 4 types plus uppercase variants (`DOCX`, `Pdf`, `TXT`, `XlSx`) via `AddPickedFilesAsync` in tests/unit/UploadViewModelTests.cs
- [X] T005 [P] [US1] Contract tests posting one valid file per allowed type to `POST /api/documents` expecting success in tests/contract/UploadAllowListContractTests.cs

### Implementation for User Story 1

- [X] T006 [US1] Consume `DocumentValidation.AllowedExtensions` in `AddPickedFilesAsync` in src/RAGGit.Client.Core/ViewModels/UploadViewModel.cs
- [X] T007 [US1] Remove unreachable `".md" => "text/markdown"` arm from `ContentTypeFor` in src/RAGGit.Client.Core/ViewModels/UploadViewModel.cs

**Checkpoint**: US1 fully functional and testable independently — QS-1 passes

---

## Phase 4: User Story 2 - Admin is blocked from uploading an unsupported type (Priority: P1)

**Goal**: Every non-allow-list type (`.md`, `.doc`, `.png`, `.zip`, `.pptx`, …) is blocked client-side with a named message and rejected server-side with 400, via picker and drag-and-drop alike

**Independent Test**: QS-2 + QS-4 — 100% of unsupported trials blocked before queueing with file-naming messages; mismatched-content trials create zero documents

### Tests for User Story 2

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T008 [P] [US2] Unit tests rejecting `.md`/`.doc`/`.png`/`.zip`/`.pptx` via picker and drop seams, mixed valid/invalid batch partial-accept, and rejection message wording in tests/unit/UploadViewModelTests.cs
- [X] T009 [P] [US2] Contract tests for `.md` upload → 400 naming the file plus renamed-content mismatch → 400 with no document retained in tests/contract/UploadAllowListContractTests.cs

### Implementation for User Story 2

- [X] T010 [US2] Update rejection message to `"Choose {SupportedTypesLabel} files."` (interpolated single source) in src/RAGGit.Client.Core/ViewModels/UploadViewModel.cs
- [X] T011 [P] [US2] Reject `text/markdown` content-type and `.md` extension in `TryMapMime` (return false → existing 400 path) in src/RAGGit.Workstation.Api/Controllers/DocumentsController.cs

**Checkpoint**: US1 AND US2 both work — QS-1, QS-2, QS-4, QS-5 pass

---

## Phase 5: User Story 3 - Admin sees only supported types offered up front (Priority: P2)

**Goal**: Dialog hint and picker filter advertise exactly the 4 supported types before any file is chosen

**Independent Test**: QS-3 — hint reads `PDF, DOCX, XLSX, TXT supported · up to 100 MB.`; picker filter offers the 4 types; 9/10 walkthrough participants name all 4 unaided

### Tests for User Story 3

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T012 [P] [US3] Unit test asserting `SupportedTypesText` lists exactly the 4 types and no `MD` in tests/unit/UploadViewModelTests.cs

### Implementation for User Story 3
- [X] T013 [US3] Update `SupportedTypesText` to `"PDF, DOCX, XLSX, TXT supported · up to 100 MB."` in src/RAGGit.Client.Core/ViewModels/UploadViewModel.cs

- [X] T014 [P] [US3] Remove `FileTypeFilter.Add(".md")` from `CreatePicker` in src/RAGGit.Client.WinUI/Services/WinUIFilePicker.cs

**Checkpoint**: All user stories independently functional — QS-3 passes (picker filter verified manually per quickstart; `FileOpenPicker` requires WinRT)

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Full validation and residue check across all stories

- [X] T015 Run quickstart validation QS-1 through QS-6 per specs/018-allowed-upload-types/quickstart.md
- [X] T016 [P] Full regression via `dotnet build RAGGit.sln` and `dotnet test RAGGit.sln` in RAGGit.sln, confirming untouched `Md` tests stay green (`DocumentMimeTypeXlsxTests`, `DocumentFormatValidatorTests`, `DocumentDisplayTests`)
- [X] T017 [P] Grep-verify no lingering `MD`/`markdown` references remain in upload intake surfaces: src/RAGGit.Client.Core/ViewModels/UploadViewModel.cs, src/RAGGit.Client.WinUI/Services/WinUIFilePicker.cs, src/RAGGit.Core/Models/Document.cs

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — starts immediately
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories
- **User Stories (Phases 3–5)**: All depend on Foundational completion
  - Sequential in priority order by default (US1 → US2 → US3); US2 and US3 touch different files from each other except `UploadViewModel.cs`, so cross-story parallelism is limited — prefer sequential
- **Polish (Phase 6)**: Depends on all desired stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: After Foundational — no dependencies on other stories
- **User Story 2 (P1)**: After Foundational — extends US1's `AddPickedFilesAsync` path but independently testable (reject verdicts)
- **User Story 3 (P2)**: After Foundational — text/filter only; independently testable without uploading

### Within Each User Story

- Tests MUST be written and FAIL before implementation
- Same-file edits run sequentially (never [P] against each other): T002→T003 (`Document.cs`); T006→T007→T010→T013 (`UploadViewModel.cs`); T004→T008→T012 (`UploadViewModelTests.cs`); T005→T009 (`UploadAllowListContractTests.cs`)
- Cross-file tasks marked [P] run in parallel (e.g., T010 + T011, T013 + T014)

### Parallel Opportunities

- Phase 3: T004 + T005 in parallel (different test files)
- Phase 4: T008 + T009 in parallel; T010 + T011 in parallel (client VM vs server controller)
- Phase 5: T012 + T013/T014 batch in parallel where files differ (T014 vs T012/T013)
- Phase 6: T016 + T017 in parallel

---

## Parallel Example: User Story 2

```bash
# Launch all tests for User Story 2 together (different files, no dependencies):
Task: "Unit tests rejecting .md/.doc/.png/.zip/.pptx in tests/unit/UploadViewModelTests.cs"   # T008
Task: "Contract tests for .md upload → 400 in tests/contract/UploadAllowListContractTests.cs" # T009

# Launch implementation across seams together (different files, no dependencies):
Task: "Update rejection message in src/RAGGit.Client.Core/ViewModels/UploadViewModel.cs"              # T010
Task: "Reject text/markdown + .md in TryMapMime in src/RAGGit.Workstation.Api/Controllers/DocumentsController.cs" # T011
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001)
2. Complete Phase 2: Foundational (T002–T003)
3. Complete Phase 3: User Story 1 (T004–T007)
4. **STOP and VALIDATE**: QS-1 independently — 4/4 types upload and cite
5. Deploy/demo if ready

### Incremental Delivery

**Recommended MVP = US1 + US2 together**: an allow-list without enforcement (US1 alone) leaves `.md` uploadable through the old paths, so US2 is the load-bearing half. Sequence: Setup → Foundational → US1 → validate QS-1 → US2 → validate QS-2/QS-4/QS-5 → US3 → validate QS-3 → Polish (QS-6 + regression).

### Parallel Team Strategy

With multiple developers after Foundational: Developer A finishes US1 while Developer B drafts US2 tests (different files); converge on `UploadViewModel.cs` edits sequentially (T006 → T007 → T010 → T013) to avoid same-file conflicts.

---

## Notes

- [P] tasks = different files, no dependencies — safe to parallelize
- [Story] label maps each story-phase task to its user story for traceability
- Same-file chains (Document.cs, UploadViewModel.cs, UploadViewModelTests.cs, UploadAllowListContractTests.cs) MUST run sequentially
- `DocumentMimeType.Md`, converter, repository, chunker, and validator stay UNCHANGED — existing `.md` rows keep working (FR-008)
- `UploadDialog.xaml` needs no change (binds `SupportedTypesText`)
- Stop at any checkpoint to validate the story independently via its quickstart scenario
