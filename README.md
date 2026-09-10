# RAGGit — Offline-Mode Single-Tenant RAG Library

> **Proprietary, on-prem, single-tenant RAG for companies.** An AI Workstation hosts the local vector store (`LanceDB` file) and local LLM (`Ollama`/`LLamaSharp`/`ONNX`) and serves a thin `.NET` desktop app over LAN — no cloud egress at query time.

**Constitution**: `v1.0.0` ratified `2026-08-31` — `Single-Tenant On-Prem`, `Workstation-Owned AI`, `.NET Library-First`, `Offline Invariant (NON-NEGOTIABLE)`, `Citation-Grounded RAG`, `Test-First`, `Simplicity & Proprietary` — see [`.specify/memory/constitution.md`](.specify/memory/constitution.md).

**Feature**: `001-offline-mode` — [spec](./specs/001-offline-mode/spec.md) | [plan](./specs/001-offline-mode/plan.md) | [research](./specs/001-offline-mode/research.md) | [tasks](./specs/001-offline-mode/tasks.md)

## Architecture

```
Company LAN (no WAN at query time)
AI Workstation (on-prem) ── LAN ── Employee Desktops (.NET thin clients)
├─ ASP.NET Core API (src/RAGGit.Workstation.Api) ──┐
├─ LanceDB file data/lancedb (lancedb.connect(path))    │
├─ Ollama localhost:11434 (nomic-embed-text + llama3.2:3b)  │
└─ SQLite rag.db ──────────────────────────────────┘
Desktop: WPF (Win-only) or Avalonia (cross-platform) — HttpClient only, zero local models
API: POST /api/documents (Admin), GET /api/documents, POST /api/query {answer,citations}, DELETE /api/documents/{id}
```

