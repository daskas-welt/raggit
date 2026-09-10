# RAGGit Architecture — 001-offline-mode (MAUI v1.1.0)

**Constitution:** `v1.1.0` • Single-tenant LAN-only • Workstation-Owned AI • MAUI Win11 + iOS/Android

## C4 Context (Single Deployment = 1 Company)

```mermaid
flowchart TB
    Admin["Admin (MAUI Client)"]
    Employee["Employee (MAUI Client)"]
    Client["RAGGit.Client.Maui<br/>net8.0-windows / ios / android<br/>HttpClient + MVVM (no models)"]
    WS["AI Workstation<br/>ASP.NET Core 8 API<br/>http://ai-workstation.local:5001"]
    LAN{{"LAN / Site VPN<br/>No WAN at query time (IV)"}}
    Admin --> Client
    Employee --> Client
    Client <--> LAN <--> WS
```

## Containers

```mermaid
flowchart TB
    subgraph ClientMaui["RAGGit.Client.Maui (thin, stateless)"]
        MVVM["ViewModels<br/>LibraryViewModel, QueryViewModel"]
        Views["Views<br/>LibraryView, UploadView, QueryView<br/>citations list"]
        Http["HttpClient<br/>X-Api-Key: Admin/Employee"]
    end

    subgraph Workstation["AI Workstation (owns all AI)"]
        API["RAGGit.Workstation.Api<br/>Controllers: Documents, Query, Health<br/>Auth: ApiKey (Admin/Employee)"]
        Core["RAGGit.Core (plain net8.0)<br/>Models: Document, Chunk, Query, Library<br/>Abstractions: IVectorStore, IEmbedder, ILlmClient"]
        Ingest["RAGGit.Ingest<br/>Chunker (512/50 PdfPig/OpenXML)<br/>OllamaEmbedder / OnnxEmbedder<br/>LanceDbLocalClient path=./data/lancedb"]
        Retrieval["RAGGit.Retrieval<br/>RetrievalService (topK=5)<br/>GenerationService (prompt+LLM)"]
        DB[("SQLite rag.db<br/>Documents, Chunks, Queries, Library(1)")]
        LanceDb[("LanceDB file ./data/lancedb<br/>table 'library' HNSW m=16")]
        Ollama[["Ollama :11434<br/>nomic-embed-text / llama3.2:3b<br/>or LLamaSharp GGUF / ONNX bge-micro-v2"]]
    end

    MVVM --> Views --> Http
    Http -. "POST /api/documents (multipart, 201 or 200 dedupe)<br/>GET /api/documents<br/>POST /api/query {answer, citations}<br/>DELETE /api/documents/{id}<br/>GET /health (anon)" .-> API
    API --> Core
    API --> Ingest
    API --> Retrieval
    Ingest --> DB
    Ingest --> LanceDb
    Ingest --> Ollama
    Retrieval --> LanceDb
    Retrieval --> Ollama
    Retrieval --> DB
```

## Query Sequence (WAN-OFF, LAN-only)

```mermaid
sequenceDiagram
    participant C as MAUI Client
    participant A as API (Workstation)
    participant E as IEmbedder (Ingest)
    participant V as IVectorStore (LanceDB file)
    participant L as ILlmClient (Ollama/ONNX)
    participant D as SQLite

    C->>A: POST /api/query {query, topK=5} (X-Api-Key)
    A->>E: GetEmbeddings(query)
    E-->>A: vector[384/768]
    A->>V: Search(limit=5, filter: - )
    V-->>A: top chunks [{docId, chunkId, text, ordinal}]
    alt no hits
        A-->>C: 200 {answer: "no relevant content found", citations: []}
    else hits
        A->>L: Chat(system+chunks+query)
        L-->>A: answer + citationIds
        A->>D: INSERT Query {latencyMs, retrievedChunkIds}
        A-->>C: 200 {answer, citations:[...], retrievedChunkIds, latencyMs}
    end
```

## Ingest Sequence (Admin Upload <100MB)

```mermaid
sequenceDiagram
    participant C as MAUI UploadView
    participant A as DocumentsController
    participant K as Chunker (512/50)
    participant E as IEmbedder
    participant V as LanceDB file
    participant D as SQLite

    C->>A: POST /api/documents multipart file (Admin)
    A->>A: mime ∈ {pdf,docx,txt,md} else 400<br/>size >100MB 413<br/>SHA-256 dedupe → if exists 200 existing
    A->>D: INSERT Document {Status=Indexing}
    A->>K: PdfPig/OpenXML → 512/50 → Chunks[]
    K-->>A: chunks
    A->>E: GetEmbeddings batch
    E-->>A: vectors
    A->>V: Upsert points id=Chunk.Id payload={docId,text,ordinal}
    A->>D: INSERT Chunks + UPDATE Document Status=Ready
    A-->>C: 201 (or 200 dedupe) Document
```

## Dependency Direction (Constitution II / III)

```
RAGGit.Client.Maui → RAGGit.Core (plain net8.0, no AI deps)
RAGGit.Workstation.Api → RAGGit.Core + RAGGit.Ingest + RAGGit.Retrieval
RAGGit.Ingest → RAGGit.Core (IVectorStore, IEmbedder)
RAGGit.Retrieval → RAGGit.Core (ILlmClient, IVectorStore via DI)
Core NEVER → Ingest/Retrieval
```

## Deployment (One-Tenant-Per-Workstation)

- Workstation: Win10+/Linux, 16GB+GPU, 10GB disk, `LanceDB path=./data/lancedb` + `rag.db` persisted, models pre-cached (no `ollama pull` at query)
- Client: MAUI `net8.0-windows10.0.19041.0` (MSIX signed) + `net8.0-android` (sideload) + `net8.0-ios` (Ad-Hoc/enterprise, Mac host) — all HttpClient-only over LAN/VPN
- Offline invariant: CI gate `T044` runs `QueryOfflineTests` WAN-disabled; `/health` anon

## File Map

`plan.md:60-70` • `spec.md:99-103` entities • `contracts/api.yaml:5` • `tasks.md:20-23` TFMs
