---
description: "Task list for feature 028-pages-ux-polish"
---

# Tasks: All-Pages UX/UI Polish

**Input**: Design documents from `/specs/028-pages-ux-polish/`

**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Included for the four `RAGGit.Client.Core` affordance seams only (notification seam, pending-ask state, clear-conversation command, per-download busy) — constitution VI mandates test-first for behavior-layer code; XAML-only work is gated by the build, the static audits, and the walkthrough in quickstart.md.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1–US5)
- Include exact file paths in descriptions

## Path Conventions

Single solution: `src/RAGGit.Client.Core/`, `src/RAGGit.Client.WPF/`, `tests/unit/` at repository root (see plan.md Project Structure).

---

## Phase 1: Setup

**Purpose**: Confirm a green baseline before any change (SC-012 regression gate).

- [X] T001 Run the baseline gates from specs/028-pages-ux-polish/quickstart.md: `dotnet build RAGGit.sln -c Release -p:Platform=x64`, `dotnet csharpier check .`, and the unit/contract/offline-integration suites — all green before starting

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared styles/components and the notification seam every story depends on (FR-001 header, FR-002 status idiom, FR-006/FR-007 feedback, FR-020 spacing tokens).

**⚠ CRITICAL**: No user story work can begin until this phase is complete.

### Tests (write first, confirm red)

- [X] T002 [P] Write unit tests for the notification seam in tests/unit/NotificationServiceTests.cs: the in-memory double records shown notifications (title, message, kind); kind is Success | Information | Danger — red until the seam exists
- [X] T003 Run the new tests in tests/unit/NotificationServiceTests.cs and confirm they FAIL (constitution VI red gate)

### Implementation

- [X] T004 Implement `INotificationService` (`Show(title, message, kind)` with `NotificationKind` Success | Information | Danger) plus the in-memory test double in src/RAGGit.Client.Core/Services/INotificationService.cs (seam pattern of IDialogService/ILauncherService)
- [X] T005 Implement WpfNotificationService wrapping the already-wired WPF-UI `ISnackbarService` (kind → ControlAppearance mapping, ~5 s auto-dismiss) in src/RAGGit.Client.WPF/Services/WpfNotificationService.cs and register it as a singleton in src/RAGGit.Client.WPF/App.xaml.cs
- [X] T006 Add shared design-system resources to src/RAGGit.Client.WPF/App.xaml: Thickness resources on the documented 4-px scale (page padding `0,24`, header card `16,12`, card paddings, empty-state body margin `0,12,0,0`, dialog button MinWidths) and a `PageHeaderCard` style for the `ui:Card` header treatment
- [X] T007 Create the shared InfoBar-based StatusFooterControl (severity, wrapped message — never `TextTrimming`, optional retry command slot) in src/RAGGit.Client.WPF/Components/StatusFooterControl.xaml and StatusFooterControl.xaml.cs

**Checkpoint**: Foundation ready — user story implementation can now begin in parallel

---

## Phase 3: User Story 1 — Every page reads as one app (Priority: P1) ⭐ MVP

**Goal**: One header treatment, one status idiom with retry everywhere, one busy rule, one refresh affordance across all content pages (FR-001–FR-005, FR-024; SC-001, SC-002, SC-004).

**Independent Test**: Walk every content page (spec Story 1): header structure identical; a forced load failure shows the shared status treatment with a working retry on every async page; busy is in-control with content visible; the refresh affordance is presented identically.

### Implementation for User Story 1

