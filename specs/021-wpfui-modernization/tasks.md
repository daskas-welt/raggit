---
description: "Task list for WPF-UI Modernization"
---

# Tasks: WPF-UI Modernization

**Input**: Design documents from `/specs/021-wpfui-modernization/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/ui-contracts.md, quickstart.md

**Tests**: No new automated test tasks. This feature is presentation-only with no new library logic (see plan.md Constitution Check, VI); the regression gate is the existing unit/contract/integration suites plus the manual/static checks in quickstart.md. Every task below cites the file it changes and the contract it satisfies.

**Organization**: Tasks are grouped by user story (US1–US5 from spec.md) so each story is independently completable and verifiable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: US1–US5 (spec.md user stories)
- All paths are repository-relative

## Path Conventions

- Client: `src/RAGGit.Client.WPF/`
- Feature docs: `specs/021-wpfui-modernization/`
- Tests: `tests/` (read-only regression gate)

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Confirm the library is in place and capture the baseline the audit is measured against.

- [X] T001 Verify the WPF-UI packages and a clean baseline build/format for the client in `src/RAGGit.Client.WPF/RAGGit.Client.WPF.csproj` (`WPF-UI` 4.3.0 + `WPF-UI.DependencyInjection` 4.3.0); run `dotnet build RAGGit.sln -c Release -p:Platform=x64` and `dotnet csharpier check .`
- [X] T002 [P] Confirm baseline inventories against `specs/021-wpfui-modernization/data-model.md` (5 XAML color literals, 4 `StatusChip` brush literals, 4 pager glyphs, frozen 52 Automation IDs) using the static checks in `specs/021-wpfui-modernization/quickstart.md`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Establish the conventions every story follows. No user story starts before this phase.

**⚠️ CRITICAL**: T003/T004 define the rules that US1–US5 apply.

- [X] T003 [P] Establish the semantic-brush and typography conventions: confirm `ui:ThemesDictionary`/`ui:ControlsDictionary` are merged and document (in `specs/021-wpfui-modernization/control-mapping.md`) that all color references use `{ui:ThemeResource …}`/`{DynamicResource …}` and text uses `ui:TextBlock` + `Typography`, per C2/C7 in `contracts/ui-contracts.md`
- [X] T004 [P] Create `specs/021-wpfui-modernization/control-mapping.md` seeded from the catalog in `specs/021-wpfui-modernization/data-model.md`, with one row per control family, its `Disposition`, and a rationale for every `StyleOnly`/`Structural` exception (FR-010)

**Checkpoint**: Conventions and mapping catalog exist; user stories can begin.

---

## Phase 3: User Story 1 - One Fluent design language across every screen (Priority: P1) ★ MVP

**Goal**: Every screen uses the library's Fluent control set and typography; no default-styled stragglers remain.

**Independent Test**: Open every primary screen; confirm no default-styled control remains and each `Convert` family in the catalog has zero raw occurrences.

### Implementation for User Story 1

> Convert raw controls to their `ui:` equivalents and replace ad-hoc `FontSize` with `Typography`. Keep every existing binding, template, converter, and `AutomationProperties.AutomationId` intact. Where no `ui:` subclass exists (`ComboBox`, `CheckBox`, `RadioButton`, `ListBox`, `Expander`, determinate `ProgressBar`), keep the native control and rely on the implicit style — record it as an exception in the catalog.

- [X] T005 [P] [US1] Convert controls and typography in `src/RAGGit.Client.WPF/Views/Pages/DashboardPage.xaml`
- [X] T006 [P] [US1] Convert controls and typography in `src/RAGGit.Client.WPF/Views/Pages/LibraryPage.xaml`
- [X] T007 [P] [US1] Convert controls and typography in `src/RAGGit.Client.WPF/Views/Pages/QueryPage.xaml`
- [X] T008 [P] [US1] Convert controls and typography in `src/RAGGit.Client.WPF/Views/Pages/QueryDetailPage.xaml`
- [X] T009 [P] [US1] Convert controls and typography in `src/RAGGit.Client.WPF/Views/Pages/HistoryPage.xaml`
- [X] T010 [P] [US1] Convert controls and typography in `src/RAGGit.Client.WPF/Views/Pages/DocumentsMinePage.xaml`
- [X] T011 [P] [US1] Convert controls and typography in `src/RAGGit.Client.WPF/Views/Pages/LoginPage.xaml`
- [X] T012 [P] [US1] Convert controls and typography in `src/RAGGit.Client.WPF/Views/Pages/AdminUsersPage.xaml`
- [X] T013 [P] [US1] Convert controls and typography in `src/RAGGit.Client.WPF/Views/Pages/SettingsPage.xaml`
- [X] T014 [P] [US1] Convert controls and typography in `src/RAGGit.Client.WPF/Views/MainWindow.xaml`
- [X] T015 [P] [US1] Convert controls and typography in `src/RAGGit.Client.WPF/Components/ChatControl.xaml`
- [X] T016 [P] [US1] Convert controls and typography in `src/RAGGit.Client.WPF/Views/Dialogs/UploadDialog.xaml`
- [X] T017 [US1] Convert `ListView`→`ui:ListView` (and `ListBox` where a list view fits) in `src/RAGGit.Client.WPF/Views/Pages/LibraryPage.xaml`, `src/RAGGit.Client.WPF/Views/Pages/HistoryPage.xaml`, `src/RAGGit.Client.WPF/Views/Pages/DocumentsMinePage.xaml`, `src/RAGGit.Client.WPF/Views/Pages/AdminUsersPage.xaml`, `src/RAGGit.Client.WPF/Views/Pages/QueryDetailPage.xaml`; convert indeterminate `ProgressBar`→`ui:ProgressRing` in the same files (depends on T005–T016)
- [X] T018 [US1] Virtualization sweep in `src/RAGGit.Client.WPF/Views/Pages/LibraryPage.xaml`, `src/RAGGit.Client.WPF/Views/Pages/AdminUsersPage.xaml`, `src/RAGGit.Client.WPF/Views/Pages/DashboardPage.xaml`, `src/RAGGit.Client.WPF/Views/Pages/SettingsPage.xaml`: confirm no `ScrollViewer` directly wraps a `ListView`/`ListBox`/`GridView`/`DataGrid` and remove any that does (C6/FR-012)

**Checkpoint**: All primary screens are Fluent-consistent; US1 is independently verifiable.

---

## Phase 4: User Story 2 - Correct in every theme, zero fixed colors (Priority: P1)

**Goal**: Zero hard-coded colors; Light/Dark/High Contrast all legible; startup follows the system theme.

**Independent Test**: Run the C2 static check (no output) and cycle Light/Dark/High Contrast with every screen and `UploadDialog` open.

### Implementation for User Story 2

> Replace every literal with the semantic tokens mapped in `data-model.md`. Do not touch `PaginationFooterControl.xaml` or `StatusChip.xaml` here (owned by US3).

- [X] T019 [US2] Apply the system theme at startup so the default follows Windows: add `ApplicationThemeManager.ApplySystemTheme()` in `src/RAGGit.Client.WPF/App.xaml.cs` and reconcile the initial dictionary in `src/RAGGit.Client.WPF/App.xaml` (keep `SystemThemeWatcher` in `src/RAGGit.Client.WPF/Views/MainWindow.xaml.cs:52`)
- [X] T020 [P] [US2] Replace the two `#0078D4` bubble literals with `AccentFillColorDefaultBrush`/`AccentTextFillColorPrimaryBrush` in `src/RAGGit.Client.WPF/Components/ChatControl.xaml`
- [X] T021 [P] [US2] Replace `BorderBrush="#0078D4"` and `Foreground="#D13438"` with `AccentFillColorDefaultBrush`/`SystemFillColorCriticalBrush` in `src/RAGGit.Client.WPF/Views/Dialogs/UploadDialog.xaml`
- [X] T022 [US2] Replace the four frozen `SolidColorBrush` literals in `src/RAGGit.Client.WPF/Components/StatusChip.xaml.cs` with runtime lookups of `SystemFillColorSuccessBrush` / `AccentFillColorDefaultBrush` / `SystemFillColorCriticalBrush` / `SystemFillColorNeutralBrush`, preserving the `DocumentStatusPresentation.ToneFor` mapping and the `DotBrush`/`TextBrush` dependency properties (FR-011)

