# Tasks: WinUI Redesign (Native Windows 11)

**Input**: Design documents from `/specs/011-ui-redesign/` (spec.md with US1–US5, plan.md with XAML-only structure + decisions)

**Prerequisites**: plan.md (present), spec.md (present), constitution v1.3.0 (no amendment; Constitution Check PASS)

**Tests**: No new test tasks — presentation-only feature. Existing suites (`tests/unit`, `tests/contract`, `tests/integration`) MUST stay green with zero assertion changes; they are the regression gate for every phase. Manual per-page checks (keyboard, Contrast theme, narrow window) are the story acceptance tests.

**Organization**: Grouped by user story; each story independently testable. Core (`src/RAGGit.Client.Core/`), converters, and all test suites are READ-ONLY — WinUI XAML + `MainWindow` sizing only.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Grounding — read-only, no UI changes)

**Purpose**: Reference grounding recorded before any XAML is touched (FR-008)

- [ ] T001 Run `winapp find-ui` lookups for dashboard cards, chat, SettingsCard, NavigationView pane and record scenario IDs in `specs/011-ui-redesign/research.md`
- [ ] T002 Study Dev Home nav/card XAML, PowerToys Settings SettingsCard usage, Gallery InfoBar/TeachingTip samples, SmrtDoodle Themes/ structure and record adoptions in `specs/011-ui-redesign/research.md`
- [ ] T003 Rule on Upload nav-vs-dialog conflict (FR-004 vs 008-sheet; default: dialog retained, nav item opens it) and record ruling in `specs/011-ui-redesign/research.md`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Theme system, window sizing, nav shell, shared controls — every page builds on this

**⚠ CRITICAL**: No user story work can begin until this phase is complete

- [ ] T004 Add Light/Dark/HighContrast `ThemeDictionaries` with semantic brush names in `src/RAGGit.Client.WinUI/App.xaml`
- [ ] T005 [P] Convert ~109 `{StaticResource}` theme sites to `{ThemeResource}` across `src/RAGGit.Client.WinUI/Views/` and `src/RAGGit.Client.WinUI/Components/` (depends on T004)
- [ ] T006 Add DPI-aware `AppWindow.Resize` rubric sizing via `GetDpiForWindow` in `src/RAGGit.Client.WinUI/MainWindow.xaml.cs`
- [ ] T007 Rebuild `NavigationView` pane with per-item `SymbolIcon`s in `src/RAGGit.Client.WinUI/MainWindow.xaml` (depends on T003)
- [ ] T008 [P] Replace emoji/glyph buttons with `SymbolIcon`/`FontIcon` in `src/RAGGit.Client.WinUI/Views/LibraryPage.xaml`, `src/RAGGit.Client.WinUI/Components/ChatControl.xaml`, and `src/RAGGit.Client.WinUI/Converters/ViewConverters.cs` consumers
- [ ] T009 [P] Standardize error/status surfaces to `InfoBar` and destructive confirms to `ContentDialog` across `src/RAGGit.Client.WinUI/Views/` using the pattern in `src/RAGGit.Client.WinUI/Views/LibraryPage.xaml.cs`

**Checkpoint**: Foundation ready — themed shell launches content-sized with iconed nav in all 3 themes; stories can proceed in priority order

---

## Phase 3: User Story 1 — Foundations look native in every theme (Priority: P1) ★ MVP

**Goal**: Content-sized window, iconed nav, semantic brushes in Light/Dark/HighContrast; no behavior change

**Independent Test**: Fresh launch → window sized to content; cycle Light/Dark/Contrast → all surfaces themed; keyboard traverses nav end to end

- [ ] T010 [US1] Reskin shell in `src/RAGGit.Client.WinUI/MainWindow.xaml(.cs)` per Dev Home anchor with Mica backdrop and native spacing

**Checkpoint**: At this point, US1 should be fully demonstrable independently (native shell, MVP shippable before page reskins)

---

## Phase 4: User Story 2 — Library reads as a native browser (Priority: P1)

**Goal**: Dev Home card styling with Explorer-like density — themed grid, responsive columns, empty state, pagination

**Independent Test**: Library with 0, 1, and 50+ documents in each theme; narrow window keeps content reachable; keyboard reaches every row action

- [ ] T011 [US2] Reskin header, `ListView` grid template, responsive columns, and empty state in `src/RAGGit.Client.WinUI/Views/LibraryPage.xaml(.cs)`
- [ ] T012 [P] [US2] Reskin footer in `src/RAGGit.Client.WinUI/Components/PaginationFooterControl.xaml`

**Checkpoint**: At this point, User Stories 1 AND 2 should both work independently

---

## Phase 5: User Story 3 — Ask reads as native chat (Priority: P1)

**Goal**: Theme-brush bubbles, citations without overlap, smooth long conversations

**Independent Test**: 20-message conversation in order with citations in all 3 themes; citations footer and status text never overlap

- [ ] T013 [US3] Fix `Grid.Row=2` citations/error collision and reskin in `src/RAGGit.Client.WinUI/Views/QueryPage.xaml`
- [ ] T014 [P] [US3] Apply theme-brush bubbles and responsive `MaxWidth` in `src/RAGGit.Client.WinUI/Components/ChatControl.xaml(.cs)`