- [X] T008 [P] [US1] Adopt the shared header card (PageHeaderCard style, Thickness resources) and StatusFooterControl in src/RAGGit.Client.WPF/Views/Pages/LibraryPage.xaml — replace the icon+text error footer, keep `LibraryRetryButton`, keep icon-only 44×44 refresh
- [X] T009 [P] [US1] Same for src/RAGGit.Client.WPF/Views/Pages/HistoryPage.xaml: shared header, StatusFooterControl, and a NEW `HistoryRetryButton` retry (previously absent); unify refresh to the app-wide icon-only 44×44 affordance (was labeled "Refresh")
- [X] T010 [P] [US1] Same for src/RAGGit.Client.WPF/Views/Pages/DocumentsMinePage.xaml: shared header, StatusFooterControl, NEW `DocumentsMineRetryButton`, icon-only refresh
- [X] T011 [P] [US1] Replace the hand-rolled Border status strip with StatusFooterControl plus NEW `AdminRetryButton` and apply the shared header card in src/RAGGit.Client.WPF/Views/Pages/AdminUsersPage.xaml (keep `AdminStatusBar` semantics on the new control)
- [X] T012 [P] [US1] Apply the shared header card to src/RAGGit.Client.WPF/Views/Pages/QueryPage.xaml (Padding `16,12`, actions area) and replace the bilingual suggestion-chip header with single-language plain copy (FR-021)
- [X] T013 [P] [US1] Apply the shared header card and `SectionHeaderText` section labels, give the citations empty state the standard `EmptyStateGlyph` treatment, and adopt StatusFooterControl in src/RAGGit.Client.WPF/Views/Pages/QueryDetailPage.xaml
- [X] T014 [P] [US1] Wrap the 027 compact header in the shared header-card structure and move `DashboardProfileCard` to a post-header row (library summary left, profile right) in src/RAGGit.Client.WPF/Views/Pages/DashboardPage.xaml — 027 metric/panel/empty-state content below unchanged; all 16 frozen Dashboard IDs preserved; InfoBar → StatusFooterControl (keep `DashboardStatusBar`, `DashboardRetryButton`)
- [X] T015 [P] [US1] Add the standard header card to src/RAGGit.Client.WPF/Views/Pages/SettingsPage.xaml (title, supporting line, actions area)
- [ ] T016 [US1] Unify action-initiated busy to the in-control idiom (icon→ProgressRing morph, control disabled) on refresh/load-more/retry buttons across src/RAGGit.Client.WPF/Views/Pages/{LibraryPage,HistoryPage,DocumentsMinePage,AdminUsersPage,DashboardPage}.xaml(.cs); initial page loads keep the centered treatment; remove any page-covering busy overlay on non-chat pages

**Checkpoint**: User Story 1 fully functional and independently testable (MVP deliverable)

---

## Phase 4: User Story 2 — Every action acknowledges itself (Priority: P1)

**Goal**: Copy/download confirmations, sign-out and cancel-mid-upload confirmations, in-window dialog conversion, no native message boxes (FR-006–FR-009, FR-025; SC-002, SC-003).

**Independent Test**: Perform each action (spec Story 2): every copy shows a transient confirmation; download shows in-flight/success/failure; sign-out and upload-cancel confirm first; both form dialogs render in-window; zero `MessageBox.Show` in the client.

### Tests (write first, confirm red)

- [ ] T017 [P] [US2] Write unit tests for per-download busy gating and outcome notifications in tests/unit/LibraryDownloadFeedbackTests.cs: the busy set contains exactly the in-flight document id; success/failure each raise one notification; other rows stay actionable — red until implemented

### Implementation for User Story 2

- [ ] T018 [US2] Add per-document download busy tracking and outcome notifications to `DownloadAndOpenAsync` in src/RAGGit.Client.Core/ViewModels/LibraryViewModel.cs (busy set; "Downloaded \<filename\>" success; failure keeps the existing ErrorMessage mapping + a danger notification)
- [ ] T019 [US2] Morph the Library row download button to a ProgressRing while that row's document downloads in src/RAGGit.Client.WPF/Views/Pages/LibraryPage.xaml (bind the row's busy state; keep AutomationProperties.Name "Download document")
- [ ] T020 [P] [US2] Confirm every copy affordance via INotificationService ("Copied"): chat message copy in src/RAGGit.Client.WPF/Components/ChatControl.xaml.cs and the prompt/answer/citation copies in src/RAGGit.Client.WPF/Views/Pages/QueryDetailPage.xaml.cs
- [ ] T021 [US2] Gate sign-out behind IDialogService.ConfirmAsync before the token store is cleared in src/RAGGit.Client.WPF/Views/Pages/SettingsPage.xaml.cs (declining leaves the session untouched; keep `SignOutButton`)
- [ ] T022 [US2] Convert CreatePersonDialog from a modal Window to an in-window ContentDialog shown through the existing IContentDialogService host (keep its ViewModel and behavior), updating src/RAGGit.Client.WPF/Views/Dialogs/CreatePersonDialog.xaml/.cs and the caller in src/RAGGit.Client.WPF/Views/Pages/AdminUsersPage.xaml.cs (keep `AddPersonButton` semantics)
- [ ] T023 [US2] Convert ResetPasswordDialog to an in-window ContentDialog and replace the raw MessageBox.Show with IDialogService.ConfirmAsync in src/RAGGit.Client.WPF/Views/Dialogs/ResetPasswordDialog.xaml/.cs and the caller in src/RAGGit.Client.WPF/Views/Pages/AdminUsersPage.xaml.cs (keep `OpenResetPasswordButton`)
- [ ] T024 [US2] Align UploadDialog chrome in src/RAGGit.Client.WPF/Views/Dialogs/UploadDialog.xaml/.cs: window icon, ResizeMode/ShowInTaskbar flags, shared dialog button MinWidths, and a cancel-mid-upload confirmation (declining resumes the upload view)

