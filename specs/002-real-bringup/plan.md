# Implementation Plan: Real Workstation Bring-Up

**Branch**: `002-real-bringup` | **Date**: 2026-09-11 | **Spec**: [spec.md](./spec.md) | **Constitution**: v1.1.0

**Input**: Feature specification from `/specs/002-real-bringup/spec.md` — real offline RAG loop with Ollama + LanceDB, thin MAUI client wiring, configurable embed model (all-minilm 384 dev / nomic-embed-text 768 prod), measured SC-001/SC-002 on dev hardware; dev is one laptop, prod is separate workstation box.

## Summary

Prove the thesis `001` only contracted: run the full ingest → embed → LanceDB `library` → retrieve topK=5 → local LLM loop for real with no fake vector store/fake embedder/LLM in the path, wire the thin MAUI client to the workstation via configuration and `GET /api/auth/me`, and enforce the configurable embedding dimension (384|768) with a startup guard. Keep Ollama as primary; ONNX/LLamaSharp remain behind a non-default fallback flag and tokenizer correctness stays a tracked todo (FR-005). Real-Ollama tests are opt-in and skip gracefully when `http://localhost:11434` is absent so CI stays green with fakes. SC-001/SC-002 wall-clock is measured once on the dev laptop and the "<7s on reference workstation" figure is a production assumption to verify at deploy.

## Technical Context

**Language/Version**: C# .NET 8 (both tiers; .NET 9 compatible)

**Primary Dependencies**: ASP.NET Core 8 (Workstation API), LanceDB .NET SDK embedded `lancedb.connect(VectorDb:Path)` file-backed, `SQLite` (`Microsoft.Data.Sqlite` doc metadata), `OllamaSharp` / `HttpClient` to `Ollama:Url` (`http://localhost:11434`) with `all-minilm` (384) dev / `nomic-embed-text` (768) prod + `phi3:mini` (dev) / `llama3.2:3b` (prod), `CommunityToolkit.Mvvm` (MAUI), `System.Text.Json` + `HttpClient` (client), Syncfusion MAUI (`Core 28.2.7`, `DataGrid 28.2.7`, `AIAssistView 28.2.7`, `Toolkit 1.0.11`), `Syncfusion.Licensing` offline key

**Storage**: Workstation: embedded LanceDB `connect("./data/lancedb")` table `library` with `FixedSizeList<float, VectorSize>` HNSW cosine (`m=16, ef=128`) + `rag.db` SQLite (`Documents`, `Chunks`, `Queries`, `Library` 1 row); Legacy `Qdrant:Path` (`./data/qdrant`) ignored with startup warning if it exists and disagrees with `VectorDb:Path`; Client: stateless (cache only) — no vector store

**Testing**: xUnit + FluentAssertions, `Microsoft.AspNetCore.Mvc.Testing` (contract), integration (offline WAN-disabled), opt-in real suite gated by `Fact(Skip=...)` probe of `Ollama:Url` (see R5) so CI without Ollama stays green; eval harness 50 Q/A for SC-003 (001) retained; 80% library coverage

**Target Platform**: AI Workstation: Windows 10+/Linux (Ubuntu 22.04) 16GB RAM + GPU recommended, 10GB disk for models+DB; Client: Windows 11 desktop primary (`net8.0-windows10.0.19041.0`); iOS/Android TFMs (`net8.0-ios`, `net8.0-android`) buildable but not acceptance for this feature (Q1) — LAN/VPN to workstation, no cloud egress

**Project Type**: Client + API (workstation service + thin MAUI client; single solution with 5 projects + tests)

**Performance Goals**: SC-001 ingest 50-page PDF → Ready <5 min on dev laptop (measured once, recorded in `verification.md`); SC-002 WAN-off `POST /api/query` cited answer measured wall-clock on dev laptop, reference workstation "<7s" explicitly a production assumption; SC-004 dimension mismatch fail-fast; SC-005 corrupted pdf → 400

**Constraints**: Offline invariant — no WAN at query time (LAN only), thin client zero local model weights, single-tenant per deployment (no `company_id`), proprietary signed distribution, thin client must handle `AI workstation unavailable` / `model unavailable offline` fail-fast within timeout (FR-007), `VectorDb:Path` single source of truth, `VectorDb:VectorSize` 384|768 validated at startup

