# Tasks: Per-Person Query & Document History

**Input**: Design documents from `/specs/005-per-person-history/`
**Prerequisites**: plan.md (required), spec.md (5 stories P1/P2), research.md (R1-R7), data-model.md (projections), contracts/api.yaml (1.4.0 delta), quickstart.md (11 steps)
**Tests**: Constitution VI Test-First NON-NEGOTIABLE — contract/integration tests written first and FAIL before implementation; WAN-disabled suite via unshare -n fallback.
**Organization**: Tasks grouped by user story to enable independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1-US5)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Tooling check — no new dependencies for this query-layer feature

- [ ] T001 Verify build/test toolchain on `005-per-person-history` branch in `specs/005-per-person-history/plan.md:15` (dotnet 8, `dotnet build`, `dotnet csharpier check .`, `unshare -n` probe) — no new NuGet package

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared query-projection infrastructure that MUST be complete before ANY user story

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [ ] T002 Create `src/RAGGit.Core/Data/QueryHistoryStore.cs` skeleton with `ClampLimit/ClampOffset`, `TruncatePreview` (≤121 with …), `HistoryPageDto`/`HistoryItemDto`, and `EXPLAIN QUERY PLAN` helper for `IX_Queries_UserId` vs `IX_Queries_UserId_CreatedAt_Id` (per `research.md` R1/R5) — no DDL yet
- [ ] T003 Create `src/RAGGit.Core/Data/DocumentMineStore.cs` skeleton with same pagination envelope per `data-model.md` recent-documents projection
- [ ] T004 [P] Add `src/RAGGit.Core/Models/QueryHistoryDtos.cs` (`HistoryItem`, `HistoryPage`, `QueryDetail`, `DocumentMineItem/Page`) matching `contracts/api.yaml` 1.4.0 schemas

**Checkpoint**: Foundational ready — stores skeletons and DTOs compile; user stories can now proceed (read-projections, no migration)

---

## Phase 3: User Story 1 — Person views own query history (Priority: P1) — MVP

**Goal**: `GET /api/queries/history?limit=&offset=` returns only caller's queries (`UserId == sub` AND `NOT IN ('admin','employee')`) ordered `CreatedAt DESC, Id DESC`, with `promptPreview/answerPreview ≤121`, `citationCount`, stable pagination, `401` if unauth.

**Independent Test**: Sign in as Employee A (2 queries) — history returns exactly 2, no Employee B rows; Employee B (1 query) — history returns exactly 1.

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T005 [P] [US1] Contract test for `GET /api/queries/history` pagination/clamping in `tests/contract/HistoryContractTests.cs` (limit default 20, max 100 clamp, offset beyond total → empty `items` with correct `total`, stable order)
- [ ] T006 [P] [US1] Contract test for `GET /api/queries/history` auth/empty in `tests/contract/HistoryContractTests.cs` (401 without token, empty-state `items:[] total:0` for new user, preview truncation ≤121)
- [ ] T007 [P] [US1] Integration test for own-history isolation seed in `tests/integration/CrossUserIsolationTests.cs` (≥2 users ≥20 mixed actions, assert own-only)

### Implementation for User Story 1

- [ ] T008 [P] [US1] Implement `QueryHistoryStore.ListAsync(sub, limit, offset)` in `src/RAGGit.Core/Data/QueryHistoryStore.cs` per `data-model.md` (filter `UserId == @sub AND NOT IN`, `ORDER BY CreatedAt DESC, Id DESC LIMIT/OFFSET`, `COUNT(*)`, `SUBSTR` preview truncation, optional `IX_Queries_UserId_CreatedAt_Id` only if `EXPLAIN` shows scan)
- [ ] T009 [US1] Implement `GET /api/queries/history` in `src/RAGGit.Workstation.Api/Controllers/QueryController.cs` (extract `sub` from `User.FindFirstValue(ClaimTypes.NameIdentifier)` / `sub`, clamp `limit/offset`, call store, return `HistoryPage` with echoed `limit/offset`, `401` if no `sub`, `200` even when empty) — depends on T008
- [ ] T010 [US1] Add `src/RAGGit.Client.Maui/Services/QueryHistoryApiClient.cs` typed `GetHistoryAsync(limit,offset)` via `BearerDelegatingHandler` (reuse `SessionTokenStore` from 004) — depends on T009
- [ ] T011 [US1] Add `src/RAGGit.Client.Maui/ViewModels/HistoryViewModel.cs` (load first page, `LoadMoreAsync` increments `offset`, `PullToRefresh`, `EmptyView` when `total==0`, stable `ObservableCollection`) — depends on T010
- [ ] T012 [US1] Add `src/RAGGit.Client.Maui/Views/HistoryView.xaml` + `HistoryView.xaml.cs` with `SfListView` `ItemTemplate` (promptPreview, answerPreview, citationCount, createdAt), `EmptyView`, `LoadMore` bound to `HistoryViewModel` — depends on T011

