---

description: "Task list for Design-System Refinement"
---

# Tasks: Design-System Refinement

**Input**: Design documents from `/specs/023-design-system-refinement/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/ui-contracts.md, quickstart.md

**Tests**: No new automated test tasks. This feature is presentation-only with no new library logic
(plan.md Constitution Check, VI); the regression gate is the existing unit/contract/integration suites
plus the static design audits and screenshot review in quickstart.md.

**Organization**: Tasks are grouped by user story (US1–US4 from spec.md) so each story is
independently completable and verifiable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: US1–US4 (spec.md user stories)
- All paths are repository-relative

## Path Conventions

- Client: `src/RAGGit.Client.WPF/`
- Feature docs: `specs/023-design-system-refinement/`
- Tests: `tests/` (read-only regression gate)

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Confirm the baseline the refinement is measured against.

- [X] T001 Verify the WPF-UI packages and a clean baseline build/format in `src/RAGGit.Client.WPF/RAGGit.Client.WPF.csproj`; run `dotnet build RAGGit.sln -c Release -p:Platform=x64` and `dotnet csharpier check .`; record which pages lack a header supporting line, which lists lack an empty state, and confirm `Opacity=` is already 0
- [X] T002 [P] Record the pre-change page/table/empty-state inventory into `specs/023-design-system-refinement/baseline.md` (page header present?, table separation present?, empty state present?) and capture the static-audit output from `specs/023-design-system-refinement/quickstart.md`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The shared definitions every story applies. No user story starts before this phase.

**⚠️ CRITICAL**: T003 defines the styles US1–US4 consume.

- [X] T003 [P] Add the shared design-system styles to `src/RAGGit.Client.WPF/App.xaml`: `PageTitleText` (`ui:TextBlock`, `FontTypography="Title"`), `PageSubtitleText` (`Body` + `Appearance="Secondary"`), `SectionHeaderText` (`BodyStrong`), `SecondaryText` (`Appearance="Secondary"`), `MetaText` (`Caption` + `Appearance="Secondary"`), and `EmptyStateGlyph` (`ui:SymbolIcon`, `FontSize="40"`, centred, `TextFillColorSecondaryBrush`) (FR-013, D6)
- [X] T004 [P] Create `specs/023-design-system-refinement/design-system.md` documenting the type roles, colour roles, icon scale, state conventions and the style keys from `data-model.md`, so new pages adopt rather than re-declare (FR-001, D1–D6)

**Checkpoint**: Shared styles and the design-system reference exist; user stories can begin.

---

## Phase 3: User Story 1 - Every screen announces itself the same way (Priority: P1) ★ MVP

**Goal**: Every primary page opens with a title plus a supporting line, using the shared styles.

**Independent Test**: Open every primary page and confirm a title **and** a supporting description line, all rendered with the same treatment.

### Implementation for User Story 1

> The Dashboard already satisfies this via its hero (title + tagline), so it is excluded.

- [X] T005 [P] [US1] Adopt `PageTitleText` and add a `PageSubtitleText` supporting line in `src/RAGGit.Client.WPF/Views/Pages/LibraryPage.xaml`
- [X] T006 [P] [US1] Adopt `PageTitleText` and add a `PageSubtitleText` supporting line in `src/RAGGit.Client.WPF/Views/Pages/HistoryPage.xaml`
- [X] T007 [P] [US1] Adopt `PageTitleText` and add a `PageSubtitleText` supporting line in `src/RAGGit.Client.WPF/Views/Pages/DocumentsMinePage.xaml`
- [X] T008 [P] [US1] Adopt `PageTitleText` and add a `PageSubtitleText` supporting line in `src/RAGGit.Client.WPF/Views/Pages/AdminUsersPage.xaml`
- [X] T009 [P] [US1] Adopt `PageTitleText` and add a `PageSubtitleText` supporting line in `src/RAGGit.Client.WPF/Views/Pages/QueryPage.xaml`
- [X] T010 [P] [US1] Adopt `PageTitleText` and add a `PageSubtitleText` supporting line in `src/RAGGit.Client.WPF/Views/Pages/SettingsPage.xaml`
- [X] T011 [US1] Adopt `SectionHeaderText` for the section headings in `src/RAGGit.Client.WPF/Views/Pages/SettingsPage.xaml` and `src/RAGGit.Client.WPF/Views/Pages/QueryDetailPage.xaml` (depends on T010 for the Settings file)

**Checkpoint**: Every primary page has a header hierarchy; US1 is independently verifiable.

---

## Phase 4: User Story 2 - Colour behaves in every theme (Priority: P1)

**Goal**: Emphasis comes from tokens (never opacity), accent roles are consistent, and supporting/meta text uses the shared styles.

**Independent Test**: Cycle Light/Dark/High Contrast; confirm supporting text, meta text and accent surfaces all read correctly and no emphasis is simulated by opacity.

### Implementation for User Story 2

- [X] T012 [P] [US2] Adopt `MetaText` for timestamps, counts and other meta lines in `src/RAGGit.Client.WPF/Views/Pages/DashboardPage.xaml`
- [X] T013 [P] [US2] Adopt `MetaText` for the meta cells in `src/RAGGit.Client.WPF/Views/Pages/LibraryPage.xaml`
- [X] T014 [P] [US2] Adopt `MetaText` for the meta lines in `src/RAGGit.Client.WPF/Views/Pages/HistoryPage.xaml`
- [X] T015 [US2] Switch the interactive accent **fill** on the chat bubbles to `{DynamicResource AccentFillColorDefaultBrush}` in `src/RAGGit.Client.WPF/Components/ChatControl.xaml`, keeping accent **decoration** on `SystemAccentColorPrimaryBrush` (D2)

**Checkpoint**: Token-based emphasis everywhere; US2 is independently verifiable.

---

## Phase 5: User Story 3 - Iconography is one vocabulary (Priority: P2)

**Goal**: A documented size scale, one symbol per concept, and a visually distinct active destination.

**Independent Test**: Compare each concept's glyph across nav, tiles, headers and rows; confirm one symbol, on-scale sizes, and that the active destination is distinguishable in all three themes.

### Implementation for User Story 3

- [X] T016 [US3] Build each navigation item with an explicit `SymbolIcon` instead of the constructor icon in `src/RAGGit.Client.WPF/ViewModels/MainWindowViewModel.cs` (keeps `Nav*` automation IDs) (FR-010, D4)
- [X] T017 [US3] Add a guarded selection handler in `src/RAGGit.Client.WPF/Views/MainWindow.xaml.cs` (wire `SelectionChanged` in `src/RAGGit.Client.WPF/Views/MainWindow.xaml`) that sets `Filled="True"` on the active item's `SymbolIcon` and `False` on the rest (FR-009, FR-010, D4)
- [X] T018 [US3] Icon-scale conformance pass: confirm every icon size maps to the documented scale (16 inline / 20 nav-buttons / 24 rows-chips / 32–44 tiles) across `src/RAGGit.Client.WPF/` and correct any outlier (FR-007, D3)

**Checkpoint**: One icon vocabulary with a distinct active state; US3 is independently verifiable.

---

## Phase 6: User Story 4 - Data surfaces are easy to scan (Priority: P2)

**Goal**: Table rows are separated with a token-based divider, and empty lists show a glyph-led empty state.

**Independent Test**: On a populated table confirm row separation and a distinct header row; on each empty list confirm a glyph, a title and a next-step hint.

### Implementation for User Story 4

- [X] T019 [P] [US4] Add row separation and header-row distinction to the document table in `src/RAGGit.Client.WPF/Views/Pages/LibraryPage.xaml` using `DividerStrokeColorDefaultBrush` (FR-011, D5)
- [X] T020 [P] [US4] Add row separation and header-row distinction to the people table in `src/RAGGit.Client.WPF/Views/Pages/AdminUsersPage.xaml` using `DividerStrokeColorDefaultBrush` (FR-011, D5)
- [X] T021 [P] [US4] Add `EmptyStateGlyph` empty states (glyph + title + hint) for the two Dashboard lists in `src/RAGGit.Client.WPF/Views/Pages/DashboardPage.xaml` (FR-012, D5)
- [X] T022 [P] [US4] Add an `EmptyStateGlyph` empty state with a title and hint in `src/RAGGit.Client.WPF/Views/Pages/LibraryPage.xaml` (FR-012, D5)
- [X] T023 [P] [US4] Add an `EmptyStateGlyph` empty state with a title and hint in `src/RAGGit.Client.WPF/Views/Pages/HistoryPage.xaml` (FR-012, D5)
- [X] T024 [P] [US4] Add an `EmptyStateGlyph` empty state with a title and hint in `src/RAGGit.Client.WPF/Views/Pages/DocumentsMinePage.xaml` (FR-012, D5)
- [X] T025 [P] [US4] Add an `EmptyStateGlyph` empty state with a title and hint in `src/RAGGit.Client.WPF/Views/Pages/AdminUsersPage.xaml` (FR-012, D5)

**Checkpoint**: Tables and empty states read cleanly; US4 is independently verifiable.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Finalize the reference and prove all contracts.

- [X] T026 [P] Finalize `specs/023-design-system-refinement/design-system.md` against the final code (style keys, page inventory, icon scale conformance)
- [X] T027 [P] Run the static design audits in `specs/023-design-system-refinement/quickstart.md` — no hard-coded colours, no `Opacity=`, no ad-hoc `FontSize` on `ui:TextBlock`, valid icon names, valid `ThemeResource` keys (SC-001, SC-003, SC-005, D1–D3)
- [X] T028 Run the automation-ID superset check against `specs/023-design-system-refinement/baseline.md` (SC-008, FR-014)
- [X] T029 Run the full gates for `RAGGit.sln`: `dotnet build RAGGit.sln -c Release -p:Platform=x64`, the Unit/Contract/offline test filters, and `dotnet csharpier check .` (SC-009)
- [X] T030 Execute the screenshot walkthrough in `specs/023-design-system-refinement/quickstart.md` (every page header, table, empty state, active nav destination) and review the PNGs (D1, D4, D5, SC-006, SC-007)
- [X] T031 Execute the manual checks: Light/Dark/High Contrast legibility and 800×600 no-clipping (FR-015, SC-003, SC-007)
- [X] T032 [P] Update `docs/architecture.md` / `AGENTS.md` only if the design system changed the client description materially

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately.
- **Foundational (Phase 2)**: Depends on Setup; blocks all stories.
- **US1/US2/US3/US4 (P1/P1/P2/P2)**: Start after Foundational; touch largely disjoint files.
- **Polish (Phase 7)**: Depends on all desired stories.

### Within Each Story

- US1: per-page tasks are parallel; T011 follows T010 (shared Settings file).
- US2: per-page meta tasks are parallel; T015 is independent (ChatControl).
- US3: T016 then T017 (same feature area: the item's icon), T018 independent.
- US4: T019/T020 (tables) and T021–T025 (empty states) touch overlapping files — run per-file tasks
  sequentially within a file (Library: T005/T013/T019/T022; History: T006/T014/T023;
  DocumentsMine: T007/T024; Admin: T008/T020/T025; Dashboard: T012/T021).

---

## Parallel Example: User Story 1

```text
Task: "T005 [P] [US1] Header + supporting line in LibraryPage.xaml"
Task: "T006 [P] [US1] Header + supporting line in HistoryPage.xaml"
Task: "T008 [P] [US1] Header + supporting line in AdminUsersPage.xaml"
```

---

## Implementation Strategy

### MVP First (User Story 1 only)

1. Complete Phase 1 (Setup) and Phase 2 (Foundational).
2. Complete Phase 3 (US1).
3. **STOP and VALIDATE**: every primary page has a header hierarchy; build + existing suites green.

### Incremental Delivery

1. Setup + Foundational → styles and reference ready.
2. US1 → header hierarchy (MVP).
3. US2 → token-based emphasis and meta text.
4. US3 → icon vocabulary and active destination.
5. US4 → data-surface legibility.
6. Polish → audits clean, gates green, screenshots reviewed.

---

## Notes

- [P] = different files, no incomplete dependency.
- Do NOT change behavior, bindings, converters, or any `RAGGit.Client.Core`/API/spec contract (FR-016).
- Preserve every automation ID listed in `data-model.md`; new interactive elements add new IDs.
- Every icon name MUST be a valid `SymbolRegular`/`SymbolFilled` member; sizes MUST be on scale.
- No `Opacity=` anywhere; emphasis comes from theme token roles.
- There are no new test tasks: correctness is proven by the static audits + screenshot review plus the
  existing suites (zero assertion changes).

---

## Implementation Record (2026-09-29)

All 32 tasks complete. This feature owns the client-wide design system (icons, typography, colour)
end to end; `022` retains only its own scope.

### What changed

- **Foundational** — `src/RAGGit.Client.WPF/App.xaml` gained the shared styles `PageTitleText`,
  `PageSubtitleText`, `SectionHeaderText`, `SecondaryText` and `EmptyStateGlyph` (the planned
  `MetaText` was dropped: meta text is `Caption` + `SecondaryText`, so the extra style was unused).
  `specs/023-design-system-refinement/design-system.md` documents the type roles, colour roles, icon
  scale, state conventions, style keys and page inventory.
- **US1 (headers)** — Library, Ask, History, My Documents, People Management, Query Detail and
  Settings now open with `PageTitleText` + `PageSubtitleText`; Settings section headings use
  `SectionHeaderText`. The Dashboard already satisfied this via its hero.
- **US2 (colour)** — all 36 remaining inline `Appearance="Secondary"` sets converted to the
  `SecondaryText` style; the chat-bubble **fill** switched to
  `{DynamicResource AccentFillColorDefaultBrush}` (accent decoration stays on
  `SystemAccentColorPrimaryBrush`).
- **US3 (icons)** — navigation items are built with an explicit `SymbolIcon`; a guarded
  `SelectionChanged` handler in `Views/MainWindow.xaml.cs` sets `Filled="True"` on the active item and
  `False` on the rest. Icon sizes confirmed on scale: 16 status chips and navigation rows · 20 pager
  (glyph family) · 24 settings rows, tiles and actions · 40 empty states (hero watermark documented as
  a 96px decoration exception); the navigation figure is corrected from "24 nav rows/chips" by T044
  after live measurement.
- **US4 (data surfaces)** — Library and People tables gained `DividerStrokeColorDefaultBrush` row
  separators and a distinct, secondary-styled header row; six empty states gained
  `EmptyStateGlyph` imagery (Dashboard ×2, Library, History, My Documents, People Management).
- **Docs** — `baseline.md`, `design-system.md` and the 023 spec/plan/research/data-model/contracts/
  quickstart set.

### Verification

| Check | Result |
|-------|--------|
| `dotnet build RAGGit.sln -c Release -p:Platform=x64` | ✅ 0 errors |
| `dotnet csharpier check .` | ✅ clean (241 files) |
| UNIT tests | ✅ 334/334 |
| D2 colour literals | ✅ 0 |
| D2 `Opacity=` | ✅ 0 |
| D1 ad-hoc `FontSize` in `ui:TextBlock` | ✅ 0 |
| D3 icon-name validity | ✅ 0 invalid |
| D2 `ThemeResource` key validity | ✅ 0 invalid |
| D7 automation IDs | ✅ all 22 baseline IDs present |
| Screenshots | Library (header + row separators + header row), People Management (same), Settings (Light and Dark headers + rows), Dashboard (active-nav filled glyph) — in `…\Temp\opencode\walkthrough-023\` |

### Caveats (updated 2026-09-29, after the Convergence phase)

- **CONTRACT (5) and OFFLINE (1) failures re-verified and confirmed pre-existing** — the same five
  Contract failures (`Expected doc.Status to be DocumentStatus.Ready … but found Queued`; a body whose
  `"status":"Queued"` where `"Uploading"` was expected) and the offline `citations` length 0 failure
  all come from the in-progress background-ingest refactor, with **zero assertion changes** from this
  feature. UNIT is green (334/334). They belong to that refactor, not here.
- **Minimum window size (800×600) now verified live.** The earlier "resize quirk" was a harness
  mistake (the wrong window handle). At 800×600 the Dashboard, Library, People Management and Settings
  were captured, and **two real clipping defects were found and fixed**: the Dashboard hero **hid its
  "Welcome back" text entirely** (the profile card squeezed the text column to zero — a `WrapPanel`
  now lets the card wrap below the text) and the Settings row descriptions **truncated mid-word**
  (now `TextWrapping="Wrap"`). Remaining accepted limitation: the People table's name/display-name
  columns ellipsize at 800×600 (`ad…`, `Walkthr…`); the full name is still reachable by selecting the
  row, and wrapping that list in a `ScrollViewer` would break its virtualization.
- **Empty states were not visually captured**: every list currently holds data (17 documents, saved
  queries, 3 users), so the seven empty states were verified by markup/static inspection instead.
- **High Contrast** was not captured live (no automated theme switch available). It is verified by
  construction: the library ships `HC1`/`HC2`/`HCBlack`/`HCWhite` theme dictionaries and every colour
  token used (`DividerStrokeColorDefaultBrush`, `ControlFillColorSecondaryBrush`,
  `TextFillColorSecondaryBrush`, `TextOnAccentFillColorDisabledBrush`, `SystemFillColorCriticalBrush`)
  is a library-defined `ThemeResource` member, with zero literals and zero `Opacity=`. A manual HC
  pass is still recommended.
- **Theme and icon verification (2026-09-30, T044/T045)** — icon sizes are now stated per step with
  their realisation (explicit `FontSize` vs the component library's control style) and the live
  measurements behind it; the T044 correction above replaces the earlier "24 nav rows/chips" claim. The
  status chip was captured on the Library in **Light** (all 17 rows `Ready` with the filled
  `CheckmarkCircle24` and the success label — the T038 colour roles hold). **Dark** could not be verified
  for that page: with the app theme demonstrably Dark (`LightThemeRadio.IsSelected = False` after
  navigating away and back) the Settings page renders correctly dark while the Library kept light
  surfaces across six captures — the next convergence pass should establish whether that page's surfaces
  follow a Dark theme. **High Contrast** remains unverified (the client exposes Light/Dark radios only).
  PNGs: `…\Temp\opencode\converge-023\`.

---

## Phase 8: Convergence

- [X] T033 Remove the `MetaText` style key from `specs/023-design-system-refinement/data-model.md` (Type Role table and Shared Style table) and `specs/023-design-system-refinement/research.md`, stating meta text as `Caption` + `SecondaryText`, so the reference matches `src/RAGGit.Client.WPF/App.xaml`, which defines no `MetaText` style — per FR-013/D6 (contradicts)
- [X] T034 Add the `EmptyStateGlyph` symbol to the chat empty state in `src/RAGGit.Client.WPF/Components/ChatControl.xaml` so the message list shows a symbol, a title and a next-step hint like every other empty list — per FR-012/SC-006 (partial)
- [X] T035 Re-run the Contract and remaining offline suites and record whether the outstanding failures are pre-existing (background-ingest refactor, not assertion changes) so SC-009 can be settled for this feature; do not modify `RAGGit.Workstation.Api` or `RAGGit.Ingest` here — per SC-009 (partial)
- [X] T036 Verify light/dark/high-contrast legibility and 800×600 no-clipping on the affected surfaces and fix any contrast or clipping found — per FR-015/SC-003/SC-007 (partial)
- [X] T037 Give the Login page header a supporting line via `PageSubtitleText` and adopt `PageTitleText` in `src/RAGGit.Client.WPF/Views/Pages/LoginPage.xaml` — per FR-004/SC-002/SC-004 (partial)

---

## Phase 9: Convergence

- [X] T038 Repaint the in-progress status indicator with the documented colour roles: in `src/RAGGit.Client.WPF/Components/StatusChip.xaml` (glyph and label triggers) and `StatusChip.xaml.cs` (`ResolveToneBrush`), use accent **decoration** (`SystemAccentColorPrimaryBrush`) for the glyph and a documented status/text role for the label instead of reusing the interactive accent **fill** token as foreground, and express the tone→brush mapping in one place so the two copies cannot drift — per FR-005/FR-009, D2/D4 (contradicts)
- [X] T039 Document the document-status vocabulary in the state conventions of `specs/023-design-system-refinement/design-system.md` and `data-model.md`: `Uploading`/`Queued`/`Indexing` → in-progress, `Ready` → positive, `Failed` → error, with each tone's glyph and the 16px inline size, noting that the two in-flight states arrived with the background-ingest feature — per FR-001/FR-009 (partial)
- [X] T040 Update the Login row of the page inventory in `specs/023-design-system-refinement/design-system.md` and `data-model.md` from "title (centred card)" to "title + supporting line (centred card)" so the reference matches `src/RAGGit.Client.WPF/Views/Pages/LoginPage.xaml` — per FR-001/FR-004 (partial)

---

## Phase 10: Convergence

- [X] T041 Make the document-status chip label the state it represents instead of "Unknown": in `src/RAGGit.Client.WPF/Components/StatusChip.xaml.cs` stop the constructor's `RefreshFromStatus(null)` from publishing `LabelFor(null)` as the chip's `Text` (or track that the label is auto-generated) so `OnStatusChanged` refreshes it for every state — today every chip on Library, My Documents and the Dashboard reads "Unknown", and because `Uploading`/`Queued`/`Indexing` share one glyph and colour the wrong label removes the only remaining signal that tells those three states apart — per FR-009/D4 (contradicts)

---

## Phase 11: Convergence

- [X] T042 Align the status-chip colour documentation with the implemented roles: in `specs/023-design-system-refinement/design-system.md` (Status vocabulary, the "Rule" paragraph) scope the `SystemFillColor*` sentence so it cannot be read as covering the label column — the neutral/unknown label is primary text (`TextFillColorPrimaryBrush`, per `LabelBrushKey`) — and in `src/RAGGit.Client.WPF/Components/StatusChip.xaml.cs` refresh the `ToneNameProperty` comment, which still says the view's `DataTrigger`s recolor the glyph and label although they now only select the glyph shape, with colour arriving on `DotBrush`/`TextBrush` — per FR-009/FR-001 (contradicts)
- [X] T043 Correct the Query Detail row of the page inventory in `specs/023-design-system-refinement/data-model.md`: it lists the header as "section headings", but `src/RAGGit.Client.WPF/Views/Pages/QueryDetailPage.xaml` renders `PageTitleText` + `PageSubtitleText` ("Query Detail" / "A saved question and its grounded answer") and uses no `SectionHeaderText`, while `design-system.md` already reads "title + line" — so the two references disagree with each other and with the page — per FR-001/FR-004 (partial)
- [X] T044 State in the icon scale how each step is realised and reconcile the navigation step: `src/RAGGit.Client.WPF/ViewModels/MainWindowViewModel.cs` builds every nav `SymbolIcon` with no `FontSize`, and the pager, settings-row, list-action and inline-action glyphs set none either (the client's only explicit sizes are the status chip 16, the Dashboard tiles 24, the empty-state style 40 and the documented 96 hero watermark), yet `design-system.md`'s Icon scale assigns navigation to the 20 step while the Implementation Record claims "24 nav rows/chips" — record what the unset glyphs inherit (the library's control style) and correct whichever statement is wrong — per FR-007/SC-005 (partial)
- [X] T045 Close out the High-Contrast half of T036 for the status chip's colour roles: the in-progress glyph now uses `SystemAccentColorPrimaryBrush` as a foreground with primary text for its label (T038) and is verified only by construction plus a dependency-property probe, while FR-015/SC-003 still rests on the recorded "a manual HC pass is still recommended" caveat — capture the affected surfaces with a status chip under Light and Dark (drivable through `LightThemeRadio`/`DarkThemeRadio`) and under High Contrast if the system theme can be switched, then record the result or the residual limitation — per FR-015/SC-003 (partial)

