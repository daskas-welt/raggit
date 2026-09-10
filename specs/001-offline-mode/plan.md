# Implementation Plan: RAGGit Offline-Mode Single-Tenant RAG Library

**Branch**: `001-offline-mode` | **Date**: 2026-08-31 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/001-offline-mode/spec.md` — single-tenant RAG library: AI Workstation hosts local AI (Ollama/LLamaSharp/ONNX) and embedded vector store (Qdrant local path) via ASP.NET Core API; .NET MAUI client (Windows 11 desktop + iOS/Android, single codebase) for admins (upload/see library) and employees (query with citations); LAN-only, no cloud egress.

## Summary

Build RAGGit as a single-tenant on-prem system: AI Workstation owns all heavy AI (chunk → embed locally → `QdrantClient(path="./data/qdrant")` → retrieve topK=5 → local LLM `llama3.2:3b-q4`/`phi-3-mini`) exposed via `ASP.NET Core` API (`POST /api/documents`, `POST /api/query`). Thin `.NET` client (`.NET MAUI` single codebase: Windows 11 + iOS + Android) calls the API over LAN — no local models on client. Satisfies constitution v1.1.0 I (single-tenant), II (workstation-owned AI), IV (offline invariant, WAN-off query), V (citation-grounded), VI (test-first), and VII (proprietary signed). Approach proven via Phase 0 research: `Qdrant local path`, `Ollama /api/embed`, `LLamaSharp GetEmbeddings`, `Semantic Kernel OnnxSimpleRAG` (bge-micro-v2 + Phi-3 ONNX).

## Technical Context

**Language/Version**: C# .NET 8 (both tiers; .NET 9 compatible)

**Primary Dependencies**: ASP.NET Core 8 (Workstation API), Qdrant.Client (embedded `path=` mode) + SQLite (`Microsoft.Data.Sqlite` for doc metadata), OllamaSharp or HttpClient to `localhost:11434` (embed `nomic-embed-text`/`all-MiniLM` + chat `llama3.2:3b-q4`) OR LLamaSharp (`LLamaWeights.LoadFromFile` + `LLamaEmbedder.GetEmbeddings` for GGUF) + ONNX Runtime (`bge-micro-v2` + `phi-3-mini-4k-instruct-onnx` as alternative), Semantic Kernel (RAG orchestration where it reduces code), Client: .NET MAUI (.NET 8) — TFMs `net8.0-windows10.0.19041.0` (Windows 11, Fluent/WinUI3-rendered), `net8.0-ios`, `net8.0-android`; `CommunityToolkit.Mvvm`; `HttpClient` + `System.Text.Json`; signing via MSIX (Windows) + private mobile distribution

**Storage**: Workstation: embedded `QdrantClient(path="./data/qdrant")` (file-backed, no server) + `rag.db` SQLite (Documents/Queries); Client: stateless (cache only). No external DB, no cloud vector store.

**Testing**: xUnit + FluentAssertions, `Microsoft.AspNetCore.Mvc.Testing` (contract), integration suite with WAN disabled (LAN-only) verifying SC-002, eval harness 50 Q/A for SC-003, `dotnet test` in CI

**Target Platform**: AI Workstation: Windows 10+/Linux (Ubuntu 22.04) — 16GB RAM + GPU recommended, 10GB disk for models+DB; Employee Client: Windows 11 (MAUI desktop) + iOS/Android (MAUI mobile, same codebase) — 4GB RAM thin client, 300MB install, LAN/VPN to workstation (mobile via site VPN or same subnet)

**Project Type**: Client + API (workstation service + thin MAUI client; single solution with multiple projects)

**Performance Goals**: SC-001 ingest 50-page PDF → Ready <5min; SC-002 WAN-off query p95 <7s (retrieval <2s + generation <5s) over 50 trials on reference workstation; SC-003 retrieval ≥80% top-5 relevant on 50 Q/A eval; SC-004 100% citation when source, 0% hallucination; SC-005 RBAC 403 enforcement

**Constraints**: Offline invariant — no WAN at query time (LAN only), thin desktop with zero local model weights, single-tenant per deployment (no company_id partition), proprietary signed distribution, thin client must handle `AI workstation unavailable` / `model unavailable offline` fail-fast (FR-007)

**Scale/Scope**: 5k documents / ~1M chunks max per library (singleton), chunk 512 tokens / 50 overlap, retrieval topK=5, 50 screens max (library + query), ~5 projects in solution

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] **I. Single-Tenant On-Prem**: Data model is singleton Library (no Company entity), API has no tenant parameter (`/api/documents` not `/companies/{cid}/…`), deployment is one-tenant-per-workstation — complies.
- [x] **II. Workstation-Owned AI**: Vectors and LLMs live only on workstation (`Qdrant path`, Ollama/LLamaSharp/ONNX); client is HttpClient-only (all TFMs), no `ModelParams` or `QdrantClient` in MAUI project — complies.
- [x] **III. .NET Library-First & Client Reuse**: Solution splits into `RAGGit.Core`/`RAGGit.Ingest`/`RAGGit.Retrieval` libraries reused by both API and MAUI client with Core plain `net8.0` constraint (no duplicated RAG logic) — complies.
- [x] **IV. Offline Invariant (NON-NEGOTIABLE)**: Plan includes WAN-disabled CI gate and `model unavailable offline` error path; no cloud fallback in design — complies.
- [x] **V. Citation-Grounded RAG**: `POST /api/query` contract requires `{answer, citations[]}` and `no relevant content found` branch; eval harness enforces SC-004 — complies.
- [x] **VI. Test-First (NON-NEGOTIABLE)**: Plan mandates xUnit TDD with offline suite and 80% library coverage before implement — complies.
- [x] **VII. Simplicity & Proprietary Stewardship**: 5 projects (`RAGGit.Core`, `RAGGit.Ingest`, `RAGGit.Retrieval`, `RAGGit.Workstation.Api`, `RAGGit.Client.Maui`) + `tests/` within limit (justified per Constitution III); versioning MAJOR.MINOR.PATCH, signed MSIX + private enterprise mobile distro — complies.

*Re-check 2026-09-10: switched client from WPF/Avalonia desktop to .NET MAUI (Windows 11 + iOS + Android, single codebase). Constitution amended to v1.1.0 (MINOR, Last Amended 2026-09-10: Principle III renamed .NET Library-First & Client Reuse with MAUI TFMs + WPF fallback, Principle VII MSIX + private mobile signing, Stack line, VI "configured auth provider" gate wording). Plan compliant with v1.1.0: AI still workstation-owned, client remains HttpClient-only, no Linux/macOS client target, Core stays plain net8.0, proprietary signing/private distribution preserved.*

## Project Structure

### Documentation (this feature)

```text
specs/001-offline-mode/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
│   └── api.yaml         # OpenAPI for /api/documents and /api/query
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
RAGGit.sln
├── src/
│   ├── RAGGit.Core/                 # Shared models (Document, Chunk, Query), chunking, interfaces
│   ├── RAGGit.Ingest/               # Chunk → embed (Ollama/LLamaSharp/ONNX) → Qdrant upsert
│   ├── RAGGit.Retrieval/            # Embed query → Qdrant search topK → prompt → local LLM
│   ├── RAGGit.Workstation.Api/      # ASP.NET Core 8 API: POST /api/documents, GET /api/documents, POST /api/query, DELETE /api/documents/{id}
│   └── RAGGit.Client.Maui/          # .NET MAUI .NET 8: Windows 11 + iOS + Android; LibraryView, UploadView, QueryView with citations
├── tests/
│   ├── unit/                        # RAGGit.Core/Ingest/Retrieval unit tests
│   ├── contract/                    # OpenAPI contract tests (Api.Tests)
│   └── integration/                 # LAN-only offline suite (WAN disabled), RBAC, eval harness
├── data/                            # .gitignored on workstation: ./data/qdrant, rag.db
└── models/                          # .gitignored: GGUF + ONNX (bge-micro-v2, phi-3-mini)
```

**Structure Decision**: Client + API — single solution with 5 library/app projects plus `tests/` (not Option 1 single `src/` nor Option 2 web `backend/frontend` split). Rationale: workstation API and MAUI client must share `RAGGit.Core`/`Ingest`/`Retrieval` libraries per Constitution III, but client must not embed AI. `RAGGit.Workstation.Api` hosts AI; `RAGGit.Client.Maui` is thin HttpClient. Alternative of single project rejected because it would force bundling Qdrant/LLM into client, violating Constitution II.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Embedded Qdrant native dep on workstation | Offline invariant requires file-backed HNSW with no server; cloud vector DB would violate WAN-off gate | Cloud Pinecone/Qdrant Cloud needs WAN, fails SC-002 |
| Native Ollama/LLamaSharp/ONNX Runtime dep on workstation | Local embed+LLM without cloud; thin desktop cannot run 2GB model | Cloud OpenAI fails offline invariant; bundling into desktop would need 8GB per employee |
| 5-project split (Core/Ingest/Retrieval/Api/Client.Maui) | Constitution III requires library reuse + II requires client thin | Single project would bundle AI into client violating II |