**Checkpoint**: US1 fully functional — own history paginated, empty-state, stable order, 401 without auth, no new tables, additive `1.4.0`.

---

## Phase 4: User Story 2 — Person views a past query with full answer and citations (Priority: P1)

**Goal**: `GET /api/queries/{id}` returns full `prompt/answer/citations[]` iff `Queries.UserId == sub` and not legacy, `404` if not owned/not found, `401` if unauth.

**Independent Test**: Ask question → 2 citations → open history → tap entry → full answer + 2 citations; tap older → its answer loads; non-owner → 404.

### Tests for User Story 2

- [ ] T013 [P] [US2] Contract test for `GET /api/queries/{id}` detail in `tests/contract/HistoryContractTests.cs` (own `200` with `citations[]`, non-owner `404`, not-found `404`, `401` without token, legacy `404`)
- [ ] T014 [P] [US2] Integration test for detail citation persistence in `tests/integration/CrossUserIsolationTests.cs` (query with `no relevant content found` → `citations:[]`)

### Implementation for User Story 2

- [ ] T015 [US2] Implement `QueryHistoryStore.GetDetailAsync(sub, id)` in `src/RAGGit.Core/Data/QueryHistoryStore.cs` (`SELECT … WHERE Id=@id AND UserId=@sub AND NOT IN ('admin','employee')`, `JOIN QueryCitations ORDER BY ordinal`) — depends on T008
- [ ] T016 [US2] Implement `GET /api/queries/{id}` in `src/RAGGit.Workstation.Api/Controllers/QueryController.cs` (ownership check, `404` if null, `401` if no `sub`, return `QueryDetail`) — depends on T015
- [ ] T017 [US2] Extend `src/RAGGit.Client.Maui/Services/QueryHistoryApiClient.cs` with `GetDetailAsync(id)` — depends on T016
- [ ] T018 [US2] Add `src/RAGGit.Client.Maui/Views/QueryDetailView.xaml` + `QueryDetailView.xaml.cs` (full answer `Label`, citations `SfListView` with `documentId/chunkId/text/ordinal`) bound to detail DTO — depends on T017

**Checkpoint**: US2 independently functional — detail scoped, citations ordered, 404 isolation.

---

## Phase 5: User Story 4 — Cross-user isolation is enforced (Priority: P1)

**Goal**: Zero cross-user rows on history/detail regardless of role (Admin sees only own; expired/deactivated → `401`); legacy `admin/employee` never appears.

**Independent Test**: Create Admin + Employee, each queries+uploads → each sees only own on `GET /history` and `GET /{id}` (non-owner 404); expired JWT → 401; deactivated mid-browse → next page `401`.

### Tests for User Story 4

