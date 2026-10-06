---

description: "Task list for feature 029-client-ux-optimization"
---

# Tasks: Client UX Optimization

**Input**: Design documents from `/specs/029-client-ux-optimization/`

**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Included for the `RAGGit.Client.Core` behavior-layer additions only (search session state, filtered views, filtered paging) — constitution VI mandates test-first for behavior-layer code. XAML-only work is gated by the build, the static audits, and the walkthrough in quickstart.md.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1–US5)
- Include exact file paths in descriptions

## Path Conventions

Single solution: `src/RAGGit.Client.Core/`, `src/RAGGit.Client.WPF/`, `tests/unit/` at repository root (see plan.md Project Structure).

---

## Phase 1: Setup

**Purpose**: Confirm a green baseline before any change (SC-010 regression gate).

- [X] T001 Run the baseline gates from specs/029-client-ux-optimization/quickstart.md: `dotnet build RAGGit.sln -c Release -p:Platform=x64`, `dotnet csharpier check .`, and the unit/contract/offline-integration suites — all green before starting

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The session-scoped search-text seam every search surface depends on (FR-003).

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

### Tests (write first, confirm red)

- [X] T002 [P] Write unit tests for the search session state in tests/unit/SearchSessionStateTests.cs: each surface (Library, History, My Documents, People) stores and returns its own text independently; a new instance starts empty; setting one surface never changes another — red until the seam exists
- [X] T003 Run the new tests in tests/unit/SearchSessionStateTests.cs and confirm they FAIL (constitution VI red gate)

### Implementation

- [X] T004 Implement `SearchSessionState` (one string per surface — Library, History, My Documents, People — empty by default, read/write, no persistence) in src/RAGGit.Client.Core/Services/SearchSessionState.cs, mirroring the `AskNavigationState` singleton seam, and register it as a singleton in src/RAGGit.Client.WPF/App.xaml.cs

**Checkpoint**: Foundation ready — user story implementation can now begin in parallel

---

## Phase 3: User Story 1 — Find any record without scrolling (Priority: P1) 🎯 MVP

**Goal**: Text search over the Library, History, and My Documents that filters as the user types, states the match count, shows a distinct no-match state, and survives navigation for the session (FR-001–FR-004; SC-001, SC-002; contracts U1).

**Independent Test**: With 60+ documents, find a specific document in the Library using only the search box — no paging or scrolling — and confirm the page count reflects the filtered total. On History and My Documents, confirm search filters the loaded records and re-applies after "Load more".

### Tests (write first, confirm red)

- [X] T005 [P] [US1] Write unit tests for Library search in tests/unit/LibrarySearchTests.cs (following the construction pattern in tests/unit/LibraryViewModelTests.cs): case-insensitive filename matching; punctuation matches literally and never errors; empty text returns everything; `MatchCount` equals the filtered count; with 60 documents and page size 20 a search matching 25 yields 2 pages and changing the text returns to page 1; replacing the loaded collection re-applies the current text with no unfiltered state observable — red until implemented
- [X] T006 [P] [US1] Write unit tests for incremental-surface search in tests/unit/HistorySearchTests.cs and tests/unit/DocumentsMineSearchTests.cs: filtering applies to loaded items only; after `LoadMore` the filter re-applies; `MatchCount` counts loaded matches, never the server total — red until implemented

### Implementation for User Story 1

- [X] T007 [US1] Add `SearchText` (seeded from and written back to `SearchSessionState`), a derived filtered view, and `MatchCount`/`HasMatch`/`IsSearchActive` to src/RAGGit.Client.Core/ViewModels/LibraryViewModel.cs: matching is case-insensitive substring over `Document.Filename` (`IndexOf` with `OrdinalIgnoreCase`), synchronous, no debounce; the pager (`LibraryPage.Create`) slices the filtered list and changing the text resets the page to 1 (contracts U1.2–U1.4, U1.6)
- [X] T008 [P] [US1] Add the same search surface to src/RAGGit.Client.Core/ViewModels/HistoryViewModel.cs (match `HistoryItem.PromptPreview` and `AnswerPreview`) and src/RAGGit.Client.Core/ViewModels/DocumentsMineViewModel.cs (match `DocumentMineItem.Filename`): filter the loaded `Items` only, leave `HasMore`/`LoadMore` untouched, and re-apply the filter after records append (contracts U1.4, U1.6)
- [X] T009 [US1] Add the search box, clear action, and match caption to src/RAGGit.Client.WPF/Views/Pages/LibraryPage.xaml: `LibrarySearchBox` (placeholder "Search documents"), `LibrarySearchClearButton` visible only while text is present, `LibraryMatchCount` reading "N of M documents", and a `LibraryNoMatchState` empty state distinct from the existing no-records state — which wins when the library is genuinely empty (contracts U1.1, U1.5)
- [X] T010 [P] [US1] Same search presentation for src/RAGGit.Client.WPF/Views/Pages/HistoryPage.xaml (`HistorySearchBox`, `HistorySearchClearButton`, `HistoryMatchCount`, `HistoryNoMatchState`; placeholder "Search questions"; caption "N matching of M loaded") and src/RAGGit.Client.WPF/Views/Pages/DocumentsMinePage.xaml (`DocumentsMineSearchBox`, `DocumentsMineSearchClearButton`, `DocumentsMineMatchCount`, `DocumentsMineNoMatchState`; placeholder "Search my documents") — the caption must never imply a server-wide total (contracts U1.4)