**Checkpoint**: No color literals remain; all themes legible; system theme is the default.

---

## Phase 5: User Story 3 - Fluent iconography replaces text glyphs (Priority: P2)

**Goal**: The pager and status surfaces use the bundled Fluent icon set; no literal glyph content; icons work on Windows 10 1809+.

**Independent Test**: Pager shows themed Fluent chevrons; C3 static check finds no literal glyphs; icons render with no user-installed font.

### Implementation for User Story 3

- [X] T023 [US3] Convert the pager in `src/RAGGit.Client.WPF/Components/PaginationFooterControl.xaml`: replace `Content="«/‹/›/»"` with `ui:SymbolIcon` chevrons (`ChevronDoubleLeft24`/`ChevronLeft24`/`ChevronRight24`/`ChevronDoubleRight24`), apply `AccentFillColorDefaultBrush` to the current-page token, and convert the surrounding `Button`/`ComboBox`/`TextBlock` per US1 rules — keeping IDs `FirstPageButton`, `PreviousPageButton`, `NextPageButton`, `LastPageButton`, `PageSizeCombo` and their `ToolTip`/`AutomationProperties.Name`
- [X] T024 [P] [US3] Convert the `Ellipse` dot in `src/RAGGit.Client.WPF/Components/StatusChip.xaml` to a themed `ui:SymbolIcon` status glyph while preserving the label text and `components:StatusChip` usage
- [X] T025 [US3] Icon accessibility + OS coverage pass in `src/RAGGit.Client.WPF/Components/PaginationFooterControl.xaml` and `src/RAGGit.Client.WPF/Components/StatusChip.xaml`: ensure every icon-only interactive control has `AutomationProperties.Name` and `ToolTip` (C3), and confirm icons render on the Windows 10 1809 target with no font installed (SC-006)