- [ ] T019 [P] [US4] Integration test cross-user history leak in `tests/integration/CrossUserIsolationTests.cs` (FR-007/FR-012/SC-002: ≥2 users ≥20 actions, assert `0` rows from other person on history and detail `404` for non-owner)
- [ ] T020 [P] [US4] Integration test legacy exclusion in `tests/integration/CrossUserIsolationTests.cs` (FR-008/SC-005: insert legacy `UserId='admin'` row, assert not in either person's history)

### Implementation for User Story 4

- [ ] T021 [US4] Harden `QueryHistoryStore` and `QueryController` to enforce `AND UserId NOT IN ('admin','employee')` + `OnTokenValidated` deactivation path already from 004 (no code change if already present; verify) — depends on T009, T016
- [ ] T022 [US4] Verify `GET /api/queries/history` and `GET /api/queries/{id}` return `401` for expired/inactive via existing `JwtBearer` + `OnTokenValidated` (no bypass) — add assertion in `tests/integration/CrossUserIsolationTests.cs` if missing

**Checkpoint**: Isolation invariant holds — Admin own-only, legacy excluded, 0 leak.

---

## Phase 6: User Story 3 — Person views own recent documents (Priority: P2)

**Goal**: `GET /api/documents/mine?limit=&offset=` returns only documents where `CreatedBy == sub` AND `NOT IN ('admin','employee')`, paginated, `401` if unauth, `200` empty if none.

**Independent Test**: Employee A uploads 2, Employee B uploads 1 — A sees only 2, B sees only 1; new user sees empty.

### Tests for User Story 3

- [ ] T023 [P] [US3] Contract test for `GET /api/documents/mine` in `tests/contract/RecentDocumentsContractTests.cs` (401 without token, empty `items:[] total:0`, own-only rows, stable order `CreatedAt DESC, Id DESC`, limit/offset clamp)
- [ ] T024 [P] [US3] Integration test for mine isolation in `tests/integration/CrossUserIsolationTests.cs` (FR-005/FR-007: ≥2 users with uploads, assert mine only own)

### Implementation for User Story 3

- [ ] T025 [US3] Implement `DocumentMineStore.ListAsync(sub, limit, offset)` in `src/RAGGit.Core/Data/DocumentMineStore.cs` (`WHERE CreatedBy == @sub AND NOT IN`, `ORDER BY CreatedAt DESC, Id DESC`, `COUNT(*)`) — depends on T003
- [ ] T026 [US3] Implement `GET /api/documents/mine` in `src/RAGGit.Workstation.Api/Controllers/DocumentsController.cs` (extract `sub`, clamp, call store, `401` if no `sub`) — depends on T025
- [ ] T027 [US3] Extend `src/RAGGit.Client.Maui/Services/DocumentsApiClient.cs` with `GetMineAsync(limit,offset)` — depends on T026
- [ ] T028 [US3] Extend `src/RAGGit.Client.Maui/Views/HistoryView.xaml` or add `DocumentsMineView.xaml` with `SfListView` for mine items (filename, size, status, createdAt), empty state — depends on T027

**Checkpoint**: Recent-documents mine independently functional — own-only, paginated, no leak.

---

## Phase 7: User Story 5 — History works offline (WAN logically off) (Priority: P2)

**Goal**: History and mine return same data/latency WAN-disabled (LAN-only SQLite), `0` egress.

**Independent Test**: `unshare -n dotnet test --filter OfflineHistoryTests` (fallback probe as in `.github/workflows/ci.yml`) — same seed returns same `total/items`.

### Tests for User Story 5

- [ ] T029 [P] [US5] Integration test `OfflineHistoryTests` in `tests/integration/OfflineHistoryTests.cs` (seed 2 users × queries, run `GET /history` and `GET /documents/mine` both WAN-enabled and WAN-disabled, assert same `total/items` and no external call, SC-004)

### Implementation for User Story 5

- [ ] T030 [US5] Verify `QueryHistoryStore`/`DocumentMineStore` are pure `Microsoft.Data.Sqlite` reads (no `Ollama`/`LanceDB`/HTTP) — already per `research.md` R6; no code change if true, add `// offline: SQLite-only` comment — depends on T009, T026

**Checkpoint**: Offline invariant proven — LAN-only reads, `SC-004` PASS.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Additive contract, performance, formatting, CI, verification

- [ ] T031 Update `specs/005-per-person-history/contracts/api.yaml` reference in `README.md` and `docs/operator-cli.md` for `1.4.0` additive (`/queries/history`, `/{id}`, `/documents/mine`) — no breaking change
- [ ] T032 Run `specs/005-per-person-history/quickstart.md` steps 1-11 validation and fill `specs/005-per-person-history/verification.md` SC-001..005 with measured timings (`SC-001 p95 <2s 1k rows`)
- [ ] T033 Extend `.github/workflows/ci.yml` WAN-disabled leg with `OfflineHistoryTests` (reuse `unshare -n` fallback) and assert `0` cross-user leak (`CrossUserIsolationTests`)
- [ ] T034 Security hardening — ensure history/detail/mine never leak `PasswordHash` or other user's rows, `401`/`404` semantics, `404` not `403` for non-owned detail to avoid enumeration
- [ ] T035 Run `dotnet csharpier check .`, `dotnet build`, `dotnet test` (contract 100%, library ≥80%), remove any placeholder `.write-probe.txt`, verify no Archify drift (history is query-layer, `specs/004-identity/docs/{architecture,workflow,sequence}.html?theme=light` still valid)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories (stores skeletons + DTOs)
- **User Stories (Phase 3+)**: All depend on Foundational completion
  - `US1 (P1)` → `US2 (P1)` (detail reuses `ListAsync` store) → `US4 (P1)` isolation (hardens same endpoints) — can be parallelized if store contracts fixed
  - `US3 (P2)` documents mine independent of history but reuses pagination envelope — can run in parallel with `US1/US2` after Foundational
  - `US5 (P2)` offline verification depends on `US1/US3` endpoints existing
- **Polish (Phase 8)**: Depends on all desired stories being complete

### User Story Dependencies

- **US1 (P1) History list**: After Foundational — no other story dependency
- **US2 (P1) Detail**: After Foundational + `US1` store (reuses `QueryHistoryStore`)
- **US4 (P1) Isolation**: After `US1`+`US2` endpoints — verifies same code paths
- **US3 (P2) Mine**: After Foundational — independent of `US1/US2`
- **US5 (P2) Offline**: After `US1`+`US3` — verifies same stores under `unshare -n`

### Within Each User Story

- Tests (contract/integration) MUST be written and FAIL before implementation
- Store before controller, controller before client
- Client `Services` before `ViewModels` before `Views`

### Parallel Opportunities

- `T002` and `T004` in Foundational can run in parallel (different files)
- `T005`/`T006` contract history tests can run in parallel
- `T013` + `T023` contract detail vs mine tests can run in parallel (different files)
- `T019`/`T020` isolation tests can run in parallel
- `T008` and `T025` store impls can run in parallel (different files)

---

## Parallel Example: User Story 1

```bash
# Launch all tests for US1 together (TDD red-first):
Task: "Contract test GET /api/queries/history pagination in tests/contract/HistoryContractTests.cs"
Task: "Contract test GET /api/queries/history auth/empty in tests/contract/HistoryContractTests.cs"
Task: "Integration test own-history isolation seed in tests/integration/CrossUserIsolationTests.cs"

# Launch all stores for history detail vs mine together (after Foundational):
Task: "Implement QueryHistoryStore.ListAsync in src/RAGGit.Core/Data/QueryHistoryStore.cs"
Task: "Implement DocumentMineStore.ListAsync in src/RAGGit.Core/Data/DocumentMineStore.cs"
```

---

## Implementation Strategy

### MVP First (US1 Only)

1. Complete Setup + Foundational → Foundation ready (T001-T004)
2. Complete US1 History list (T005-T012) → Test independently → Deploy/Demo (MVP! own history paginated, 401, no leak)
3. Validate `SC-002` partial (own-only) and `SC-003` pagination on MVP

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. Add US1 (History list) → Test independently → Deploy/Demo
3. Add US2 (Detail with citations) → Test independently → Deploy/Demo
4. Add US4 (Isolation hardening) → `0` leak verification → Deploy/Demo
5. Add US3 (Mine) → Test independently → Deploy/Demo
6. Add US5 (Offline) → `unshare -n` verification → Deploy/Demo
7. Each story adds value without breaking previous stories (additive `1.4.0`)

### Parallel Team Strategy

With multiple developers after Foundational:

1. Developer A: `US1` History list (T005-T012)
2. Developer B: `US3` Documents mine (T023-T028)
3. Developer C: `US2` Detail (T013-T018) + `US4` isolation (T019-T022)
4. Stories complete and integrate independently; `US5` offline verification last

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story independently completable and testable per `spec.md` US1-US5
- Verify tests fail before implementing (Constitution VI Test-First)
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- File paths are absolute per `plan.md` Project Structure (`RAGGit.sln` single-project hybrid)
