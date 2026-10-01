---

description: "Task list for Dashboard Redesign — Work overview"
---

# Tasks: Dashboard Redesign — "Work overview"

**Input**: Design documents from `/specs/027-dashboard-redesign/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/ui-contracts.md,
quickstart.md

**Tests**: No new automated tests — presentation-only re-binding of existing view-model
values with no behavior change (same basis as sibling client features 019–026).
Verification is the 023 static audits, the `winapp ui` walkthrough in quickstart.md, and
the existing unit/contract/offline-integration suites.

**Organization**: Tasks are grouped by the four user stories in spec.md. All XAML work
touches `src/RAGGit.Client.WPF/Views/Pages/DashboardPage.xaml` (and its code-behind), so
implementation tasks are sequential — no `[P]` markers on same-file work.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: US1–US4 from spec.md
- Repository-relative paths are used throughout.

## Path Conventions

- WPF page: `src/RAGGit.Client.WPF/Views/Pages/DashboardPage.xaml(.cs)`
- Shared view model (UNCHANGED): `src/RAGGit.Client.Core/ViewModels/DashboardViewModel.cs`
- Feature docs: `specs/027-dashboard-redesign/`

---

## Phase 1: Setup

**Purpose**: Record the current Dashboard layout and frozen IDs before changing anything.

- [ ] T001 Inspect `src/RAGGit.Client.WPF/Views/Pages/DashboardPage.xaml` and record the
  current hero, tiles, metric strip, panels, and all 11 frozen Dashboard automation IDs
  in `specs/027-dashboard-redesign/tasks.md` (Implementation Record baseline)

---

## Phase 2: Foundational

**Purpose**: No new project, package, schema, contract, or shared framework is required.
Finish T001 before story work so visual and ID regressions have a recorded baseline.

**Checkpoint**: Baseline recorded; story phases may proceed in order below.

---

## Phase 3: User Story 1 - Read the library at a glance (Priority: P1) 🎯 MVP

**Goal**: A compact header plus six navigating metric cards; last-known numbers survive
a failed refresh.

**Independent Test**: With mixed-state documents and saved questions, verify all six
cards show the right counts and land on their destinations; fail a refresh and verify
last-known numbers remain beside the error.

- [ ] T002 [US1] Replace the hero banner in
  `src/RAGGit.Client.WPF/Views/Pages/DashboardPage.xaml` with the compact header (title
  with `PageTitleText`, supporting line with `PageSubtitleText`, existing
  `DashboardProfileCard`, `DashboardRefreshButton`, labeled History/MyDocs/Admin actions
  keeping `DashboardTileHistory`/`DashboardTileMyDocs`/`DashboardTileAdmin`; Admin bound
  to existing admin visibility)
- [ ] T003 [US1] Replace the tiles and inline metric strip in
  `src/RAGGit.Client.WPF/Views/Pages/DashboardPage.xaml` with six Fluent metric cards
  bound to the existing view-model counts (`DashboardMetricDocuments` and
  `DashboardMetricQueries` keep their IDs/destinations; add `DashboardMetricReady`,
  `DashboardMetricIndexing`, `DashboardMetricFailed`, `DashboardMetricMine`) wired to
  the existing navigation handlers in
  `src/RAGGit.Client.WPF/Views/Pages/DashboardPage.xaml.cs`
- [ ] T004 [US1] Verify US1 via `specs/027-dashboard-redesign/quickstart.md` (header
  walkthrough steps 1–3: header, six cards + destinations, failed-refresh
  last-known-good) and record evidence in
  `specs/027-dashboard-redesign/tasks.md`

**Checkpoint**: Header and metric cards work independently; panels remain as-is until US2.

---

## Phase 4: User Story 2 - Scan recent activity in two panels (Priority: P2)

**Goal**: Recent-documents and recent-questions panels as real Fluent cards that stack at
narrow widths, each with its own empty state.

**Independent Test**: With and without panel content, verify content and empty states at
a wide width and at 800×600, confirming the stacked layout at ≤720 DIPs content width.

- [ ] T005 [US2] Replace the two hand-rolled Border panels in
  `src/RAGGit.Client.WPF/Views/Pages/DashboardPage.xaml` with real `ui:Card` controls
  keeping the existing recent-documents/questions bindings, `DashboardLibraryButton`,
  `DashboardAskButton`, and both per-panel empty states
- [ ] T006 [US2] Implement page-owned side-by-side→stacked switching at ≤720 DIPs
  content width in `src/RAGGit.Client.WPF/Views/Pages/DashboardPage.xaml.cs`, leaving
  all existing handlers and the shared view model unchanged
- [ ] T007 [US2] Verify US2 via `specs/027-dashboard-redesign/quickstart.md`
  (walkthrough step 4: wide/stacked panels, per-panel empty states) and record evidence
  in `specs/027-dashboard-redesign/tasks.md`

**Checkpoint**: Panels adapt independently; the whole-dashboard empty state follows in US3.

---

## Phase 5: User Story 3 - Start from an empty library (Priority: P1)

**Goal**: One whole-dashboard empty state with an "Open the library" action, shown only
when the library is empty and no saved questions exist.

**Independent Test**: Fresh library with no saved questions shows the single empty
state whose action opens the library; adding one document hides it.

- [ ] T008 [US3] Add the whole-dashboard empty state with its "Open the library" action
  (wired to the existing library navigation) in
  `src/RAGGit.Client.WPF/Views/Pages/DashboardPage.xaml`, shown only when the existing
  empty-library AND empty-history conditions both hold
- [ ] T009 [US3] Verify US3 via `specs/027-dashboard-redesign/quickstart.md`
  (walkthrough step 5: empty state shown, action opens library, disappears with content)
  and record evidence in `specs/027-dashboard-redesign/tasks.md`

**Checkpoint**: First-run guidance works independently of populated-library flows.

---

## Phase 6: User Story 4 - Operate accessibly in any theme (Priority: P2)

**Goal**: Frozen IDs intact, keyboard/screen-reader operability, and Light/Dark/High
Contrast legibility across the redesigned page.

**Independent Test**: Complete header-action, card, and panel flows by keyboard and
screen reader in Light/Dark/High Contrast at 800×600 and a wide size.

- [ ] T010 [US4] Audit and fix keyboard order, visible focus, per-control accessible
  names, tooltips, ≥44-DIP targets, and theme-token-only styling in
  `src/RAGGit.Client.WPF/Views/Pages/DashboardPage.xaml`; confirm all 11 frozen IDs
  remain exactly present alongside the four new metric-card IDs
- [ ] T011 [US4] Verify US4 via `specs/027-dashboard-redesign/quickstart.md`
  (walkthrough step 6: keyboard, screen reader, three themes, both sizes) and record
  screenshots and any residual limitation in
  `specs/027-dashboard-redesign/tasks.md`

**Checkpoint**: The redesigned page is operable by every user in every supported theme.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Prove the contract, scope, and regression gates; record the implementation.

- [ ] T012 Run the 023 design audits from
  `specs/023-design-system-refinement/quickstart.md` over
  `src/RAGGit.Client.WPF/Views/Pages/DashboardPage.xaml`: valid icon/theme keys, no
  hard-coded colors or `Opacity=`, no ad-hoc `ui:TextBlock FontSize`, frozen ID set
- [ ] T013 Run `dotnet tool restore`, `dotnet csharpier check .`, `dotnet build
  RAGGit.sln -c Release -p:Platform=x64`, all unit/contract suites, and the offline
  integration subset; record results in `specs/027-dashboard-redesign/tasks.md`
- [ ] T014 Write the Implementation Record and Caveats in
  `specs/027-dashboard-redesign/tasks.md`: baseline summary, changed files,
  frozen-ID/new-ID audit, last-known-good confirmation, UI captures, gates, and any
  remaining limitation

## Implementation Record and Caveats

### Baseline (T001)

- _To be recorded: current hero, five tiles, inline metric strip, two Border panels,
  and the 11 frozen Dashboard automation IDs._

### Delivered

- _To be recorded on implementation._

### Verification

- _To be recorded: csharpier, build, unit/contract/offline-integration results, design
  audits, walkthrough captures._

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: T001 records the baseline; no code dependency.
- **Foundational (Phase 2)**: No shared infrastructure prerequisite; T001 gates visual
  comparisons.
- **US1 (Phase 3)**: Can start after T001; establishes the header and metric cards.
- **US2 (Phase 4)**: Depends on US1's page shell; converts the panels and adds stacking.
- **US3 (Phase 5)**: Depends on US1 counts and US2 panels to define the fully-empty
  condition.
- **US4 (Phase 6)**: After all UI stories; validates the integrated page.
- **Polish (Phase 7)**: After all desired stories.

### Within-Story / File Dependencies

- US1: T002 header → T003 cards → T004 walkthrough (same XAML file, sequential).
- US2: T005 panel cards → T006 stacking code-behind → T007 walkthrough.
- US3: T008 empty state → T009 walkthrough.
- US4: T010 audit/fixes → T011 walkthrough.
- Same-file work is sequential throughout: US1 edits precede US2, then US3, then US4.

### Parallel Opportunities

- None for implementation: every implementation task edits `DashboardPage.xaml` or its
  code-behind and must run sequentially. Verification tasks (T004, T007, T009, T011) and
  polish audits (T012) can be prepared in parallel but executed against the final page.

---

## Parallel Example: Verification Preparation

```text
Task: "Prepare US1/US2/US3 walkthrough captures per specs/027-dashboard-redesign/quickstart.md"
Task: "Prepare 023 static-audit commands per specs/023-design-system-refinement/quickstart.md"
```

(Note: execution against the page remains sequential; only preparation parallelizes.)

---

## Implementation Strategy

### MVP First

1. Complete T001 baseline.
2. Complete US1 (T002–T004): compact header, six navigating cards, last-known-good.
3. Validate counts and destinations before moving to panels.

### Incremental Delivery

1. US1 → glanceable numbers with destinations.
2. US2 → responsive recent-activity panels.
3. US3 → first-run empty state.
4. US4 + Polish → accessibility/theme coverage and full regression gates.

---

## Notes

- Presentation only: bind the existing view-model values; do not modify
  `src/RAGGit.Client.Core/ViewModels/DashboardViewModel.cs`, services, API, contracts,
  storage, or retrieval.
- Preserve all 11 existing Dashboard automation IDs with unchanged meaning; the four
  new metric cards use `DashboardMetricReady`, `DashboardMetricIndexing`,
  `DashboardMetricFailed`, `DashboardMetricMine`.
- Fluent (`ui:*`) controls only; theme tokens only; shared text styles
  (`PageTitleText`, `PageSubtitleText`, `SecondaryText`, `EmptyStateGlyph`).