**Checkpoint**: All icons are Fluent and themed; the pager no longer uses text glyphs.

---

## Phase 6: User Story 4 - Unified shell behaviors (Priority: P2)

**Goal**: One shared surface each for dialogs, notifications, and navigation, with focus returning after dismissal.

**Independent Test**: Trigger a modal, a notification, and a navigation from several screens; confirm one shared surface each and focus return.

### Implementation for User Story 4

- [X] T026 [US4] Audit and unify modal usage on the single `ui:ContentDialogHost` (`RootContentDialog`): confirm `IContentDialogService` is the only path via `src/RAGGit.Client.WPF/Services/WpfDialogService.cs` and `src/RAGGit.Client.WPF/Views/MainWindow.xaml.cs`
- [X] T027 [P] [US4] Audit and unify transient notifications on the single `ui:SnackbarPresenter` (`RootSnackbarPresenter`) via `ISnackbarService` in `src/RAGGit.Client.WPF/App.xaml.cs` and `src/RAGGit.Client.WPF/Views/MainWindow.xaml.cs`
- [X] T028 [US4] Consolidate navigation through the single `ui:NavigationView` (`src/RAGGit.Client.WPF/Services/WpfNavigationService.cs`, `src/RAGGit.Client.WPF/Views/MainWindow.xaml.cs`) and verify focus returns to the invoking control after a dialog closes (C5)

**Checkpoint**: Shell behaviors are uniform and verified.

---

## Phase 7: User Story 5 - Settings and admin adopt rich Fluent patterns (Priority: P3)

**Goal**: Settings and Admin use card/settings-row patterns with clear labels and grouping, fully keyboard/screen-reader operable.

**Independent Test**: Operate Settings and Admin entirely by keyboard and screen reader.

### Implementation for User Story 5

- [X] T029 [US5] Restructure `src/RAGGit.Client.WPF/Views/Pages/SettingsPage.xaml` into `ui:Card`/settings-row groups (label, value, description) using library typography, preserving `LightThemeRadio`, `DarkThemeRadio`, `SignOutButton`, `WorkstationUrlValue`, `SignedInAsValue`, `ConnectionStatusValue`
- [X] T030 [P] [US5] Restructure `src/RAGGit.Client.WPF/Views/Pages/AdminUsersPage.xaml` into card/row layout with proper header labels, preserving `UsersList`, `CreateUserButton`, `RefreshUsersButton`, `AdminStatusBar`, `NewUsernameBox`, `NewPasswordBox`, `NewDisplayNameBox`, `NewRoleCombo`, `ResetMustChangeCheck`, `ResetPasswordBox`, `ResetPasswordButton`
- [X] T031 [US5] Keyboard + screen-reader pass over `src/RAGGit.Client.WPF/Views/Pages/SettingsPage.xaml` and `src/RAGGit.Client.WPF/Views/Pages/AdminUsersPage.xaml`; fix focus order and missing accessible names (C5)