**Scale/Scope**: 5k documents / ~1M chunks max per library (singleton), chunk 512 tokens / 50 overlap, retrieval topK=5, 50 screens max, ~5 projects in solution; model swap requires fresh `data/lancedb` (dimension mismatch expected)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] **I. Single-Tenant On-Prem**: Data model remains singleton Library (no Company entity), API has no tenant parameter (`/api/documents` not `/companies/{cid}/...`), one-tenant-per-workstation — unchanged from 001, complies.
- [x] **II. Workstation-Owned AI**: Vectors and LLMs live only on workstation (LanceDB path, Ollama); MAUI client remains `HttpClient`-only, no `IVectorStore`/`IEmbedder`/`ModelParams` in `RAGGit.Client.Maui` — wiring adds only `Workstation:Url` + API key + `GET /api/auth/me`, complies.
- [x] **III. .NET Library-First & Client Reuse**: Libraries `RAGGit.Core`/`RAGGit.Ingest`/`RAGGit.Retrieval` reused by both API and client; `RAGGit.Workstation.Api` hosts AI, `RAGGit.Client.Maui` thin with `CommunityToolkit.Mvvm` — TFMs `net8.0-windows10.0.19041.0` / `net8.0-ios` / `net8.0-android` (`net8.0` fallback for CI without MAUI workload), complies.
- [x] **IV. Offline Invariant (NON-NEGOTIABLE)**: Real path verified with WAN logically disabled (or Ollama unreachable) — `503 model unavailable offline` fail-fast, no cloud pull, no hang; opt-in suite does not require WAN; Ollama pull at query time forbidden, complies.
- [x] **V. Citation-Grounded RAG**: `POST /api/query` still `{answer, citations[]}` + `no relevant content found` branch, measured in SC-002, complies.
- [x] **VI. Test-First (NON-NEGOTIABLE)**: TDD gates unit + contract (`contracts/api.yaml` v1.1.0) + integration + opt-in real (skips when Ollama absent); no merge without offline-invariant passing, complies.
- [x] **VII. Simplicity & Proprietary Stewardship**: Still 5 projects + tests within limit; no new project; versioning `1.1.0` MINOR for non-breaking `GET /api/auth/me` envelope (`identityType` + `role`, Q2), `Qdrant:Path` legacy warning additive; signing MSIX + private mobile, complies.

*Re-check after Phase 1 2026-09-11: No new violations introduced by dimension guard, client wiring, or corrupted-pdf hardening (400).*

## Project Structure

### Documentation (this feature)

```text
specs/002-real-bringup/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── verification.md      # Phase 1 output (SC-001/SC-002 measurement log)
├── contracts/           # Phase 1 output (/speckit.plan command)
│   └── api.yaml         # OpenAPI 1.1.0: 001 + GET /api/auth/me + 400 corrupted pdf/docx note
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
RAGGit.sln
├── src/
│   ├── RAGGit.Core/                 # Shared models (Document, Chunk, Query), chunking, interfaces
│   ├── RAGGit.Ingest/               # Chunk 512/50 → embed (Ollama) → LanceDB upsert; LanceDbLocalClient
│   ├── RAGGit.Retrieval/            # Embed query → LanceDB search topK=5 → prompt → local LLM
│   ├── RAGGit.Workstation.Api/      # ASP.NET Core 8 API: POST/GET /api/documents, DELETE /api/documents/{id}, POST /api/query, GET /api/auth/me, /health
│   └── RAGGit.Client.Maui/          # .NET MAUI .NET 8: TFMs net8.0-windows10.0.19041.0 / net8.0-ios / net8.0-android; fallback net8.0; LibraryView, UploadView, QueryView, app settings wiring
├── tests/
│   ├── unit/                        # RAGGit.Core/Ingest/Retrieval + Api dimension guard (fake/pre-seeded, no Ollama)
│   ├── contract/                    # OpenAPI contract tests (Api.Tests) against api.yaml 1.1.0
│   └── integration/                 # LAN-only offline suite + opt-in real suite (skip when Ollama absent) + RBAC
├── data/                            # .gitignored: ./data/lancedb (VectorDb:Path), rag.db; legacy ./data/qdrant warned
└── models/                          # .gitignored: GGUF + ONNX (bge-micro-v2, phi-3-mini) — not acceptance for this feature
```

**Structure Decision**: Client + API — same 5-project split as `001-offline-mode` (Rationale: workstation API and MAUI client share `RAGGit.Core`/`Ingest`/`Retrieval` per Constitution III but client must stay thin per II). No new project; dimension guard lives in `RAGGit.Ingest.Vector.LanceDbLocalClient` + startup hosted service; client wiring is `Workstation:Url`/`Api:AdminKey`/`Api:EmployeeKey`/`Workstation:ApiKey` configuration + `DelegatingHandler` for `X-Api-Key` + `GET /api/auth/me` discover; corrupted-pdf hardening is Ingest validation returning 400.

## Phase 0 Research Summary

Seven decisions (R1-R7). Full details in [research.md](./research.md).

