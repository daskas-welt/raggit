# Tasks: File Upload UI Refresh

**Input**: Design documents from `/specs/015-file-upload-ui/` (spec.md + Session 2026-09-18 clarifications, plan.md, research.md, data-model.md, contracts/upload-queue.md, quickstart.md)

**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Included — constitution VI mandates test-first (red-green-refactor); upload ViewModel tests are the primary gate.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

- WinUI client: `src/RAGGit.Client.WinUI/`
- Shared behavior library: `src/RAGGit.Client.Core/`
- Unit tests: `tests/unit/`

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Baseline before any upload work

- [ ] T001 Verify baseline `dotnet build src\RAGGit.Client.WinUI\RAGGit.Client.WinUI.csproj -p:Platform=x64` succeeds with 0 errors
- [ ] T002 Verify baseline upload tests pass via `dotnet test tests\unit\RAGGit.Tests.Unit.csproj --filter "FullyQualifiedName~UploadViewModelTests"`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Queue data model + ViewModel state that ALL user stories build on

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [ ] T003 [US1] Add `UploadQueueItem` entity (FileName/Stream/ContentType/SizeBytes/Progress/State/ErrorMessage/StateLabel/FileMetaText) in `src/RAGGit.Client.Core/ViewModels/UploadViewModel.cs`
- [ ] T004 [US1] Add queue state (`Queue`, `HasFiles`, `TotalCount`, `CompletedCount`, `CompletedCountText`, `StatusSeverity`, `IsAllSucceeded`, `SupportedTypesText`) in `src/RAGGit.Client.Core/ViewModels/UploadViewModel.cs` (depends on T003)
- [ ] T005 [P] Add `StatusSeverityConverter` in `src/RAGGit.Client.WinUI/Converters/DocumentConverters.cs`
- [ ] T006 Add pick-time validation (extension allow-list + `DocumentValidation.MaxFileSizeBytes`) with `RejectionMessage`/`HasRejection` in `src/RAGGit.Client.Core/ViewModels/UploadViewModel.cs` (depends on T004)

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - Admin uploads with clear progress and outcome (Priority: P1) ⭐ MVP

**Goal**: Admin picks files from the Library upload dialog, watches per-file progress plus overall count, and gets an unmistakable success outcome with auto-close and list refresh.

**Independent Test**: As admin, open upload → queue 2 valid files → start → per-file bars advance, count ticks "1 of 2 done" → "2 of 2 done" → success shown → dialog auto-closes → both rows appear in the library. Repeat with a simulated failure → dialog stays open with the reason and next step.

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T007 [P] [US1] Queue/gating tests (start disabled when empty, enabled with entries, duplicate-start blocked) in `tests/unit/UploadViewModelTests.cs`
- [ ] T008 [P] [US1] Sequential-run success tests (per-file progress, `CompletedCountText`, `IsAllSucceeded`, legacy status strings preserved) in `tests/unit/UploadViewModelTests.cs`

### Implementation for User Story 1

- [ ] T009 [US1] Implement sequential queue runner (upload → mark → continue, rewind streams for retry) in `src/RAGGit.Client.Core/ViewModels/UploadViewModel.cs` (depends on T004, T007, T008)
- [ ] T010 [US1] Build queue `ListView` UI (virtualized, per-row icon/meta/progress, `SymbolIcon` Add/Upload actions, `{ThemeResource}`-only brushes) in `src/RAGGit.Client.WinUI/Views/UploadDialog.xaml` (depends on T003)
- [ ] T011 [US1] Add summary `InfoBar` bound to `StatusMessage`/`StatusSeverity` via `StatusSeverityConverter` in `src/RAGGit.Client.WinUI/Views/UploadDialog.xaml` (depends on T005, T009)
- [ ] T012 [US1] Implement auto-close on `IsAllSucceeded` (~900 ms perceivable success) + focus Add-files on open in `src/RAGGit.Client.WinUI/Views/UploadDialog.xaml.cs` (depends on T009)
- [ ] T013 [US1] Refresh library documents on dialog close so new rows appear in `src/RAGGit.Client.WinUI/Views/LibraryPage.xaml.cs` (depends on T012)

