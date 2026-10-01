---

description: "Task list for Fluent System Icons Across the Client"
---

# Tasks: Fluent System Icons Across the Client

**Input**: Design documents from `/specs/025-fluent-system-icons/`

**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/ui-contracts.md, quickstart.md

**Tests**: No new automated test tasks. This feature is presentation-only with no new library logic
(plan.md Constitution Check, VI) and the client has no unit-test project; correctness is proven by the
static icon audits A1–A4 and the screenshot review in [quickstart.md](quickstart.md), plus the existing
unit/contract/integration suites with zero assertion changes — the approach `023` and `024` established.

**Organization**: Tasks are grouped by user story (US1–US4 from spec.md) so each story is independently
completable and verifiable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: US1–US4 (spec.md user stories)
- All paths are repository-relative

## Path Conventions

- Client: `src/RAGGit.Client.WPF/`
- Feature docs: `specs/025-fluent-system-icons/`
- Vocabulary reference (owned by `023`): `specs/023-design-system-refinement/design-system.md`
- Tests: `tests/` (read-only regression gate)

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Confirm the baseline the icon pass is measured against.

- [X] T001 Confirm a clean baseline for `src/RAGGit.Client.WPF`: run `dotnet tool restore`, `dotnet csharpier check .` and `dotnet build RAGGit.sln -c Release -p:Platform=x64`; confirm 0 errors and record the current icon inventory (which controls already carry a glyph)
- [X] T002 Record the pre-change inventory into `specs/025-fluent-system-icons/baseline.md`: every action control lacking a glyph and every state surface shown by words or colour alone, each with its target glyph from `data-model.md` — this is the SC-001 baseline, captured at plan time per spec.md

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Prove the proposed vocabulary before any XAML is written. The repository's standing trap is
an icon name that compiles but throws at load, so this blocks every story.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T003 Verify every proposed icon name resolves in `Wpf.Ui.Controls.SymbolRegular` by reflection under PowerShell 7 (the audit A1 pattern in `specs/025-fluent-system-icons/quickstart.md`), using the vocabulary table in `research.md`; correct any name that does not resolve **before** it is written into XAML — per FR-001, SC-007

**Checkpoint**: The vocabulary is proven to resolve; user story implementation can begin.

---

## Phase 3: User Story 1 - Every action can be recognised by its icon (Priority: P1) 🎯 MVP

**Goal**: Every text-only action control shows its agreed Fluent glyph.

**Independent Test**: Walk every page and dialog; confirm each action control with an unambiguous
meaning shows its agreed glyph, the same action uses the same glyph everywhere, and the Ask page's
person suggestion chips stay text-only.

### Implementation for User Story 1

> The controls that already carry a glyph (Library header Refresh/Upload, Library row Download/Delete,
> Chat Send/Copy, Upload dialog Browse/Remove, Dashboard tiles) are not changed. The Ask page's
> `SuggestionChips` template is deliberately left without an icon (clarified 2026-09-30).

- [X] T004 [P] [US1] Give the Sign in button the `ArrowEnterLeft24` glyph (label retained) in `src/RAGGit.Client.WPF/Views/Pages/LoginPage.xaml` — per FR-002, FR-012
- [X] T005 [P] [US1] Give the Query Detail actions their glyphs in `src/RAGGit.Client.WPF/Views/Pages/QueryDetailPage.xaml`: Ask again `ArrowRepeatAll24`, Back `ArrowLeft24`, and each Copy (prompt, answer, citation) `Copy24` (labels retained) — per FR-002, FR-003
- [X] T006 [P] [US1] Give the Retry button the `ArrowClockwise24` glyph (label retained) in `src/RAGGit.Client.WPF/Views/Pages/LibraryPage.xaml` — per FR-002, FR-003
- [X] T007 [P] [US1] Give the History actions their glyphs in `src/RAGGit.Client.WPF/Views/Pages/HistoryPage.xaml`: Refresh `ArrowClockwise24`, per-row View `Eye24`, per-row Ask again `ArrowRepeatAll24`, Load more `ArrowDown24` (labels retained) — per FR-002, FR-003
- [X] T008 [P] [US1] Give the My Documents actions their glyphs in `src/RAGGit.Client.WPF/Views/Pages/DocumentsMinePage.xaml`: Refresh `ArrowClockwise24`, Load more `ArrowDown24` (labels retained) — per FR-002, FR-003
- [X] T009 [P] [US1] Give the Admin *actions* their glyphs in `src/RAGGit.Client.WPF/Views/Pages/AdminUsersPage.xaml`: Refresh `ArrowClockwise24`, per-row role button `PersonEdit24`, Create user `PersonAdd24`, Reset password `KeyReset24` (labels retained; the per-row *state* glyphs are T014/T017 in the same file) — per FR-002, FR-003
- [X] T010 [P] [US1] Give the upload dialog's footer actions their glyphs in `src/RAGGit.Client.WPF/Views/Dialogs/UploadDialog.xaml`: Upload `ArrowUpload24`, Cancel `Dismiss24`, Close `Dismiss24` (labels retained) — per FR-002, FR-003
- [X] T011 [P] [US1] Give the Settings Sign out button the `SignOut24` glyph (label retained; matches the row icon) in `src/RAGGit.Client.WPF/Views/Pages/SettingsPage.xaml` — per FR-002, FR-003
- [X] T012 [US1] Give the chat "Sources" disclosure the `TextQuote24` glyph in `src/RAGGit.Client.WPF/Components/ChatControl.xaml`, keeping the expander's content and its `SourcesAutomationId` — per FR-003, FR-011
- [X] T013 [US1] Verify US1 against its independent test: run audit A2 from `specs/025-fluent-system-icons/quickstart.md` for the action glyphs, capture the touched pages, and confirm every action control shows its glyph with its label intact — per SC-001, SC-002

