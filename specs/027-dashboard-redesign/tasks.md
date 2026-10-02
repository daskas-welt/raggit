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

- [x] T001 Inspect `src/RAGGit.Client.WPF/Views/Pages/DashboardPage.xaml` and record the
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

- [x] T002 [US1] Replace the hero banner in
  `src/RAGGit.Client.WPF/Views/Pages/DashboardPage.xaml` with the compact header (title
  with `PageTitleText`, supporting line with `PageSubtitleText`, existing
  `DashboardProfileCard`, `DashboardRefreshButton`, labeled History/MyDocs/Admin actions
  keeping `DashboardTileHistory`/`DashboardTileMyDocs`/`DashboardTileAdmin`; Admin bound
  to existing admin visibility)
- [x] T003 [US1] Replace the tiles and inline metric strip in
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

- [x] T005 [US2] Replace the two hand-rolled Border panels in
  `src/RAGGit.Client.WPF/Views/Pages/DashboardPage.xaml` with real `ui:Card` controls
  keeping the existing recent-documents/questions bindings, `DashboardLibraryButton`,
  `DashboardAskButton`, and both per-panel empty states
- [x] T006 [US2] Implement page-owned side-by-side→stacked switching at ≤720 DIPs
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

- [x] T008 [US3] Add the whole-dashboard empty state with its "Open the library" action
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

- [x] T010 [US4] Audit and fix keyboard order, visible focus, per-control accessible
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

- [x] T012 Run the 023 design audits from
  `specs/023-design-system-refinement/quickstart.md` over
  `src/RAGGit.Client.WPF/Views/Pages/DashboardPage.xaml`: valid icon/theme keys, no
  hard-coded colors or `Opacity=`, no ad-hoc `ui:TextBlock FontSize`, frozen ID set
- [x] T013 Run `dotnet tool restore`, `dotnet csharpier check .`, `dotnet build
  RAGGit.sln -c Release -p:Platform=x64`, all unit/contract suites, and the offline
  integration subset; record results in `specs/027-dashboard-redesign/tasks.md`
- [x] T014 Write the Implementation Record and Caveats in
  `specs/027-dashboard-redesign/tasks.md`: baseline summary, changed files,
  frozen-ID/new-ID audit, last-known-good confirmation, UI captures, gates, and any
  remaining limitation

## Implementation Record and Caveats

### Baseline (T001)

- Tall `HeroAccentGradientBrush` hero banner ("Welcome back" + supporting line +
  `RoleContext` + inline `·`-separated metric strip binding `TotalDocuments`,
  `TotalQueries`, `ReadyDocuments`, `MyDocuments`).
- Five `ui:CardAction` nav tiles: Library (`DashboardMetricDocuments` → library),
  Ask (`DashboardMetricQueries` → ask), History (`DashboardTileHistory`), My Docs
  (`DashboardTileMyDocs`), Admin (`DashboardTileAdmin`, admin-gated).
- Two hand-rolled `<Border>`-as-card panels (explicit comment: avoided `ui:Card`
  because its style centres content), fixed two-column `1.15*` / `*` grid.
- Status row: `ui:InfoBar` (`DashboardStatusBar`) + Retry `ui:Button`
  (`DashboardRetryButton`).
- All 11 frozen Dashboard automation IDs present: `DashboardProfileCard`,
  `DashboardRefreshButton`, `DashboardMetricDocuments`, `DashboardMetricQueries`,
  `DashboardLibraryButton`, `DashboardAskButton`, `DashboardStatusBar`,
  `DashboardRetryButton`, `DashboardTileHistory`, `DashboardTileMyDocs`,
  `DashboardTileAdmin`.

### Delivered

- Changed files (only these two):
  - `src/RAGGit.Client.WPF/Views/Pages/DashboardPage.xaml`
  - `src/RAGGit.Client.WPF/Views/Pages/DashboardPage.xaml.cs`
- Row 0: compact header in nested `WrapPanel`s — `PageTitleText` "Dashboard" +
  `PageSubtitleText` bound to `LibrarySummary`; labeled History / My Docs / Admin
  `ui:Button`s (icon + text, `MinHeight="44"`); unchanged `DashboardProfileCard`;
  unchanged refresh button pattern (`LoadCommand`, `InverseBooleanConverter`,
  `ArrowClockwise24` ↔ `ProgressRing` swap).
- Row 1: six `ui:CardAction` metric cards (fixed `Width="200"`, wrap; 44px glyph
  chip; count `FontTypography="Title"`; label `SecondaryText`;
  `IsChevronVisible="False"`; per-card `AutomationProperties.Name` with count via
  `StringFormat`, e.g. "{n} documents — open library"): Documents (`Library24` →
  library), Ready (`CheckmarkCircle24` → library), Indexing (`ArrowSyncCircle24`,
  accent → library), Failed (`ErrorCircle24`, critical when > 0 via
  `DataTrigger` on `FailedDocuments == 0`, accent otherwise → library),
  Questions (`Chat24` → ask), Mine (`Document24` → my docs).
- Row 2: whole-dashboard `ui:Card` empty state (`DashboardEmptyState`,
  `EmptyStateGlyph` + "No documents yet" + hint + primary "Open the library" →
  `OnLibraryClicked`), visible only when `HasLibraryData == False` AND
  `HasQueryData == False` (XAML `MultiDataTrigger`, no view-model change).