**Checkpoint**: User Stories 1 AND 2 both work independently

---

## Phase 5: User Story 3 — The Ask conversation feels finished (Priority: P2)

**Goal**: Auto-scroll, in-stream busy, reliable "Ask again", clear conversation (FR-010–FR-013; SC-006, SC-007).

**Independent Test**: Hold a 10+ exchange conversation — newest message always visible, conversation visible while answering; "Ask again" from History and QueryDetail populates the Ask page every time (never auto-sends); Clear empties the conversation and focuses input.

### Tests (write first, confirm red)

- [ ] T025 [P] [US3] Write unit tests for AskNavigationState in tests/unit/AskNavigationStateTests.cs: write → single read returns the prompt and clears it (null on every later read) — red until implemented
- [ ] T026 [P] [US3] Write unit tests for the clear-conversation command in tests/unit/QueryViewModelTests.cs (new cases, following the file's existing construction pattern): clears `Messages` and the conversation store's in-session messages; input state reset

### Implementation for User Story 3

- [ ] T027 [US3] Implement the AskNavigationState DI singleton (`string? PendingPrompt`) in src/RAGGit.Client.Core/Services/AskNavigationState.cs (mirroring QueryDetailNavigationState) and register it in src/RAGGit.Client.WPF/App.xaml.cs
- [ ] T028 [US3] Add the `[RelayCommand] ClearConversationCommand` to src/RAGGit.Client.Core/ViewModels/QueryViewModel.cs: clears `Messages` and the conversation store's in-session messages (the store's ReplaceAll bulk-clear), no new deletion API
- [ ] T029 [US3] Wire reliable "Ask again": HistoryPage and QueryDetailPage code-behind write `AskNavigationState.PendingPrompt` with the original question before navigating to Ask, and QueryPage's Loaded handler reads + clears it and populates `QueryText` (populate only, never auto-send) — replace the broken best-effort presets in src/RAGGit.Client.WPF/Views/Pages/HistoryPage.xaml.cs, QueryDetailPage.xaml.cs, and QueryPage.xaml.cs; keep `HistoryAskAgainButton` and `QueryDetailAskAgainButton`
- [ ] T030 [US3] Auto-scroll the messages list to the newest item on every collection change already observed in src/RAGGit.Client.WPF/Components/ChatControl.xaml.cs (`ScrollIntoView` on the UI-thread marshal path)
- [ ] T031 [US3] Remove the page-covering ProgressRing from src/RAGGit.Client.WPF/Views/Pages/QueryPage.xaml and deliver in-stream busy: send-button icon→ring morph plus a status row with the input bar in src/RAGGit.Client.WPF/Components/ChatControl.xaml (conversation stays visible while a question is in flight)
- [ ] T032 [US3] Add the Clear-conversation header action (`QueryClearConversationButton`) invoking ClearConversationCommand in src/RAGGit.Client.WPF/Views/Pages/QueryPage.xaml

**Checkpoint**: User Stories 1–3 all independently functional

---

## Phase 6: User Story 4 — Data surfaces work at any window size (Priority: P2)

**Goal**: 44-DIP row targets, graceful table degradation at ≤720 DIPs, wrapped status text, role-aware empty states (FR-014–FR-016, FR-019; SC-005, SC-008–SC-011).

**Independent Test**: At 800×600 with the nav pane open, the Library table shows Filename/Status/actions without horizontal scrolling; row buttons measure ≥44 DIPs; a non-admin on an empty library never sees upload guidance; long errors wrap.

### Implementation for User Story 4