**Checkpoint**: Every action control is recognisable by its icon; US1 is independently verifiable.

---

## Phase 4: User Story 2 - State and status read as icons, not only as words or colour (Priority: P1)

**Goal**: Each stateful surface distinguishes its states with a glyph as well as a colour token.

**Independent Test**: For each stateful surface change the state and confirm the glyph changes with it;
confirm the same state shows the same glyph wherever it appears.

### Implementation for User Story 2

> The document status chip (`Components/StatusChip.xaml`) already switches glyph and colour per state
> and is not changed. `016`'s decision that the admin controls stay clearly labelled is preserved: every
> state glyph is added **beside** its label, never instead of it (FR-012).

- [X] T014 [US2] In `src/RAGGit.Client.WPF/Views/Pages/AdminUsersPage.xaml` give the per-row active toggle the glyph matching its verb (`Checkmark24` for Activate, `Dismiss24` for Deactivate) and the lock cell the glyph matching its state (`LockClosed24` "Locked", `LockOpen24` "Not locked"), selected by `DataTrigger` on `IsActive`/`LockedOut`, with the existing per-row accessible names and tooltips unchanged — the glyph tracks the action verb while the label carries the state wording (spec.md Edge Cases) — per FR-004, FR-007, FR-012
- [X] T015 [P] [US2] Give the connection status value a reachability glyph in `src/RAGGit.Client.WPF/Views/Pages/SettingsPage.xaml` and `src/RAGGit.Client.WPF/Views/Pages/SettingsPage.xaml.cs`: `PlugConnected24` when reachable, `PlugDisconnected24` when `WpfConnectionState.IsUnavailable`, colour from a status token, preserving the `ConnectionStatusValue` automation ID — per FR-004, FR-006, FR-011
- [X] T016 [P] [US2] Add the leading `ErrorCircle24` glyph (critical colour token) to the plain error text on `src/RAGGit.Client.WPF/Views/Pages/LibraryPage.xaml`, `src/RAGGit.Client.WPF/Views/Pages/HistoryPage.xaml`, `src/RAGGit.Client.WPF/Views/Pages/DocumentsMinePage.xaml` and `src/RAGGit.Client.WPF/Views/Pages/QueryDetailPage.xaml`, keeping each existing error `TextBlock` and its binding — per FR-004, FR-006
- [X] T017 [US2] Add the severity glyph to the admin status surface in `src/RAGGit.Client.WPF/Views/Pages/AdminUsersPage.xaml`, driven by `StatusSeverity`: Informational `Info24`, Success `CheckmarkCircle24`, Warning `Warning24`, Error `ErrorCircle24`, coloured by the matching status token and preserving the `AdminStatusBar` element and its live-region attributes — per FR-004, FR-006
- [X] T018 [US2] Verify US2 against its independent test across `src/RAGGit.Client.WPF/Views/Pages/` (AdminUsersPage, SettingsPage, LibraryPage, HistoryPage, DocumentsMinePage, QueryDetailPage) and `src/RAGGit.Client.WPF/Components/StatusChip.xaml`: confirm the document status chip is unchanged and that the active/locked, connection, and message surfaces each change their glyph as well as their colour when the state changes — per SC-003

