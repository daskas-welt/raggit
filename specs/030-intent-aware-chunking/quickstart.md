# Quickstart & Validation: Intent-Aware Chunking

**Feature**: `030-intent-aware-chunking` | **Date**: 2026-10-07 | **Plan**: [plan.md](plan.md)

A runnable validation guide proving the feature end-to-end. It references the data model
([data-model.md](data-model.md)) and contracts ([contracts/api.yaml](contracts/api.yaml),
[contracts/ui-contracts.md](contracts/ui-contracts.md)) rather than duplicating them. No
implementation code here.

## Prerequisites

- Windows, .NET SDK 10.0.401 (`global.json`), `dotnet tool restore` done.
- Ollama running locally with the configured embed + chat models
  (`snowflake-arctic-embed2`, `qwen2.5:3b` by default). The offline subset runs without Ollama.
- A local login (see AGENTS.md "Create a local login"): run the API exe with
  `user add --username me --role Admin --password 'Passw0rd!'` from `src/RAGGit.Workstation.Api`.

## Setup

```powershell
# Build the server filter (no WPF client needed for API validation)
dotnet build RAGGit.Server.slnf -c Release

# Formatter gate — CSharpier formats XAML too; run after any .xaml change
dotnet tool restore
dotnet csharpier check .

# Full Windows build (includes the WPF client) when validating the selector
dotnet build RAGGit.sln -c Release -p:Platform=x64
```

## Automated validation

```powershell
# Unit — classifier, parent grouping, retrieval routing
dotnet test tests/unit/RAGGit.Tests.Unit.csproj -c Release --no-build `
  --filter "FullyQualifiedName~Tests.Unit"

# Contract — POST /api/queries accepts + echoes mode (additive, backward compatible)
dotnet test tests/contract/RAGGit.Tests.Contract.csproj -c Release --no-build

# Integration — two-level ingest, broad/granular retrieval, backfill, offline
dotnet test tests/integration/RAGGit.Tests.Integration.csproj -c Release --no-build

# Offline subset (must stay green with WAN disabled, no Ollama-dependent cases)
dotnet test tests/integration/RAGGit.Tests.Integration.csproj -c Release --no-build `
  --filter "FullyQualifiedName~QueryOfflineTests|FullyQualifiedName~OfflineIdentityTests|FullyQualifiedName~OfflineHistoryTests|FullyQualifiedName~CrossUserIsolationTests"
```

> Test projects resolve `./data/lancedb` relative to their **output** dir
> (`tests/<suite>/bin/<Config>/net10.0/data/lancedb`). Delete that path to clear stale test vectors —
> deleting the repo-root `data/` fixes nothing.

## Manual scenarios

Start the API (`dotnet run --project src/RAGGit.Workstation.Api`), upload a document with a fact
embedded mid-paragraph **and** a multi-section document, wait for `Ready` (see
`IntegrationTestFactory.WaitForSettledAsync` semantics — never assume status is settled immediately).

### V1 — Granular fact retrieval (SC-001, US1)

1. `POST /api/queries` with `{ "query": "What is the renewal term?", "mode": "auto" }` (or omit
   `mode`).
2. **Expected**: the answer states the exact figure and cites a chunk whose text is tightly scoped
   to that fact. The response's `mode` is `granular`.
3. Repeat with `"mode": "specific"` — same granular behaviour forced.

### V2 — Broad synthesis retrieval (SC-002, US2)

1. `POST /api/queries` with `{ "query": "Summarize the onboarding process", "mode": "auto" }`.
2. **Expected**: a comprehensive answer covering the steps across sections; `mode` is `broad`; each
   citation's `text` is a larger (parent) passage.
3. Repeat with `{ "query": "onboarding", "mode": "broad" }` — broad behaviour forced even though the
   text alone would classify granular.

### V3 — Override is additive/backward compatible (FR-007, SC-004)

1. `POST /api/queries` with only `{ "query": "..." }` (no `mode`).
2. **Expected**: `200`, identical semantics to the pre-feature contract (server classifies).
3. `POST` with `{ "query": "...", "mode": "banana" }`.
4. **Expected**: `400` with the standard error shape.

### V4 — No relevant content unchanged (FR-005, SC-004)

1. Ask a granular question whose answer is absent, both `auto` and forced `broad`.
2. **Expected**: `200` with `answer` exactly `no relevant content found` and empty citations, in
   both modes.

### V5 — Re-ingestion preserves the library (FR-011, SC-006)

1. With a pre-feature `rag.db`/`lancedb` (chunks only, no parent rows), start the API.
2. **Expected**: the backfill re-enqueues stale `Ready` documents; each moves
   `Queued → Indexing → Ready`; afterwards every document has parent rows and the same documents
   are still present (no loss, no duplicates). Restart again — no document is re-processed
   (idempotent).

### V6 — Offline invariant (FR-006, Constitution IV)

1. Disable WAN, keep LAN and local Ollama.
2. Repeat V1 and V2.
3. **Expected**: both succeed end-to-end with no cloud egress; classification is local.

## Client walkthrough (optional, exercises U1)

Point the WPF client at the live API (patch the client's **output** `appsettings.json` with
`Workstation:ApiKey`), sign in, and on the Ask surface:

1. Confirm the selector defaults to **Auto** and is keyboard-operable.
2. Ask a fact question; then switch to **Broad** and re-ask the same text — the second answer is
   broader and cites larger passages.
3. Restart the app — the selector is back to **Auto**.

## Out of scope for this guide

Implementation detail (chunker internals, SQL, DI wiring) belongs in `tasks.md` and the
implementation phase; the PlantUML set under `docs/` is the visual contract for those flows.