**Checkpoint**: User Story 1 fully functional and independently testable (MVP deliverable)

---

## Phase 4: User Story 2 — See what needs attention at a glance (Priority: P1)

**Goal**: The Dashboard distinguishes a non-zero failure count by glyph shape and text, shows em-dash placeholders while loading, and names each metric card's destination in visible text (FR-005–FR-007; SC-003, SC-004; contracts U2).

**Independent Test**: Load the Dashboard once with a failed document and once with none — the Failed metric is distinct only when non-zero, and the distinction survives high contrast. Watch a first load: em dashes, then real numbers, never a digit before completion.

### Implementation for User Story 2

- [X] T011 [US2] Extend the Failed metric in src/RAGGit.Client.WPF/Views/Pages/DashboardPage.xaml: when `FailedDocuments > 0` show `ErrorCircle24` plus a "Needs attention" caption (`DashboardMetricFailedAttention`); at zero show `CheckmarkCircle24` and no caption. Carry the distinction by glyph shape and text, not color alone, and gate all attention styling on `HasLoaded` so it never renders over a load failure (contracts U2.1–U2.3)
- [X] T012 [P] [US2] Render em-dash placeholders in place of metric numbers while `HasLoaded` is false in src/RAGGit.Client.WPF/Views/Pages/DashboardPage.xaml, styled with `SecondaryText` and containing no digit, replaced by the real bindings on success (contracts U2.4)
- [X] T013 [P] [US2] Add a visible destination caption to each of the six metric cards in src/RAGGit.Client.WPF/Views/Pages/DashboardPage.xaml (Library, Ask, My documents as appropriate), keeping the existing tooltips and accessible names on `DashboardMetric{Documents,Ready,Indexing,Failed,Queries,Mine}` (contracts U2.5)

**Checkpoint**: User Stories 1 AND 2 both work independently

---

## Phase 5: User Story 3 — Work the dense tables at any window size (Priority: P2)

**Goal**: Pinned, sortable People-directory headers and graceful column collapse on both tables at 720 DIPs, with collapsed information still reachable (FR-008–FR-010; SC-005, SC-006; contracts U3).

**Independent Test**: Scroll a 100-row People directory and confirm the headers and sort indicator stay put. At 800×600 with the navigation pane open, reach name, status, and row actions on both tables without horizontal scrolling; widen and confirm columns and sort order restore.

### Implementation for User Story 3

- [X] T014 [US3] Pin the People directory header in src/RAGGit.Client.WPF/Views/Pages/AdminUsersPage.xaml: suppress the `GridView` header row and render an identical header row as a sibling above `UsersList`, reusing the existing `GridViewColumnHeader.Click` sort handler and shared column widths so header and cells cannot drift; the header shows the current sort column and direction at any scroll position (contracts U3.1)
- [X] T015 [P] [US3] Extend the ≤720 DIP collapse in src/RAGGit.Client.WPF/Views/Pages/LibraryPage.xaml.cs to also collapse Type (70) and Size (90) — Creator and Created already collapse — keeping Filename, Status, and the row actions, and restoring all four on widening with sort order unchanged (contracts U3.2, U3.3)
- [X] T016 [US3] Add the same ≤720 DIP collapse to the People directory in src/RAGGit.Client.WPF/Views/Pages/AdminUsersPage.xaml.cs: collapse Role (135), Account status (190), and Action (132), keep the Person column (235), and restore on widening without changing the selected row, sort order, or loaded data. Collapsed information stays reachable through the details panel, which already shows role, status, lockout, and account actions (contracts U3.2–U3.5)

**Checkpoint**: User Stories 1, 2, AND 3 work independently

---

## Phase 6: User Story 4 — Finish frequent admin tasks in fewer steps (Priority: P2)