**Checkpoint**: At this point, User Stories 1–3 should all work independently

---

## Phase 6: User Story 4 — Secondary pages match the shell (Priority: P2)

**Goal**: History, My Docs, Query Detail share the card language and virtualize (no `ScrollViewer`-wrapped collections)

**Independent Test**: Each page scrolls smoothly with 100+ rows; empty states themed; keyboard walkthrough clean

- [ ] T015 [P] [US4] Remove `ScrollViewer`-wraps-`ListView` and reskin `src/RAGGit.Client.WinUI/Views/HistoryPage.xaml` and `src/RAGGit.Client.WinUI/Views/DocumentsMinePage.xaml`
- [ ] T016 [P] [US4] Remove `ScrollViewer`-wraps-`ListView` and reskin `src/RAGGit.Client.WinUI/Views/QueryDetailPage.xaml`

**Checkpoint**: At this point, User Stories 1–4 should all work independently

---

## Phase 7: User Story 5 — Admin and Login meet platform conventions (Priority: P2)

**Goal**: Admin on `SettingsCard`/`SettingsExpander` with `Header` labels; Login binds per-keystroke with correct error visibility

**Independent Test**: Full admin form operable by keyboard + screen reader; login errors surface while typing in all themes

- [x] T017 [US5] Rebuild form on inbox `Expander` + `Header`-labelled editors in `src/RAGGit.Client.WinUI/Views/AdminUsersPage.xaml` (toolkit `SettingsCard` rejected: 22621-only assets vs 17763 floor — see `research.md`)
- [x] T018 [US5] Fix `UpdateSourceTrigger` and `HasError` visibility binding plus Mica/native spacing in `src/RAGGit.Client.WinUI/Views/LoginPage.xaml`

**Checkpoint**: All user stories should now be independently functional

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Gates and checklists that cover all stories

- [ ] T019 Run full validation gates with Core untouched: `dotnet build` + `dotnet test` + `dotnet csharpier check .`
- [ ] T020 [P] Record per-page keyboard, Contrast-theme, and narrow-window results with `winapp find-ui` conformance notes in `specs/011-ui-redesign/checklists/`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately (read-only research)
- **Foundational (Phase 2)**: Depends on Setup completion — BLOCKS all user stories
- **User Stories (Phases 3–7)**: All depend on Foundational phase completion
  - Run sequentially in priority order (P1 → P1 → P1 → P2 → P2); stories share shell files, do NOT parallelize across stories
- **Polish (Phase 8)**: Depends on all desired user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational (Phase 2) — no dependencies on other stories
- **User Story 2 (P1)**: Can start after Foundational (Phase 2) — independently testable
- **User Story 3 (P1)**: Can start after Foundational (Phase 2) — independently testable
- **User Story 4 (P2)**: Can start after Foundational (Phase 2) — independently testable
- **User Story 5 (P2)**: Can start after Foundational (Phase 2) — independently testable

### Within Each User Story

- Foundational theme/shell names agreed before pages consume them
- Converters/themes before pages that consume them if done sequentially
- Story complete (including its theme/keyboard checks) before moving to next priority

### Parallel Opportunities

- T001–T003 sequential (same `research.md` file — do not parallelize)
- T005, T008, T009 in parallel after T004 (different files)
- T012 with T011; T014 with T013 (component vs page files)
- T015 with T016 (separate page files); T017 with T018 (separate page files)
- T020 in parallel with T019 (checklist vs gates)

---

## Parallel Example: Foundational shared controls

```bash
# Launch theme-site conversion and shared-control fixes together (different files):
Task: "Convert StaticResource sites in Views/ and Components/"
Task: "Replace emoji/glyph buttons in LibraryPage.xaml, ChatControl.xaml, ViewConverters consumers"
Task: "Standardize InfoBar and ContentDialog confirms across Views/"
```

## Parallel Example: User Story 4 pages

```bash
# Launch secondary-page reskins together (separate files, shared foundation):
Task: "Reskin HistoryPage.xaml and DocumentsMinePage.xaml"
Task: "Reskin QueryDetailPage.xaml"
```

---

## Implementation Strategy

### MVP First (Foundations Only)

1. Complete Phase 1: Setup (grounding recorded)
2. Complete Phase 2: Foundational (CRITICAL — blocks all stories)
3. Complete Phase 3: User Story 1 (native shell)
4. **STOP and VALIDATE**: US1 independent test in all 3 themes; `dotnet test` green
5. Deploy/demo MVP if ready (native shell before page reskins)

### Incremental Delivery

1. Setup + Foundational → themed native shell (MVP!)
2. + US2 → Library reskinned → Validate independently
3. + US3 → Ask reskinned → Validate independently
4. + US4 → Secondary pages → Validate independently
5. + US5 → Admin + Login → Validate independently
6. Polish → gates + checklists

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Core (`src/RAGGit.Client.Core/`), converters, and all test suites are READ-ONLY — XAML + `MainWindow` sizing only
- `winapp find-ui` grounding (T001–T002) and Upload ruling (T003) precede all XAML edits
- Commit after each task or logical group; stop at any checkpoint to validate story independently
- Avoid: vague tasks, same-file conflicts, cross-story dependencies that break independence