- [ ] T033 [US4] Raise the Library row download/delete buttons from 36×32 to the 44×44 standard in src/RAGGit.Client.WPF/Views/Pages/LibraryPage.xaml (keep AutomationProperties.Name values)
- [ ] T034 [US4] Add the page-owned responsive switch to src/RAGGit.Client.WPF/Views/Pages/LibraryPage.xaml.cs: at content width ≤720 DIPs hide the Creator and Created columns and lower the 880 MinWidth floor so Filename/Status/actions fit without horizontal scrolling (SizeChanged pattern of DashboardPage/AdminUsersPage); restore all columns when wide — keep the shared ColumnDefinitions string consistent per state in src/RAGGit.Client.WPF/Views/Pages/LibraryPage.xaml
- [ ] T035 [US4] Make the Library empty state role-aware: expose the viewer's admin visibility on src/RAGGit.Client.Core/ViewModels/LibraryViewModel.cs and bind the empty-state hint in src/RAGGit.Client.WPF/Views/Pages/LibraryPage.xaml so non-admins get guidance referencing an action they can take (never "Upload a document")
- [ ] T036 [US4] Normalize remaining ad-hoc sizing to the shared scale (FR-020/SC-011): chat bubble MaxWidths and input sizing in src/RAGGit.Client.WPF/Components/ChatControl.xaml, and dialog footer button sizes in src/RAGGit.Client.WPF/Views/Dialogs/UploadDialog.xaml — reference the Thickness/style resources from T006 instead of literals

**Checkpoint**: User Stories 1–4 all independently functional

---

## Phase 7: User Story 5 — Login and Settings reach parity (Priority: P3)

**Goal**: Enter-to-submit, shared busy/error idioms on Login; Settings header, re-checkable connection status, copyable values (FR-017, FR-018).

**Independent Test**: Sign in using only the keyboard (Enter from either box); busy/error use the shared idioms; Settings shows the standard header, a re-check that refreshes the status row, and copyable URL/identity values.

### Implementation for User Story 5

- [ ] T037 [P] [US5] In src/RAGGit.Client.WPF/Views/Pages/LoginPage.xaml.cs and LoginPage.xaml: Enter in `UsernameBox` or `PasswordBox` submits sign-in (when not busy); replace the bare critical TextBlock with StatusFooterControl (NEW `LoginStatusMessage` id) and the inline ring below the button with the in-button busy morph (keep `UsernameBox`, `PasswordBox`, `SignInButton`)
- [ ] T038 [P] [US5] Add the connection re-check: make WpfConnectionState observable (message + last-checked moment) in src/RAGGit.Client.WPF/App.xaml.cs, add the `SettingsRecheckConnectionButton` action in src/RAGGit.Client.WPF/Views/Pages/SettingsPage.xaml.cs calling the existing `AuthApiClient.GetAuthMeAsync()`, with in-control busy and the status row re-rendering from the shared presentation (no new API surface)
- [ ] T039 [P] [US5] Add copy buttons with snackbar confirmation for the workstation URL and signed-in-as rows (`SettingsCopyWorkstationUrlButton`, `SettingsCopySignedInAsButton`) in src/RAGGit.Client.WPF/Views/Pages/SettingsPage.xaml(.cs)

**Checkpoint**: All five user stories independently functional

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Whole-feature verification (SC-012, FR-022, FR-024; quickstart.md).

- [ ] T040 [P] Run the extended static audits (quickstart.md items 1–10) over every touched file — no hard-coded colours, no `Opacity=`, no ad-hoc FontSize, valid icon/theme keys, zero `MessageBox.Show`, no `TextTrimming` on status text, ≥44-DIP row targets, all frozen IDs present, spacing from shared resources — fix any misses
- [ ] T041 Run `dotnet csharpier format .`, the full build, and all three suites from quickstart.md — everything green with zero unrelated assertion changes
- [ ] T042 Run the full interactive walkthrough (quickstart.md parts 1–8) at a wide size and 800×600, cycling Light/Dark/High Contrast, keyboard-through every new affordance
- [ ] T043 Record the walkthrough evidence against the quickstart.md expected-outcomes table in specs/028-pages-ux-polish/ (all U1–U8 rows pass)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately (green baseline)
- **Foundational (Phase 2)**: Depends on Phase 1 — **BLOCKS all user stories** (header style, StatusFooterControl, notification seam, spacing resources)
- **User Stories (Phases 3–7)**: All depend on Phase 2; can proceed in parallel (if staffed) or sequentially in priority order (US1 → US2 → US3 → US4 → US5)
- **Polish (Phase 8)**: Depends on all desired user stories being complete