**Checkpoint**: Every stateful surface reads by glyph and colour; US1 and US2 both work independently.

---

## Phase 5: User Story 3 - Icons stay legible in every theme and are announced to assistive tech (Priority: P2)

**Goal**: The new iconography keeps the theme and accessibility guarantees `023` established.

**Independent Test**: Cycle Light / Dark / High Contrast across every affected surface and confirm all
icons remain visible; walk the same surfaces with a screen reader and confirm each icon-only control
announces its action.

### Implementation for User Story 3

- [X] T019 [P] [US3] Accessibility confirmation over `src/RAGGit.Client.WPF/Views/` and `src/RAGGit.Client.WPF/Components/`: confirm every icon-only control (pager First/Previous/Next/Last, message copy, row download/delete, chat send, upload-dialog browse/remove) still exposes `AutomationProperties.Name` and a `ToolTip` describing its action. This feature introduces **no new** icon-only control — every new glyph sits beside a label (FR-012) — so no new attributes are expected, and none may be dropped — per FR-007, SC-005
- [X] T020 [US3] Screen-reader walkthrough of the affected pages in `src/RAGGit.Client.WPF/Views/Pages/`: confirm each icon-only control announces its action and no control announces a raw glyph, and that labelled controls still announce their label — per FR-007, SC-005
- [X] T021 [US3] Theme and size capture: cycle Light / Dark / High Contrast and capture the affected surfaces of `src/RAGGit.Client.WPF/Views/Pages/` at the minimum window size (800×600), confirming every new glyph stays visible and correctly contrasted and no labelled control clips or displaces its neighbours — if the High Contrast theme cannot be switched on the verifying machine, record it as verified-by-construction (the `023` precedent) — per FR-008, SC-006

**Checkpoint**: Icons are legible and announced in all three themes; US3 is independently verifiable.

---

## Phase 6: User Story 4 - One vocabulary and one size scale (Priority: P2)

**Goal**: The vocabulary lives in one reference and every icon and size conforms to it.

**Independent Test**: Compare each concept's glyph across nav, tiles, headers, rows and dialogs;
confirm one glyph per concept, sizes drawn from the documented scale, and every referenced glyph present
in the Fluent set.

### Implementation for User Story 4

