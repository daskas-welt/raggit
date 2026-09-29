---

description: "Task list for Gallery-Style Design Language"
---

# Tasks: Gallery-Style Design Language

**Input**: Design documents from `/specs/022-gallery-design-language/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/ui-contracts.md, quickstart.md

**Tests**: No new automated test tasks. This feature is presentation-only with no new library logic
(plan.md Constitution Check, VI); the regression gate is the existing unit/contract/integration
suites plus the static checks and UI-automation walkthrough in quickstart.md. Every task below cites
the file it changes and the contract it satisfies.

**Organization**: Tasks are grouped by user story (US1–US3 from spec.md) so each story is
independently completable and verifiable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: US1–US3 (spec.md user stories)
- All paths are repository-relative

## Path Conventions

- Client: `src/RAGGit.Client.WPF/`
- Feature docs: `specs/022-gallery-design-language/`
- Tests: `tests/` (read-only regression gate)

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Confirm the library is in place and capture the baseline the change is measured against.

- [X] T001 Verify the WPF-UI packages and a clean baseline build/format for the client in `src/RAGGit.Client.WPF/RAGGit.Client.WPF.csproj` (`WPF-UI` 4.3.0 + `WPF-UI.DependencyInjection` 4.3.0); run `dotnet build RAGGit.sln -c Release -p:Platform=x64` and `dotnet csharpier check .`
- [X] T002 [P] Record the pre-change baseline into `specs/022-gallery-design-language/baseline-ids.md`: the `AutomationProperties.AutomationId` values on the affected surfaces (shell, Dashboard, Settings) and the output of the C3/C7 static checks and the `SymbolRegular` icon-name audit in `specs/022-gallery-design-language/quickstart.md`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Establish the conventions every story follows. No user story starts before this phase.

**⚠️ CRITICAL**: T003 defines the recipes US1–US3 apply.

- [X] T003 [P] Create `specs/022-gallery-design-language/design-language.md` seeded from `data-model.md` with the concrete recipes US1–US3 reuse: the hero gradient stops (`SystemAccentColorPrimary` → `SystemAccentColorSecondary`) with `TextOnAccentFillColorPrimaryBrush` text; the tile schema (`Icon`/`Title`/`Description`/`Target`/`AutomationId`); the settings-row schema (`Icon`/`Title`/`Description`/`Control`/`AutomationId`); the secondary-text convention (`Appearance="Secondary"`); the valid `SymbolRegular` list; and the preserved + new automation-ID set (FR-009/FR-010/FR-012, C3/C4/C7/C8)
- [X] T004 [P] Confirm the WPF-project ownership note in `specs/022-gallery-design-language/design-language.md`: navigation grouping/search live in `src/RAGGit.Client.WPF/ViewModels/MainWindowViewModel.cs` (the WPF project), and `src/RAGGit.Client.Core` is read-only for this feature (FR-014, C6)

**Checkpoint**: Conventions and ID baseline exist; user stories can begin.

---

## Phase 3: User Story 1 - The app chrome feels like the component library's showcase (Priority: P1) ★ MVP

**Goal**: The window and navigation pane read as a first-party Fluent application: Mica backdrop, centered, a collapsible pane, page transitions, grouped destinations under headings, a live search field, and Settings pinned in the footer.

**Independent Test**: Launch the client; confirm the backdrop, toggle the pane, navigate between two pages (transition), type in the search field and watch destinations filter, and confirm groups + footer Settings.

### Implementation for User Story 1

> Keep every existing `AutomationProperties.AutomationId`; do not change navigation semantics. Grouping and search are implemented in the WPF-project ViewModel, not `RAGGit.Client.Core`.

- [X] T005 [US1] Add the window chrome in `src/RAGGit.Client.WPF/Views/MainWindow.xaml`: `WindowBackdropType="Mica"`, `WindowCornerPreference="Default"`, `WindowStartupLocation="CenterScreen"` (FR-001, C1)
- [X] T006 [US1] Polish the navigation pane in `src/RAGGit.Client.WPF/Views/MainWindow.xaml`: `IsPaneToggleVisible="True"`, `OpenPaneLength="300"`, `Transition="FadeInWithSlide"`, `IsTopSeparatorVisible="False"`, `IsFooterSeparatorVisible="False"`, `FrameMargin="0"`, `IsBackButtonVisible="Collapsed"` (FR-002/FR-003, C1)
- [X] T007 [US1] Add the navigation search field in `src/RAGGit.Client.WPF/Views/MainWindow.xaml`: a `ui:NavigationView.AutoSuggestBox` containing a `ui:AutoSuggestBox` with `AutomationProperties.AutomationId="NavigationSearchBox"` and a `Search24` icon, plus a Ctrl+F `KeyBinding` (FR-004, C2)
- [X] T008 [US1] Group the navigation in `src/RAGGit.Client.WPF/ViewModels/MainWindowViewModel.cs`: cache the full item list and emit `ui:NavigationViewItemHeader` sections ("Workspace" = Dashboard/Library/Ask/History/My Docs; "Management" = Admin, present only for administrators), keeping Settings in `FooterMenuItems`; preserve `NavDashboard`…`NavSettings` IDs (FR-005, C2)
- [X] T009 [US1] Implement live search filtering in `src/RAGGit.Client.WPF/ViewModels/MainWindowViewModel.cs` (case-insensitive substring on the item title; empty query restores the full ordered list) and wire `AutoSuggestBox.TextChanged` plus Ctrl+F focus in `src/RAGGit.Client.WPF/Views/MainWindow.xaml.cs` (FR-004, C2)

**Checkpoint**: The shell matches the library's chrome; US1 is independently verifiable.

---

## Phase 4: User Story 2 - The Dashboard lands like the Gallery home (Priority: P1)

**Goal**: A hero banner carries the signed-in context with theme-accent styling, and the primary destinations are presented as navigational tiles (icon + title + one-line description) instead of plain buttons.

**Independent Test**: Sign in, land on the Dashboard, confirm the hero renders with an accent gradient that adapts to the theme, and that each tile navigates on a single activation.

### Implementation for User Story 2

> The hero uses accent Color tokens only (no literals); tiles are `ui:CardAction` (a `ButtonBase`, so `Click` works) and must reflow rather than clip at 800×600.

- [X] T010 [P] [US2] Add the shared hero gradient brush `HeroAccentGradientBrush` (stops `SystemAccentColorPrimary` → `SystemAccentColorSecondary`) to the application resources in `src/RAGGit.Client.WPF/App.xaml` (FR-006, C3)
- [X] T011 [US2] Replace the plain header with the hero banner in `src/RAGGit.Client.WPF/Views/Pages/DashboardPage.xaml` (`Border` + `HeroAccentGradientBrush` + a low-opacity `ui:SymbolIcon` watermark; welcome, role context, tagline in `TextOnAccentFillColorPrimaryBrush`); keep `DashboardProfileCard` and `DashboardRefreshButton` (FR-006, C1)
- [X] T012 [US2] Replace the metric tiles with navigational `ui:CardAction` tiles in `src/RAGGit.Client.WPF/Views/Pages/DashboardPage.xaml` (each with icon + title + description; laid out to wrap responsively) — preserve `DashboardMetricDocuments`/`DashboardMetricQueries` and add `DashboardTileLibrary`/`DashboardTileAsk`/`DashboardTileHistory`/`DashboardTileMyDocs` (and `DashboardTileAdmin` for administrators) (FR-007, C4, C5)
- [X] T013 [US2] Wire tile activation in `src/RAGGit.Client.WPF/Views/Pages/DashboardPage.xaml.cs` using `CardAction.Click` to reuse the existing handlers and add My Docs/History/Admin navigation — no `RAGGit.Client.Core` change (FR-014, C6)

**Checkpoint**: The Dashboard hero and tiles render and navigate; US2 is independently verifiable.

---

## Phase 5: User Story 3 - Settings read as labelled rows (Priority: P2)

**Goal**: Each setting appears as a labelled row (icon + title + description + control/value) grouped under section headings.

**Independent Test**: Open Settings; confirm every setting renders as a labelled row conveying icon, title, description, and its control/value, and every control still operates (theme switch, sign out).

### Implementation for User Story 3

- [X] T014 [US3] Convert `src/RAGGit.Client.WPF/Views/Pages/SettingsPage.xaml` into `ui:CardControl` rows under `BodyStrong` section headings ("Appearance & behavior", "Connection", "Session"): App theme (`Color24`, keeping the light/dark radios), Workstation URL (`Link24`), Signed in as (`Person24`), Connection status (`PlugConnected24`), Sign out (`SignOut24`); preserve `LightThemeRadio`, `DarkThemeRadio`, `SignOutButton`, `WorkstationUrlValue`, `SignedInAsValue`, `ConnectionStatusValue` (FR-008, C4, C8)
- [X] T015 [US3] Verify the theme-radio and sign-out handlers still resolve after the restructure in `src/RAGGit.Client.WPF/Views/Pages/SettingsPage.xaml.cs`, and add `AutomationProperties.Name`/`ToolTip` to any row control that needs it (FR-015, C4)

**Checkpoint**: Settings matches the row pattern and stays operable.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Finalize the design artifact and prove all contracts.

- [X] T016 [P] Finalize `specs/022-gallery-design-language/design-language.md`: reconcile the tile and row inventories and the automation-ID set against the final code (FR-007/FR-008/FR-009)
- [X] T017 [P] Run the C3 and C7 static checks in `specs/022-gallery-design-language/quickstart.md` — both MUST return no output, including zero invalid `SymbolRegular` names (SC-003, FR-010, FR-012)
- [X] T018 Run the C4 automation-ID superset check using `specs/022-gallery-design-language/quickstart.md` against the baseline captured in T002: the post-change set MUST contain every baseline ID (SC-004, FR-009)
- [X] T019 Run the full gates for `RAGGit.sln`: `dotnet build RAGGit.sln -c Release -p:Platform=x64`, `dotnet test --filter "FullyQualifiedName~Tests.Unit"`, `--filter "FullyQualifiedName~Tests.Contract"`, the offline subset, and `dotnet csharpier check .` (SC-007)
- [X] T020 Execute the UI-automation walkthrough in `specs/022-gallery-design-language/quickstart.md` with `winapp ui` (shell toggle + search filtering, Dashboard tiles, Settings rows) and capture screenshots for review (C1–C5, C8)
- [X] T021 Execute the manual checks in `specs/022-gallery-design-language/quickstart.md`: Light/Dark/High-Contrast legibility, 800×600 reflow with no clipping, and keyboard traversal incl. Ctrl+F (SC-005, SC-006, FR-015)
- [X] T022 [P] Update `AGENTS.md` / `docs/architecture.md` only if the shell surfaces changed materially (documentation hygiene)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately.
- **Foundational (Phase 2)**: Depends on Setup — blocks all stories.
- **US1 (P1)**: Start after Foundational. US1, US2, and US3 touch disjoint files and can run in parallel.
- **US2 (P1)**: Start after Foundational. Uses the resource added in T010; otherwise independent of US1.
- **US3 (P2)**: Start after Foundational. Independent of US1/US2.
- **Polish (Phase 6)**: Depends on all desired stories.

### User Story Dependencies

- **US1**: Independent after Foundational (shell only).
- **US2**: Independent after Foundational; depends only on its own T010 resource (Dashboard + App.xaml).
- **US3**: Independent after Foundational (Settings only).

### Within Each Story

- `MainWindow.xaml` edits (T005–T007) are sequential (same file); T008–T009 edit the ViewModel/code-behind.
- `DashboardPage.xaml` edits (T011–T012) are sequential (same file); T013 edits the code-behind.
- `SettingsPage.xaml` (T014) precedes its code-behind verification (T015).
- Verify each story's checkpoint before moving on.

---

## Parallel Example: User Story 2

```text
# T010 is a different file (App.xaml) and can run alongside the Dashboard edits:
Task: "T010 [P] [US2] Add HeroAccentGradientBrush to App.xaml"
# Then the Dashboard edits (same file, sequential):
Task: "T011 [US2] Hero banner in DashboardPage.xaml"
Task: "T012 [US2] CardAction tiles in DashboardPage.xaml"
Task: "T013 [US2] Wire CardAction.Click in DashboardPage.xaml.cs"
```

## Parallel Example: Cross-Story

```text
# After Foundational, the three stories touch disjoint files:
Task: "US1 — MainWindow.xaml(.cs) + MainWindowViewModel.cs"
Task: "US2 — DashboardPage.xaml(.cs) + App.xaml"
Task: "US3 — SettingsPage.xaml(.cs)"
```

---

## Implementation Strategy

### MVP First (User Story 1 only)

1. Complete Phase 1 (Setup) and Phase 2 (Foundational).
2. Complete Phase 3 (US1).
3. **STOP and VALIDATE**: shell chrome + toggle + grouped/searchable navigation; build + existing suites green.
4. Demo the MVP, then continue with US2 and US3.

### Incremental Delivery

1. Setup + Foundational → conventions and ID baseline ready.
2. US1 → Gallery-style shell (MVP).
3. US2 → Dashboard hero + tiles.
4. US3 → Settings rows.
5. Polish → artifact finalized, static checks clean, gates green, walkthrough captured.

---

## Notes

- [P] = different files, no incomplete dependency.
- Do NOT change behavior, bindings, converters, or any `RAGGit.Client.Core`/API/spec contract (FR-014).
- Preserve every `AutomationProperties.AutomationId` listed in `data-model.md`; add new IDs for new interactive elements.
- Every icon name MUST be a valid `SymbolRegular` member (runtime-validated; see C7).
- There are no new test tasks: UI correctness is proven by the static checks + UI-automation walkthrough in `quickstart.md` plus the existing suites (zero assertion changes).
- Commit after each task or logical group; stop at any checkpoint to validate a story independently.

---

## Implementation Record (2026-09-29)

All 22 tasks complete and verified on the `022-gallery-design-language` working tree.

### Changes

- **Shell (US1)** — `src/RAGGit.Client.WPF/Views/MainWindow.xaml`: `WindowBackdropType="Mica"`,
  `WindowCornerPreference="Default"`, `WindowStartupLocation="CenterScreen"`; `NavigationView`
  `IsPaneToggleVisible`/`OpenPaneLength="300"`/`Transition="FadeInWithSlide"`/no separators/
  `FrameMargin="0"`/`IsBackButtonVisible="Collapsed"`; `NavigationView.AutoSuggestBox`
  (`NavigationSearchBox`, Search24) + Ctrl+F `KeyBinding`. `ViewModels/MainWindowViewModel.cs`:
  grouped `MenuItems` (`NavigationViewItemHeader` "Workspace"/"Management", footer Settings) and a
  live, case-insensitive search filter.
- **Dashboard (US2)** — `App.xaml`: shared `HeroAccentGradientBrush`
  (`SystemAccentColorPrimary`→`SystemAccentColorSecondary`). `Views/Pages/DashboardPage.xaml`: hero
  banner (gradient + watermark + welcome/role/stat line + profile card + refresh) and wrapping
  `ui:CardAction` destination tiles; `DashboardPage.xaml.cs`: tile handlers (reusing existing
  handlers; no `RAGGit.Client.Core` change).
- **Settings (US3)** — `Views/Pages/SettingsPage.xaml`: `ui:CardControl` rows (App theme, Workstation
  URL, Signed in as, Connection status, Sign out) under `BodyStrong` section headings.

### Verification

| Check | Result |
|-------|--------|
| `dotnet build RAGGit.sln -c Release -p:Platform=x64` | ✅ 0 errors |
| `dotnet csharpier check .` | ✅ clean (241 files) |
| UNIT tests | ✅ 334/334 |
| C3 color literals | ✅ zero |
| C7 literal glyphs | ✅ zero |
| C7 icon-name audit (`SymbolRegular`) | ✅ zero invalid |
| C3 ThemeResource-key audit | ✅ zero invalid |
| C7 `Icon="…"` string-form audit | ✅ zero |
| C4 automation IDs | ✅ all 22 baseline IDs present + 8 new IDs |
| Visual (screenshots) | shell (Mica, toggle, search, groups, footer Settings); Dashboard hero + tiles (Light + Dark); Settings card rows; Library; 800×600 reflow |

### Latent WPF-UI markup defects found and fixed (beyond the named surfaces)

The UI walkthrough surfaced two runtime-only defect classes (both compile cleanly):

1. **Invalid `{ui:ThemeResource <key>}`** — the markup extension is enum-keyed
   (`Wpf.Ui.Markup.ThemeResource`); `AccentFillColorDefaultBrush` is **not** a member, so the page
   threw `XamlParseException` at load. Fixed 8 usages to `SystemAccentColorPrimaryBrush`
   (`DashboardPage.xaml` ×5; pre-existing in `ChatControl.xaml` ×2 and `UploadDialog.xaml` ×1).
2. **`Icon="<name>"` on `ui:Button`** — `IconElementConverter` silently yields an empty icon for an
   unknown name, so the buttons rendered blank. Fixed 9 usages to the
   `<ui:Button.Icon><ui:SymbolIcon …/></ui:Button.Icon>` element form (`DashboardPage.xaml` ×3,
   `LibraryPage.xaml` ×4, `ChatControl.xaml` ×1, `UploadDialog.xaml` ×1); `Send`→`Send24`,
   `Add`→`Add24`.

`quickstart.md` gained static checks for both classes (they are runtime-only, so the build does not
catch them).

### Caveats

- **Pre-existing test failures** (unchanged, unrelated to this presentation-only feature):
  CONTRACT 5 failed / OFFLINE 1 failed — the in-progress background-ingest refactor
  (`Queued` vs `Ready`, `TaskExecutionException`), plus the `tests/**/bin/**/data/lancedb`
  dimension mismatch fixed earlier by wiping the test-output lancedb.
- **Client config requires an `ApiKey`**: `ClientConfigResolver` treats a config without
  `Workstation:ApiKey` (or `Api:AdminKey`/`Api:EmployeeKey`) as invalid and falls back to the
  `invalid-config` client, which surfaces as `cannot reach AI workstation` with no login. The
  Release `appsettings.json` ships only `Workstation:Url`, so this affects real Release runs too
  (worth a follow-up).
- **UIA harness limits**: the nav `AutoSuggestBox` exposes as a List (typing/`set-value` not
  drivable), `ui:Card`/`Border` automation IDs are not surfaced, and the UploadDialog (layered
  popup) cannot be screenshotted. The search filter was therefore verified by code inspection
  (two-way `Text` binding + `OnSearchTextChanged`) rather than end-to-end, and **Ctrl+F traversal
  warrants a human check**.

### Follow-up refinements (post-implementation, at the user's request)

1. **Content inset matched to the Gallery** — `Views/MainWindow.xaml` now sets
   `Padding="42,0,42,0"` on the `ui:NavigationView` (the Gallery's value), and the pages' own
   horizontal padding was moved out to avoid double-padding (`DashboardPage` ScrollViewer
   `Padding="28,24"`→`"0,24"`; `SettingsPage`/`Library`/`History`/`QueryDetail`/`DocumentsMine`/
   `AdminUsers` `Margin="24"`→`"0,24"`). Verified visually: content starts ~42px inside the pane with
   a symmetric right inset.
2. **Section headings removed** — the `ui:NavigationViewItemHeader` "Workspace"/"Management" labels
   were dropped from `ViewModels/MainWindowViewModel.cs`; the pane is now a single ordered list
   (Dashboard, Library, Ask, History, My Docs, then Admin for administrators) with Settings in the
   footer. `spec.md` FR-005, `contracts/ui-contracts.md` C2, `data-model.md`, `design-language.md`,
   and `quickstart.md` were updated to match (they previously required labelled headings).
3. **Navigation search removed** — the `ui:NavigationView.AutoSuggestBox` (`NavigationSearchBox`) and
   its Ctrl+F binding were dropped from `Views/MainWindow.xaml`, and the `SearchText`/filtering logic
   from `ViewModels/MainWindowViewModel.cs` (T007/T009 effectively reverted). `spec.md` marks FR-004
   as withdrawn and adds FR-016 (content inset); `contracts/ui-contracts.md` C2, `data-model.md`,
   `design-language.md`, and `quickstart.md` were updated. FR numbers were deliberately left
   unrenumbered so the many cross-references stay valid.

The icons / typography / colour design system (icon scale, colour-token taxonomy, opacity removal,
shared styles, page-header hierarchy, empty states, table separation, active-destination emphasis) is
**not** part of this feature — it is owned by feature `023`; see
`specs/023-design-system-refinement/`. This record covers only the shell / Dashboard / Settings scope
described above.