**Goal**: The first search match is highlighted automatically so the details panel offers account actions without a row click, and every dialog places the confirming action first and the dismissing action last (FR-011, FR-012; SC-007, SC-008; contracts U4, U5).

**Independent Test**: Reset a password and disable an account by typing the name and using only the details panel — no row click. Open every confirmation dialog and confirm the confirming action comes first and the dismissing action is last.

### Tests (write first, confirm red)

- [X] T017 [P] [US4] Write unit tests for first-match highlighting in tests/unit/AdminUsersSearchHighlightTests.cs: with search text set, the highlighted person is the first match; a keystroke never leaves a filtered-out person highlighted; clearing the search leaves the highlight unchanged — red until implemented

### Implementation for User Story 4

- [X] T018 [US4] Auto-highlight the first filtered match while the People search has text in src/RAGGit.Client.WPF/Views/Pages/AdminUsersPage.xaml.cs: set `SelectedUser` to the filtered view's first item after the existing rule that deselects a person filtered out of view (`AdminUsersPage.xaml.cs:121-123`), so a stale selection never survives a keystroke; clearing the search leaves the selection unchanged. The details panel already binds to `SelectedUser`, so reset password, change role, and enable/disable appear with no new controls and reuse their existing confirmations (contracts U4.1–U4.4)
- [X] T019 [P] [US4] Move the committing action before Cancel in src/RAGGit.Client.WPF/Views/Dialogs/CreatePersonDialog.xaml and src/RAGGit.Client.WPF/Views/Dialogs/ResetPasswordDialog.xaml (both currently declare Cancel first); keep `Appearance="Primary"` since these commit rather than destroy (contracts U5.1, U5.4)
- [X] T020 [US4] Verify dialog action order and record it: confirm `WpfDialogService.ConfirmAsync` in src/RAGGit.Client.WPF/Services/WpfDialogService.cs already renders "Yes" before "No" (the `ContentDialog` footer lays out Primary then Close), confirm the Upload dialog footer and its cancel confirmation in src/RAGGit.Client.WPF/Views/Dialogs/UploadDialog.xaml already comply with danger styling only on "Yes, cancel", and note the shared confirm's single "Yes" as the documented exception to per-action danger styling (contracts U5.2, U5.3)

**Checkpoint**: User Stories 1 through 4 work independently

---

## Phase 7: User Story 5 — Know what the app is doing during a wait (Priority: P3)

**Goal**: Plain-language progress during sign-in and answer preparation, and long answers arriving with their opening visible (FR-013, FR-014; SC-009; contracts U6).

**Independent Test**: Sign in against a slow workstation and confirm "Signing in…" shows during the wait and disappears after, never overlapping the error. Ask a question with a long answer and confirm its opening is in view on arrival.

### Implementation for User Story 5

- [X] T021 [P] [US5] Add a "Signing in…" line bound to `IsBusy` in src/RAGGit.Client.WPF/Views/Pages/LoginPage.xaml with automation id `LoginProgressText`, hidden when sign-in completes or fails so it never appears together with the error status (contracts U6.1)
- [X] T022 [US5] Reword the in-conversation progress line in src/RAGGit.Client.WPF/Components/ChatControl.xaml to name what is happening, and change `ScrollToNewest` in src/RAGGit.Client.WPF/Components/ChatControl.xaml.cs so the top of the newest message aligns with the top of the viewport — the opening is visible without scrolling and the remainder is reachable by scrolling down (contracts U6.2, U6.3)

**Checkpoint**: All user stories independently functional

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Audits, regression gate, and the recorded walkthrough across all stories.

- [X] T023 [P] Extend the static design audits per specs/029-client-ux-optimization/quickstart.md section 3: no new hard-coded color, no `Opacity=` emphasis, no spacing outside the 4px scale on touched surfaces; every new automation identifier from contracts/ui-contracts.md present and no pre-existing identifier renamed or removed; dialog button order verified in both dialog files
- [X] T024 Run `dotnet csharpier format .` on the touched `.xaml` and `.cs` files, then `dotnet build RAGGit.sln -c Release -p:Platform=x64` and the full unit/contract/offline-integration suites — green with zero assertion changes (SC-010)
- [ ] T025 Execute the 22-row `winapp ui` walkthrough in specs/029-client-ux-optimization/quickstart.md section 4 against a live workstation and record evidence under specs/029-client-ux-optimization/evidence/

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories
- **User Stories (Phase 3+)**: All depend on Foundational completion
  - US2, US3, US4, and US5 touch no file US1 touches except `AdminUsersPage` (US3 then US4, in that order) — so US2 and US5 can proceed in parallel with US1
  - US4 depends on US3 only because both edit `AdminUsersPage.xaml.cs`; its behavior does not depend on US3