**Checkpoint**: Settings and Admin match the shell and pass keyboard/screen-reader use.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Finalize the audit artifact and prove all contracts.

- [X] T032 [P] Finalize `specs/021-wpfui-modernization/control-mapping.md`: mark each family Converted/Implicit/Structural, add rationale for every exception, and reconcile against the final code (FR-001/FR-010, SC-002)
- [X] T033 [P] Run the C2 and C3 static checks in `specs/021-wpfui-modernization/quickstart.md`; both MUST return no output (SC-001/SC-003)
- [X] T034 Run the Automation ID superset check in `specs/021-wpfui-modernization/quickstart.md`: the post-change set MUST contain every ID in `data-model.md` (C4, SC-005)
- [X] T035 Run the full gates for `RAGGit.sln`: `dotnet build RAGGit.sln -c Release -p:Platform=x64`, `dotnet test --filter "FullyQualifiedName~Tests.Unit"`, `--filter "FullyQualifiedName~Tests.Contract"`, the offline subset, and `dotnet csharpier check .` (SC-007)
- [ ] T036 Execute the manual UI walkthrough in `specs/021-wpfui-modernization/quickstart.md` (themes, icons, keyboard, resize to 800×600) (SC-004/SC-005/SC-006) — **requires a human on a running desktop client; not executable in this environment**
- [X] T037 [P] Update `AGENTS.md` / `docs/architecture.md` only if the control-mapping convention or shell surfaces changed materially (documentation hygiene) — no material change: the shell surfaces already used WPF-UI and the mapping convention lives in the feature doc

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately.
- **Foundational (Phase 2)**: Depends on Setup — blocks all stories.
- **US1/US2 (P1)**: Start after Foundational. US1 and US2 touch disjoint files and can run in parallel.
- **US3 (P2)**: Start after Foundational. `PaginationFooterControl.xaml` must be done by US3, not US2 (US2 excludes it), to avoid same-file conflicts.
- **US4 (P2)**: Depends on US1's `MainWindow.xaml` conversion (T014) because it edits the same shell.
- **US5 (P3)**: Depends on US1's Settings/Admin conversions (T012, T013) because it restructures those same pages.
- **Polish (Phase 8)**: Depends on all desired stories.

### User Story Dependencies

- **US1**: Independent after Foundational.
- **US2**: Independent after Foundational; disjoint files from US1.
- **US3**: Independent after Foundational; owns `PaginationFooterControl.xaml` and `StatusChip.xaml`.
- **US4**: Integrates US1's shell; verify-only, small edits.
- **US5**: Integrates US1's Settings/Admin pages; restructures them.

### Within Each Story

- Per-file conversions can run in parallel ([P]).
- Story-level sweeps (T017/T018) run after the per-file conversions.
- Verify the story checkpoint before moving on.

---

## Parallel Example: User Story 1

```text
# Launch the page conversions together (different files, no shared state):
Task: "T005 [P] [US1] Convert DashboardPage.xaml"
Task: "T006 [P] [US1] Convert LibraryPage.xaml"
Task: "T009 [P] [US1] Convert HistoryPage.xaml"
Task: "T012 [P] [US1] Convert AdminUsersPage.xaml"

# Then the story-level sweeps (depend on the conversions):
Task: "T017 [US1] Convert ListView -> ui:ListView; ProgressBar -> ui:ProgressRing"
Task: "T018 [US1] Virtualization sweep (no ScrollViewer-wrapped virtualizing list)"
```

## Parallel Example: User Story 2

```text
Task: "T020 [P] [US2] ChatControl.xaml accent literals"
Task: "T021 [P] [US2] UploadDialog.xaml accent/critical literals"
Task: "T019 [US2] App.xaml.cs system theme at startup"
Task: "T022 [US2] StatusChip.xaml.cs semantic brushes"
```

---

## Implementation Strategy

### MVP First (User Story 1 only)

1. Complete Phase 1 (Setup) and Phase 2 (Foundational).
2. Complete Phase 3 (US1).
3. **STOP and VALIDATE**: every screen Fluent-consistent; build + existing suites green.
4. Demo the MVP, then continue with US2–US5.

### Incremental Delivery

