# Tasks: Multi-File Upload (Picker + Drag-and-Drop)

**Input**: Design documents from `/specs/017-multi-file-upload/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md, constitution.md (v1.3.0)

**Tests**: Included — Constitution VI mandates TDD and spec SC-002/SC-003 demand validation. Unit tests in `tests/unit/UploadViewModelTests.cs`; no server changes so no contract/integration tests.

**Organization**: Grouped by user story. All tasks below are complete ([X]) — implemented and validated 2026-09-18 (build 0 errors; unit 254/254; `DocumentsContractTests` 13/13).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [X] T001 Verify .gitignore covers C# essentials (bin/, obj/, *.user, *.suo — confirmed present, no append needed)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared intake seam both stories depend on

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T002 [P] Add `IFilePicker.PickMultipleAsync` with default impl over `PickAsync` + `DummyFilePicker` empty-list override in `src/RAGGit.Client.Core/Services/IFilePicker.cs`
- [X] T003 [P] Add `UploadViewModel.AddPickedFilesAsync` validation loop + `ReportRejectedNames`/`ReportDropError` in `src/RAGGit.Client.Core/ViewModels/UploadViewModel.cs`

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - Multi-select picker pass (Priority: P1) 🎯 MVP

**Goal**: One picker pass queues multiple validated files

**Independent Test**: `PickFile_MultiSelect_QueuesAllInOnePass` — 3 files queued with names/sizes from a single `PickFileCommand` execution

### Tests for User Story 1 ⚠️

- [X] T004 [P] [US1] Unit tests for multi-select queueing, mixed valid/invalid rejection, and default-impl delegation in `tests/unit/UploadViewModelTests.cs` (`PickFile_MultiSelect_QueuesAllInOnePass`, `PickFile_MixedValidInvalid_QueuesValidNamesRejected`, `PickMultiple_DefaultImpl_DelegatesToSinglePick`)

### Implementation for User Story 1

- [X] T005 [US1] Route `PickFileCommand` through `PickMultipleAsync` + `AddPickedFilesAsync` with upload-time lock (`CanPick`, execute guard) in `src/RAGGit.Client.Core/ViewModels/UploadViewModel.cs`
- [X] T006 [P] [US1] Implement `WinUIFilePicker.PickMultipleAsync` via `FileOpenPicker.PickMultipleFilesAsync` in `src/RAGGit.Client.WinUI/Services/WinUIFilePicker.cs`

**Checkpoint**: At this point, User Story 1 should be fully functional and testable independently

---

## Phase 4: User Story 2 - Drag-and-drop target (Priority: P1)

**Goal**: OS file drops feed the same validated queue with hover affordance and upload-time lock

**Independent Test**: `AddPickedFiles_DropMixed_QueuesValidReportsInvalid` plus lock/hint test — drop conversion validated at the VM seam without platform UI

### Tests for User Story 2 ⚠️

- [X] T007 [P] [US2] Unit tests for drop intake, queue lock, and hint text in `tests/unit/UploadViewModelTests.cs` (`AddPickedFiles_DropMixed_QueuesValidReportsInvalid`, `PickFile_WhileUploading_BlockedAndNooped`)

### Implementation for User Story 2

- [X] T008 [US2] Add drop state (`IsDragOver`, `IsDropEnabled`, `DropHintText`) with upload-change notification in `src/RAGGit.Client.Core/ViewModels/UploadViewModel.cs`
- [X] T009 [P] [US2] Add drop-target Border with highlight overlay and bindings in `src/RAGGit.Client.WinUI/Views/UploadDialog.xaml`
- [X] T010 [US2] Add `DropArea_DragOver`/`DragLeave`/`Drop` handlers (StorageFile conversion, folder/virtual rejection, lock guards) in `src/RAGGit.Client.WinUI/Views/UploadDialog.xaml.cs`

**Checkpoint**: At this point, User Stories 1 AND 2 should both work independently

---

## Phase 5: User Story 3 - Mixed-intake queue (Priority: P2)

**Goal**: Picker and drop passes accumulate in one reviewable, removable queue

**Independent Test**: Queue 1 file via picker seam, add 2 more via drop seam, remove 1, upload rest — library shows exactly the remainder

### Tests for User Story 3 ⚠️

- [X] T011 [P] [US3] Unit test for empty intake no-op in `tests/unit/UploadViewModelTests.cs` (`AddPickedFiles_Empty_LeavesQueueUnchanged`; mixed accumulation covered by shared-seam tests + existing remove/queue tests)

### Implementation for User Story 3

- [X] T012 [US3] Verify combined queue/remove across passes via the shared `AddPickedFilesAsync` seam — no new code needed (existing `RemoveFileCommand`, counts, and status updates reused)

**Checkpoint**: All user stories should now be independently functional

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [X] T013 Full-solution `dotnet build` — 0 errors (1 XAML fix applied: `Border` has no `IsEnabled`; lock enforced behaviorally)
- [X] T014 Full unit suite 254/254 + `DocumentsContractTests` 13/13 regression check
- [X] T015 [P] Sync design docs to as-built (`research.md` R-05, `contracts/ui-contracts.md` C-01/C-03)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3+)**: All depend on Foundational phase completion
  - User stories can then proceed in parallel (if staffed)
  - Or sequentially in priority order (P1 → P1 → P2)
- **Polish (Final Phase)**: Depends on all desired user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational (Phase 2) - No dependencies on other stories
- **User Story 2 (P1)**: Can start after Foundational (Phase 2) - Shares the intake seam; independently testable at the VM seam
- **User Story 3 (P2)**: Can start after Foundational (Phase 2) - Emerges from the shared seam; no dedicated code

### Within Each User Story

- Tests written alongside implementation (Constitution VI); VM seam keeps all intake logic unit-testable without platform UI
- Shared seam before story-specific surface
- Story complete before moving to next priority

### Parallel Opportunities

- T002 and T003 in parallel (different files: `IFilePicker.cs` vs `UploadViewModel.cs`)
- T005 and T006 in parallel (different files: ViewModel vs WinUI picker)
- T007, T009, T010 in parallel (test file vs xaml vs code-behind); T008 sequential before T009/T010 bindings reference the state members

---

## Parallel Example: User Story 2

```bash
# Tests and view work together (different files, no shared edits):
Task: "Unit tests for drop intake, queue lock, and hint text in tests/unit/UploadViewModelTests.cs"
Task: "Drop-target Border with highlight overlay and bindings in src/RAGGit.Client.WinUI/Views/UploadDialog.xaml"
Task: "DropArea_DragOver/DragLeave/Drop handlers in src/RAGGit.Client.WinUI/Views/UploadDialog.xaml.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL - blocks all stories)
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: `PickFile_MultiSelect_QueuesAllInOnePass` green
5. Deploy/demo if ready

### Incremental Delivery

1. Complete Setup + Foundational → Foundation ready
2. Add User Story 1 → Test independently → Deploy/Demo (MVP!)
3. Add User Story 2 → Test independently → Deploy/Demo
4. Add User Story 3 → No new code; verify via shared seam → Deploy/Demo
5. Each story adds value without breaking previous stories

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- All tasks verified complete 2026-09-18: build 0 errors; unit 254/254 (26 upload incl. 6 new); contract upload regression 13/13
- No server, contract, or RBAC changes; offline invariant untouched
- quickstart.md Scenarios 1–5 remain the manual end-to-end validation guide (client launch required)