**Checkpoint**: At this point, User Story 1 should be fully functional and testable independently

---

## Phase 4: User Story 2 - State at a glance and cancel (Priority: P2)

**Goal**: Admin sees each queued file (name + size), can remove/replace before starting, gets immediate pick-time rejection messages, and can cancel mid-upload with a clear per-file outcome; partial successes refresh the library with only successes.

**Independent Test**: Queue a file → verify name/size + remove option → remove it → start stays disabled. Queue 2 files → start → cancel mid-run → "cancelled" outcome, dialog stays open, library unchanged. Queue 2 files with one forced failure → per-file outcomes shown, library shows only the success.

### Tests for User Story 2

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T014 [P] [US2] Rejection tests (unsupported type/oversize blocked at pick, message names file, queue unchanged, start still gated) in `tests/unit/UploadViewModelTests.cs`
- [ ] T015 [P] [US2] Partial-success/cancel tests (failure marks item and continues, cancel marks item, successes-only outcome) in `tests/unit/UploadViewModelTests.cs`

### Implementation for User Story 2

- [ ] T016 [US2] Add per-row remove handling (`RemoveQueueItemCommand`) in `src/RAGGit.Client.Core/ViewModels/UploadViewModel.cs` (depends on T004, T014)
- [ ] T017 [US2] Add warning `InfoBar` for `RejectionMessage` + per-row inline errors in `StatusErrorBrush` in `src/RAGGit.Client.WinUI/Views/UploadDialog.xaml` (depends on T006, T015)
- [ ] T018 [US2] Cancel in-flight upload on Cancel button and on dialog `Closing` in `src/RAGGit.Client.WinUI/Views/UploadDialog.xaml.cs` (depends on T009, T015)
- [ ] T019 [US2] Keep dialog open on any failure/cancel with per-file outcomes; refresh library with successes only in `src/RAGGit.Client.WinUI/Views/LibraryPage.xaml.cs` (depends on T013, T015)

**Checkpoint**: At this point, User Stories 1 AND 2 should both work independently

---

## Phase 5: User Story 3 - Keyboard and screen-reader operability (Priority: P2)

**Goal**: Every control reachable by keyboard with visible focus, Esc handling, screen-reader announcements for open/progress/outcome/close with focus return, and Light/Dark/HighContrast legibility.

**Independent Test**: Full keyboard walkthrough (Tab/Enter/Esc) with zero unreachable controls; Narrator announces open, queue additions, progress milestones, outcomes, and close with focus back on the Library Upload button; Light/Dark/HighContrast visual check passes including ≤720px width.

### Tests for User Story 3

- [ ] T020 [P] [US3] Verify `AutomationId` on every interactive control and logical tab order in `src/RAGGit.Client.WinUI/Views/UploadDialog.xaml`
- [ ] T021 [P] [US3] Verify `LiveSetting="Polite"` on both `InfoBar` surfaces in `src/RAGGit.Client.WinUI/Views/UploadDialog.xaml`

### Implementation for User Story 3

- [ ] T022 [US3] Set ≥44px targets, 14/13/12 type ramp, 4/8/12 spacing, and `AutomationProperties` names in `src/RAGGit.Client.WinUI/Views/UploadDialog.xaml` (depends on T010, T020)
- [ ] T023 [US3] Return focus to the Library Upload button on dialog close in `src/RAGGit.Client.WinUI/Views/LibraryPage.xaml.cs` (depends on T013)

**Checkpoint**: All user stories should now be independently functional

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Conformance checks and validation across all stories

- [ ] T024 [P] Run `csharpier check` on touched `.cs` files and format if needed (`src/RAGGit.Client.Core/ViewModels/UploadViewModel.cs`, `src/RAGGit.Client.WinUI/Views/UploadDialog.xaml.cs`, `src/RAGGit.Client.WinUI/Converters/DocumentConverters.cs`, `tests/unit/UploadViewModelTests.cs`)
- [ ] T025 [P] Audit touched XAML for zero hard-coded color literals and no `ScrollViewer`-wrapped collection controls in `src/RAGGit.Client.WinUI/Views/UploadDialog.xaml`
- [ ] T026 Run full upload test filter plus `dotnet build` (0 warnings/0 errors) per `specs/015-file-upload-ui/quickstart.md`
- [ ] T027 Run `specs/015-file-upload-ui/quickstart.md` manual validation (walkthrough + a11y + themes) and record results

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3+)**: All depend on Foundational phase completion
  - User stories can then proceed in parallel (if staffed)
  - Or sequentially in priority order (P1 → P2 → P2)
