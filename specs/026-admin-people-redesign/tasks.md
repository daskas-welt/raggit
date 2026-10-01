---

description: "Task list for Admin People Management Redesign"
---

# Tasks: Admin People Management Redesign

**Input**: Design documents from `/specs/026-admin-people-redesign/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/admin-page.md, quickstart.md

**Tests**: Required for the server-side last-admin invariant and the explicit role command per the
constitution's TDD rule. Write those tests first and confirm they fail before implementation. No new
WPF unit-test project; UI behavior is verified by the documented UI Automation/manual walkthrough.

**Organization**: Tasks are grouped by the six user stories in spec.md. US5 (last-admin protection) is
implemented before US2 because the row actions must not ship without the server guard.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: US1–US6 from spec.md
- Repository-relative paths are used throughout.

## Path Conventions

- WPF client: `src/RAGGit.Client.WPF/`
- Shared client ViewModels: `src/RAGGit.Client.Core/`
- Domain/persistence/API: `src/RAGGit.Core/`, `src/RAGGit.Workstation.Api/`
- Existing identity API contract: `specs/004-identity/contracts/api.yaml`
- Feature docs: `specs/026-admin-people-redesign/`
- Tests: `tests/unit/`, `tests/contract/`, `tests/integration/`

---

## Phase 1: Setup

**Purpose**: Capture the Admin screen and frozen IDs before changing the layout.

- [X] T001 Run baseline `dotnet csharpier check .` and `dotnet build RAGGit.sln -c Release -p:Platform=x64`; record the current Admin layout, screenshot path, 11 frozen IDs, and existing suite counts in `specs/026-admin-people-redesign/baseline.md`

---

## Phase 2: Foundational

**Purpose**: No new project, package, database table, or shared framework is required. Finish T001 before story work so visual and ID regressions have a recorded baseline.

**Checkpoint**: Baseline captured; story phases may proceed subject to their dependencies below.

---

## Phase 3: User Story 1 - Find and inspect a person (Priority: P1) 🎯 MVP

**Goal**: A searchable/filterable/sortable directory and selected-person detail panel that adapts to narrow widths.

**Independent Test**: With 100 accounts, locate a known username/display name, filter by role/state, sort a visible column, select a result, verify matching details, then check the 800×600 stacked layout.

- [X] T002 [US1] Replace the three-section Admin layout in `src/RAGGit.Client.WPF/Views/Pages/AdminUsersPage.xaml` with the header/actions, local search and filters, virtualized people list, selected-person detail panel, and separate Active/Disabled and Locked state displays; preserve the existing frozen automation IDs and keep the detail panel beside the list at wide widths
- [X] T003 [US1] Implement the page-owned `ICollectionView` in `src/RAGGit.Client.WPF/Views/Pages/AdminUsersPage.xaml.cs`: filter username/display name, role, and active state; sort requested columns; refresh/rebind when `ViewModel.Users` is replaced; clear selection/details when filters hide the selected account; stack details below the list at ≤720 DIPs
- [X] T004 [US1] Add the selected-account detail panel in `src/RAGGit.Client.WPF/Views/Pages/AdminUsersPage.xaml` with username, display name, role, active/lock state, created time, last sign-in when present, and a clear no-selection state
- [ ] T005 [US1] Verify US1 via `specs/026-admin-people-redesign/quickstart.md`: 100-row virtualized list, search/filter/sort, selection-to-detail consistency, no-results state, and responsive layout; capture evidence in `specs/026-admin-people-redesign/baseline.md`

**Checkpoint**: Directory navigation and details work independently; role and security actions remain unchanged until US5 is complete.

---

## Phase 4: User Story 5 - Preserve the last active admin (Priority: P1)

**Goal**: The workstation atomically refuses a patch that would remove the last active Admin, returns a user-safe 409, and allows the change when another active Admin remains.

**Independent Test**: Contract/integration tests prove single-admin demotion/deactivation is refused with unchanged storage, two-admin changes are allowed, and simultaneous removal attempts leave one active Admin.

### Tests first — these must fail before implementation

- [X] T006 [P] [US5] Add contract tests in `tests/contract/UsersPatchContractTests.cs` for sole-active-admin demotion and deactivation (409 Error body, persisted row unchanged), plus allowed removal when a second active Admin exists
- [X] T007 [P] [US5] Add a race integration test in `tests/integration/LastActiveAdminGuardTests.cs` that concurrently demotes/deactivates two active admins and asserts exactly one removal succeeds and at least one active Admin remains
- [X] T008 [P] [US5] Add `UsersApiClient` unit tests in `tests/unit/UsersApiClientTests.cs` proving a structured 409 `{ error: ... }` response is surfaced as the actionable server message rather than serialized JSON

### Implementation

- [X] T009 [US5] Add the transient patch outcome type (`Updated`, `NotFound`, `LastActiveAdmin`) in `src/RAGGit.Core/Models/UserPatchResult.cs` and the patch operation signature to `src/RAGGit.Core/Abstractions/Repositories/IUserRepository.cs`; do not add persisted fields
- [X] T010 [US5] Implement the guarded patch in `src/RAGGit.Core/Data/SqliteUserRepository.cs` as one serialized SQLite write transaction: reread current target, count other `Role=Admin && IsActive=true` accounts only when removing the target's active-admin state, apply only requested patch fields, and return the explicit outcome
- [X] T011 [US5] Update `UsersController.Patch` in `src/RAGGit.Workstation.Api/Controllers/UsersController.cs` to call the atomic repository operation, preserve existing validation/RBAC, return 404 for missing target, 409 with the standard Error body for last-admin refusal, and 200 for success
- [X] T012 [US5] Add the PATCH 409 response and last-active-admin description to `specs/004-identity/contracts/api.yaml`; advance the API contract and `src/RAGGit.Workstation.Api/RAGGit.Workstation.Api.csproj` version metadata to 1.4.0
- [X] T013 [US5] Implement structured conflict-body parsing in `src/RAGGit.Client.Core/Services/UsersApiClient.cs` so the Admin status surface receives the plain actionable `error` text for HTTP 409; keep other mapped errors unchanged
- [X] T014 [US5] Run the US5 tests from `tests/contract/UsersPatchContractTests.cs`, `tests/integration/LastActiveAdminGuardTests.cs`, and `tests/unit/UsersApiClientTests.cs`; confirm new cases pass and existing user PATCH cases remain unchanged

**Checkpoint**: The last-admin invariant is server-enforced and transaction-safe before row role/active controls are redesigned.

---

## Phase 5: User Story 2 - Change a person's role or active state safely (Priority: P1)

**Goal**: Role and active-state changes are explicit, state is separately legible, and self-actions warn before submission.

**Independent Test**: Cancel and confirm role/active changes; verify canceled choices revert, successful changes refresh row/detail, and self-actions show the access warning; server refusal leaves state unchanged.

### Tests first

- [X] T015 [P] [US2] Add unit tests in `tests/unit/AdminUsersViewModelTests.cs` for an explicit target-role command: it sends the requested role (not a role toggle), updates the collection on success, and preserves the account on failure

### Implementation

- [X] T016 [US2] Add an explicit `ChangeRoleToCommand` and typed request/result handling in `src/RAGGit.Client.Core/ViewModels/AdminUsersViewModel.cs`, retaining existing `ChangeRoleCommand` behavior until all callers are migrated
- [X] T017 [US2] Replace the role-flip row button in `src/RAGGit.Client.WPF/Views/Pages/AdminUsersPage.xaml` with a role ComboBox; render Active/Disabled chips, Locked only when true, and a separate Activate/Deactivate action; preserve row identity, status details, and frozen IDs
- [X] T018 [US2] Implement role selection confirmation/cancel restoration and self-demotion/deactivation warning in `src/RAGGit.Client.WPF/Views/Pages/AdminUsersPage.xaml.cs`; call the explicit role command only after confirmation and leave the server result authoritative
- [ ] T019 [US2] Run the US2 unit tests in `tests/unit/AdminUsersViewModelTests.cs` and verify role cancel/confirm, active cancel/confirm, self-warning, and server last-admin refusal from the UI

**Checkpoint**: Role and active actions communicate both current state and consequence without exposing an unguarded last-admin action.

---

## Phase 6: User Story 3 - Create a person in a focused dialog (Priority: P1)

**Goal**: Add person is a focused modal form; existing validation and failed drafts are preserved.

**Independent Test**: Open, cancel, fail validation, fail on duplicate, and successfully create; verify the list and status outcome each time.

- [X] T020 [US3] Create `src/RAGGit.Client.WPF/Views/Dialogs/CreatePersonDialog.xaml` with labeled Username, Display Name, Role, Password, Create, and Cancel controls; move the existing `NewUsernameBox`, `NewDisplayNameBox`, `NewRoleCombo`, `NewPasswordBox`, and `CreateUserButton` automation IDs with their fields/actions
- [X] T021 [US3] Implement `src/RAGGit.Client.WPF/Views/Dialogs/CreatePersonDialog.xaml.cs` as an owner-modal dialog bound to the existing `AdminUsersViewModel`; close only after successful `CreateUserCommand`, preserve draft on failure, and dismiss without request on cancel
- [X] T022 [US3] Replace the inline Create Person form with an Add person action that opens `CreatePersonDialog` in `src/RAGGit.Client.WPF/Views/Pages/AdminUsersPage.xaml` and `src/RAGGit.Client.WPF/Views/Pages/AdminUsersPage.xaml.cs`; keep the frozen form IDs in the dialog

**Checkpoint**: Create flow is independently testable and no longer occupies permanent page space.

---

## Phase 7: User Story 4 - Reset a person's password in a targeted dialog (Priority: P1)

**Goal**: Reset is a focused dialog whose target is unmistakable and whose existing confirmation/must-change behavior is preserved.

**Independent Test**: Open reset from a row/detail target, confirm the displayed username, cancel, submit invalid data, then complete a valid reset and verify the intended account changed.

- [X] T023 [P] [US4] Create `src/RAGGit.Client.WPF/Views/Dialogs/ResetPasswordDialog.xaml` with target identity, New Password, Must change password, Reset, and Cancel controls; move `ResetPasswordBox`, `ResetMustChangeCheck`, and `ResetPasswordButton` automation IDs with their controls
- [X] T024 [US4] Implement `src/RAGGit.Client.WPF/Views/Dialogs/ResetPasswordDialog.xaml.cs` to capture the target account by ID, reuse the existing reset command/confirmation, retain draft on failure, and close only after success
- [X] T025 [US4] Remove the inline Reset Password form and open `ResetPasswordDialog` from the row/detail action in `src/RAGGit.Client.WPF/Views/Pages/AdminUsersPage.xaml` and `src/RAGGit.Client.WPF/Views/Pages/AdminUsersPage.xaml.cs`; ensure a later selection change cannot retarget an already-open dialog

**Checkpoint**: Reset remains targeted to the displayed account and is independently testable.

---

## Phase 8: User Story 6 - Operate accessibly at supported sizes and themes (Priority: P2)

**Goal**: Preserve frozen IDs and make list, detail, filters, row actions, and dialogs usable by keyboard and assistive technology.

**Independent Test**: Complete search/filter/select/change/create/reset/cancel with keyboard and screen reader, in Light/Dark/High Contrast at 800×600 and wide desktop.

- [X] T026 [US6] Add stable automation IDs and accessible names to new search, role/status filters, detail panel, row role selector, and dialog launch/cancel controls in `src/RAGGit.Client.WPF/Views/Pages/AdminUsersPage.xaml` and both new dialog XAML files; confirm all 11 frozen IDs remain exactly present
- [ ] T027 [US6] Verify logical tab order, visible focus, per-user accessible action names, modal dismissal, High Contrast token use, and 44-DIP targets on `src/RAGGit.Client.WPF/Views/Pages/AdminUsersPage.xaml` and `src/RAGGit.Client.WPF/Views/Dialogs/`
- [ ] T028 [US6] Execute the narrow/wide and Light/Dark/High Contrast walkthrough in `specs/026-admin-people-redesign/quickstart.md`; record screenshots and any residual limitation in `specs/026-admin-people-redesign/baseline.md`

**Checkpoint**: All primary journeys remain operable in supported themes and sizes.

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: Prove the contract, scope, and regression gates; record the implementation.

- [X] T029 [P] Run the 023 design audits from `specs/023-design-system-refinement/quickstart.md` over `src/RAGGit.Client.WPF/`: valid icon/theme keys, no hard-coded colors or `Opacity=`, frozen automation IDs
- [X] T030 [P] Verify the last-admin OpenAPI 1.4.0 contract and all new contract/integration tests in `specs/004-identity/contracts/api.yaml`, `tests/contract/UsersPatchContractTests.cs`, and `tests/integration/LastActiveAdminGuardTests.cs`
- [X] T031 Run `dotnet tool restore`, `dotnet csharpier check .`, `dotnet build RAGGit.sln -c Release -p:Platform=x64`, all unit/contract suites, and the offline integration subset; record results in `specs/026-admin-people-redesign/tasks.md`
- [X] T032 Write the Implementation Record and Caveats in `specs/026-admin-people-redesign/tasks.md`: changed files, API version/409 behavior, frozen-ID audit, UI captures, gates, and any remaining limitation

## Implementation Record and Caveats

### Delivered

- Rebuilt the Admin page as a locally searchable/filterable/sortable, virtualized people directory with a selected-person detail panel. The panel stacks at the approved ≤720-DIP breakpoint. Role and active-state actions are separate; role changes and self-actions require confirmation.
- Added targeted owner-modal create/reset password dialogs. Reset keeps the selected account as its target and retains the existing confirmation and must-change option.
- Enforced the last-active-Admin invariant in a serialized SQLite write transaction. PATCH returns HTTP 409 with an actionable `error` body when the last active Admin would be demoted or deactivated; the client surfaces that message without transport-error wrapping. OpenAPI metadata is 1.4.0.
- Preserved all 11 frozen Admin automation IDs (each present once); added stable per-user role/action IDs, detail and dialog-cancel IDs, and accessible names.

### Verification

- `dotnet tool restore`: passed. `dotnet csharpier check .`: passed (249 files).
- `dotnet build RAGGit.sln -c Release -p:Platform=x64`: passed, 0 errors; one unrelated nullable warning in `tests/unit/XlsxWorkbookBuilder.cs:200`.
- Unit: 346 passed. Contract: 89 passed. Integration: 63 passed. Offline integration subset: 12 passed.
- Design audit: no invalid `SymbolRegular` or `ThemeResource` keys; zero hard-coded-color, `Opacity=`, or ad-hoc `ui:TextBlock FontSize` hits. Frozen IDs audited separately and each occurs once.
- Live smoke loaded the Admin page and both dialogs; search narrowed the four-account development directory to one result. Captures: `%LOCALAPPDATA%\Temp\opencode\walkthrough-026\admin-final.png`, `create-dialog.png`, and `reset-dialog.png`.

### Remaining manual verification

- The full 100-account interaction/performance walkthrough, final 800×600 and wide layout review, Light/Dark/High Contrast and keyboard/screen-reader walkthrough, and role/active confirmation flows were not completed. The listed smoke captures predate the final 720-DIP breakpoint and the follow-up review fixes; they are not current visual acceptance evidence. See unchecked tasks T005, T019, T027, and T028.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: T001 establishes baseline; no code dependency.
- **Foundational (Phase 2)**: No shared infrastructure or schema prerequisite; T001 gates visual comparisons.
- **US1 (Phase 3)**: Can start after T001; establishes the page shell/list/detail and view-only collection.
- **US5 (Phase 4)**: Can start after T001; must complete before US2 exposes role/active changes in the redesigned row.
- **US2 (Phase 5)**: Depends on US1's list/detail surfaces and US5's server guard.
- **US3/US4 (Phases 6/7)**: Depend on US1 page shell; each edits separate dialog files, but both integrate through the Admin page and existing view model.
- **US6 (Phase 8)**: After all UI stories; validates the integrated controls.
- **Polish (Phase 9)**: After all desired stories.

### Within-Story / File Dependencies

- US1: T002 XAML frame → T003 view/filter/responsive code-behind → T004 detail bindings → T005 walkthrough.
- US5: T006/T007/T008 tests first (independent files) → T009 result/interface → T010 SQLite transaction → T011 controller → T012 contract/version → T013 client error text → T014 verification.
- US2: T015 test → T016 explicit ViewModel command → T017 row UI → T018 confirmation/cancel wiring → T019 verification.
- US3: T020 dialog XAML → T021 dialog code-behind → T022 page integration.
- US4: T023 dialog XAML → T024 target-bound code-behind → T025 page integration.
- Same-file work is sequential: US1 T002–T004 precedes US2 T017/T018, then US3 T022 and US4 T025; US6 T026/T027 follow all of them.

### Parallel Opportunities

- T006, T007, and T008 are independent first-failing tests in different files.
- US1 presentation work and US5 test-first guard work touch disjoint files and can begin after baseline.
- US3 dialog XAML and US4 dialog XAML can be developed in parallel; their page integration remains sequential in `AdminUsersPage.xaml(.cs)`.
- Polish static audits and API contract review can run in parallel with UI walkthrough preparation.

---

## Parallel Example: Guard Tests Before Implementation

```text
Task: "T006 [P] [US5] PATCH contract refusal/allowed cases in tests/contract/UsersPatchContractTests.cs"
Task: "T007 [P] [US5] concurrent last-admin race test in tests/integration/LastActiveAdminGuardTests.cs"
Task: "T008 [P] [US5] structured 409 message test in tests/unit/UsersApiClientTests.cs"
```

---

## Implementation Strategy

### MVP First

1. Complete T001 baseline.
2. Complete US1 (T002–T005): directory, view filters, selected details, responsive layout.
3. Validate list browsing with 100 accounts and 800×600 before moving to management actions.

### Incremental Delivery

1. US1 → usable people directory and selected details.
2. US5 → atomic last-active-admin protection and documented 409.
3. US2 → confirmed role/active changes with clear state.
4. US3 → create dialog.
5. US4 → targeted reset dialog.
6. US6 + Polish → accessibility/theme coverage and full regression gates.

---

## Notes

- TDD applies to the API/repository invariant and explicit role command: tests must fail before their implementation tasks.
- Preserve all existing Admin automation IDs; moving an element to a dialog does not rename its ID.
- No user fields, storage tables, search endpoints, new projects, or packages are introduced.
- Update historical `016-admin-redesign` as superseded only if needed for maintainers; do not rewrite its historical record.