- [X] T022 [P] [US4] Extend the design-system reference in `specs/023-design-system-refinement/design-system.md`: add the new action concepts (sign in, create person, change role, reset password, retry, load more, cancel, close, ask again, back, view, sources) and state concepts (person active/locked, connection reachability, message severity) to the Concepts list, in place alongside the existing entries — per FR-010, FR-003
- [X] T023 [US4] Run audit A1 from `specs/025-fluent-system-icons/quickstart.md` (every `Symbol="…"` in XAML **and** every `SymbolRegular.*` in C# is a valid member) and confirm every new glyph is a `*24` row/control glyph with no ad-hoc `FontSize` introduced — per FR-005, SC-006, SC-007
- [X] T024 [US4] Concept-consistency check across `src/RAGGit.Client.WPF/Views/Pages/` and `src/RAGGit.Client.WPF/Components/ChatControl.xaml`: confirm one glyph per concept across surfaces — refresh ×4 (Library, History, My Docs, Admin), copy ×4 (message, prompt, answer, citation), dismiss ×3 (Cancel, Close, Deactivate), load-more ×2, ask-again ×2, and the Sources disclosure — per FR-003, SC-002

**Checkpoint**: One documented vocabulary, one size scale; US4 is independently verifiable.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Prove the behaviour freeze and record the outcome.

- [X] T025 [P] Run the full gates for `RAGGit.sln`: `dotnet csharpier check .`, `dotnet build RAGGit.sln -c Release -p:Platform=x64`, and the Unit/Contract/offline test filters; confirm zero assertion changes — per SC-009
- [X] T026 [P] Run the unchanged `023` audits over `src/RAGGit.Client.WPF/` (no hard-coded colour, no `Opacity=`, valid `ThemeResource` keys, automation-ID superset); confirm no second icon font or package was introduced (the `PackageReference` list in `src/RAGGit.Client.WPF/RAGGit.Client.WPF.csproj` is unchanged); and prove the change set stays read-only — `git diff --name-only` lists nothing under `src/RAGGit.Client.Core/`, `src/RAGGit.Workstation.Api/` or any `specs/*/contracts/api.yaml`, and only `src/RAGGit.Client.WPF/`, `specs/023-design-system-refinement/design-system.md` and this feature's docs — per FR-001, FR-006, FR-011, FR-013, SC-004, SC-008
- [X] T027 [P] Run audit A3 from `specs/025-fluent-system-icons/quickstart.md` and confirm the Ask page's person suggestion chips remain text-only, and that no icon was added to table column headers, numeric page buttons or the pager ellipsis — per FR-009
- [X] T028 Run the screenshot walkthrough in `specs/025-fluent-system-icons/quickstart.md` across the affected surfaces and themes and review the PNGs against contracts F1–F7 — per SC-001–SC-006
- [X] T029 Write the Implementation Record and Caveats into `specs/025-fluent-system-icons/tasks.md`: what changed, the verification evidence (A1–A4, the captures, the gate results), and any residual limitation — per FR-010, SC-007

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies — start immediately.
- **Foundational (Phase 2)**: depends on Setup; **blocks every user story** (a name that does not resolve must be caught before it is written).
- **US1 (Phase 3, P1)**: after Foundational. Delivers the MVP on its own.
- **US2 (Phase 4, P1)**: after Foundational; independent of US1 in intent, but its tasks touch several files US1 also touches (see per-file notes).
- **US3 (Phase 5, P2)**: after the stories it verifies (US1, US2).
- **US4 (Phase 6, P2)**: the vocabulary document (T022) is independent; the audits (T023, T024) need US1 and US2 landed.
- **Polish (Phase 7)**: after all desired stories.

### Within Each Story / Shared Files

- **US1**: T004–T011 each edit a distinct file and can run in parallel; T012 (ChatControl) is independent; T013 follows all of them.
- **US2**: T014 and T017 share `AdminUsersPage.xaml` (T014 then T017) and both follow US1's T009 in that file; T015 (`SettingsPage`) and T016 (four files) are independent.
- **US3/US4**: verification and documentation tasks have no file conflicts with each other.
- **Cross-story file overlap** — `LibraryPage.xaml`, `HistoryPage.xaml`, `DocumentsMinePage.xaml`, `QueryDetailPage.xaml`, `AdminUsersPage.xaml` and `SettingsPage.xaml` are edited by both US1 and US2. Run per-file work sequentially (US1's action glyph first, then US2's state glyph) even when the phases interleave.

### Parallel Opportunities

- Setup: T001 → T002.
- US1: T004–T012 in parallel (nine distinct files), then T013.
- US2: T015 and T016 in parallel; T014 → T017 on the shared Admin file.
- Polish: T025, T026, T027 in parallel; T028 then T029.

---

## Parallel Example: User Story 1

```text
Task: "T004 [P] [US1] Sign in glyph in LoginPage.xaml"
Task: "T005 [P] [US1] Ask again / Back / Copy glyphs in QueryDetailPage.xaml"
Task: "T007 [P] [US1] Refresh / View / Ask again / Load more glyphs in HistoryPage.xaml"
Task: "T010 [P] [US1] Upload / Cancel / Close glyphs in UploadDialog.xaml"
```

---

## Implementation Strategy

### MVP First (User Story 1 only)

1. Complete Phase 1 (Setup) and Phase 2 (Foundational).
2. Complete Phase 3 (US1): the text-only action controls gain their icons — this is the bulk of the
   request ("use fluent system icons where appropriate") and is demonstrable on its own.
3. **STOP and VALIDATE**: run T013; every action control shows its glyph with its label intact.

### Incremental Delivery

1. Setup + Foundational → baseline recorded, vocabulary proven to resolve.
2. US1 → actions are recognisable by icon (MVP).
3. US2 → state reads by glyph and colour.
4. US3 → legible and announced in every theme.
5. US4 → one documented vocabulary and size scale.
6. Polish → gates green, audits clean, behaviour freeze proven, recorded.

---

## Notes

- [P] = different files, no incomplete dependency.
- Do NOT change behaviour, bindings, commands, converters' contracts, or any `RAGGit.Client.Core`/API/spec contract (FR-013).
- Every icon name MUST be a valid `SymbolRegular`/`SymbolFilled` member (T003 proves the set up front); sizes MUST be on the `023` scale.
- No `Opacity=` and no hard-coded colour anywhere; glyph colour comes from theme roles.
- Preserve every automation ID from `022`/`023`; new interactive elements add new stable IDs.
- The Ask page's person suggestion chips stay text-only (clarified 2026-09-30; audit A3).
- There are no new test tasks: correctness is proven by the static audits A1–A4 and the screenshot review plus the existing suites (zero assertion changes).

---

## Implementation Record (2026-09-30)

All 29 tasks complete. Every qualifying action control now carries its agreed Fluent system glyph, every
stateful surface reads by glyph as well as colour, and no behaviour moved.

### What changed

- **US1 (actions)** — glyphs added beside the existing labels: Sign in `ArrowEnterLeft24` (LoginPage);
  Ask again `ArrowRepeatAll24` / Back `ArrowLeft24` / Copy ×3 `Copy24` (QueryDetailPage); Retry
  `ArrowClockwise24` (LibraryPage); Refresh `ArrowClockwise24` / View `Eye24` / Ask again
  `ArrowRepeatAll24` / Load more `ArrowDown24` (HistoryPage); Refresh / Load more
  (DocumentsMinePage); Refresh / role `PersonEdit24` / Create user `PersonAdd24` / Reset password
  `KeyReset24` (AdminUsersPage); Upload `ArrowUpload24` / Cancel / Close `Dismiss24` (UploadDialog);
  Sign out `SignOut24` (SettingsPage); Sources `TextQuote24` (ChatControl).
- **US2 (state)** — active toggle `Checkmark24` (Activate) ⇄ `Dismiss24` (Deactivate) and lock cell
  `LockOpen24` ⇄ `LockClosed24` (AdminUsersPage, via `DataTrigger`s on `IsActive`/`LockedOut`); admin
  status severity `Info24`/`CheckmarkCircle24`/`Warning24`/`ErrorCircle24` driven by `StatusSeverity`;
  connection reachability `PlugConnected24` ⇄ `PlugDisconnected24` (SettingsPage, code-behind using
  theme brushes); leading `ErrorCircle24` on the four footer error surfaces. The document status chip
  was already compliant and is unchanged.
- **US3/US4** — no new icon-only controls were introduced (every new glyph sits beside a label), so all
  existing accessible names and tooltips are retained; the vocabulary was recorded in
  `specs/023-design-system-refinement/design-system.md`.
- **Docs** — `specs/025-fluent-system-icons/baseline.md` records the pre-change inventory;
  `quickstart.md` audits A1/A2 were hardened to also read glyphs set through a `Style` setter
  (`<Setter Property="Symbol" Value="…">`), which csharpier may format across three lines.
- **Scope** — exactly `src/RAGGit.Client.WPF/` (10 files) plus
  `specs/023-design-system-refinement/design-system.md`; `RAGGit.Client.Core`, `RAGGit.Workstation.Api`
  and every contract are untouched (FR-013).

### Verification

| Check | Contract | Result |
|-------|----------|--------|
| A1 icon-name validity (XAML attribute + style setter + C#) | F1/SC-007 | ✅ 0 invalid |
| A2 concept vocabulary present | F1/SC-001 | ✅ all 16 present |
| A3 suggestion chips text-only | F6/FR-009 | ✅ OK |
| A4a no hard-coded colour | F3/SC-004 | ✅ 0 |
| A4b no `Opacity=` | F3/SC-004 | ✅ 0 |
| A4c `ThemeResource` keys valid | F3 | ✅ 0 invalid |
| A4d frozen automation IDs | F7/SC-008 | ✅ all 22 present |
| No new package | F7 | ✅ `PackageReference` list unchanged |
| `dotnet csharpier check .` | SC-009 | ✅ clean (242 files) |
| `dotnet build RAGGit.sln -c Release -p:Platform=x64` | SC-009 | ✅ 0 errors |
| Unit tests | SC-009 | ✅ 340/340 |
| Contract tests | SC-009 | ✅ 86/86 |
| Offline integration subset | SC-009 | ✅ 12/12 |
| Screenshots (Light) | F1/F5/SC-001–003 | ✅ Login, Admin, Settings, History, My Docs, Library, Ask |
| Screenshots (Dark) | F3/SC-006 | ✅ Admin — all new glyphs legible |
| Minimum size | F2/SC-006 | ✅ Admin at 816×639 — no icon clipping |
| Accessibility | F4/SC-005 | ✅ every icon-only `ui:Button` has an accessible name; UIA names present |

Screenshot evidence: `…\Temp\opencode\walkthrough-025\` — `login.png`, `admin2.png`,
`admin-dark2.png`, `admin-800x600-dark.png`, `settings3.png`, `history4.png`, `mydocs3.png`,
`library4.png`, `ask3.png`.

### Caveats

- **High Contrast** was not captured live (the client exposes only Light/Dark radios; High Contrast
  follows the OS theme). It is verified by construction: every new glyph colour is a
  `{ui:ThemeResource …}` role (`SystemFillColorSuccess/Caution/Critical`, `TextFillColorSecondary`),
  with A4a/A4b/A4c proving 0 literals, 0 `Opacity=` and 0 invalid keys — the `023` precedent.
- **Query Detail, the upload dialog and the chat "Sources" disclosure were not captured live**: the
  local session had no document to open in a query detail, the upload dialog needs files, and the Ask
  conversation was empty (no citations ⇒ no Sources expander). All three are covered by A1/A2 and use
  the same button-icon and inline-glyph patterns proven on the captured surfaces.
- **Ask suggestion chips** were confirmed text-only by audit A3 (the card shows only when a suggested
  person exists; none did in this session).
- **Connection row** now shows the reachability glyph in the value; the row already carried a static
  `PlugConnected24` icon, so a connected state shows two similar plugs. Accepted per T015 (the row icon
  is the row's subject; the value glyph is the state).
- **800×600** pre-existing behaviours remain (both documented by `023`): the People table's name column
  ellipsizes, and the side-by-side admin form headings clip at the right edge. No new icon clips.
- **Local dev state** (gitignored, not committed): the verification added a dev user `me` to
  `src/RAGGit.Workstation.Api/data/rag.db` and temporarily pointed the client's **output**
  `appsettings.json` at `http://localhost:5142` with a dev key; the output config was restored and the
  API/client processes stopped. No source `appsettings.json` was modified.
- **Audit gap found and fixed**: the quickstart A1/A2 patterns originally read only the
  `Symbol="…"` attribute, so glyphs set through a `Style` setter were invisible to them (A2 reported
  five false misses on the first run). The patterns now cover the setter form as well.

---

## Follow-up (2026-09-30): Library list layout

Requested after the feature landed: on the Library page, keep the top-right actions icon-only, anchor
the table to the top, and host it in a `ListView`.

**What changed** — `src/RAGGit.Client.WPF/Views/Pages/LibraryPage.xaml` and
`src/RAGGit.Client.WPF/Views/Pages/DashboardPage.xaml`:

- The list surface is now a `ListView` (`x:Name="DocumentsList"`, row `DataTemplate` unchanged with the
  `StatusChip` and the icon-only Download/Delete actions) instead of an `ItemsControl`.
- The column header is pinned in an `Auto` row above the `ListView`, which occupies the `*` row and owns
  the scrolling/virtualization; the empty state sits in the list area below the header.
- The container is a `Border` (the Card's own `CardBackground`/`CardBorderBrush`/`ControlCornerRadius`
  theme resources) rather than `ui:Card`: the Card style sets `VerticalContentAlignment="Center"` and a
  local override does not win against it in 4.3.0's template, which was floating the short table in the
  middle of the stretched card.
- The header actions (`RefreshButton`, `UploadButton`) stay icon-only at the top right, unchanged.
- **Dashboard** — the two stacked section cards ("Recent documents", "Recent questions") get the same
  treatment (`Border` + card theme resources), so their titles read at the top of the card instead of
  being pushed down whenever the sibling card is taller. Card backgrounds, borders and corner radius are
  unchanged; only the vertical placement moves.

**Trap found**: `SelectionMode="None"` is invalid — WPF-UI's `ListView` uses its own
`Wpf.Ui.Controls.SelectionMode` enum, which has no `None`; the value threw `XamlParseException` and
blanked the page. The list now uses `SelectionMode="Single"` (as the Admin people list does), so rows
are selectable where the old `ItemsControl` rows were not.

**Verified**: full solution builds with 0 errors; csharpier clean; unit 340/340 and contract 86/86; audits
A1–A4 still pass; and both surfaces were captured live — the Library header pinned at the top with the
ListView filling below it and the actions icon-only at the top right, and the two Dashboard cards with
their titles at the top. (Verification routed the startup page to each surface temporarily; that
scaffolding was reverted.)
