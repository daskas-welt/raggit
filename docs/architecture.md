# RAGGit Architecture — 001-offline-mode

**Constitution:** `v1.3.0` • Single-tenant LAN-only • Workstation-Owned AI • WinUI 3 (Windows 10 1809+ / 11) • NavigationView shell + ListView grid + chat (Windows App SDK 1.5)

**Diagram:** [`architecture.puml`](./architecture.puml) — PlantUML system topology. Presentation render: [`images/architecture.svg`](./images/architecture.svg) (via official MCP `npx -y @plantuml/mcp-js@0.2.2`; PNG optionally via `scripts/Render-PlantUml.ps1`).

> Edit the `.puml` source, re-render the `.svg`, and commit both together.

## Source

PlantUML: [`architecture.puml`](./architecture.puml).

## Views

- **Primary query path** — Employee → WinUI → API → Retrieval → LanceDB topK=5 → Ollama cited answer (WAN-off)
- **Ingest path** — Admin → WinUI → API → Ingest (512/50) → LanceDB + SQLite Ready
- **Trust and cache** — X-Api-Key RBAC, IMemoryCache 10k/24h + ETag 30s, rag.db persisted

## Components

- **Client** `RAGGit.Client.WinUI` (frontend) — WinUI 3 net8.0-windows10.0.17763.0 (Win10 1809+ / 11, x64/x86/ARM64), NavigationView shell + ListView grid (Library) + chat (Query citations) over shared `RAGGit.Client.Core` ViewModels, HttpClient only, CommunityToolkit.Mvvm, no vendor license
- **Workstation** `RAGGit.Workstation.Api` (backend) — ASP.NET Core 8, Core plain net8.0, Ingest + Retrieval
- **Storage** — LanceDB `./data/lancedb` HNSW m=16 + SQLite `rag.db`
- **AI** — Ollama :11434 nomic-embed + llama3.2:3b / ONNX bge-micro-v2, cached, no pull at query
- **Cache** — IMemoryCache + ETag, SemaphoreSlim herd guard for 200×50+ q/day, p95 <7s
- **Placement** — `RAGGit.Client.Core` ships only in the employee MSIX, never to the workstation (the API references `RAGGit.Core`/`Ingest`/`Retrieval` only)

## Invariants

- LAN/VPN only, WAN disabled CI gate T044 (IV)
- Singleton Library, RBAC, citations or no relevant content found (V)
- No vendor UI license — CommunityToolkit.Mvvm + built-in WinUI 3 controls only
