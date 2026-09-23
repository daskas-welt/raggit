---

description: "Implementation tasks for the dashboard visual refresh"

---

# Tasks: Dashboard Visual Refresh

**Input**: Design documents from `/specs/019-dashboard-visual-refresh/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/dashboard-page.md](contracts/dashboard-page.md), [quickstart.md](quickstart.md)

**Tests**: Unit tests are included because the project constitution requires test-first development. WinUI rendering is validated through XAML compilation and manual keyboard, Narrator, theme, responsive, and virtualization checks because the repository has no dedicated UI automation project.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Establish the implementation baseline without changing application behavior.

- [ ] T001 Review `specs/019-dashboard-visual-refresh/research.md`, `data-model.md`, and `contracts/dashboard-page.md` against the current `src/RAGGit.Client.WinUI/` and `src/RAGGit.Client.Core/` layout; record any path or API-shape mismatch in `specs/019-dashboard-visual-refresh/plan.md`
- [ ] T002 [P] Capture the current WinUI baseline by running `dotnet build src\RAGGit.Client.WinUI\RAGGit.Client.WinUI.csproj -p:Platform=x64` and record the result in `specs/019-dashboard-visual-refresh/quickstart.md` only if the documented command or prerequisites are inaccurate

**Checkpoint**: The repository layout, current shell behavior, and design contract are confirmed before code changes.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Add shared theme and shell seams required by all user stories.

**CRITICAL**: Complete this phase before user-story implementation.

- [ ] T003 [P] Add purpose-based Light, Dark, and High Contrast resources for the navigation rail, selected navigation state, dashboard surface, metric tile, panel border, and dashboard status in `src/RAGGit.Client.WinUI/App.xaml`
- [ ] T004 [P] Add shared style resources for dashboard/page spacing, heading hierarchy, panel surfaces, and minimum interactive target sizing in `src/RAGGit.Client.WinUI/App.xaml` without replacing existing semantic status resources
- [ ] T005 Add a shared shell-level status and session-context seam in `src/RAGGit.Client.WinUI/MainWindow.xaml` and `src/RAGGit.Client.WinUI/MainWindow.xaml.cs` that preserves the existing configuration-error and session-expiry paths
- [ ] T006 Document the existing client dependency-injection registration point and shell-to-ViewModel composition boundary in `specs/019-dashboard-visual-refresh/contracts/dashboard-page.md` before adding the new dashboard type

**Checkpoint**: Theme resources, shared styles, shell status/session composition, and the documented ViewModel registration boundary are ready for story work.

---

## Phase 3: User Story 1 - See a clear work dashboard at a glance (Priority: P1) MVP

**Goal**: Authenticated employees land on a dashboard that communicates context, existing activity, and primary next actions in one readable desktop view.

**Independent Test**: Sign in with representative document/history data and verify Dashboard is the landing page, metric summaries reflect existing data, Ask/Library actions reach existing workflows, and empty data produces actionable empty states.

### Tests for User Story 1

> Write tests first and verify they fail before implementing the ViewModel behavior.

- [ ] T007 [P] [US1] Add failing unit tests for dashboard loading, existing-count mapping, bounded recent items, empty-state flags, busy gating, refresh failure, and session-expiry propagation in `tests/unit/DashboardViewModelTests.cs`
- [ ] T008 [P] [US1] Add failing unit tests proving unsupported aggregates are omitted and loaded page data does not fabricate whole-history trends in `tests/unit/DashboardViewModelTests.cs`

### Implementation for User Story 1

- [ ] T009 [US1] Implement transient dashboard summary state, source composition, explicit loading/error transitions, retry behavior, and duplicate-refresh gating in `src/RAGGit.Client.Core/ViewModels/DashboardViewModel.cs`
- [ ] T010 [US1] Create the dashboard layout with context header, metric tiles, recent-document panel, recent-query panel, ingestion/status summary, empty states, loading state, and accessible status surface in `src/RAGGit.Client.WinUI/Views/DashboardPage.xaml`
- [ ] T011 [US1] Implement dashboard page binding, refresh behavior, navigation callbacks, accessible names, stable Automation IDs (`DashboardRefreshButton`, `DashboardAskButton`, `DashboardLibraryButton`, `DashboardMetricDocuments`, `DashboardMetricQueries`), and focus behavior in `src/RAGGit.Client.WinUI/Views/DashboardPage.xaml.cs`
- [ ] T012 [US1] Add the Dashboard navigation item and map its route while preserving existing destination tags and Automation IDs in `src/RAGGit.Client.WinUI/MainWindow.xaml`
- [ ] T013 [US1] Change authenticated startup and post-login navigation from Library to Dashboard while retaining direct Library navigation and admin visibility gating in `src/RAGGit.Client.WinUI/MainWindow.xaml.cs` and `src/RAGGit.Client.WinUI/Views/LoginPage.xaml.cs`
- [ ] T014 [US1] Register and bind `DashboardViewModel` through the existing client dependency-injection pattern, expose existing session context only, and avoid introducing username/avatar data absent from `src/RAGGit.Client.WinUI/App.xaml.cs`, `src/RAGGit.Client.WinUI/Views/DashboardPage.xaml`, and `src/RAGGit.Client.WinUI/Views/DashboardPage.xaml.cs`

**Checkpoint**: US1 is independently demonstrable with populated and empty data, and all new dashboard unit tests pass.

---

## Phase 4: User Story 2 - Move through the app with a consistent visual system (Priority: P1)

**Goal**: Every existing authenticated destination shares the reference-inspired shell, selected navigation treatment, page spacing, and responsive behavior.

**Independent Test**: Navigate through Dashboard, Library, Ask, History, My Docs, and Admin where permitted by mouse and keyboard at desktop and <=720px widths; verify selection, focus, reflow, and permission visibility.

### Implementation for User Story 2

- [ ] T015 [US2] Apply the semantic blue rail, selected/hover/focus states, user/session area, and compact/top navigation behavior at the 720px breakpoint in `src/RAGGit.Client.WinUI/MainWindow.xaml` and `src/RAGGit.Client.WinUI/MainWindow.xaml.cs`
- [ ] T016 [P] [US2] Align the header, panel, spacing, empty-state, and action styling with shared resources while preserving existing behavior and Automation IDs in `src/RAGGit.Client.WinUI/Views/LibraryPage.xaml`
- [ ] T017 [P] [US2] Align the header, chat surface, suggestion area, and page status styling with shared resources while preserving query behavior and citations in `src/RAGGit.Client.WinUI/Views/QueryPage.xaml`
- [ ] T018 [P] [US2] Align headers, list surfaces, load-more controls, and empty states with shared resources without wrapping virtualized lists in an outer `ScrollViewer` in `src/RAGGit.Client.WinUI/Views/HistoryPage.xaml` and `src/RAGGit.Client.WinUI/Views/DocumentsMinePage.xaml`
- [ ] T019 [P] [US2] Align the admin and query-detail headers/panels with shared resources while preserving admin gating, confirmation flows, selectable answers, citations, and existing Automation IDs in `src/RAGGit.Client.WinUI/Views/AdminUsersPage.xaml` and `src/RAGGit.Client.WinUI/Views/QueryDetailPage.xaml`
- [ ] T020 [US2] Verify every navigation item has a meaningful icon, accessible name, stable Automation ID, visible selected/focus state, and correct employee/admin visibility in `src/RAGGit.Client.WinUI/MainWindow.xaml` and `src/RAGGit.Client.WinUI/MainWindow.xaml.cs`

**Checkpoint**: US2 is independently demonstrable across all existing destinations and responsive widths without behavior or permission regressions.

---

## Phase 5: User Story 3 - Read dense information without losing trust (Priority: P2)

**Goal**: Refreshed visual hierarchy improves scanning while preserving status, error, citation, and empty-state trust signals.

**Independent Test**: Exercise loading, success, error, empty, session-expired, and citation-bearing states in Light, Dark, and High Contrast; verify the outcome is visible, correctly labeled, and never obscures grounded answers or citations.

### Implementation for User Story 3

- [ ] T021 [P] [US3] Update dashboard status, loading, failure, empty, and retry presentation to use accessible live announcements and semantic severity resources in `src/RAGGit.Client.WinUI/Views/DashboardPage.xaml` and `src/RAGGit.Client.Core/ViewModels/DashboardViewModel.cs`
- [ ] T022 [P] [US3] Preserve and visually prioritize answer text, citation lists, copy actions, and query status while applying shared surfaces in `src/RAGGit.Client.WinUI/Components/ChatControl.xaml` and `src/RAGGit.Client.WinUI/Views/QueryDetailPage.xaml`
- [ ] T023 [P] [US3] Verify status chips, document ingestion states, and empty/error outcomes remain distinguishable without color-only meaning in `src/RAGGit.Client.WinUI/Components/StatusChip.xaml`, `src/RAGGit.Client.WinUI/Views/LibraryPage.xaml`, and `src/RAGGit.Client.WinUI/Views/DocumentsMinePage.xaml`
- [ ] T024 [US3] Add or preserve accessible names, live settings, focus restoration, and non-color status text for all new dashboard metrics and actions in `src/RAGGit.Client.WinUI/Views/DashboardPage.xaml` and `src/RAGGit.Client.WinUI/Views/DashboardPage.xaml.cs`

**Checkpoint**: US3 is independently demonstrable across all required semantic states and themes, with grounded answer/citation prominence intact.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Validate the full feature against the quickstart, constitution, and visual/accessibility requirements.

- [ ] T025 [P] Run focused dashboard and existing client unit tests, including `tests/unit/DashboardViewModelTests.cs`, `LibraryPagingTests`, `HistoryViewModelTests`, `DocumentsMineViewModelTests`, `QueryViewModelTests`, `ClientRoleGatingTests`, and citation/status tests; fix regressions without weakening assertions
- [ ] T026 [P] Build the WinUI client and verify XAML compilation with `dotnet build src\RAGGit.Client.WinUI\RAGGit.Client.WinUI.csproj -p:Platform=x64`; resolve binding, resource, or platform errors in the touched XAML/code-behind files
- [ ] T027 Run the contract, integration, and offline regression commands from `specs/019-dashboard-visual-refresh/quickstart.md` and confirm no API, RBAC, WAN, upload, or citation behavior changed
- [ ] T028 Perform the complete manual keyboard, Narrator, Light/Dark/High Contrast, 720px responsive, 100+ collection, employee/admin, empty/error/loading, and citation walkthrough from `specs/019-dashboard-visual-refresh/quickstart.md`; record any findings in that file
- [ ] T029 Run `dotnet tool restore` and `dotnet csharpier check .`; correct formatting in all changed C# and XAML files
- [ ] T030 Review the final diff for hard-coded feature colors, broken existing Automation IDs, nested `ScrollViewer` collection wrappers, new HTTP/API files, unsupported metrics, and accidental changes outside `src/RAGGit.Client.Core/`, `src/RAGGit.Client.WinUI/`, `tests/unit/`, and `specs/019-dashboard-visual-refresh/`

**Checkpoint**: The feature is ready for implementation review when all automated gates pass and the manual walkthrough has no unresolved findings.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies; confirms the current project baseline.
- **Foundational (Phase 2)**: Depends on Setup; blocks all story work because theme, shell, and ViewModel registration seams are shared.
- **User Story 1 (Phase 3)**: Depends on Foundational; MVP dashboard can be implemented and validated independently.
- **User Story 2 (Phase 4)**: Depends on Foundational and the Dashboard route from US1 for the complete shell destination set; page styling tasks can run in parallel after shared resources exist.
- **User Story 3 (Phase 5)**: Depends on the dashboard/page surfaces from US1 and US2; state and accessibility polishing can run in parallel by surface.
- **Polish (Phase 6)**: Depends on all selected user stories.

### User Story Dependencies

- **User Story 1 (P1)**: Starts after Phase 2; no dependency on US2 or US3.
- **User Story 2 (P1)**: Requires the Dashboard route from US1 but can otherwise proceed in parallel with US1's ViewModel/page work after shell resources are available.
- **User Story 3 (P2)**: Requires the surfaces produced by US1/US2; it does not add new data or API behavior.

### Within Each User Story

- Tests are written before the corresponding implementation and must fail before implementation begins.
- ViewModel/data state precedes XAML bindings.
- Shell route changes precede manual navigation validation.
- Shared resources precede page-level visual alignment.
- Each checkpoint must pass before moving to the next story.

## Parallel Execution Examples

### User Story 1

```text
Task T007: DashboardViewModel loading/count tests in tests/unit/DashboardViewModelTests.cs
Task T008: DashboardViewModel unsupported-aggregate tests in tests/unit/DashboardViewModelTests.cs
```

After the tests fail, these can proceed in parallel where files do not overlap:

```text
Task T010: DashboardPage.xaml layout
Task T012: MainWindow.xaml Dashboard route item
Task T013: MainWindow.xaml.cs and LoginPage.xaml.cs startup routing
```

### User Story 2

```text
Task T016: LibraryPage.xaml visual alignment
Task T017: QueryPage.xaml visual alignment
Task T018: HistoryPage.xaml and DocumentsMinePage.xaml visual alignment
Task T019: AdminUsersPage.xaml and QueryDetailPage.xaml visual alignment
```

### User Story 3

```text
Task T021: Dashboard status and retry state
Task T022: ChatControl and QueryDetail trust presentation
Task T023: StatusChip and document-state presentation
```

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1 baseline confirmation.
2. Complete Phase 2 shared resources and registration seams.
3. Write and fail the US1 ViewModel tests.
4. Implement the dashboard ViewModel, page, route, and authenticated landing change.
5. Run the US1 unit tests and the manual populated/empty dashboard walkthrough.
6. Stop for an MVP demo before expanding the shell restyle.

### Incremental Delivery

1. Add US1: dashboard landing and existing-data summaries.
2. Add US2: consistent navigation rail, page surfaces, and responsive shell behavior.
3. Add US3: trust-preserving status, citation, accessibility, and theme polish.
4. Run Phase 6 regression and manual validation gates.

### Parallel Team Strategy

1. Complete Setup and Foundational together.
2. Assign one contributor to US1 ViewModel/dashboard, one to existing-page alignment for US2, and one to trust-state/accessibility review for US3 after the shared resources are stable.
3. Integrate each story at its checkpoint and run the focused tests before proceeding.

## Notes

- `[P]` means tasks touch different files and have no incomplete prerequisite dependency.
- `[US1]`, `[US2]`, and `[US3]` map directly to the prioritized stories in `spec.md`.
- Existing navigation Automation IDs and citation/status semantics are compatibility constraints, not optional cleanup.
- No server, API contract, retrieval, generation, tenant, storage, or upload workflow task is included because FR-011 makes those changes out of scope.
