# Tasks: FontIcon Glyphs (Replace Converter-Driven Text Glyphs)

**Input**: Design documents from `/specs/012-fonticon-glyphs/` (spec.md with US1â€“US2, plan.md with converter-preserving structure, research.md with verified codepoints E73E/E711/E72E, contracts/icon-glyphs.md, data-model.md, quickstart.md)

**Prerequisites**: plan.md (present), spec.md (present), constitution v1.3.0 (no amendment; Constitution Check PASS)

**Tests**: Constitution VI adaptation (documented 2026-09-17): direct unit asserts are BLOCKED â€” `tests/unit` is plain `net8.0` and the glyph converters live behind the WinUI `net8.0-windows10.0.17763.0` TFM boundary (`Microsoft.UI.Xaml` types unloadable from net8.0 tests; referencing the WinExe would force retargeting the shared test project â€” rejected per VII). Substitute gates: XAML-compiler verification + output grep contract check + 3-theme visual validation (T002). Existing suites must stay green with zero assertion changes.

**Organization**: Grouped by user story; each story independently testable. Writable: `src/RAGGit.Client.WinUI/Converters/ViewConverters.cs` (the two glyph converters only) + `src/RAGGit.Client.WinUI/Views/AdminUsersPage.xaml`. Core, converters beyond the two glyph ones, and all tests READ-ONLY.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Confirm current glyph sites before changing them

- [x] T001 Inspect `ActiveGlyphConverter`/`LockedGlyphConverter` in `src/RAGGit.Client.WinUI/Converters/ViewConverters.cs` and their usages in `src/RAGGit.Client.WinUI/Views/AdminUsersPage.xaml`; record current outputs (âœ“/âœ—/ðŸ”’/â€”) against `specs/012-fonticon-glyphs/data-model.md`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Contract gate before implementation â€” direct unit asserts BLOCKED by the WinUI TFM boundary (see Tests header); substitute verification below

**âš  CRITICAL**: No user story work can begin until this phase is complete

- [x] T002 Verify converter contract gate in `src/RAGGit.Client.WinUI/Converters/ViewConverters.cs` (current outputs âœ“/âœ—/ðŸ”’/â€” recorded per T001) and confirm the WinUI build is green BEFORE the change (baseline for T003 comparison)

**Checkpoint**: Red confirmed â€” implementation may begin

---

## Phase 3: User Story 1 â€” Admin status glyphs render as proper icons (Priority: P1) â˜… MVP

**Goal**: Check/cross/lock FontIcons themed in Light/Dark/Contrast; unlocked cells empty

**Independent Test**: Admin list in all 3 themes shows CheckMark/Cancel/Lock icons, all legible; unlocked cells empty

- [x] T003 [US1] Change `ActiveGlyphConverter`/`LockedGlyphConverter` in `src/RAGGit.Client.WinUI/Converters/ViewConverters.cs` to emit `"\uE73E"`/`"\uE711"`/`"\uE72E"`/`""` (depends on T002) and grep-verify outputs plus green WinUI build
- [x] T004 [US1] Swap Active toggle content and Locked indicator to `FontIcon` with `Glyph` binding, `FontFamily="{ThemeResource SymbolThemeFontFamily}"`, theme `Foreground`, and accessible names/tooltips in `src/RAGGit.Client.WinUI/Views/AdminUsersPage.xaml` per `specs/012-fonticon-glyphs/contracts/icon-glyphs.md` (depends on T003)

**Checkpoint**: At this point, US1 should be fully functional and testable independently (MVP shippable)

---

## Phase 4: User Story 2 â€” Icons announced correctly by screen readers (Priority: P2)

**Goal**: Meaning announced, never raw characters; empty cells silent

**Independent Test**: Screen-reader walk of Admin list announces toggle name + state and "Locked"; empty cells silent

- [x] T005 [US2] Verify unlocked empty-cell rendering and accessible names/tooltips in `src/RAGGit.Client.WinUI/Views/AdminUsersPage.xaml` against `specs/012-fonticon-glyphs/contracts/icon-glyphs.md` (static markup check, no new bindings) plus live screen-reader walkthrough per `specs/012-fonticon-glyphs/quickstart.md` Â§3

**Checkpoint**: At this point, User Stories 1 AND 2 should both work independently

---

## Phase 5: Polish & Cross-Cutting Concerns

**Purpose**: Gates and scope guard

- [x] T006 Run full validation: `dotnet build RAGGit.sln` + `dotnet test` (unit 228, contract 78, integration 62) + `dotnet csharpier check .` with zero test assertion changes
- [x] T007 [P] Scope guard: verify `git status` shows no changes under `src/RAGGit.Client.Core/` or `tests/`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies â€” can start immediately (read-only inspection)
- **Foundational (Phase 2)**: Depends on Setup â€” BLOCKS all user stories (red gate)
- **User Stories (Phases 3â€“4)**: Depend on Foundational gate; T004 depends on T003; T005 verifies T004 output
- **Polish (Phase 5)**: Depends on all stories complete

### User Story Dependencies

- **User Story 1 (P1)**: After Foundational â€” no dependencies on other stories
- **User Story 2 (P2)**: Verifies US1 markup â€” independently testable via screen reader + markup check

### Within Each User Story

- T002 contract gate (baseline build green) before T003 implementation
- Converters (T003) before XAML usages (T004)
- Story complete before moving to next priority

### Parallel Opportunities

- T007 can run with T006 (verification vs gates, different concerns)
- No other parallelism: stories share `AdminUsersPage.xaml` and `ViewConverters.cs` â€” strictly sequential

---

## Parallel Example: Polish

```bash
# Launch gates and scope guard together:
Task: "Full validation: build + test + csharpier"
Task: "Scope guard: git status Core/tests check"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (inspect current glyphs)
2. Complete Phase 2: Foundational (red asserts â€” CRITICAL)
3. Complete Phases 3: US1 (converters green + FontIcon XAML)
4. **STOP and VALIDATE**: US1 independent test in all 3 themes; suites green
5. Deploy/demo MVP if ready

### Incremental Delivery

1. Setup + red asserts â†’ failing baseline proven
2. + US1 â†’ icons themed (MVP!)
3. + US2 â†’ announcements verified
4. Polish â†’ gates + scope guard

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Converter signatures (`IValueConverter`, boolâ†’string) MUST NOT change â€” only emitted values
- Codepoints fixed by research: E73E CheckMark, E711 Cancel, E72E Lock (MDL2-shared, 1809-safe)
- Commit after each task or logical group; stop at any checkpoint to validate story independently
- Avoid: guessing codepoints, touching Core/tests beyond the new asserts, new packages

