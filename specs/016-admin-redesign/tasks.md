# Tasks: Admin Page Redesign

**Input**: Design documents from `/specs/016-admin-redesign/` (spec.md, plan.md, research.md, data-model.md, contracts/admin-page.md, quickstart.md)

**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Included — constitution VI mandates test-first (red-green-refactor); admin ViewModel tests are the primary gate.

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

**Purpose**: Baseline before any admin work

- [ ] T001 Verify baseline `dotnet build src\RAGGit.Client.WinUI\RAGGit.Client.WinUI.csproj -p:Platform=x64` succeeds with 0 errors
- [ ] T002 Verify baseline admin tests pass via `dotnet test tests\unit\RAGGit.Tests.Unit.csproj --filter "FullyQualifiedName~Admin"`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Status/severity presentation state + converters that ALL user stories build on

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [ ] T003 Add `StatusMessage`/`StatusSeverity`/`HasStatus` + `ClearStatus`/`SetSuccess`/`SetWarning`/`SetError` helpers (keeping `ErrorMessage` in sync) in `src/RAGGit.Client.Core/ViewModels/AdminUsersViewModel.cs`
- [ ] T004 [P] Add row-label converters (`RoleActionLabel`, `ActiveVerb`, `ActiveToggleLabel`, `LockedLabel`, `LockStatusLabel`, `InverseBool`) and always-a-line `SelectedUserLabel` in `src/RAGGit.Client.WinUI/Converters/ViewConverters.cs`
- [ ] T005 Set a success confirmation on every admin command (load/create/role/active/reset) with field-naming validation failures and value preservation in `src/RAGGit.Client.Core/ViewModels/AdminUsersViewModel.cs` (depends on T003)

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - Organized people-management page (Priority: P1) ⭐ MVP

**Goal**: Constrained-column layout with readable user list (role/active/lock per row with actions), create form, and reset form; confirms preserved; list refreshes after each action.

**Independent Test**: As admin, open Admin → change a role, toggle active, create an employee, reset a password — each succeeds with a clear outcome and the list reflects it; layout holds a readable column at desktop width.

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T006 [P] [US1] Status/severity state tests (helpers, `HasStatus`, compat `ErrorMessage` sync) in `tests/unit/AdminUsersStatusTests.cs`
- [ ] T007 [P] [US1] Per-command confirmation tests (load/create/role/active/reset set success) in `tests/unit/AdminUsersStatusTests.cs`

### Implementation for User Story 1

- [ ] T008 [US1] Build constrained-column layout (root Grid MaxWidth=1040, Header/InfoBar/List/Forms rows) in `src/RAGGit.Client.WinUI/Views/AdminUsersPage.xaml` (depends on T006)
- [ ] T009 [US1] Build virtualized users `ListView` (own scroll, MinHeight=240, empty state) with labeled role/active row buttons + display-only lock in `src/RAGGit.Client.WinUI/Views/AdminUsersPage.xaml` (depends on T004, T008)
- [ ] T010 [US1] Build Create Person + Reset Password `Border` cards (`PropertyChanged` bindings, 44px busy-gated buttons, kept `ContentDialog` confirms) in `src/RAGGit.Client.WinUI/Views/AdminUsersPage.xaml` (depends on T008)
- [ ] T011 [US1] Wire selection-driven reset target + list refresh after each action in `src/RAGGit.Client.WinUI/Views/AdminUsersPage.xaml.cs` (depends on T009, T010)

**Checkpoint**: At this point, User Story 1 should be fully functional and testable independently

---

## Phase 4: User Story 2 - Outcomes always visible (Priority: P2)

**Goal**: Dedicated `InfoBar` status (severity + polite announcements), busy overlay, no silent outcomes, no duplicate submits, field-naming failures.

**Independent Test**: Duplicate-username create → prominent conflict message with values preserved; reset with no selection → selection-required message; refresh → visible loading with gated buttons; every success shows a confirmation.

### Tests for User Story 2

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T012 [P] [US2] Failure-path tests (conflict message, no-selection message, value preservation) in `tests/unit/AdminUsersStatusTests.cs`

### Implementation for User Story 2

- [ ] T013 [US2] Add status `InfoBar` (severity via `StatusSeverityConverter`, non-closable, `LiveSetting="Polite"`, `AutomationId="AdminStatusBar"`) + list `ProgressRing` overlay in `src/RAGGit.Client.WinUI/Views/AdminUsersPage.xaml` (depends on T003, T008)
- [ ] T014 [US2] Gate all action buttons on `IsBusy` in `src/RAGGit.Client.WinUI/Views/AdminUsersPage.xaml` (depends on T010, T012)