- **Polish (Final Phase)**: Depends on all desired user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational (Phase 2) - No dependencies on other stories
- **User Story 2 (P2)**: Can start after Foundational (Phase 2) - Extends US1 queue/progress surfaces; independently testable via rejection/partial-success scenarios
- **User Story 3 (P2)**: Can start after Foundational (Phase 2) - Applies a11y/theme attributes onto US1/US2 surfaces; independently testable via keyboard/theme walkthrough

### Within Each User Story

- Tests MUST be written and FAIL before implementation
- Models/state before XAML bindings
- Core ViewModel behavior before dialog code-behind
- Story complete before moving to next priority

### Parallel Opportunities

- T001 + T002 (baseline checks, independent commands)
- T005 (converter, separate file) with T003/T004/T006 (ViewModel)
- T007 + T008 (both append to `tests/unit/UploadViewModelTests.cs` — same file, coordinate to avoid conflicts; parallel only with care)
- T014 + T015 (same-file caveat as above)
- T020 + T021 (both read `UploadDialog.xaml` — read-only audits, safe in parallel)
- T024 + T025 (different files, independent)

---

## Parallel Example: User Story 1

```bash
# Launch test tasks for User Story 1 together (coordinate edits to the same test file):
Task: "Queue/gating tests in tests/unit/UploadViewModelTests.cs"
Task: "Sequential-run success tests in tests/unit/UploadViewModelTests.cs"

# Converter (separate file) alongside ViewModel entity work:
Task: "Add StatusSeverityConverter in src/RAGGit.Client.WinUI/Converters/DocumentConverters.cs"
Task: "Add UploadQueueItem entity in src/RAGGit.Client.Core/ViewModels/UploadViewModel.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL - blocks all stories)
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: Test User Story 1 independently (2-file queue → progress → auto-close → rows appear)
5. Deploy/demo if ready

### Incremental Delivery

1. Complete Setup + Foundational → Foundation ready
2. Add User Story 1 → Test independently → Deploy/Demo (MVP!)
3. Add User Story 2 → Test independently → Deploy/Demo
4. Add User Story 3 → Test independently → Deploy/Demo
5. Each story adds value without breaking previous stories

### Parallel Team Strategy

With multiple developers:

1. Team completes Setup + Foundational together
2. Once Foundational is done:
   - Developer A: User Story 1
   - Developer B: User Story 2
   - Developer C: User Story 3
3. Stories complete and integrate independently

---

## Notes

- [P] tasks = different files, no dependencies (same-file test tasks need coordination)
- [Story] label maps task to specific user story for traceability
- Each user story is independently completable and testable
- Verify tests fail before implementing
- Constitution VI: TDD red-green-refactor is non-negotiable; ≥80% library coverage, 100% contracts
- Stop at any checkpoint to validate story independently
- Avoid: vague tasks, same-file conflicts, cross-story dependencies that break independence

---

## Phase 7: Convergence

**Purpose**: Remaining gaps found by `/speckit.converge` assessment of the code against spec.md, plan.md, and tasks.md (2026-09-18). All other requirements verified present: queue state, pick-time rejection, per-file progress + count, InfoBar surfaces, auto-close/refresh, cancel semantics, admin gating, theming, build 0/0, 18/18 upload tests.

- [X] T028 Record the quickstart.md §4 keyboard/Narrator/Light-Dark-HighContrast walkthrough results per US3/AC1, US3/AC2, SC-004 — see verification.md (static audit pass; live Narrator audio/visual eyeball pending human)
- [X] T029 Reconcile contracts/upload-queue.md `RemoveQueueItemCommand` name with the implemented `RemoveFileCommand` in `src/RAGGit.Client.Core/ViewModels/UploadViewModel.cs` per contracts/upload-queue.md (partial)
