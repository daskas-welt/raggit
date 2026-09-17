# Tasks: Remove Upload Nav Item

**Input**: Design documents from `/specs/013-remove-upload-nav/` (spec.md with US1, plan.md with two-file removal, research.md with removal inventory, contracts/nav-items.md, data-model.md, quickstart.md)

**Prerequisites**: plan.md (present), spec.md (present), constitution v1.3.0 (no amendment; Constitution Check PASS)

**Tests**: No new test tasks — removal, not logic. Existing suites (`tests/unit`, `tests/contract`, `tests/integration`) MUST stay green with zero assertion changes.

**Organization**: Single user story; removal verified by build + nav walkthrough + scope guard. Writable: `src/RAGGit.Client.WinUI/MainWindow.xaml` + `src/RAGGit.Client.WinUI/MainWindow.xaml.cs` only. Core, Library flow, and tests READ-ONLY.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Confirm removal inventory before touching the shell

- [ ] T001 Verify Upload nav sites via grep (`UploadItem`, `"upload"` case, visibility line) in `src/RAGGit.Client.WinUI/MainWindow.xaml` and `src/RAGGit.Client.WinUI/MainWindow.xaml.cs` against `specs/013-remove-upload-nav/research.md`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Green baseline before removal

**⚠ CRITICAL**: No story work can begin until this phase is complete

- [ ] T002 Confirm green baseline build of `src/RAGGit.Client.WinUI/RAGGit.Client.WinUI.csproj` (`-p:Platform=x64`, 0 errors) BEFORE the removal

**Checkpoint**: Baseline green — removal may begin

---

## Phase 3: User Story 1 — Upload lives only in the Document Library (Priority: P1) ★ MVP

**Goal**: Five-item nav pane for every role; Library header remains the sole upload entry

**Independent Test**: Admin pane shows Library/Ask/History/My Docs/Admin (no Upload); employee minus Admin; Library Upload button uploads end-to-end

- [ ] T003 [US1] Remove the `UploadItem` `NavigationViewItem` from `src/RAGGit.Client.WinUI/MainWindow.xaml` (depends on T002)
- [ ] T004 [US1] Remove the `"upload"` selection branch, the now-unreferenced dialog-open helper, and the `UploadItem.Visibility` line in `src/RAGGit.Client.WinUI/MainWindow.xaml.cs` (depends on T003); keep `NavItemForPage` (maps page types, none upload)
- [ ] T005 [US1] Verify Library header Upload flow untouched (button, `UploadDialog`, refresh, admin gating) and rebuild `src/RAGGit.Client.WinUI/RAGGit.Client.WinUI.csproj` green (depends on T004)

**Checkpoint**: US1 fully functional and independently testable (MVP shippable)

---

## Phase 4: Polish & Cross-Cutting Concerns

**Purpose**: Gates and scope guard

- [ ] T006 Run full validation: `dotnet build RAGGit.sln` + `dotnet test` (unit 228, contract 78, integration 62) + `dotnet csharpier check .` with zero test assertion changes
- [ ] T007 [P] Scope guard: verify `git status` shows no changes under `src/RAGGit.Client.Core/` or `tests/`, and grep confirms no `upload` references remain in `src/RAGGit.Client.WinUI/MainWindow.xaml*`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — read-only verification
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS the story
- **User Story (Phase 3)**: Depends on Foundational; T003 → T004 → T005 strictly sequential (same files)
- **Polish (Phase 4)**: Depends on story complete; T007 can run with T006

### Within the Story

- XAML item (T003) before code-behind branch (T004) before rebuild verify (T005)
- Story complete before Polish

### Parallel Opportunities

- T007 with T006 only — everything else shares the two shell files

---

## Parallel Example: Polish

```bash
# Launch gates and scope guard together:
Task: "Full validation: build + test + csharpier"
Task: "Scope guard: Core/tests clean, no upload refs in shell"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (inventory verified)
2. Complete Phase 2: Foundational (baseline green — CRITICAL)
3. Complete Phase 3: US1 (remove → rebuild → verify Library flow)
4. **STOP and VALIDATE**: US1 independent test (5-item pane per role, Library upload end-to-end)
5. Deploy/demo MVP if ready

---

## Notes

- [P] tasks = different files, no dependencies
- Removal only — never hide instead of delete (dead XAML/branches are the drift this kills)
- `NavItemForPage` stays: it maps page types, and no upload page type ever existed
- Commit after each task or logical group
- Avoid: touching the Library flow, Core, or tests