### User Story Dependencies

- **US1 (P1)**: After Phase 2 — independent of other stories
- **US2 (P1)**: After Phase 2 — independent; T020 (chat copy confirmation) also satisfies US3's copy scenario
- **US3 (P2)**: After Phase 2 + T020 for its copy scenario; otherwise independent
- **US4 (P2)**: After Phase 2 — independent (T034 touches LibraryPage.xaml.cs, which T033/T035 also touch: run those three sequentially)
- **US5 (P3)**: After Phase 2 — independent

### Within Each User Story

- Tests (T002, T017, T025, T026) written and confirmed red before their implementations
- Seam/model tasks before the pages that consume them
- Core/VM tasks before XAML tasks that bind them
- Story complete and checkpoint-tested before moving to the next priority

### Parallel Opportunities

- T004/T006/T007 are parallelizable after T002–T003 (different files)
- T008–T015: all eight page-header/status tasks are parallel (different files) once Phase 2 is done
- T020/T021/T024 are parallelizable (different files); T022/T023 touch the same caller file — run sequentially
- T025/T026 are parallelizable test files
- T037/T038/T039 are parallelizable (different files)
- US1–US5 phases can run in parallel across developers after Phase 2

---

## Parallel Example: User Story 1

```text
# After Phase 2, launch the per-page header/status tasks together:
Task T008: "Adopt shared header + StatusFooterControl in src/RAGGit.Client.WPF/Views/Pages/LibraryPage.xaml"
Task T009: "Adopt shared header + StatusFooterControl + HistoryRetryButton in src/RAGGit.Client.WPF/Views/Pages/HistoryPage.xaml"
Task T010: "Adopt shared header + StatusFooterControl + DocumentsMineRetryButton in src/RAGGit.Client.WPF/Views/Pages/DocumentsMinePage.xaml"
Task T011: "Replace Admin status strip with StatusFooterControl + AdminRetryButton in src/RAGGit.Client.WPF/Views/Pages/AdminUsersPage.xaml"
Task T012: "Shared header + single-language chip copy in src/RAGGit.Client.WPF/Views/Pages/QueryPage.xaml"
Task T013: "Shared header + section labels + citations empty state in src/RAGGit.Client.WPF/Views/Pages/QueryDetailPage.xaml"
Task T014: "Shared header + profile row in src/RAGGit.Client.WPF/Views/Pages/DashboardPage.xaml"
Task T015: "Standard header card in src/RAGGit.Client.WPF/Views/Pages/SettingsPage.xaml"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (green baseline)
2. Complete Phase 2: Foundational (styles, StatusFooterControl, notification seam)
3. Complete Phase 3: User Story 1 — one header, one status idiom with retry, one busy rule
4. **STOP and VALIDATE**: Walk every page per the US1 independent test
5. Demo-ready: the app finally reads as one app

### Incremental Delivery

1. Setup + Foundational → foundation ready
2. + US1 → test independently → demo (MVP!)
3. + US2 → test independently → every action acknowledges itself
4. + US3 → test independently → the conversation feels finished
5. + US4 → test independently → data surfaces at any window size
6. + US5 → test independently → login/settings parity
7. Phase 8 polish → whole-feature gates and walkthrough evidence

### Parallel Team Strategy

1. Team completes Setup + Foundational together
2. Once Foundational is done: Developer A: US1; Developer B: US2; Developer C: US3+US4; Developer D: US5
3. Phase 8 polish after the stories integrate

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] labels map tasks to spec.md user stories for traceability
- Commit after each task or logical group; stop at any checkpoint to validate the story independently
- Every touched XAML file must be re-checked with `dotnet csharpier format .` (CSharpier formats XAML too)
- All 25 FRs are covered by T006–T039; the frozen-ID inventory and prohibited clauses live in specs/028-pages-ux-polish/contracts/ui-contracts.md (U8) — consult it while editing any page
- No task may change the workstation API, any contract, any persisted store, or any frozen automation ID (FR-022/FR-023)
