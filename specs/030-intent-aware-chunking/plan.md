# Implementation Plan: Intent-Aware Chunking

**Branch**: `030-intent-aware-chunking` | **Date**: 2026-10-07 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/030-intent-aware-chunking/spec.md` (all clarifications resolved during specification: intent control = automatic detection with an optional Broad/Specific override, default **Auto**; chunking model = parent-child, ~512-token child units grouped into ~2048-token parent units, derived by grouping children, **not** separately embedded).

## Summary

Documents are indexed at **two granularities** so both query intents are served well:

1. **Child chunks** (~512 tokens, overlap 50) — embedded and searched for **granular** queries (a metric, name, date, or isolated setting).
2. **Parent chunks** (~2048 tokens, each a group of `IngestOptions.ParentGroupSize` consecutive children) — **not** separately embedded; they supply broad context for **broad** queries (synthesis, comparison, summarization). Each parent has its own id and text so citations stay meaningful.

At query time a deterministic, rule-based classifier labels the query broad vs granular; the user may override with Broad/Specific (default **Auto**). Granular retrieval is unchanged (child vectors). Broad retrieval searches child vectors for a wider candidate set, groups the matches by parent, resolves the distinct parent rows, and answers from parent text.

Existing libraries are re-processed into the two-level structure by an idempotent backfill that reuses the stored originals and the existing background-ingest path (`Uploading → Queued → Indexing → Ready|Failed`), so no content is lost or duplicated.

No new project and no new dependency. The classifier is deterministic string matching (offline, zero added latency). This is a *chunk-size* change, so `VectorDb:VectorSize` (1024) is untouched and no `DimensionMismatchException` is triggered.

## Technical Context

**Language/Version**: C# / .NET 10 — libraries `RAGGit.Core`, `RAGGit.Ingest`, `RAGGit.Retrieval`; ASP.NET Core host `RAGGit.Workstation.Api`; WPF-UI desktop `RAGGit.Client.WPF` + shared `RAGGit.Client.Core`.

**Primary Dependencies**: Reused, no additions — LanceDB (`RAGGit.Ingest/Vector/LanceDbLocalClient`), Ollama embedder/chat (`IEmbedder`, `ILlmClient`), `Microsoft.Data.Sqlite`, `Microsoft.Extensions.Options`, `CommunityToolkit.Mvvm`, WPF-UI `4.3.0`. The intent classifier is in-process and rule-based (no LLM call, no new package).

**Storage**: LanceDB `./data/lancedb` (child vectors only; payload gains `parentId`) + SQLite `rag.db` (`Chunks` gains `Level`/`ParentId`; parent rows persisted for stable citation ids). `VectorSize` stays 1024.

**Testing**: xUnit unit/contract/integration. Unit: classifier (`QueryIntentClassifier`), parent grouping (`Chunker`), retrieval routing (`RetrievalService`) with the existing fake vector store/embedder. Contract: `POST /api/queries` accepts and echoes `mode` (additive). Integration: two-level ingest, broad vs granular retrieval, re-ingestion backfill, offline invariant. The eval harness gains a fact-based set (SC-001) and a synthesis set (SC-002) alongside the existing 50-question set (SC-003). `dotnet csharpier check .` (XAML included) plus the offline subset gate.

**Target Platform**: Windows workstation (API) + Windows desktop client; single-tenant, LAN-only, offline at query time.

**Project Type**: Multi-project .NET (libraries + web service + desktop client) — existing solution, no new project.

**Performance Goals**: No perceivable latency added (SC-005) — classification is O(query length); broad retrieval adds one bounded child search plus one SQLite parent lookup. The existing latency envelope is preserved.

**Constraints**: Offline invariant untouched; citation-grounding preserved (broad answers cite parents, granular cite children); test-first; no new project/dependency; single-tenant (no tenant field anywhere); classification must complete with no query-time WAN.

**Scale/Scope**: Six areas touched — classification (new), chunking/ingest (parent grouping + persistence), retrieval routing, query request/response + client selector, re-ingestion backfill, and the contract. One config addition in each of `IngestOptions` and `RetrievalOptions`.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Single-Tenant On-Prem**: PASS — no tenant concept is introduced; parent/child chunks scope to a `DocumentId` only, exactly as today.
- **II. Workstation-Owned AI**: PASS — all chunking, classification, and retrieval stay on the workstation; the desktop stays a thin HttpClient client that only sends a `mode` string.
- **III. .NET Library-First & Client Reuse**: PASS — classifier and parent grouping live in the existing libraries (`RAGGit.Retrieval`, `RAGGit.Ingest`); the client reuses them through the API with no duplicated RAG logic.
- **IV. Offline Invariant (NON-NEGOTIABLE)**: PASS — classification and grouping are in-process and local; no model pull and no egress; the WAN-disabled offline integration suite must pass.
- **V. Citation-Grounded RAG**: PASS — every answer still cites chunks; broad answers cite the parent unit and granular answers cite the child unit; the "no relevant content found" path is unchanged.
- **VI. Test-First (NON-NEGOTIABLE)**: PASS — all new logic (classifier, grouping, routing) is unit-testable and written red→green; contract tests cover the additive field; the eval harness quantifies SC-001/002/003.
- **VII. Simplicity & Proprietary Stewardship**: PASS — no new project and no new dependency; parents are groupings of existing children rather than a second, independently searchable index.

**Pre-Phase-0 gate**: PASS. No violation requires Complexity Tracking.

**Post-Phase-1 re-check**: PASS — the design in [data-model.md](data-model.md), [contracts/](contracts/), and [research.md](research.md) adds no project, no dependency, and no tenant concept; all new behaviour is local and citation-preserving. No Complexity Tracking entry needed.

## Project Structure

### Documentation (this feature)

```text
specs/030-intent-aware-chunking/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   ├── api.yaml         # POST /api/queries additive mode field (delta contract)
│   └── ui-contracts.md  # Ask surface Broad/Auto/Specific selector
├── docs/                # PlantUML visual contract (planner-owned)
│   ├── architecture.puml
│   ├── workflow.puml
│   ├── sequence.puml
│   ├── dataflow.puml
│   └── lifecycle.puml
├── checklists/
│   └── requirements.md
├── spec.md
└── tasks.md             # Phase 2 output (/speckit.tasks — NOT created here)
```

### Source Code (repository root)

```text
src/
├── RAGGit.Core/
│   ├── Models/
│   │   ├── Chunk.cs                     # + Level, ParentId
│   │   ├── ChunkLevel.cs                # NEW enum (Child, Parent)
│   │   ├── QueryIntent.cs               # NEW enums (QueryMode, QueryIntent)
│   │   ├── QueryRequest.cs              # + request Mode, + response Mode
│   │   ├── IngestOptions.cs             # + ParentGroupSize
│   │   └── RetrievalOptions.cs          # + BroadCandidateMultiplier, MaxCandidates
│   ├── Abstractions/
│   │   ├── SearchResult.cs              # + ParentId (from vector payload)
│   │   └── Repositories/
│   │       └── IDocumentRepository.cs   # + parent/child chunk reads
│   └── Data/
│       ├── RagDbContext.cs              # Chunks + Level/ParentId columns (probe-add)
│       └── SqliteDocumentRepository.cs  # persist/read two-level chunks
├── RAGGit.Ingest/
│   ├── Chunker.cs                       # + parent grouping helper
│   └── IngestService.cs                 # persist parents; payload parentId
├── RAGGit.Retrieval/
│   ├── QueryIntentClassifier.cs         # NEW (rule-based, deterministic)
│   └── RetrievalService.cs              # route granular vs broad
├── RAGGit.Workstation.Api/
│   ├── Controllers/QueryController.cs   # pass mode; echo effective intent
│   └── ReindexBackfillService.cs        # NEW one-shot two-level backfill
└── RAGGit.Client.Core/ + RAGGit.Client.WPF/
    ├── Services/QueryApiClient.cs       # send mode
    ├── ViewModels/QueryViewModel.cs     # + QueryMode
    └── Views/Pages/...Ask surface       # Broad/Auto/Specific selector

tests/
├── unit/        # classifier, parent grouping, retrieval routing
├── contract/    # POST /api/queries mode accept + echo
├── integration/ # two-level ingest, broad/granular retrieval, backfill, offline
└── (eval)       # fact-based set (SC-001) + synthesis set (SC-002) + existing 50 (SC-003)
```

**Structure Decision**: The existing multi-project layout is retained — no new project (Constitution VII). New logic lands in the libraries that already own chunking (`RAGGit.Ingest`), retrieval (`RAGGit.Retrieval`), and models/options (`RAGGit.Core`), plus the API host (request/response + backfill) and the client (`RAGGit.Client.Core` + `RAGGit.Client.WPF`).

## Complexity Tracking

> No Constitution Check violations — section intentionally empty.