- **Polish (Phase 8)**: Depends on all desired user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: After Foundational — no dependencies on other stories
- **User Story 2 (P1)**: After Foundational — fully independent (Dashboard only)
- **User Story 3 (P2)**: After Foundational — independent of US1/US2; its Library and People-directory edits touch different files and regions from US1
- **User Story 4 (P2)**: After Foundational — sequence after US3 for the shared `AdminUsersPage.xaml.cs`, otherwise independent
- **User Story 5 (P3)**: After Foundational — fully independent (Login and chat only)

### Within Each User Story

- Tests MUST be written and FAIL before the implementation they cover (T005/T006 before T007/T008; T017 before T018)
- Behavior-layer changes before the XAML that binds them (T007 before T009; T008 before T010)
- A story completes before the next priority begins, unless the parallel plan below is used

### Parallel Opportunities

- T005 and T006 can run together (different test files, both red-first)
- T009 and T010 can run together once T007 and T008 land (different pages)
- T011, T012, and T013 edit one file — run sequentially despite touching independent regions
- T015 runs parallel to T014 (different files); T016 follows T014 (same page)
- T019 runs parallel to T018 (different files)
- T021 runs parallel to everything in its phase

---

## Parallel Example: User Story 1

```bash
# Write both red tests together:
Task: "Library search tests in tests/unit/LibrarySearchTests.cs"
Task: "History and My Documents search tests in tests/unit/HistorySearchTests.cs and DocumentsMineSearchTests.cs"

# After the ViewModels land, build both search presentations together:
Task: "Search box, caption, and no-match state in LibraryPage.xaml"
Task: "Search presentation in HistoryPage.xaml and DocumentsMinePage.xaml"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL — blocks all stories)
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: Run the Library, History, and My Documents search scenarios from quickstart.md (rows 1–6)
5. Demo if ready — findability is the highest-value increment on its own

### Incremental Delivery

1. Setup + Foundational → session state ready
2. User Story 1 → search works everywhere → validate (MVP)
3. User Story 2 → Dashboard signals attention → validate
4. User Story 3 → tables work at 800×600 → validate
5. User Story 4 → admin tasks take fewer steps, dialogs agree → validate
6. User Story 5 → waits explain themselves → validate
7. Each story adds value without breaking previous stories

### Parallel Team Strategy

With multiple developers:

1. Team completes Setup + Foundational together
2. Once Foundational is done:
   - Developer A: User Story 1 (Library, History, My Documents)
   - Developer B: User Story 2 (Dashboard) and User Story 5 (Login, chat)
   - Developer C: User Story 3 then User Story 4 (People directory, dialogs)
3. Stories complete and integrate independently

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story should be independently completable and testable
- Verify tests fail before implementing (constitution VI)
- Commit after each task or logical group
- Stop at any checkpoint to validate the story independently
- Avoid: vague tasks, same-file conflicts, cross-story dependencies that break independence
- The tenth-of-a-second bound (SC-002) is met structurally by synchronous filtering — validate it in the walkthrough (T025), not with a timed unit test (research R2)

---

## Phase 9: Convergence

- [X] T026 Hide Dashboard attention styling whenever a load has failed, including a failed refresh that leaves the previous counts on screen, in src/RAGGit.Client.WPF/Views/Pages/DashboardPage.xaml and src/RAGGit.Client.Core/ViewModels/DashboardViewModel.cs per FR-007 (partial)
- [X] T027 State the Library match caption as the filtered count of the full library total ("N of M documents") in src/RAGGit.Client.WPF/Views/Pages/LibraryPage.xaml and src/RAGGit.Client.Core/ViewModels/LibraryViewModel.cs per contracts U1.4 (partial)
- [X] T028 Restore the Library page position when the search is cleared, while a new query still returns to page 1, in src/RAGGit.Client.Core/ViewModels/LibraryViewModel.cs per spec edge case "Clearing a search restores the prior page position" (partial)
- [X] T029 Show the current sort column and direction on the pinned People header from first load, not only after a header click, in src/RAGGit.Client.WPF/Views/Pages/AdminUsersPage.xaml and AdminUsersPage.xaml.cs per FR-008 (partial)
- [X] T030 Seed the People directory search box from SearchSessionState and write it back as the user types, so the text survives leaving and returning, in src/RAGGit.Client.WPF/Views/Pages/AdminUsersPage.xaml.cs per plan: SearchSessionState (partial)
