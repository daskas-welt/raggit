# Tasks: RAGGit Offline-Mode Single-Tenant RAG Library

**Input**: Design documents from `/specs/001-offline-mode/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/api.yaml
**Tests**: Included per Constitution VI (Test-First) and spec Independent Tests
**Organization**: Tasks grouped by user story (US1-US4) for independent MVP increments; Phase 1-2 must complete before any story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story (US1-US4) or `Found` for foundational
- File paths per `plan.md: RAGGit.Core / Ingest / Retrieval / Workstation.Api / Client.Maui / tests/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Initialize .NET 8 solution and structure per plan.md

- [ ] T001 Create solution `RAGGit.sln` and projects `src/RAGGit.Core`, `src/RAGGit.Ingest`, `src/RAGGit.Retrieval`, `src/RAGGit.Workstation.Api`, `src/RAGGit.Client.Maui` per plan.md Project Structure
- [ ] T002 [P] Initialize `src/RAGGit.Core` (.NET 8 classlib) with `Microsoft.Data.Sqlite`, `Qdrant.Client`, `OllamaSharp` / `LLamaSharp` / `Microsoft.ML.OnnxRuntime` package refs per plan.md Primary Dependencies
- [ ] T003 [P] Initialize `src/RAGGit.Workstation.Api` (ASP.NET Core 8) with `Swashbuckle.AspNetCore`, `Serilog` and reference `RAGGit.Core/Ingest/Retrieval`
- [ ] T004 [P] Initialize `src/RAGGit.Client.Maui` (.NET MAUI .NET 8; TFMs `net8.0-windows10.0.19041.0`, `net8.0-ios`, `net8.0-android`) with `HttpClient`, `CommunityToolkit.Mvvm` and reference `RAGGit.Core` (Core stays plain `net8.0`)
- [ ] T005 [P] Initialize `tests/unit`, `tests/contract`, `tests/integration` (xUnit) with `FluentAssertions`, `Microsoft.AspNetCore.Mvc.Testing`
- [ ] T006 [P] Configure `Directory.Build.props`, `editorconfig`, `dotnet format`, `.gitignore` (`/data/`, `/models/*.gguf`, `/models/*.onnx`)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story — ⚠️ CRITICAL

- [ ] T007 Setup SQLite `rag.db` schema and migrations for `Documents`, `Chunks`, `Queries`, `Library(singleton)` per data-model.md in `src/RAGGit.Core/Data/DbContext.cs`
- [ ] T008 [P] Implement Qdrant embedded wrapper `src/RAGGit.Core/Vector/QdrantLocalClient.cs` with `QdrantClient(path="./data/qdrant")` collection `library` HNSW `m=16` payload index `documentId` per research.md
- [ ] T009 [P] Implement Ollama/ONNX abstraction `src/RAGGit.Core/Ai/Embedder.cs` + `LlmClient.cs` (Ollama `POST /api/embed` & `/api/chat` or LLamaSharp `LLamaEmbedder.GetEmbeddings` or ONNX `bge-micro-v2`) per research.md
- [ ] T010 [P] Implement auth/RBAC `src/RAGGit.Workstation.Api/Auth/ApiKeyAuthHandler.cs` — `X-Api-Key` → `Admin` vs `Employee` per FR-003, `AllowAnonymous` for `/health`
- [ ] T011 Setup API routing and middleware in `src/RAGGit.Workstation.Api/Program.cs` (routing, Serilog, error handling, CORS for LAN, `appsettings.json` `Qdrant:Path`, `Ollama:Url`) per contracts/api.yaml servers
- [ ] T012 Create base models `src/RAGGit.Core/Models/Document.cs`, `Chunk.cs`, `Query.cs`, `Library.cs` with validation (Mime enum PDF/docx/txt/md, Size ≤100MB, Prompt not empty) per data-model.md
- [ ] T013 Configure env secrets (`dotnet user-secrets` `Api:Key`, `Onnx:EmbeddingModelPath`) and health endpoint `GET /health` in `src/RAGGit.Workstation.Api/Controllers/HealthController.cs`

**Checkpoint**: Foundation ready — `dotnet build` passes, `GET /health` returns 200 `{qdrant: ok, llm: ok}` with `data/qdrant` file created. No story work before this.

---

## Phase 3: User Story 1 - Admin Uploads and Indexes Content (Priority: P1) 🎯 MVP (Ingest Half)

**Goal**: Admin uploads PDF/docx/txt/md (<100MB) via desktop → workstation chunks 512/50, embeds locally, indexes into Qdrant file, status Ready <5min for 50 pages (FR-001, FR-002, FR-006, SC-001)

**Independent Test**: Seed: admin uploads 10 PDFs (50 pages each) on LAN → `GET /api/documents` shows 10 `Ready` + Qdrant count >0 + semantic query returns hit. No US-2 needed.

### Tests for User Story 1 (Write FIRST, must FAIL before implementation)

