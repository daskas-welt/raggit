---

description: "Task list for Intent-Aware Chunking"
---

# Tasks: Intent-Aware Chunking

**Input**: Design documents from `/specs/030-intent-aware-chunking/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/](contracts/), [quickstart.md](quickstart.md)

**Tests**: **REQUIRED.** Constitution VI (Test-First, NON-NEGOTIABLE) and AGENTS.md mandate TDD — write these tests first, confirm they FAIL, then implement. Never delete a test to make a build pass.

**Organization**: Tasks are grouped by user story (US1–US4 from spec.md) so each is an independently testable increment. US1 is the MVP.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: US1–US4; omitted for Setup, Foundational, and Polish phases
- Every task names its exact file path

## Path Conventions

- Existing multi-project solution (no new project). Libraries `src/RAGGit.Core`, `src/RAGGit.Ingest`, `src/RAGGit.Retrieval`; host `src/RAGGit.Workstation.Api`; client `src/RAGGit.Client.Core` + `src/RAGGit.Client.WPF`; tests `tests/unit|contract|integration`.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Confirm a clean baseline and add feature configuration defaults.

- [x] T001 Confirm the baseline is green before any change: from repo root run `dotnet build RAGGit.Server.slnf -c Release`, then `dotnet test` for unit, contract, and the offline subset (`QueryOfflineTests|OfflineIdentityTests|OfflineHistoryTests|CrossUserIsolationTests`). Record any pre-existing failure so it is not later attributed to this feature.
- [x] T002 [P] Add feature config defaults to `src/RAGGit.Workstation.Api/appsettings.json` and `src/RAGGit.Workstation.Api/appsettings.Development.json`: `Ingest:ParentGroupSize` = `4`, `Retrieval:BroadCandidateMultiplier` = `4`, `Retrieval:MaxCandidates` = `50`.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The two-level data/index substrate that every user story builds on.

**⚠️ CRITICAL**: No user-story work can begin until this phase is complete.

- [x] T003 [P] Add enums in `src/RAGGit.Core/Models/ChunkLevel.cs` (`Child = 0, Parent = 1`) and `src/RAGGit.Core/Models/QueryIntent.cs` (`QueryMode { Auto, Broad, Specific }`, `QueryIntent { Granular, Broad }`).
- [x] T004 [P] Extend `src/RAGGit.Core/Models/Chunk.cs` with `public ChunkLevel Level { get; set; }` and `public Guid? ParentId { get; set; }` (child ⇒ parent's id; parent ⇒ `null`).
- [x] T005 [P] Extend `src/RAGGit.Core/Abstractions/SearchResult.cs` (positional record) with an **optional trailing** `string? ParentId = null` so existing constructor calls keep compiling.
- [x] T006 [P] Extend `src/RAGGit.Core/Models/IngestOptions.cs` with `int ParentGroupSize { get; set; } = 4` (must be ≥ 1) and `src/RAGGit.Core/Models/RetrievalOptions.cs` with `int BroadCandidateMultiplier { get; set; } = 4` and `int MaxCandidates { get; set; } = 50`.
- [x] T007 Extend `src/RAGGit.Core/Data/RagDbContext.cs`: add `Chunks.Level INTEGER NOT NULL DEFAULT 0`, `Chunks.ParentId TEXT`, `Queries.Mode TEXT` to the fresh-schema commands **and** via the existing probe-then-`ALTER TABLE` pattern (SQLite lacks `ADD COLUMN IF NOT EXISTS`); create `IX_Chunks_Document_Level (DocumentId, Level)` and `IX_Chunks_Parent (ParentId)`.
- [x] T008 Extend `src/RAGGit.Core/Abstractions/Repositories/IDocumentRepository.cs` and `src/RAGGit.Core/Data/SqliteDocumentRepository.cs`: persist `Level`/`ParentId` in `AddChunksAsync`; read them back; add `GetChunksByIdsAsync(IEnumerable<Guid>)` (parent resolution) and `ListReadyDocumentIdsWithoutParentChunkAsync()` (backfill detection).
- [x] T009 [P] Unit tests in `tests/unit/ChunkPersistenceTests.cs`: `Level`/`ParentId` round-trip through `SqliteDocumentRepository`; the schema probe-add is idempotent on an existing database; parent/child rows survive the delete-cascade.

**Checkpoint**: Two-level data substrate ready — user-story work can begin.

---

## Phase 3: User Story 1 - Precise answers to fact-based questions (Priority: P1) 🎯 MVP

**Goal**: A granular query resolves to tightly-scoped child chunks and returns the exact fact with a precise child-chunk citation.

**Independent Test**: Upload a document with a fact embedded mid-paragraph; ask `"What is the renewal term?"`; confirm the answer states the exact figure and cites a small child chunk (not a broad excerpt). An absent fact returns exactly `no relevant content found`.

### Tests for User Story 1 (write first, confirm they fail)

- [x] T010 [P] [US1] Unit tests `QueryIntentClassifier` in `tests/unit/QueryIntentClassifierTests.cs`: each broad trigger ⇒ `Broad`; a plain fact question ⇒ `Granular`; whitespace/empty ⇒ `Granular` (safe default, FR-008); determinism (same input ⇒ same output).
- [x] T011 [P] [US1] Unit tests granular routing in `tests/unit/RetrievalRoutingTests.cs` using the existing fake embedder/vector store: `Auto` + non-trigger ⇒ child results; `Specific` ⇒ child results; ordering, `RetrievalOptions.MinScore` filtering, and the `topK` cap are unchanged.

### Implementation for User Story 1

- [x] T012 [US1] Implement `QueryIntentClassifier` (rule-based, `QueryIntent Classify(string query)`; broad phrase lexicon, granular default) in `src/RAGGit.Retrieval/QueryIntentClassifier.cs` per [research.md](research.md) R2 — no LLM, no I/O.
- [x] T013 [US1] Change `RetrievalService.RetrieveAsync` to `RetrieveAsync(string query, int topK, QueryMode mode, CancellationToken ct)` in `src/RAGGit.Retrieval/RetrievalService.cs`: resolve intent (explicit `Broad`/`Specific` wins; `Auto` ⇒ classifier), and keep the granular child path exactly as today (embed → child search → order → `MinScore` → `topK`).
- [ ] T014 [US1] Update `QueryController.Post` in `src/RAGGit.Workstation.Api/Controllers/QueryController.cs` to pass `request.Mode` (default `Auto`) into retrieval and keep the existing no-content branch and error mapping.
- [ ] T015 [US1] Integration test in `tests/integration/IntentRetrievalTests.cs`: a granular fact query returns the exact fact with a child-chunk citation; an absent fact returns exactly `no relevant content found` with empty citations.

**Checkpoint**: US1 fully functional and independently testable — this is the MVP.

---

## Phase 4: User Story 2 - Comprehensive answers to synthesis questions (Priority: P2)

**Goal**: A broad query retrieves parent-chunk context so synthesis/comparison answers are complete across passage boundaries; existing libraries are upgraded to the two-level structure.

**Independent Test**: Ask `"Summarize the onboarding process"` / `"Compare the two plans"`; confirm the answer covers all relevant points and each citation's text is a larger parent passage.

### Tests for User Story 2 (write first, confirm they fail)

- [ ] T016 [P] [US2] Unit tests parent grouping in `tests/unit/ParentChunkingTests.cs`: groups of `ParentGroupSize`; parent text trims the inter-child overlap (no duplicated sentence); trailing short group; `ParentGroupSize = 1` ⇒ parent == child; empty text ⇒ no chunks.
- [ ] T017 [P] [US2] Unit tests broad routing in `tests/unit/RetrievalRoutingTests.cs`: children grouped by `ParentId`; duplicates de-duplicated; each parent keeps its best child score; `MinScore` and the `topK` parent cap applied; candidate limit = `min(topK × BroadCandidateMultiplier, MaxCandidates)`.
- [ ] T018 [US2] Integration test broad synthesis in `tests/integration/IntentRetrievalTests.cs`: a summary/compare query returns parent-context citations covering content split across adjacent child chunks.
- [ ] T019 [US2] Integration test re-ingestion in `tests/integration/ReindexBackfillTests.cs`: a pre-feature DB (chunks only) is upgraded so every `Ready` document has parent rows; no document lost or duplicated; a second run processes none (idempotent).

### Implementation for User Story 2

- [ ] T020 [US2] Add a parent-grouping helper to `src/RAGGit.Ingest/Chunker.cs`: from the child chunk list produce parent chunks (own `Guid`, `Level = Parent`, `ParentId = null`, `Ordinal` = group index) and stamp each child's `ParentId`; parent text = ordered child texts with the first `min(ChunkOverlap, childTokens-1)` tokens of every child after the first dropped.
- [ ] T021 [US2] Wire two-level ingest in `src/RAGGit.Ingest/IngestService.cs` (both `IngestAsync` and `ProcessStagedAsync`): persist children **and** parents, and add `["parentId"] = chunk.ParentId` to each **child** `VectorRecord` payload. Only children are embedded.
- [ ] T022 [US2] Read the payload in `src/RAGGit.Ingest/Vector/LanceDbLocalClient.cs`: `MapRow` populates `SearchResult.ParentId` from the `"parentId"` payload key (absent ⇒ `null`).
- [ ] T023 [US2] Implement the broad path in `src/RAGGit.Retrieval/RetrievalService.cs`: child search with the wide candidate limit, group by `ParentId`, resolve distinct parents via `IDocumentRepository.GetChunksByIdsAsync`, keep the best child score per parent, apply `MinScore`, take `topK` parents, and return them as `SearchResult`s (`ChunkId` = parent id, `Text` = parent text, `Ordinal` = parent ordinal).
- [ ] T024 [US2] Add `src/RAGGit.Workstation.Api/ReindexBackfillService.cs`: a hosted service that, after the schema exists, calls `ListReadyDocumentIdsWithoutParentChunkAsync()` and enqueues each stale document through `IngestWorkQueue`; idempotent and safe when the queue is busy. Register it in `src/RAGGit.Workstation.Api/Program.cs`.

**Checkpoint**: US1 and US2 both work independently.

---

## Phase 5: User Story 3 - Overriding the detected intent (Priority: P2)

**Goal**: The Ask surface offers Broad/Auto/Specific (default Auto); the choice is sent with the request and the effective intent is echoed.

**Independent Test**: Force `Broad` on a granular question and `Specific` on a synthesis question; retrieval granularity follows the choice. Omitting the mode behaves exactly as before.

### Tests for User Story 3 (write first, confirm they fail)

- [ ] T025 [P] [US3] Contract test in `tests/contract/QueryModeContractTests.cs` against `POST /api/queries`: omitted `mode` ⇒ 200 and pre-feature behaviour; `"mode":"broad"` / `"specific"` ⇒ 200 with echoed effective `mode`; unknown `mode` ⇒ 400 with the standard error shape.
- [ ] T026 [P] [US3] Unit tests client plumbing in `tests/unit/QueryModeClientTests.cs`: `QueryViewModel.QueryMode` defaults to `Auto`; `QueryApiClient.QueryAsync` serializes the selected mode.

### Implementation for User Story 3

- [ ] T027 [US3] Add `public QueryMode Mode { get; set; } = QueryMode.Auto;` to `QueryRequest` and an optional effective-intent field (e.g. `QueryIntent? Mode`) to `QueryResponse` in `src/RAGGit.Core/Models/QueryRequest.cs`.
- [ ] T028 [US3] Persist the effective intent: add `Mode` to `src/RAGGit.Core/Models/Query.cs` and include it in the `AddAsync` INSERT in `src/RAGGit.Core/Data/SqliteQueryRepository.cs` (column added in T007).
- [ ] T029 [US3] Echo the resolved effective intent in the `QueryResponse` built by `src/RAGGit.Workstation.Api/Controllers/QueryController.cs` (both the answered and the no-content branches).
- [ ] T030 [P] [US3] Extend `QueryApiClient.QueryAsync` in `src/RAGGit.Client.Core/Services/QueryApiClient.cs` to accept a `QueryMode` (default `Auto`), send it, and surface the echoed mode.
- [ ] T031 [P] [US3] Add a `QueryMode` observable property (default `Auto`) to `src/RAGGit.Client.Core/ViewModels/QueryViewModel.cs` and pass it to `QueryApiClient`.
- [ ] T032 [US3] Add the three-way selector to `src/RAGGit.Client.WPF/Views/Pages/QueryPage.xaml` (+ code-behind if required) per [contracts/ui-contracts.md](contracts/ui-contracts.md): a stock `ComboBox` (matching the `PageSizeCombo` idiom, since WPF-UI exposes no ComboBox equivalent), options Auto/Broad/Specific, default Auto, a stable `AutomationProperties.AutomationId`, keyboard operable, no colour-only state.
- [ ] T033 [US3] Run `dotnet csharpier format .` after the XAML change (CSharpier formats XAML) and confirm `dotnet csharpier check .` is clean.

**Checkpoint**: The override works end-to-end; US1, US2, US3 all function independently.

---

## Phase 6: User Story 4 - Consistent grounding across all intents (Priority: P3)

**Goal**: Every answer stays citation-grounded (or exactly "no relevant content found") regardless of intent or override; ambiguous intent degrades safely.

**Independent Test**: Across `auto`, `broad`, and `specific`, answers always carry citations or the exact no-content message; ambiguous queries still return grounded answers.

### Tests for User Story 4 (write first, confirm they fail)

- [ ] T034 [P] [US4] Integration test in `tests/integration/IntentGroundingTests.cs`: a query with no relevant content returns exactly `no relevant content found` with zero citations in both `auto`-resolved modes and in forced `broad`/`specific`.
- [ ] T035 [P] [US4] Integration test in `tests/integration/IntentGroundingTests.cs`: an ambiguous query falls back to granular and still returns a grounded answer or the exact no-content message (never ungrounded text).

### Implementation for User Story 4

- [ ] T036 [US4] In `src/RAGGit.Workstation.Api/Controllers/QueryController.cs`, ensure citation mapping and persistence handle **parent** citation ids (broad) as well as child ids: `retrievedChunkIds`/`citationIds` are saved and the `citations` array resolves parent `text`/`ordinal` for broad answers.

**Checkpoint**: All user stories independently functional and grounded.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [ ] T037 [P] Extend `tests/integration/EvalTests.cs` (and its fixture data) with a curated **fact-based** set (SC-001) and a curated **synthesis** set (SC-002), keeping the existing 50-question precision set (SC-003).
- [ ] T038 [P] Verify `specs/030-intent-aware-chunking/contracts/api.yaml` matches the implemented `mode` behaviour and version comment (1.5.0).
- [ ] T039 [P] Render the PlantUML set to SVG and commit source + render together: `powershell -File scripts/Render-PlantUml.ps1 -Sources specs/030-intent-aware-chunking/docs/architecture.puml,specs/030-intent-aware-chunking/docs/workflow.puml,specs/030-intent-aware-chunking/docs/sequence.puml,specs/030-intent-aware-chunking/docs/dataflow.puml,specs/030-intent-aware-chunking/docs/lifecycle.puml` (requires `tools/plantuml.jar` + Java; if unavailable, leave sources and note it).
- [ ] T040 Run full verification: `dotnet build RAGGit.sln -c Release -p:Platform=x64`; unit + contract + integration suites (plus the offline subset); `dotnet csharpier check .`; walk [quickstart.md](quickstart.md) scenarios V1–V6.
- [ ] T041 [P] Update `AGENTS.md` if the two-level chunk model or the re-ingestion backfill changes operational guidance (e.g. the "re-ingest all documents" note).

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately.
- **Foundational (Phase 2)**: Depends on Setup — **BLOCKS all user stories**.
- **User Stories (Phases 3–6)**: All depend on Foundational. US2, US3, and US4 build on the router signature introduced by US1 (T013), but each remains independently testable once that signature exists.
- **Polish (Phase 7)**: Depends on the desired stories being complete.

### User Story Dependencies

- **US1 (P1)**: After Foundational — no dependency on other stories (MVP).
- **US2 (P2)**: After Foundational and the US1 router signature (T013); independently testable (broad retrieval).
- **US3 (P2)**: After Foundational and the US1 router signature; independently testable (mode plumbing + selector).
- **US4 (P3)**: After US1/US2 — verifies grounding and the no-content path across both intents.

### Within Each User Story

- Tests are written first and MUST fail before implementation (TDD).
- Models before services; services before endpoints/UI; core before integration.

### ⚠️ Same-file conflicts

- `src/RAGGit.Retrieval/RetrievalService.cs` is edited by **T013** (US1) and **T023** (US2) — run sequentially, never in parallel.
- `src/RAGGit.Workstation.Api/Controllers/QueryController.cs` is edited by **T014** (US1), **T029** (US3), and **T036** (US4) — run sequentially.

### Parallel Opportunities

- **Setup**: T002 alone after T001.
- **Foundational**: T003, T004, T005, T006, T009 are `[P]` (different files); T007 → T008 are sequential (schema → repository).
- **US1**: T010 and T011 `[P]`; then T012 → T013 → T014 → T015.
- **US2**: T016 and T017 `[P]`; T018/T019 integration `[P]`; implementation T020 → T021 → T022 → T023 (T024 parallel to T020–T023).
- **US3**: T025 and T026 `[P]`; server T027 → T028 → T029; client T030 and T031 `[P]` → T032 → T033.
- **US4**: T034 and T035 `[P]`; T036 after them.
- **Polish**: T037, T038, T039, T041 `[P]`; T040 last.

---

## Parallel Example: User Story 1

```bash
# Launch the US1 tests together (they must fail first):
Task: "Unit tests QueryIntentClassifier in tests/unit/QueryIntentClassifierTests.cs"
Task: "Unit tests granular routing in tests/unit/RetrievalRoutingTests.cs"