**Checkpoint**: At this point, User Stories 1 AND 2 should both work independently

---

## Phase 5: User Story 3 - Keyboard and screen-reader operability (Priority: P2)

**Goal**: Full keyboard reachability with visible focus, per-user action announcements, Light/Dark/HighContrast legibility, ≤720px reflow.

**Independent Test**: Keyboard-only walkthrough with zero unreachable controls; Narrator announces row actions per user plus outcomes; theme check passes; narrow window stacks forms with nothing clipped.

### Tests for User Story 3

- [ ] T015 [P] [US3] Verify per-row announcement strings (role/active names include username) and selection-hint label in `src/RAGGit.Client.WinUI/Converters/ViewConverters.cs` and their `AutomationProperties` bindings in `src/RAGGit.Client.WinUI/Views/AdminUsersPage.xaml`
- [ ] T016 [P] [US3] Verify `AutomationId`s, `LiveSetting="Polite"`, and 44px targets in `src/RAGGit.Client.WinUI/Views/AdminUsersPage.xaml`

### Implementation for User Story 3

- [ ] T017 [US3] Add `VisualStateManager`/`AdaptiveTrigger` narrow-window form stacking in `src/RAGGit.Client.WinUI/Views/AdminUsersPage.xaml` (depends on T008, T016)
- [ ] T018 [US3] Confirm Esc dismisses confirms and focus order is logical across header/rows/forms in `src/RAGGit.Client.WinUI/Views/AdminUsersPage.xaml.cs` (depends on T011)

**Checkpoint**: All user stories should now be independently functional

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Conformance checks and validation across all stories

- [ ] T019 [P] Run `csharpier check` on touched `.cs` files and format if needed (`src/RAGGit.Client.Core/ViewModels/AdminUsersViewModel.cs`, `src/RAGGit.Client.WinUI/Converters/ViewConverters.cs`, `tests/unit/AdminUsersStatusTests.cs`)
- [ ] T020 [P] Audit touched XAML for zero hard-coded color literals and no `ScrollViewer`-wrapped users list in `src/RAGGit.Client.WinUI/Views/AdminUsersPage.xaml`
- [ ] T021 Run full admin test filter plus `dotnet build` (0 warnings/0 errors) per `specs/016-admin-redesign/quickstart.md`
- [ ] T022 Run `specs/016-admin-redesign/quickstart.md` manual validation (walkthrough + a11y + themes, including the non-admin gating check per FR-011) and record results

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
- **User Story 2 (P2)**: Can start after Foundational (Phase 2) - Applies status/busy surfaces onto US1 layout; independently testable via failure scenarios
- **User Story 3 (P2)**: Can start after Foundational (Phase 2) - Applies a11y/theme attributes onto US1/US2 surfaces; independently testable via keyboard/theme walkthrough

### Within Each User Story

- Tests MUST be written and FAIL before implementation
- Models/state before XAML bindings
- Core ViewModel behavior before page code-behind
- Story complete before moving to next priority

### Parallel Opportunities

- T001 + T002 (baseline checks, independent commands)
- T004 (converters, separate file) with T003/T005 (ViewModel)
- T006 + T007 (same new test file — coordinate edits to avoid conflicts)
- T015 + T016 (read-only audits, safe in parallel)
- T019 + T020 (different files, independent)

---

## Parallel Example: User Story 1

```bash
# Launch test tasks for User Story 1 together (coordinate edits to the same test file):
Task: "Status/severity state tests in tests/unit/AdminUsersStatusTests.cs"
Task: "Per-command confirmation tests in tests/unit/AdminUsersStatusTests.cs"

# Converter (separate file) alongside ViewModel state work:
Task: "Add row-label converters in src/RAGGit.Client.WinUI/Converters/ViewConverters.cs"
Task: "Add status/severity state in src/RAGGit.Client.Core/ViewModels/AdminUsersViewModel.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL - blocks all stories)
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: Test User Story 1 independently (role change + create + reset with list refresh)
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

**Purpose**: Remaining gap found by `/speckit.converge` assessment of the code against spec.md, plan.md, and tasks.md (2026-09-18). All other requirements verified present in code: constrained column, virtualized list outside any ScrollViewer, labeled row actions, InfoBar status with polite announcements, busy gating, VSM narrow states, theme-only brushes, build 0/0, admin tests green.

- [X] T023 Record the quickstart.md §3–§4 manual validation results (walkthrough, keyboard/Narrator, Light-Dark-HighContrast, ≤720px, non-admin gating per FR-011) per US3/AC1, US3/AC2, SC-001, SC-003, T022 — see verification.md (static audit pass; live Narrator audio/visual eyeball pending human)