**Dev on one machine**: workstation = `localhost:5001` + `localhost:11434`, desktop → `localhost:5001` (same machine, still LAN-only). See [Quickstart](#quickstart-single-machine-dev).

## Tech Stack

- **Language**: C# .NET 8 (`RAGGit.Core`/`Ingest`/`Retrieval`/`Workstation.Api`/`Desktop`)
- **Vector**: `LanceDB` .NET SDK embedded `connect(path)` (file, no server) — approved alt `Sqlite-vec`; `Qdrant.Client` rejected because its .NET SDK lacks embedded `path=` mode
- **AI**: `OllamaSharp` (`POST /api/embed` + `/api/chat`) or `LLamaSharp` (`LLamaEmbedder.GetEmbeddings` GGUF) + `ONNX Runtime` (`bge-micro-v2` 80MB + `Phi-3-mini` int4) via `Semantic Kernel` `OnnxSimpleRAG`
- **Desktop**: WPF (.NET 8) for Windows-only, Avalonia UI 11 for cross-platform — same ViewModels
- **Testing**: xUnit + FluentAssertions, WAN-disabled integration suite, 50 Q/A eval harness

## Project Structure

```
RAGGit.sln
├── src/
│   ├── RAGGit.Core/              # Models Document/Chunk/Query, abstractions IVectorStore/IEmbedder/ILlmClient
│   ├── RAGGit.Ingest/            # Chunker 512/50 → embed → upsert
│   ├── RAGGit.Retrieval/         # embed query → search topK=5 → prompt → local LLM
│   ├── RAGGit.Workstation.Api/   # ASP.NET Core: /api/documents, /api/query, /health
│   └── RAGGit.Desktop/           # WPF/Avalonia thin client
├── tests/
│   ├── unit/ | contract/ | integration/  # including WAN-disabled offline suite
├── specs/001-offline-mode/       # spec.md, plan.md, research.md, data-model.md, quickstart.md, contracts/api.yaml, tasks.md
├── data/                         # .gitignored: data/lancedb, rag.db
└── models/                       # .gitignored: *.gguf, *.onnx
```

## Quickstart — Single-Machine Dev

No workstation needed. Everything runs on `localhost`.

### Prereqs

- .NET 8 SDK (`dotnet --version` ≥8.0), Git LFS (for ONNX)
- 16GB RAM recommended (8GB works with `all-minilm` + `phi-3-mini`), 10GB disk

### 1. Build

```powershell
git clone https://github.com/daskas-welt/raggit.git; cd raggit
dotnet build RAGGit.sln  # after T001 scaffolding
```

### 2. AI Models (once, then WAN can be off)

```powershell
# Ollama (one binary for embed+chat)
ollama serve
ollama pull nomic-embed-text   # or all-minilm 384d
ollama pull llama3.2:3b        # ~2GB Q4

# OR pure .NET (no daemon)
git lfs install
git clone https://huggingface.co/TaylorAI/bge-micro-v2 ./models/bge-micro-v2
```

`src/RAGGit.Workstation.Api/appsettings.Development.json`:
```json
{ "VectorDb": { "Path": "./data/lancedb", "Provider": "LanceDB", "VectorSize": 384 }, "Qdrant": { "Path": "./data/qdrant" }, "Ollama": { "Url": "http://localhost:11434", "EmbedModel": "nomic-embed-text", "ChatModel": "llama3.2:3b" } }
```
```powershell
dotnet user-secrets set "Api:Key" "dev-key-123" --project src/RAGGit.Workstation.Api
```

### 3. Run (2 terminals)

```powershell
# Terminal A — workstation (your "AI workstation")
dotnet watch --project src/RAGGit.Workstation.Api --urls http://localhost:5001
# Swagger http://localhost:5001/swagger  Health http://localhost:5001/health

# Terminal B — desktop
dotnet run --project src/RAGGit.Desktop -- --workstation http://localhost:5001 --api-key dev-key-123
```

### 4. Verify Offline Invariant

```powershell
curl -X POST http://localhost:5001/api/documents -H "X-Api-Key: dev-key-123" -F "file=@sample.pdf"
curl http://localhost:5001/api/documents -H "X-Api-Key: dev-key-123"
# Disable WiFi (keep localhost), then:
curl -X POST http://localhost:5001/api/query -H "X-Api-Key: dev-key-123" -H "Content-Type: application/json" -d '{"query":"refund policy"}'
# → {answer, citations:[{documentId, chunkId, text}]}
```

### 5. Tests

```powershell
dotnet test --filter Category=unit
dotnet test --filter Category=contract
dotnet test --filter Category=integration  # WAN-disabled suite
```

See [specs/001-offline-mode/quickstart.md](specs/001-offline-mode/quickstart.md) for LAN deploy to real workstation.

## Specs — 001-offline-mode

- **US-1 P1**: Admin upload → index Ready <5min for 50 pages
- **US-2 P1**: Employee WAN-off query → cited answer <7s p95 or `no relevant content found`
- **US-3 P2**: Admin delete purges vectors
- **US-4 P3**: Employee browse read-only (403 on write)
- **FR-010**: PDF/docx/txt/md only v1 (video/audio out of scope)
- **FR-011**: 5k docs / ~1M chunks, 512/50, topK=5

## Development Workflow

Constitution-driven SDD: `constitution` → `specify` → `clarify` → `plan` → `tasks` → `implement`. `tasks.md` has 43 tasks phased as `Setup (T001-T006) → Foundational (T007-T013 BLOCKS) → US-1 (T014-T021) → US-2 (T022-T029 MVP) → US-3/4 → Polish`.

```powershell
# In .specify scripts (PowerShell)
.specify/scripts/powershell/create-new-feature.ps1 -ShortName "offline-mode" "..."
.specify/scripts/powershell/setup-plan.ps1
.specify/scripts/powershell/setup-tasks.ps1
```

## License

Proprietary — all rights reserved. Models stay on customer-owned workstations, not redistributed.

---

**Next**: `dotnet new` scaffolding per `tasks.md T001` → implement `Foundational` → `US-1` MVP ingest half → `US-2` query half (closed-loop shippable).