- [ ] T014 [P] [US1] Contract test `POST /api/documents` 201 + `GET /api/documents` 200 in `tests/contract/DocumentsContractTests.cs` per contracts/api.yaml
- [ ] T015 [P] [US1] Integration test `Upload → Index → Searchable <5min` in `tests/integration/IngestTests.cs` (including unsupported type 400, >100MB 413, duplicate hash dedupe)
- [ ] T016 [P] [US1] Unit test chunking `512/50` and hash SHA-256 dedupe in `tests/unit/ChunkingTests.cs`

### Implementation for User Story 1

- [ ] T017 [P] [US1] Implement chunking `src/RAGGit.Ingest/Chunker.cs` (PdfPig/OpenXML → 512 tokens /50 overlap) per data-model.md Chunk
- [ ] T018 [US1] Implement ingest service `src/RAGGit.Ingest/IngestService.cs` → `Embedder` batch → `QdrantLocalClient.Upsert` with payload `{documentId, text, ordinal}` + SQLite `Documents`/`Chunks` transaction (depends on T017, T008, T009)
- [ ] T019 [US1] Implement `POST /api/documents` + `GET /api/documents` in `src/RAGGit.Workstation.Api/Controllers/DocumentsController.cs` (multipart `file`, mime validation per FR-010, hash dedupe → existing id, Admin-only 403 per FR-003) per contracts/api.yaml
- [ ] T020 [US1] Implement client `LibraryView` + `UploadView` in `src/RAGGit.Client.Maui/Views/` calling `POST /api/documents` with progress and error `unsupported type` per FR-006
- [ ] T021 [US1] Add validation, logging (Serilog), and `DELETE` purge stub for later (Qdrant filter `documentId`)

**Checkpoint**: US-1 independently functional — upload 10 PDFs via desktop → `Ready` <5min, `GET` lists them, Qdrant `collection library` count matches chunks.

---

## Phase 4: User Story 2 - Employee Queries Library Offline with Citations (Priority: P1) 🎯 MVP (Query Half)

**Goal**: Employee query over LAN with WAN disabled → workstation `embed query → Qdrant search topK=5 → local LLM prompt → {answer, citations[]}` or `no relevant content found`, p95 <7s (FR-004, FR-005, SC-002, SC-003, SC-004)

**Independent Test**: Pre-seed library (from US-1). Disable WAN on workstation (keep LAN). Employee (Employee role) queries `refund policy` via desktop → `200 {answer, citations ≥1}` <7s; query nonsense → `no relevant content found` 0 citations. Verify no cloud egress.

### Tests for User Story 2 (Write FIRST, must FAIL)

- [ ] T022 [P] [US2] Contract test `POST /api/query` 200 `{answer,citations}` and `NoRelevantContent` branch in `tests/contract/QueryContractTests.cs` per contracts/api.yaml
- [ ] T023 [P] [US2] Integration test `WAN-disabled query` in `tests/integration/QueryOfflineTests.cs` (LAN-only, assert no WAN, p95 <7s, 503 when Ollama down per FR-007)
- [ ] T024 [P] [US2] Eval harness `tests/integration/EvalTests.cs` — 50 Q/A set, assert ≥80% top-5 relevant (SC-003) and 100% citation/0% hallucination (SC-004)

### Implementation for User Story 2

- [ ] T025 [P] [US2] Implement retrieval `src/RAGGit.Retrieval/RetrievalService.cs` — `Embedder.GetEmbeddings(query)` → `QdrantClient.Search(limit:5)` → payload `text` per research.md
- [ ] T026 [US2] Implement generation `src/RAGGit.Retrieval/GenerationService.cs` — prompt template (system + chunks + query) → `LlmClient.Chat` (Ollama `/api/chat` or Phi-3 ONNX) → `answer` + `citationIds` (depends on T025)
- [ ] T027 [US2] Implement `POST /api/query` in `src/RAGGit.Workstation.Api/Controllers/QueryController.cs` (validate `query` not empty, `topK` clamp 1-10, call Retrieval+Generation, map `no relevant content found` when 0 hits, 503 when LLM down per FR-007) per contracts/api.yaml
- [ ] T028 [US2] Implement client `QueryView` in `src/RAGGit.Client.Maui/Views/QueryView.xaml` with streaming answer + citations list (documentId/chunkId/ordinal) calling `POST /api/query` via `HttpClient`
- [ ] T029 [US2] Instrument latency `latencyMs` in `Queries` table and expose via health for SC-002

**Checkpoint**: US-1+US-2 together form closed-loop MVP — upload on desktop → query on second desktop with WAN-off workstation → cited answer <7s. Deployable.

---

## Phase 5: User Story 3 - Admin Manages Library (Priority: P2)

**Goal**: Admin lists/deletes documents; deletion purges SQLite + Qdrant points filter `documentId`; subsequent queries exclude deleted content (FR-001, FR-002)

**Independent Test**: Admin deletes one of 10 docs from US-1 → `GET /api/documents` count N-1, Qdrant `count filter documentId` 0, query unique term → 0 hits/no citation.

### Tests for User Story 3 (Write FIRST)