| # | Topic | Decision | Why |
|---|-------|----------|-----|
| R1 | LanceDB dimension introspection + guard timing | Startup guard reads existing `library` table schema (Arrow `FixedSizeList` size) via `Connection.OpenTable` + `Schema` and compares to `VectorDb:VectorSize`; mismatch → fail-fast with both dims + recovery (wipe `data/lancedb` or migrate); first-request check as backstop for lazy-created table | Q3 normative startup; existing `LanceDbLocalClient.EnsureTableAsync` already creates schema — guard precedes creation when table exists |
| R2 | Configurable embed model (384 vs 768) | `Ollama:EmbedModel` selects model (`all-minilm` 384 dev / `nomic-embed-text` 768 prod) and must align with `VectorDb:VectorSize`; `appsettings.json` dev `all-minilm`/`VectorSize:384`, prod `nomic-embed-text`/`768`; prod verified at deploy, dev measured | Single source of truth per FR-001; mismatch produces junk embeddings |
| R3 | Guard vs health precedence | Dimension mismatch is startup failure (prevents `/health` healthy); `/health` reports `vectorDb: down` with mismatch detail when backstop fires; legacy `Qdrant:Path` disagreement is startup warning, not failure | Health must not mask misconfiguration (spec S3); warning preserves additive compat |
| R4 | Contract tooling | Keep `contracts/api.yaml` as source of truth for contract tests (`Microsoft.AspNetCore.Mvc.Testing`); no codegen for this feature (manual `DocumentsApiClient`/`QueryApiClient` + new `AuthApiClient`); Swift/TS gen deferred | Matches `001` contract test pattern; avoids churn for MINOR bump |
| R5 | xUnit opt-in skip mechanics | Real-Ollama suite marked `[Fact(Skip="...")]` dynamically via `ITestCase` trait or conditional `Skip.IfNot` helper that probes `GET {Ollama:Url}/api/tags` with 1s timeout; CI without Ollama stays green; `dotnet test --filter Trait!=RequiresOllama` still passes | FR-002/FR-007 + SC-003 require green CI without Ollama |
| R6 | MAUI config loading | `RAGGit.Client.Maui/appsettings.json` (`Workstation:Url`, `Workstation:ApiKey` + `Api:AdminKey`/`Api:EmployeeKey`) loaded via `MauiAppBuilder.Configuration.AddJsonFile` + `UserSecrets` fallback; missing key/url → config error UI at launch (never unauthenticated); no hard-coded URLs | FR-003 + spec edge case; `MauiProgram.cs` today uses `CreateDefault()` — extended to `CreateWithOptions` |
| R7 | Ollama timeout / offline fail-fast | `Ollama:Url` unreachable or model not pulled → 2-5s `HttpClient` timeout, then `503 model unavailable offline` / `AI workstation unavailable`; never hang waiting for `ollama pull`; `Ollama:EmbedModel` pulled offline before WAN-off | Constitution IV `model unavailable offline` + spec edge cases; timeout validated in `OllamaEmbedder`/`OllamaChatClient` |

Open verification items (kept in `research.md`): LanceDB Arrow schema size retrieval API exact (needs live SDK probe), xUnit conditional skip pattern choice (trait vs `Skip.IfNot`), MAUI `appsettings.json` `MauiAsset` vs `EmbeddedResource` loading, Ollama `2s` vs `5s` timeout tuning, guard-vs-health message wording, contract tooling decision re-confirmed.

## Phase 1 Design

### Data Model — delta on 001

- **No SQLite schema change** — `Documents`, `Chunks`, `Queries`, `Library` unchanged (see [data-model.md](./data-model.md)).
- **LanceDB `library` collection dimension now enforced**: `384` (all-minilm / bge-micro-v2) or `768` (nomic-embed-text); Arrow `FixedSizeList` size is the stored truth; config `VectorDb:VectorSize` must match at startup (Q3) or first request (backstop).
- **ClientSession (client-memory only)**: `WorkstationUrl` + `ApiKey` + discovered `{identityType, role}` from `GET /api/auth/me` (extensible envelope per Q2, client ignores unknown fields); gates `IsAdmin` and `Upload`/`Delete` visibility; held in MAUI DI, not persisted.
- **Embedding dim note becomes enforced** — previously informational (001), now startup-validated (FR-001/SC-004).

### Contracts — OpenAPI 1.1.0

Copy of `specs/001-offline-mode/contracts/api.yaml` with `info.version: 1.1.0` (MINOR per Constitution VII — additive, non-breaking) plus:

- `GET /api/auth/me` — `security: ApiKey`, `200 {identityType: string, role: Admin|Employee}` (extensible, Q2), `401` when unauthenticated.
- `POST /api/documents` `400` annotated for corrupted/invalid `pdf`/`docx` (no partial index, FR-006).
- All other paths/schemas unchanged (`POST /api/documents` 201/200/413, `GET /api/documents`, `DELETE /api/documents/{id}`, `POST /api/query` 200/503, `/health`).

See [contracts/api.yaml](./contracts/api.yaml).

### Quickstart & Verification

- [quickstart.md](./quickstart.md): real bring-up steps — `ollama pull all-minilm` + `phi3:mini` (dev), `dotnet run` workstation, `/health`, `curl` upload + query, guard demo (384 → 768 restart), opt-in `dotnet test --filter RequiresOllama`, client `appsettings.json` sample (`Workstation:Url`, keys), and note that `--workstation`/`--api-key` flags are fictional (per current `MauiProgram.cs`).
- [verification.md](./verification.md): template for SC-001/SC-002 measured wall-clock log with dev laptop specs and production assumption note ("<7s on reference workstation" is assumption to verify at deploy, not asserted on dev laptop).

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |

No violations. Still 5 projects + tests; no new project, no cloud fallback, no per-person identity in this feature, ONNX/LLamaSharp remain fallback-only behind flag.

