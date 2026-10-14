<!-- Sync Impact Report
Version change: 1.4.0 → 1.5.0
Modified principles: IV. Offline Invariant (NON-NEGOTIABLE) — verification clause scoped to LAN-mode desktop queries plus workstation-local queries, with the client↔workstation transport explicitly out of scope
Added sections: Amendment 1.4.0 → 1.5.0 (MINOR) with rationale + migration plan
Removed sections: none
Follow-up TODOs: workstation OS (Win/Linux) per deploy still open; hardware baseline after first benchmark still open; stale "WinUI 3" client wording (Principles III/VII and the Tech stack sentence) still to be reconciled with the shipped WPF client via a later PATCH.
Prior: 1.3.0 → 1.4.0 modified I (renamed "…or Private Cloud"), II (runtime reference only), Technology & Deployment Constraints (runtime + network wording), for feature 031.
Prior: 1.2.0 → 1.3.0 modified III (MAUI → WinUI 3), VII (project list + MSIX sideload), Technology & Deployment Constraints (client stack + storage).
Prior: 1.0.0 → 1.1.0 modified III (was Desktop Reuse), VII (signing), VI wording; resolved MAUI clarification 2026-09-10.
-->
# RAGGit Constitution

## Core Principles

### I. Single-Tenant On-Prem or Private Cloud

Each deployment serves exactly one company (single-tenant), whether it runs on-prem (local mode) or on a company-controlled private cloud (private cloud mode). There is no multi-tenant isolation, no `company_id` partitioning, and no cross-company data sharing. One AI Workstation (or small cluster) owns the library for that company; every employee desktop connects to that workstation over the company's own network - the LAN, or the company's private cloud network (VPN and/or restricted source addresses). A workstation MUST NOT be left as an open public endpoint. A fresh install is a fresh company. This MUST be enforced in data model (singleton Library), API (no tenant parameter), and deployment docs.

### II. Workstation-Owned AI

The AI Workstation owns all heavy AI: Ollama (embed `nomic-embed-text`/`all-MiniLM` + chat `llama3.2:3b-q4`/`phi-3-mini`/`mistral:7b-q4`), vector store (`Qdrant` via `QdrantClient(path=)` local file or `Sqlite-vec`/`LanceDB` embedded; `LanceDB` is the implemented store), and the RAG API (ASP.NET Core .NET 10). Employee desktops MUST remain thin: no local model weights, no embedded vector DB, only UI + HttpClient. Model updates happen only on workstations, not on N desktops.

### III. .NET Library-First & Client Reuse

Every feature MUST start as a .NET library under `src/` (`RAGGit.Core`, `RAGGit.Ingest`, `RAGGit.Retrieval`) that is self-contained, independently testable, and documented with a clear purpose. Libraries expose functionality via both a .NET API and a CLI where applicable (text in/out, JSON + human-readable). Clients MUST reuse libraries: WinUI 3 for Windows 10 1809+ / 11 (Windows App SDK, `net8.0-windows10.0.17763.0`, single-project MSIX) sharing ViewModel/services via `RAGGit.Client.Core`, OR WPF for Windows-only fallback. No duplicated RAG logic.

### IV. Offline Invariant (NON-NEGOTIABLE)