- [ ] T030 [P] [US3] Contract test `DELETE /api/documents/{id}` 204 + `GET` after delete in `tests/contract/DeleteContractTests.cs`
- [ ] T031 [P] [US3] Integration test `Delete → purge → query excludes` in `tests/integration/DeleteIntegrationTests.cs` (also 403 when Employee calls DELETE)

### Implementation for User Story 3

- [ ] T032 [US3] Implement `DELETE /api/documents/{id}` in `DocumentsController.cs` — remove `Documents`/`Chunks` rows + `QdrantClient.Delete(filter: documentId)` (depends on T008, T007)
- [ ] T033 [US3] Update client `LibraryView` delete button (Admin-only, confirm dialog) calling `DELETE /api/documents/{id}` and refreshing list

**Checkpoint**: US-3 independently testable after US-1 data exists; no dependency on US-2.

---

## Phase 6: User Story 4 - Employee Browses Library (Priority: P3)

**Goal**: Employee browses library read-only (list same as admin, upload/delete disabled 403 per SC-005)

**Independent Test**: Employee login → `GET /api/documents` 200 same list; `POST /api/documents` and `DELETE` → 403.

### Tests for User Story 4 (Write FIRST)

- [ ] T034 [P] [US4] Contract test RBAC `GET 200` vs `POST/DELETE 403` for Employee role in `tests/contract/RbacContractTests.cs` per FR-003
- [ ] T035 [P] [US4] Integration test `Employee browse read-only` in `tests/integration/RbacIntegrationTests.cs`

### Implementation for User Story 4

- [ ] T036 [US4] Enforce `[Authorize(Roles="Admin")]` on `POST/DELETE` and allow `GET` for `Employee` in `DocumentsController.cs`; add client role-aware UI (hide upload/delete for Employee) in `src/RAGGit.Client.Maui/ViewModels/LibraryViewModel.cs`

**Checkpoint**: All 4 stories independently functional; full role matrix verified.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Hardening for single-tenant proprietary LAN distribution

- [ ] T037 [P] Add Serilog structured logging + `X-Request-Id` + error problem details across API
- [ ] T038 [P] Harden `POST /api/documents` with magic-byte mime check + virus-scan hook stub per FR-006
- [ ] T039 [P] Performance: index batching, Qdrant HNSW tuning, chunk cache; validate SC-001 <5min for 50 pages and SC-002 p95 offline
- [ ] T040 [P] Security: single-tenant proprietary signing (MSIX Windows + iOS enterprise/Ad-Hoc + Android sideload) docs + `secrets` not in repo, `data/` + `models/` gitignored per quickstart.md
- [ ] T041 [P] Add `src/RAGGit.Client.Maui` builds: `dotnet publish -f net8.0-windows10.0.19041.0`, `-f net8.0-android`, `-f net8.0-ios` and workstation `docker-compose` (optional) + LAN discovery doc
- [ ] T042 [P] Extra unit tests for edge cases: large file queue, duplicate hash, `model unavailable offline` 503, LAN partition retry (no cloud fallback) per spec Edge Cases
- [ ] T043 Run `quickstart.md` validation: build + workstation + desktop + WAN-off query + `dotnet test` (all filters) passes per `plan.md:Constitution Check`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 Setup**: No deps — start immediately
- **Phase 2 Foundational**: Depends on Phase 1 — BLOCKS all stories
- **Phases 3-6 (US1-US4)**: Depend on Phase 2 — US1 and US2 are co-P1 MVP but US1 must land first for seed data; US3/US4 can run parallel after Foundational if staffed, or sequentially P1→P2→P3
- **Phase 7 Polish**: Depends on desired stories (at least US-1+US-2) complete

### Within Each Story

- Contract/integration tests → FAIL → Models → Services → Controllers/Views → Validation → Checkpoint

### Parallel Opportunities

- T002-T006 [P] parallel in Setup; T008-T010 [P] parallel in Foundational; T014-T016 [P] tests parallel per story; T022-T024 [P] parallel; US3/US4 can parallelize after Foundational with 2 developers (Dev A: US1, Dev B: US2) then merge.

---

## Implementation Strategy

### MVP First (US1 Only → then US2)

1. Phases 1+2 → Foundation (`/health` ok)
2. Phase 3 US1 → Deploy workstation + desktop, validate 10 PDFs upload → Ready <5min
3. Phase 4 US2 → Validate WAN-off query <7s with citations → **MVP shippable** (closed loop)
4. Phases 5-6 incrementally → each adds value without breaking MVP

### Incremental Delivery

- After Foundational, each story tested independently per spec `Independent Test` before next story starts.

---

## Notes

- Single-tenant: no `CompanyId` anywhere — singleton `Library`, API is `/api/*` per `data-model.md`.
- Offline invariant: `tests/integration/QueryOfflineTests.cs` MUST run with WAN disabled, LAN connected; fail if egress detected (FR-004).
- Chunk `512/50`, topK `5`, 5k docs assumption per FR-010/011 — tunable via `appsettings.json`, not per-user.
- Video/audio out of scope — do not add transcription pipeline tasks.
