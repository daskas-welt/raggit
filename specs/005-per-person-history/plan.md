# Implementation Plan: Per-Person Query & Document History

**Branch**: `005-per-person-history` | **Date**: 2026-09-13 | **Spec**: [specs/005-per-person-history/spec.md](spec.md)
**Input**: Feature specification from `/specs/005-per-person-history/spec.md` — per-person query history + recent docs, paginated, scoped to JWT `sub`, zero cross-user leak, offline WAN-off.

## Summary

Query-layer addition over `004-identity` attribution: expose `GET /api/queries/history` (paginated, `UserId == sub`), `GET /api/queries/{id}` (single, ownership-checked), and `GET /api/documents/mine` (paginated, `CreatedBy == sub`), all `401` without valid session, `404` if not owned, `0` cross-user rows, legacy `admin/employee` excluded, stable `CreatedAt DESC, Id DESC` pagination, MAUI History screen (`SfListView`), no new tables or columns, contract MINOR `1.3.0 → 1.4.0` additive, offline SQLite-only.

## Technical Context

**Language/Version**: C# .NET 8 (Workstation ASP.NET Core API + `.NET MAUI` thin client `net8.0-windows10.0.19041.0` / `net8.0-android` / `net8.0-ios` fallback `net8.0` CI)

**Primary Dependencies**: ASP.NET Core 8, `Microsoft.AspNetCore.Authentication.JwtBearer 8.0.10` (HS256, from 004), `Microsoft.Data.Sqlite 8.0.10`, LanceDB local (`./data/lancedb`), `OllamaSharp`/HttpClient `localhost:11434`, `Syncfusion.Maui.Core 28.2.7` + `Toolkit/ListView` material light, `System.Text.Json`, `CSharpier 1.3.0`

**Storage**: SQLite `rag.db` (tables `Users`, `Documents` incl `CreatedBy`, `Queries` incl `UserId`, `Chunks`) + LanceDB vectors `./data/lancedb`; no new tables for this feature

**Testing**: `xUnit` `contract`/`integration`/`unit` via `WebApplicationFactory`/`TestApiFactory` (already has `Auth:JwtSigningKeyPath` isolation, `OfflineIdentityTests` WAN-disabled via `unshare -n` fallback), `dotnet build` + `dotnet csharpier check .` + `dotnet test`

**Target Platform**: Windows 11 Workstation API (`Kestrel` LAN) + MAUI thin client (Windows 11 + iOS/Android portable); CI headless `net8.0` fallback

**Project Type**: Single-project hybrid (library `RAGGit.Core`/`RAGGit.Ingest` + API `RAGGit.Workstation.Api` + mobile/desktop `RAGGit.Client.Maui` + `tests/`)

**Performance Goals**: First history page (20 items, 1k total per person) `<2s` p95 on LAN `SC-001`; pagination stable `SC-003`; query detail `<500ms`; doc list same as history

**Constraints**: Offline invariant NON-NEGOTIABLE (IV — WAN disabled history still works, no egress), citation-grounded (V — history detail must return persisted citations), Test-First (VI — contract/integration red-first,≥80% library /100% contracts), Single-Tenant singleton Library no `company_id` (I), no new project until 4th justified (VII), additive contract `1.4.0` no breaking, `401`/`403`/`404` semantics, legacy `admin/employee` excluded

**Scale/Scope**: `200 users × 50 q/day ≈10k/day`, `1k queries/person` hot path, `5k docs/~1M chunks` existing, `topK 5` unchanged

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Evidence |
|-----------|--------|----------|
| **I. Single-Tenant On-Prem** | ✅ PASS | No `company_id`, singleton Library, `UserId/CreatedBy == sub` scoping not tenant partitioning; no cross-company data |
| **II. Workstation-Owned AI** | ✅ PASS | Thin MAUI remains `HttpClient` only, no vector DB or model weights on client; history is API read-over-LAN |
| **III. .NET Library-First & Client Reuse** | ✅ PASS | History logic lives in `Core` `QueryHistoryStore`/`DocumentStore` reused by `Api` + `MAUI` via `HttpClient`; no duplicated RAG logic |
| **IV. Offline Invariant (NON-NEGOTIABLE)** | ✅ PASS | History endpoints are SQLite-only reads, no Ollama/LanceDB at read time, no WAN; verified by `OfflineHistoryTests` + LAN CI gate |
| **V. Citation-Grounded RAG** | ✅ PASS | Query detail returns persisted `Answer` + `Citations[]` from `Queries`/`QueryCitations`; no hallucination path |
| **VI. Test-First (NON-NEGOTIABLE)** | ✅ PASS | `contract/HistoryContractTests` + `integration/CrossUserIsolationTests` written first and FAIL before impl; coverage gates preserved |
| **VII. Simplicity & Proprietary** | ✅ PASS | No new project, no new table, no new package; Complexity Tracking table empty |

*All gates PASS — proceed to Phase 0.*

## Project Structure

### Documentation (this feature)

```text
specs/005-per-person-history/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
│   └── api.yaml         # Δ vs contracts/api.yaml 1.3.0 → 1.4.0 (additive)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
RAGGit.sln (v1.4.0)
├── src/
│   ├── RAGGit.Core/
│   │   ├── Data/
│   │   │   ├── RagDbContext.cs          # no DDL change (uses existing Queries.Documents indexes)
│   │   │   ├── QueryHistoryStore.cs     # NEW — per-person paginated reads
│   │   │   └── DocumentMineStore.cs     # NEW — per-person doc reads (thin wrapper over Documents)
│   │   ├── Models/
│   │   │   ├── Query.cs                 # already has UserId, CreatedAt, Prompt, Answer, Citations
│   │   │   └── Document.cs              # already has CreatedBy
│   │   └── Auth/
│   │       └── User.cs / UserStore.cs   # unchanged (from 004)
│   ├── RAGGit.Workstation.Api/
│   │   ├── Controllers/
│   │   │   ├── QueryController.cs       # EXTEND — history + detail endpoints
│   │   │   └── DocumentsController.cs   # EXTEND — /mine endpoint
│   │   └── Program.cs                   # no auth change (reuses 004 JwtBearer + OnTokenValidated)
│   └── RAGGit.Client.Maui/
│       ├── Services/
│       │   ├── QueryHistoryApiClient.cs # NEW
│       │   └── DocumentsApiClient.cs    # EXTEND
│       ├── ViewModels/
│       │   └── HistoryViewModel.cs      # NEW
│       └── Views/
│           └── HistoryView.xaml         # NEW (SfListView + empty state)
└── tests/
    ├── contract/
    │   ├── HistoryContractTests.cs          # NEW
    │   └── RecentDocumentsContractTests.cs  # NEW
    ├── integration/
    │   ├── CrossUserIsolationTests.cs       # NEW (≥2 users ≥20 actions 0 leak)
    │   └── OfflineHistoryTests.cs           # NEW
    └── unit/
        └── QueryHistoryStoreTests.cs       # NEW
```

**Structure Decision**: Single-project hybrid (Option 1) — reuses existing `RAGGit.Core` + `Api` + `MAUI` + `tests/`; no new project, no new top-level `api/`/`frontend/`.

## Complexity Tracking

> Fill ONLY if Constitution Check has violations that must be justified

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
