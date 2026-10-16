# Implementation Plan: Reasoning Progress While Waiting

**Branch**: `032-reasoning-progress` | **Date**: 2026-10-15 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/032-reasoning-progress/spec.md` (all clarifications resolved during specification: (1) reasoning = named work stages **plus the sources consulted**, not the model's internal thinking text and not progressive answer text; (2) updates arrive **live from the workstation** as each stage happens, not on a client-side timer; (3) the reasoning account is **transient** — discarded the moment the answer or failure is presented, never stored or shown from history).

## Summary

Today the Ask surface shows one static line ("Searching the library and writing your answer…") while a local model takes seconds to answer, so employees cannot tell progress from a stall. This feature streams a short, plain-language account of the work actually being done — *understanding the question → searching the library → found N relevant sources (named) → composing the answer* — to the desktop client while it waits.

The server gains one **additive** endpoint, `POST /api/queries/stream`, which returns newline-delimited JSON (NDJSON) events over the existing authenticated HTTPS connection, and the existing `POST /api/queries` is left byte-for-byte unchanged. To keep the two transports from diverging, the query pipeline currently inlined in `QueryController.Post` is extracted into a single library-owned pipeline (`RAGGit.Retrieval.QueryPipeline`) that reports progress through a callback; the non-streaming endpoint passes a no-op observer, the streaming endpoint writes each event and flushes. Progress text is rendered by the workstation in the question's language (the same language rule already applied to answers) and each event also carries a stable, language-independent stage identifier for tests and automation.

The client replaces the static busy line with a live reasoning region on the Ask surface, fed by the existing `QueryApiClient` over the same `HttpClient` + bearer handler, and clears it the instant the answer or an error arrives. Nothing about the answer, citations, history, or the "no relevant content found" rule changes; no reasoning is persisted. No new project, no new dependency.

## Technical Context

**Language/Version**: C# / .NET 10 — libraries `RAGGit.Core`, `RAGGit.Ingest`, `RAGGit.Retrieval`; ASP.NET Core host `RAGGit.Workstation.Api`; WPF-UI desktop `RAGGit.Client.WPF` + shared `RAGGit.Client.Core`.

**Primary Dependencies**: Reused, no additions — ASP.NET Core MVC + `System.Text.Json` (server), `System.Net.Http` + `System.Text.Json` (client), `CommunityToolkit.Mvvm`, WPF-UI `4.3.0`. Streaming is plain HTTP/1.1 with `Response.Body.FlushAsync()`; nothing new is pulled in (no SignalR, no gRPC, no WebSocket package).

**Storage**: Unchanged. Reasoned progress is never persisted (FR-014): no schema change, no new table, no new column. The streaming path still writes exactly the same `Queries` row through `IQueryRepository.AddAsync` as the non-streaming path (FR-010).

**Testing**: xUnit unit/contract/integration. Unit: `QueryPipeline` stage sequence and honesty rules, progress-text language, NDJSON event parsing (a partial final line without a trailing newline, malformed input, unknown event types), `QueryViewModel` reasoning lifecycle. Contract: the new endpoint's content type, event ordering, auth/validation/503 behaviour, event-shape parity with `POST /api/queries`, and a redaction check that no event carries chunk or prompt text. Integration: end-to-end stream over `WebApplicationFactory`, history row still written and reasoning not persisted, offline subset unchanged. UI: `winapp ui` automation-ID checks (documented in `contracts/ui-contracts.md` and `quickstart.md`). `dotnet csharpier check .` (XAML included) is part of the gate.

**Target Platform**: Windows workstation (API) + Windows desktop client; single-tenant, LAN-only, offline at query time.

**Hardware baseline**: the workstation the constitution defines — 16 GB RAM with a GPU where available. SC-002 and SC-007 are measured against it; the reference development workstation used for the 032 measurements is Windows with Ollama `snowflake-arctic-embed2` + `qwen2.5:3b` doing CPU-only inference, where an answer takes 3–25 s and the first reasoning update lands at ~80 ms.

**Project Type**: Multi-project .NET (libraries + web service + desktop client) — existing solution, no new project.

**Performance Goals**: First reasoning update visible within 1 s of submit on ≥95% of questions on the hardware baseline above (SC-002); ≥2 distinct updates for every answer slower than 2 s (SC-001); no more than 5% added end-to-end latency (SC-007). The streaming path performs the same AI work as today — the only additions are a few small JSON writes and a flush per stage, on the order of microseconds against multi-second generation.

**Constraints**: Offline invariant untouched (no new egress; nothing is fetched at query time). Citation-grounding unchanged; "no relevant content found" remains exact. Non-streaming `POST /api/queries` behaviour must remain identical (FR-010), so the pipeline extraction must be a pure refactor of that path. Test-first. No new project/dependency. Single-tenant — no tenant field anywhere. No progress line may leak raw prompt text, chunk bodies beyond the answer's own Sources, or any other person's data (FR-007/FR-008).

**Scale/Scope**: One deployment with a handful of concurrent employee desktops; a document library of thousands of chunks. Scope covers: one additive API endpoint, one extracted library pipeline, one shared NDJSON event contract in `RAGGit.Core`, one client API-client method, one ViewModel collection, one Ask-surface region. No config additions, no schema changes, no new background service.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Single-Tenant On-Prem or Private Cloud**: PASS — no tenant concept is introduced; progress events are scoped to the authenticated caller's own request (`[Authorize(Roles = "Employee,Admin")]` plus the existing JWT `sub` check), and nothing cross-user is emitted or stored.
- **II. Workstation-Owned AI**: PASS — all retrieval, classification, generation, and progress narration stay on the workstation. The desktop client remains UI + `HttpClient`: it renders the text the workstation sends and performs no AI work.
- **III. .NET Library-First & Client Reuse**: PASS — the new orchestration (`QueryPipeline`) and its progress model land in `RAGGit.Retrieval` / `RAGGit.Core`, are independently unit-testable, and are reused by both transports; the client reuses the shared contract types in `RAGGit.Core.Models` rather than redefining them.
- **IV. Offline Invariant (NON-NEGOTIABLE)**: PASS — no query-time egress is added. The stream runs over the same LAN/private-network HTTPS connection as today, uses only local models, and adds no startup or per-query fetch. The WAN-disabled offline integration suite must keep passing.
- **V. Citation-Grounded RAG**: PASS — the answer, its citations, the fallback rule, and the exact-string "no relevant content found" response are untouched; the progress events describe retrieval and generation but never replace or weaken grounding, and the `sources` stage is suppressed when retrieval found nothing.
- **VI. Test-First (NON-NEGOTIABLE)**: PASS — new logic (pipeline stage emission, progress text, NDJSON parsing, ViewModel lifecycle) is written before the implementation it covers and verified failing first at phase granularity (a brand-new type fails to compile, so a meaningful red run is per phase, not per task); contract tests cover the new endpoint and its parity with `POST /api/queries`; the offline gate is unaffected.
- **VII. Simplicity & Proprietary Stewardship**: PASS — no new project and no new dependency; one pipeline with two transports rather than two pipelines; no server-side session state, no WebSocket, no status polling, and no persisted reasoning.

**Pre-Phase-0 gate**: PASS. No violation requires Complexity Tracking.

**Post-Phase-1 re-check**: PASS — [data-model.md](data-model.md), [contracts/](contracts/) and [research.md](research.md) confirm the design adds no project, no dependency, no schema change, and no tenant concept; the non-streaming path stays a pure refactor, and all new behaviour is local, authed, and citation-preserving. No Complexity Tracking entry needed.

## Project Structure

### Documentation (this feature)

```text
specs/032-reasoning-progress/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   ├── api.yaml         # POST /api/queries/stream NDJSON event contract (delta)
│   └── ui-contracts.md  # Ask-surface reasoning region behaviour
├── checklists/
│   └── requirements.md
├── spec.md
└── tasks.md             # Phase 2 output (/speckit.tasks — NOT created here)
```

### Source Code (repository root)

```text
src/
├── RAGGit.Core/
│   └── Models/
│       ├── QueryStage.cs                # NEW enum (understanding|searching|sources|composing)
│       ├── QueryProgress.cs             # NEW progress event payload (stage, text, sourceCount, documentNames)
│       └── QueryStreamEvent.cs          # NEW stream envelope + answer/error variants (shared by API and client)
├── RAGGit.Retrieval/
│   ├── QueryPipeline.cs                 # NEW: single pipeline both transports call, with onProgress callback
│   ├── ReasoningText.cs                 # NEW: plain-language stage text, localized to the question language
│   └── RetrievalService.cs              # unchanged (retrieval stays as-is)
├── RAGGit.Workstation.Api/
│   └── Controllers/QueryController.cs   # Post delegates to QueryPipeline (no-op observer); NEW PostStream (NDJSON)
├── RAGGit.Client.Core/
│   ├── Services/QueryApiClient.cs       # NEW StreamAsync(...) → IAsyncEnumerable<QueryStreamEvent> over NDJSON
│   ├── ViewModels/QueryViewModel.cs     # NEW ReasoningSteps; AskQuestionAsync consumes the stream
│   └── Models/ReasoningStep.cs          # NEW client-side reasoning row (stage, text, is-current)
└── RAGGit.Client.WPF/
    ├── Components/ChatControl.xaml(.cs) # static busy line → reasoning region (new ReasoningSteps DP)
    └── Views/Pages/QueryPage.xaml       # binds ReasoningSteps to the chat's reasoning region

tests/
├── unit/        # QueryPipeline stages, ReasoningText language, NDJSON parsing, ViewModel lifecycle
├── contract/    # stream endpoint: content type, ordering, auth/400/503, parity, redaction
├── integration/ # end-to-end stream, history row written, reasoning not persisted, offline subset
└── (UI)         # winapp ui automation-ID checks per contracts/ui-contracts.md
```

**Structure Decision**: The existing multi-project layout is retained — no new project (Constitution VII). The query orchestration moves *into* the library that already owns retrieval and generation (`RAGGit.Retrieval`) so both controllers and tests share one implementation; the shared wire types land in `RAGGit.Core.Models` beside `QueryRequest`/`QueryResponse`, which the desktop client already references. The API host keeps only transport and history persistence; the client keeps only rendering.

## Complexity Tracking

> No Constitution Check violations — section intentionally empty.
