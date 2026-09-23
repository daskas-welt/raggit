---

description: "Implementation tasks for the Raggit branding refresh"

---

# Tasks: Raggit Branding Refresh

**Input**: Design documents from `/specs/020-raggit-branding-refresh/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/branding-shell.md](contracts/branding-shell.md), [quickstart.md](quickstart.md)

**Tests**: No new business-data unit tests are required because the feature is presentation-only. Existing build, unit, contract, integration, offline, formatting, and manual WinUI accessibility/theme checks are mandatory validation tasks.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Confirm the existing shell and resource keys before changing the branding surface.

- [ ] T001 Review `specs/020-raggit-branding-refresh/research.md`, `data-model.md`, and `contracts/branding-shell.md` against `src/RAGGit.Client.WinUI/App.xaml` and `src/RAGGit.Client.WinUI/MainWindow.xaml`; record any mismatch in `specs/020-raggit-branding-refresh/plan.md`
- [ ] T002 [P] Capture the current Release WinUI baseline with `dotnet build src\RAGGit.Client.WinUI\RAGGit.Client.WinUI.csproj -c Release -p:Platform=x64` and record any pre-existing lock or build issue in `specs/020-raggit-branding-refresh/quickstart.md`

**Checkpoint**: Existing rail resources, navigation IDs, route tags, and responsive shell behavior are confirmed.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Establish the semantic resource contract used by all branding stories.

**CRITICAL**: Complete this phase before user-story implementation.

- [ ] T003 [P] Define or refine Light, Dark, and High Contrast semantic brand-name and navigation-rail resources, including `BrandNameForeground`, in `src/RAGGit.Client.WinUI/App.xaml`
- [ ] T004 [P] Define or correct `NavigationViewExpandedPaneBackground`, `NavigationViewDefaultPaneBackground`, and `NavigationViewTopPaneBackground` mappings for the branded rail in `src/RAGGit.Client.WinUI/App.xaml`
- [ ] T005 Verify the resource contract uses system brushes for High Contrast and does not introduce hard-coded feature colors at usage sites in `src/RAGGit.Client.WinUI/App.xaml` and `src/RAGGit.Client.WinUI/MainWindow.xaml`

**Checkpoint**: Theme resources support the branded rail and product label in all supported modes.

---

## Phase 3: User Story 1 - Recognize Raggit immediately (Priority: P1) MVP

**Goal**: Show `Raggit` above the existing navigation destinations and make the Light shell match the reference hierarchy.

**Independent Test**: Launch an authenticated desktop session and verify the exact `Raggit` label, distinct blue rail, light workspace, stable selected state, and unchanged navigation behavior.

### Implementation for User Story 1

- [ ] T006 [US1] Add a non-interactive `PaneHeader` containing the exact text `Raggit`, accessible name `Product name Raggit`, and semantic foreground resource in `src/RAGGit.Client.WinUI/MainWindow.xaml`
- [ ] T007 [US1] Change the `NavigationView` shell background to `{ThemeResource NavigationRailBackground}` while preserving its existing foreground, menu items, route tags, and Automation IDs in `src/RAGGit.Client.WinUI/MainWindow.xaml`
- [ ] T008 [US1] Apply the reference-inspired Light rail/workspace hierarchy through semantic resources without altering dashboard/profile content in `src/RAGGit.Client.WinUI/App.xaml` and `src/RAGGit.Client.WinUI/Views/DashboardPage.xaml`
- [ ] T009 [US1] Verify that pane-header branding does not participate in selection changes or route navigation in `src/RAGGit.Client.WinUI/MainWindow.xaml.cs`

**Checkpoint**: US1 is independently demonstrable in Light mode with `Raggit` visible and all current routes intact.

---

## Phase 4: User Story 2 - Use the branded shell in every supported theme and size (Priority: P1)

**Goal**: Make the product name and branded rail legible in Light, Dark, High Contrast, and responsive Top navigation modes.

**Independent Test**: Perform keyboard/Narrator walkthroughs in all three themes at desktop and exactly 720px-or-narrower widths; verify product name, labels, selected state, focus, and navigation reachability.

### Implementation for User Story 2

- [ ] T010 [P] [US2] Add or refine Dark-theme rail, product-name, selected-state, and workspace values for readable contrast in `src/RAGGit.Client.WinUI/App.xaml`
- [ ] T011 [P] [US2] Map High Contrast rail, product-name, selected-state, border, and workspace resources to Windows system brushes in `src/RAGGit.Client.WinUI/App.xaml`
- [ ] T012 [US2] Preserve the existing 720px `NavigationViewPaneDisplayMode.Top` switch and confirm the pane-header branding remains visible/reachable in `src/RAGGit.Client.WinUI/MainWindow.xaml.cs` and `src/RAGGit.Client.WinUI/MainWindow.xaml`
- [ ] T013 [US2] Validate product-name, navigation-label, selected-state, and focus layout at desktop and <=720px widths without changing existing Automation IDs in `src/RAGGit.Client.WinUI/MainWindow.xaml`

**Checkpoint**: US2 is independently demonstrable across themes, keyboard navigation, Narrator, and narrow widths.

---

## Phase 5: User Story 3 - Keep existing work intact while the appearance changes (Priority: P2)

**Goal**: Prove branding does not change workflows, permissions, trust states, or collection behavior.

**Independent Test**: Exercise all destinations and representative loading, empty, error, upload, citation, session, configuration, and role-gated states after branding changes.

### Implementation and validation for User Story 3

- [ ] T014 [P] [US3] Review existing navigation IDs, route tags, role gating, and employee/admin visibility for regressions in `src/RAGGit.Client.WinUI/MainWindow.xaml` and `src/RAGGit.Client.WinUI/MainWindow.xaml.cs`
- [ ] T015 [P] [US3] Review Dashboard, profile card, Library, Ask, History, My Docs, Admin, and Query Detail surfaces for unchanged content and status semantics in `src/RAGGit.Client.WinUI/Views/` and `src/RAGGit.Client.WinUI/Components/`
- [ ] T016 [US3] Validate configuration-error, session-expiry, upload, grounded-answer/citation, loading, empty, and error states against the branded shell using `specs/020-raggit-branding-refresh/quickstart.md`
- [ ] T017 [US3] Validate that existing virtualized collections retain list-owned scrolling and no page-level `ScrollViewer` is introduced in `src/RAGGit.Client.WinUI/Views/` and `src/RAGGit.Client.WinUI/Components/`

**Checkpoint**: US3 is independently validated with no functional or trust-model regression.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Run the complete implementation gates and record the final review.

- [ ] T018 [P] Run the full unit, contract, integration, and offline regression commands from `specs/020-raggit-branding-refresh/quickstart.md`
- [ ] T019 [P] Build the WinUI client in Release mode with `src\RAGGit.Client.WinUI\RAGGit.Client.WinUI.csproj` and resolve any XAML/resource compiler errors
- [ ] T020 Run `dotnet tool restore` and `dotnet csharpier check .` for all changed files in `src/RAGGit.Client.WinUI/`
- [ ] T021 Perform the complete Light/Dark/High Contrast, keyboard/Narrator, desktop/720px, employee/admin, configuration/session, upload, citation, and collection walkthrough from `specs/020-raggit-branding-refresh/quickstart.md`; record findings there
- [ ] T022 Review the final diff for changed routes, removed Automation IDs, hard-coded usage-site colors, accidental data/API changes, and files outside `src/RAGGit.Client.WinUI/` and `specs/020-raggit-branding-refresh/`

**Checkpoint**: The branding refresh is ready for review when all automated gates pass and the manual walkthrough has no unresolved findings.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies.
- **Foundational (Phase 2)**: Depends on Setup and blocks all story work.
- **User Story 1 (Phase 3)**: Depends on semantic resources from Phase 2; MVP.
- **User Story 2 (Phase 4)**: Depends on the shell branding surface from US1; resource tasks can run in parallel after Phase 2.
- **User Story 3 (Phase 5)**: Depends on the branded shell from US1/US2 and validates unchanged workflows.
- **Polish (Phase 6)**: Depends on all selected stories.

### User Story Dependencies

- **User Story 1 (P1)**: Starts after Phase 2; no dependency on later stories.
- **User Story 2 (P1)**: Depends on US1's pane-header and shell binding; theme resource tasks can proceed in parallel with US1 page work after Phase 2.
- **User Story 3 (P2)**: Depends on US1 and US2; it adds no new data or API behavior.

### Parallel Opportunities

```text
Task T003: Light/Dark/High Contrast semantic resource definitions in App.xaml
Task T004: NavigationView pane-background resource mappings in App.xaml

Task T010: Dark theme values in App.xaml
Task T011: High Contrast values in App.xaml

Task T014: Navigation and role-gating review
Task T015: Page/component behavior review
```

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Setup and Foundational phases.
2. Add the `Raggit` pane header and correct the Light navigation rail binding.
3. Build the WinUI client and manually verify the desktop Light-theme shell.
4. Stop for an MVP review before theme and regression polish.

### Incremental Delivery

1. Add US1: visible Raggit identity and reference-inspired Light rail.
2. Add US2: Dark/High Contrast and narrow-window support.
3. Add US3: workflow, accessibility, permission, and virtualization regression validation.
4. Complete Phase 6 automated and manual gates.

## Notes

- `[P]` means tasks touch different files or validation surfaces and have no incomplete prerequisite dependency.
- `[US1]`, `[US2]`, and `[US3]` map directly to the stories in `spec.md`.
- This feature must not add server/API, retrieval, generation, tenant, storage, or upload behavior.