# Then implement:
Task: "Implement QueryIntentClassifier in src/RAGGit.Retrieval/QueryIntentClassifier.cs"
Task: "Extend RetrievalService.RetrieveAsync (granular path) in src/RAGGit.Retrieval/RetrievalService.cs"
```

## Parallel Example: Foundational

```bash
Task: "Add ChunkLevel + QueryMode/QueryIntent enums (src/RAGGit.Core/Models)"
Task: "Extend Chunk with Level/ParentId (src/RAGGit.Core/Models/Chunk.cs)"
Task: "Extend SearchResult with optional ParentId (src/RAGGit.Core/Abstractions/SearchResult.cs)"
Task: "Extend IngestOptions/RetrievalOptions (src/RAGGit.Core/Models)"
Task: "Unit tests chunk persistence (tests/unit/ChunkPersistenceTests.cs)"
# Sequential after the model edits:
Task: "Extend RagDbContext schema + indexes (src/RAGGit.Core/Data/RagDbContext.cs)"
Task: "Extend IDocumentRepository + SqliteDocumentRepository (src/RAGGit.Core/Data)"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL — blocks all stories)
3. Complete Phase 3: US1 (classifier + granular routing)
4. **STOP and VALIDATE**: fact queries return precise child citations (quickstart V1)
5. Deploy/demo if ready — this is the MVP

### Incremental Delivery

1. Setup + Foundational → substrate ready
2. US1 → validate (V1) → MVP
3. US2 → validate (V2, V5) → broad synthesis + library upgrade
4. US3 → validate (V3, client walkthrough) → user override
5. US4 → validate (V4, V6) → grounding + offline invariant
6. Polish → eval sets, contracts, diagrams, full verification

### Notes

- `[P]` tasks touch different files with no incomplete dependency.
- [Story] labels map tasks to US1–US4 for traceability.
- Commit after each task or logical group; keep the worktree clean between tasks.
- Avoid: same-file conflicts (see above), vague tasks, cross-story dependencies that break independence.