No query-time cloud egress is allowed. With WAN disabled, `POST /api/query` on the workstation MUST succeed end-to-end (embed → filtered vector search → generation), and in local mode a desktop query over the LAN MUST succeed as well. This MUST be verified by a CI gate that disables WAN but keeps LAN and runs the full RAG integration suite; in private cloud mode that same gate verifies the workstation-local query, and the client↔workstation transport (VPN over the company's private network) is explicitly out of scope for this invariant. Model weights and embedding ONNX files MUST be bundled or cached on the workstation; `ollama pull` from the internet at query time is forbidden. If a model is missing offline, the system MUST fail fast with `model unavailable offline`, not hang.

### V. Citation-Grounded RAG

Every answer generated from the library MUST include citations (source `documentId` + `chunkId` + text) when source exists. If no relevant chunks are found, the system MUST respond `no relevant content found` and MUST NOT hallucinate. Chunking defaults to 512 tokens / 50 overlap (tunable via config, not per-user v1), retrieval topK=5, and retrieval precision MUST be measured on a curated 50 Q/A eval set (SC-003 ≥80% top-5 contains relevant doc).

### VI. Test-First (NON-NEGOTIABLE)

TDD is mandatory: tests written → reviewed → fail → then implement. Red-Green-Refactor is strictly enforced. Required gates: unit (chunking, payload filtering), contract (OpenAPI in `contracts/api.yaml`), integration (LAN-only offline query, configured auth provider (API key, local per-person accounts, or Windows AD), `GET/POST /api/documents`), and eval harness. No feature merges without the offline-invariant test passing. Coverage target: ≥80% for libraries, 100% for contracts.

### VII. Simplicity & Proprietary Stewardship

Start simple, keep a single project (`src/` + `RAGGit.Client.WinUI/` + `RAGGit.Workstation.Api/` + `tests/`) until a 4th project is justified in the plan's Complexity Tracking table. Proprietary: artifacts are signed (MSIX sideload for Windows 10 1809+ / 11 desktop) and distributed privately per company; model weights stay on customer-owned workstations, not redistributed publicly. Use `MAJOR.MINOR.PATCH` versioning; breaking API changes require a MAJOR bump and migration notes.

## Technology & Deployment Constraints

**Stack**: C# .NET 10 (Workstation ASP.NET Core API + Client WinUI 3 Windows 10 1809+ / 11, thin HttpClient), `LanceDB` local file vector store (`VectorDb:Path`) with `Qdrant.Client` `path=`-based local file or `Sqlite-vec` remaining permitted alternatives + `SQLite` for doc metadata, `Ollama` (`OllamaSharp` or `HttpClient` to `localhost:11434`, native on the workstation host) or `LLamaSharp` (`LLamaWeights.LoadFromFile` + `LLamaEmbedder.GetEmbeddings` for GGUF), `ONNX Runtime` with `bge-micro-v2` as alternative embedder, `Semantic Kernel` for RAG orchestration where it reduces code (OnnxSimpleRAG pattern). **Hardware baseline**: Workstation 16GB RAM + NVIDIA GPU recommended, 10GB disk for models+DB; Desktop 4GB RAM thin. **Network**: the company's own network only - the LAN, or the company's private cloud network over VPN/restricted source addresses; a workstation MUST NOT be left as an open public endpoint, and there is no WAN AI egress at query time. **Storage**: `LanceDB` persists under `./data/lancedb` on the workstation (`QdrantClient(path="./data/qdrant")` remains the permitted alternative); the desktop client is stateless (credential/token cache only).

## Development Workflow & Quality Gates

Code review requires 1 approval and verifies constitution compliance, especially Offline Invariant and Single-Tenant assumptions. CI MUST run: `dotnet build`, `dotnet test`, contract tests, and the WAN-disabled offline suite. `Complexity Tracking` in `plan.md` MUST justify any extra project or vector DB swap (Qdrant ↔ LanceDB). `plan.md` Constitution Check (pre-Phase 0 and post-Phase 1) MUST pass before `tasks.md` generation. All `spec.md` requirements MUST be testable; no `[NEEDS CLARIFICATION]` remains at `plan` gate without an explicit Assumption.

## Governance

This constitution supersedes all other practices. Amendments require: (1) documentation in this file with rationale, (2) approval by project owner, (3) a migration plan for affected specs/plans/tasks, and (4) a semantic version bump: MAJOR for incompatible principle removal/redefinition, MINOR for new principle/expanded guidance, PATCH for wording/clarification. All PRs and reviews MUST verify compliance. Use `.specify/memory/constitution.md` as the runtime source of truth for `/speckit.specify`, `/speckit.plan`, and `/speckit.tasks`. Historical values are preserved when re-scaffolding via `resolve-template.ps1`.

**Version**: 1.5.0 | **Ratified**: 2026-08-31 | **Last Amended**: 2026-10-14

### Amendment 1.4.0 → 1.5.0 (MINOR)

- **Modified principle**: IV. Offline Invariant (NON-NEGOTIABLE) — the verification clause is scoped so the invariant is proved where it actually applies. Local mode keeps the LAN-verified desktop query; private cloud mode verifies the **workstation-local** `POST /api/query` offline, and the client↔workstation transport (VPN over the company's private network) is explicitly **out of scope**. The invariant's substance is unchanged: no query-time cloud egress, model weights bundled or cached on the workstation, fail fast with `model unavailable offline`.
- **Rationale**: `031-remote-workstation-hosting` adds a private-cloud mode whose desktop↔workstation link crosses a VPN rather than a LAN. Read literally, the previous wording ("desktop query via LAN MUST succeed" with WAN disabled) could not be satisfied in that mode, even though the invariant's purpose - the workstation answering entirely with its own local models - is unaffected. This amendment removes the ambiguity without weakening the invariant.
- **Migration plan**: Historical specs `001`–`030` are unaffected (they are LAN-mode and remain covered by the existing LAN CI gate). `031-remote-workstation-hosting` verifies the workstation-local query offline in private cloud mode. No API or data-model change.

### Amendment 1.3.0 → 1.4.0 (MINOR)

- **Modified principle**: I. Single-Tenant On-Prem → **Single-Tenant On-Prem or Private Cloud**. Reworded so the workstation may run on the company's own network (local mode) or on a company-controlled private cloud machine reachable only over the company's own network (VPN / restricted source addresses); a workstation MUST NOT be left as an open public endpoint. Single-tenant, no-`company_id`, and singleton-Library requirements are unchanged.
- **Modified section**: Technology & Deployment Constraints - refreshed the stale ".NET 8 / Qdrant" runtime wording to the current stack (.NET 10, LanceDB, native Ollama on the host), and restated **Network** as "the company's own network only (LAN, or the company's private cloud network over VPN/restricted source addresses); never an open public endpoint; no WAN AI egress at query time". Principle II's runtime reference was aligned (.NET 8 → .NET 10) as part of the same wording refresh.
- **Rationale**: `031-remote-workstation-hosting` adds a private-cloud deployment mode alongside the existing local (on-prem) mode, so a company without an on-network machine can still host and own its own workstation. Both modes stay inside the LAN/VPN boundary and preserve the Offline Invariant (IV): the workstation answers with its own local models; only the client↔workstation transport differs, and it is never public.
- **Migration plan**: Historical specs `001`–`030` are unaffected (they describe behavior, not topology). No API or data-model change. `031-remote-workstation-hosting` is the first feature under the widened wording. The stale "WinUI 3" client wording in Principles III/VII and in this section's stack sentence is out of scope here and is recorded as a follow-up TODO for a later PATCH.

### Amendment 1.2.0 → 1.3.0 (MINOR)

- **Modified principle**: III. .NET Library-First & Client Reuse — client technology changed from ".NET MAUI for Windows 11 + iOS/Android (or WPF fallback)" to "WinUI 3 for Windows 10 1809+ / 11 (or WPF fallback)". VII project list and distribution updated (`RAGGit.Client.Maui/` → `RAGGit.Client.WinUI/`; signed MSIX sideload replaces MAUI/mobile distribution).
- **Rationale**: `010-winui-client` replaces the MAUI shell outright with a Windows-only WinUI 3 client reusing the shared `RAGGit.Client.Core` behavior layer; Android/iOS targets are dropped by owner decision.
- **Migration plan**: Historical specs `001`–`009` are unaffected (they describe behavior, not shell technology). `010-winui-client` and onward use the amended wording. No API or data-model change.

### Amendment 1.1.0 → 1.2.0 (MINOR)

- **Modified principle**: VI. Test-First — expanded the configured auth provider list from "API key or Windows AD" to "API key, local per-person accounts, or Windows AD".
- **Rationale**: `004-identity` introduces local per-person accounts as the per-person authentication provider on the AI Workstation. API key remains valid for machine/bootstrap use; Windows AD remains a documented future provider.
- **Migration plan**: Historical specs `001`–`003` are unaffected. `004-identity` and onward use the amended wording. No code migration is required.
