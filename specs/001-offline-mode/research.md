# Research: RAGGit Offline-Mode Single-Tenant RAG Library

**Feature**: `001-offline-mode` | **Date**: 2026-08-31 | **Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

## Summary

Phase 0 research proves the single-tenant on-prem split (thin .NET desktop → LAN → AI Workstation owning all AI) satisfies the Offline Invariant with no cloud egress. All pillars verified via Context7 docs: `QdrantClient(path=)` local file, `Ollama /api/embed` + `/api/chat`, `LLamaSharp GetEmbeddings` with GGUF, and `Semantic Kernel OnnxSimpleRAG` (bge-micro-v2 + Phi-3 ONNX) for pure .NET without Python.

## Decisions

### 1. Vector Store — Embedded, No Server

| Candidate | Offline Pattern (verified) | Decision |
|-----------|----------------------------|----------|
| **Qdrant local** `qdrant/qdrant-client` | `QdrantClient(path="./data/qdrant")` persists to disk; `:memory:` for tests — `APIDOC QdrantClient Constructor location/path` | **Selected** — full payload filtering (for future `documentId` filter), HNSW Rust core, parity with cloud if later needed |
| **LanceDB** `lancedb/lancedb` | `lancedb.connect("<PATH>")` + `table.search().limit()` | Approved alternative — columnar, hybrid FTS+vector+SQL; defer unless FTS needed |
| **Chroma** `chroma-core/chroma` | `chromadb.PersistentClient(path="/path")` → `chroma.sqlite3` | Rejected for production — docs state `PersistentClient is intended for local dev/testing. For production prefer server` |
| **Sqlite-vec** | SQLite extension | Fallback for minimal footprint if Qdrant native dep is an issue |

**Rationale**: Single-tenant needs no `company_id` partition; a single Qdrant file per workstation is simplest to backup/ship. Chroma caveat makes it unsuitable for SC-003 scale. Qdrant local satisfies `plan.md:Storage` without Docker/server.

### 2. Embeddings — Local, No Cloud

| Candidate | Pattern | Decision |
|-----------|---------|----------|
| **Ollama** `ollama/ollama` `nomic-embed-text` / `all-MiniLM` | `POST /api/embed {model, input}` → `embeddings[][]` — verified `ollama: POST /api/embed` | **Selected for workstation** — one Ollama binary serves both embed and chat, simplest ops |
| **ONNX bge-micro-v2** `huggingface` via `Microsoft.ML.OnnxRuntime` | `bge-micro-v2/onnx/model.onnx` + `vocab.txt` — verified `semantic-kernel: git clone huggingface.co/TaylorAI/bge-micro-v2` | **Approved alternative** — 80MB, best for .NET-only without Ollama daemon |
| **LLamaSharp** `scisharp/llamasharp` | `new ModelParams(modelPath){EmbeddingMode=true}` → `LLamaEmbedder(weights).GetEmbeddings(text)` — verified `LLamaSharp: Get Text Embeddings` | **Selected** if GGUF-only (no Ollama); dims model-dependent, not normalized by default |

**Rationale**: Ollama unifies embed+LLM management; ONNX is fallback for pure .NET offline without daemon. All are WAN-free after first cache.

### 3. LLM — Local Inference on Workstation Only

| Candidate | Pattern | Decision |
|-----------|---------|----------|
| **Ollama** `llama3.2:3b-q4` / `phi-3-mini` / `mistral:7b-q4` | `ollama run` + `POST /api/chat` | **Selected** — easiest cross-platform, OpenAI-compatible, `keep_alive=5m` |
| **LLamaSharp** GGUF | `LLamaWeights.LoadFromFile` + streaming `LLamaExecutor` — needs GGUF `scisharp/llamasharp: Model preparation` | **Approved** — minimal C++ via `llama.cpp`, best for quantized CPU, used directly if Ollama is too heavy |
| **ONNX Phi-3-mini-4k-instruct-onnx** `Microsoft` | `phi-3-mini-4k-instruct-onnx/cpu-int4` — verified `semantic-kernel: OnnxSimpleRAG` sample | **Approved** — pure ONNX, 3.8B int4, fits `Semantic Kernel` sample exactly |

**Rationale**: Desktop MUST NOT run LLM (Constitution II). Workstation GPU/CPU runs one of the above; Ollama wraps `llama.cpp` so LLamaSharp is the escape hatch. Target `SC-002` p95 <7s on 16GB workstation with quantized 3-4B.

### 4. RAG Orchestration

**Thin wrapper over Semantic Kernel**: direct `Qdrant search(filter=documentId?) → prompt template (system + chunks + query) → local LLM → {answer, citations}`. LangChain/LlamaIndex rejected — hide retrieval control and add Python deps. `Semantic Kernel` `OnnxSimpleRAG` sample `semantic-kernel: Configure ONNX Model Paths` shows the exact flow for .NET without cloud.

### 5. Client Framework (Desktop + Mobile)

| Candidate | Scope | Decision |
|-----------|-------|----------|
| **.NET MAUI (.NET 8)** | Windows 11 desktop + iOS + Android from one codebase | **Selected** — matches Windows 11 modern desktop + mobile requirement; WinUI 3/Fluent rendering on Windows; HttpClient-only, no AI deps |
| **WPF (.NET 8)** | Windows-only desktop | Approved alternative — fastest Windows desktop, but no mobile; rejected for v1 because mobile is required |
| **WinUI 3 / Windows App SDK** | Windows-only desktop | Approved — modern Windows UI, but no mobile; rejected for v1 because mobile is required |
| **WinForms (.NET 8)** | Windows-only desktop | Rejected — legacy, not suitable for modern RAGGit client |
| **Avalonia UI 11** | Windows/macOS/Linux desktop | Rejected for v1 — supports desktop but mobile path (Avalonia XPF/iOS/Android) is less mature than MAUI and adds Linux/macOS targets out of scope |
| **Uno Platform** | Cross-platform desktop + mobile | Rejected for v1 — heavier than MAUI for a single shared codebase; WinUI-compatible abstraction adds complexity |

**Rationale**: MAUI is the only Microsoft-supported path that covers Windows 11 modern desktop (WinUI 3/Fluent via `net8.0-windows10.0.19041.0`) and iOS/Android mobile from a single .NET 8 codebase while keeping the client `HttpClient`-only and free of workstation AI dependencies.

## Alternatives Considered

- Single project bundling AI into desktop — rejected: violates Constitution II (workstation-owned AI) and needs 8GB per employee.
- Cloud vector DB (Pinecone/Qdrant Cloud) — rejected: fails offline invariant `FR-004`/`SC-002`.
- Python FastAPI for workstation — viable but rejected for this team: `.NET/C#` skill per user request; ASP.NET Core achieves same `Qdrant path` + `Ollama` via `OllamaSharp`.

## Open Items Resolved

- FR-010 formats → PDF/docx/txt/md only (video/audio out of scope) — no transcription pipeline needed.
- FR-011 scale → 5k docs / ~1M chunks, 512/50, topK=5 — sizes HNSW `m=16 ef=128`, payload index on `documentId`.
- Single-tenant confirmed — no `company_id` partition, no `SC-004` cross-tenant audit.

## References

- `QdrantClient:55` `path` persistent local storage
- `Ollama: POST /api/embed` embed endpoint
- `LLamaSharp: GetEmbeddings.md:15` EmbeddingMode
- `Semantic Kernel: OnnxSimpleRAG/README.md:25` ONNX chat + bge-micro-v2 local
- `.NET MAUI docs` cross-platform UI for Windows, iOS, Android