- Row 3: both panels are real `ui:Card`s (`DocumentsPanel` / `QuestionsPanel`,
  `HorizontalContentAlignment="Stretch"` + `VerticalContentAlignment="Stretch"`)
  in `PanelsGrid` (`1.15*` / `*` wide); the grid collapses via `MultiDataTrigger`
  when fully empty so it never coexists with the whole-dashboard empty state.
  All recent-documents/questions bindings, `DashboardLibraryButton`,
  `DashboardAskButton`, and per-panel empty states kept verbatim.
- Code-behind: existing `ViewModel`, constructor, `OnLoaded` (plus one
  `ApplyResponsiveLayout(DashboardLayout.ActualWidth)` call), and all
  `On*Clicked` handlers unchanged; added `_compactPanels` +
  `OnDashboardLayoutSizeChanged` + `ApplyResponsiveLayout` mirroring
  `AdminUsersPage` (stacked: second column `0`, `QuestionsPanel` to row 1/col 0;
  ≤720 DIPs content width).
- No `Client.Core`, API, contract, storage, or retrieval change.
- Frozen-ID audit: each of the 11 frozen IDs appears exactly once with unchanged
  navigation/meaning; new IDs `DashboardMetricReady`, `DashboardMetricIndexing`,
  `DashboardMetricFailed`, `DashboardMetricMine` (plus `DashboardEmptyState`)
  appear exactly once.
- Last-known-good: unchanged view-model behavior — failed `LoadAsync` sets only
  the error status and never clears counts; the page binds the same properties,
  so previously loaded numbers stay beside the error (confirmed by inspection;
  live failed-refresh is part of the remaining manual walkthrough below).
- Interpretation note: when fully empty the six metric cards remain visible
  (showing zeros, still navigating — zero is a displayable value per
  data-model.md) while only the two panels hide behind the whole-dashboard
  empty state, per the agreed layout ("The two panels are hidden in that
  state").

### Verification

- `dotnet tool restore`: ok. `dotnet csharpier check .`: clean (249 files).
- `dotnet build RAGGit.sln -c Release -p:Platform=x64`: 0 errors (5 pre-existing
  warnings in unrelated files).
- Unit: 346/346 passed (incl. `DashboardViewModelTests` 4/4 — view model
  untouched). Contract: 89/89 passed. Offline integration subset
  (`QueryOfflineTests|OfflineIdentityTests|OfflineHistoryTests|CrossUserIsolationTests`):
  12/12 passed.
- 023 static audits over `src/RAGGit.Client.WPF`: 0 hard-coded colours,
  0 `Opacity=`, 0 ad-hoc `ui:TextBlock FontSize`, 0 invalid `SymbolRegular`
  names, 0 invalid `ThemeResource` keys (reflection under `pwsh` against
  WPF-UI 4.3.0), frozen-ID superset confirmed (11 × exactly-once + 5 new IDs).
- Live `winapp ui` walkthrough (T004/T007/T009/T011 — header/cards/failed-refresh,
  wide+stacked panels, whole empty state, keyboard/screen-reader ×
  Light/Dark/High Contrast at 800×600 and wide): NOT attempted — remaining
  manual verification. Requires a running workstation API with seeded
  (mixed-state + saved-question), fresh-empty, admin, and non-admin fixtures
  plus an interactive desktop session; no screenshots are claimed. `winapp`
  CLI is present on PATH for the follow-up run.
- Diagram status: no PlantUML set exists for this feature (presentation-only
  page re-bind; no topology/flow change) — nothing to update.

### Post-review corrections (2026-10-02)

A design review of the committed feature flagged two behavioral gaps; both required
touching `RAGGit.Client.Core`, which the original plan had scoped out ("no
`RAGGit.Client.Core` change"). The scope change is recorded here and reflected in
[data-model.md](data-model.md).

1. **Empty-state flicker** — `ClearStatus()` reset `StatusSeverity` to
   "Informational" at the start of every load, so the whole-dashboard empty state's
   `StatusSeverity == "Success"` gate dropped out for the duration of any refresh of
   an empty library. `ClearStatus()` no longer resets it; the severity now carries the
   outcome of the last completed load. Regression test:
   `LoadAsync_RefreshOfEmptySourcesDoesNotResetSeverity`.
2. **Unloaded zeros presented as data** — on a first-load failure the six metric cards
   and the (new-in-027) `LibrarySummary` subtitle rendered the initial zeros and
   "No documents in the library" as if loaded, violating the first-load-failure rule;
   a first-load failure and a confirmed empty library are indistinguishable from the
   counts alone. Added `HasLoaded` (true once a load has completed successfully, never
   resets); the subtitle and the metric row hide while it is false, and last-known
   numbers still show after a failed refresh. Tests:
   `LoadAsync_EmptySourcesExposeIntentionalEmptyState` (extended),
   `LoadAsync_FirstLoadFailureLeavesDashboardUnloaded`,
   `LoadAsync_FailedRefreshAfterSuccessKeepsLoadedFlagAndLastKnownCounts`.

Gates after both corrections: `dotnet csharpier check .` clean; build 0 errors;
unit 349/349, contract 89/89, offline integration 12/12; 023 static audits and the
frozen-ID superset unchanged. The `winapp ui` walkthrough items above remain the
outstanding manual verification, and now also cover the gated first-load appearance.

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