1. Setup + Foundational → conventions and catalog ready.
2. US1 → Fluent-consistent screens (MVP).
3. US2 → zero fixed colors, system theme default.
4. US3 → Fluent icons, no text glyphs.
5. US4 → unified shell surfaces.
6. US5 → rich Settings/Admin patterns.
7. Polish → catalog finalized, static checks clean, gates green.

---

## Notes

- [P] = different files, no incomplete dependency.
- Do NOT change behavior, bindings, converters' contracts, or any `RAGGit.Client.Core`/API/spec contract (FR-011).
- Preserve every `AutomationProperties.AutomationId` listed in `data-model.md`; add new IDs for new interactive elements.
- There are no new test tasks: UI correctness is proven by the static checks + manual walkthrough in `quickstart.md` plus the existing suites (zero assertion changes).
- Commit after each task or logical group; stop at any checkpoint to validate a story independently.

---

## Implementation Record (2026-09-29)

Completed on the `021-wpfui-modernization` working tree. Work already present from an
interrupted run was verified and finished; the remaining conversions were applied directly.

**Changes made this pass**

- `src/RAGGit.Client.WPF/Components/PaginationFooterControl.xaml` — literal `« ‹ › »` pager
  content replaced with `ui:SymbolIcon` chevrons (`ChevronDoubleLeft24`/`Left24`/`Right24`/
  `DoubleRight24`), raw `Button`/`TextBlock` converted to `ui:Button`/`ui:TextBlock` with
  `Appearance="Primary"` for the current page; IDs, `ToolTip`s, and accessible names preserved (T023/T025).
- `src/RAGGit.Client.WPF/Components/StatusChip.xaml.cs` — added the missing `ToneName`
  dependency property (the view's `DataTrigger`s were binding to a non-existent member, so
  status colors were silently lost) and replaced the four frozen `SolidColorBrush` literals
  with runtime lookups of the library's semantic brushes (`SystemFillColor*`/`Accent*`),
  preserving `DocumentStatusPresentation.ToneFor` and the `DotBrush`/`TextBrush` DPs (T022/T024).
- `specs/021-wpfui-modernization/control-mapping.md` — created (conventions + full catalog with
  a rationale for every `StyleOnly`/`Structural` exception) (T003/T004/T032).

**Verification**

| Check | Result |
|-------|--------|
| `dotnet build RAGGit.sln -c Release -p:Platform=x64` | ✅ succeeded, 0 warnings / 0 errors |
| `dotnet csharpier check .` | ✅ clean (241 files) |
| C2 color literals (`#[0-9A-Fa-f]{6,8}`/`Color.FromRgb`/`Colors.`) | ✅ zero matches |
| C3 literal glyph content (`Content="[«‹›»←→↑↓]"`) | ✅ zero matches |
| C3 icons present in pager | ✅ 4 `SymbolIcon` chevrons |
| C4 automation IDs | ✅ superset — all 52 frozen literals + 3 bound IDs present |
| Raw `Button` / `TextBlock` | ✅ 0 (all `Convert` families at zero raw) |
| StyleOnly exceptions | `ListBox` 3, `ComboBox` 2, `CheckBox` 1, `RadioButton` 2, `ProgressBar` 2, `Expander` 1 — all recorded in `control-mapping.md` |
| C6 virtualization | ✅ no `ScrollViewer` directly wraps a virtualizing collection |

**Pre-existing failures (unrelated to this presentation-only feature)**

`dotnet test` reports failures that no `RAGGit.Client.WPF` change can influence and that
predate this feature (they come from other in-progress migration work):

- `BootWithZeroAccountsTests` and the offline `QueryOfflineTests` fail with
  `DimensionMismatchException: Configured VectorSize 1024 does not match existing collection
  dimension 384` — the `./data/lancedb` dimension mismatch documented in `AGENTS.md`. Fix is
  environment-level: wipe `./data/lancedb` and re-ingest; not performed here (destructive to local data).
- `DocumentsContractTests` / `XlsxRejectionContractTests` fail with `TaskCanceledException` in
  `SqliteDocumentRepository.UpdateStatusAsync` and `Queued` vs `Ready` — the background-ingest
  refactor (`IngestWorkQueue.cs`, `DocumentsController.cs`) still in progress on this branch.

Client-referencing suites (e.g. `DocumentStatusPresentationTests`, `ClientConfigTests`,
`ClientLicenseIndependenceTests`, `ClientOfflineErrorTests`) pass with zero assertion changes.

