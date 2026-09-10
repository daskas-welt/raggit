# Data Model: RAGGit Offline-Mode

**Feature**: `001-offline-mode` | **Date**: 2026-08-31 | **Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

## Entities

### Library (Singleton)

Single company library per deployment. Not a multi-tenant table — one row (`id=1`) or no table (implicit).
- `Id` (int, singleton=1, PK)
- `Name` (string, e.g., "RAGGit Library")
- `CreatedAt` (DateTime, UTC)

*Constraints*: One per workstation install. No `CompanyId`. Deleting library is out of scope (clear via `DELETE /api/documents/*`).

### Document

File uploaded by Admin, source for chunks.
- `Id` (Guid, PK)
- `Filename` (string, indexed)
- `Mime` (string, enum: `application/pdf`, `application/vnd.openxmlformats-officedocument.wordprocessingml.document`, `text/plain`, `text/markdown`)
- `Size` (long, bytes; reject >100MB per FR-006)
- `Hash` (string, SHA-256 hex, unique index for dedupe)
- `Status` (enum: `Indexing` → `Ready` | `Failed`)
- `CreatedBy` (string, userId/API key)
- `CreatedAt` (DateTime, UTC)

*Relationships*: `Library 1:N Document` (implicit singleton). `Document 1:N Chunk` (composition). *SQLite table*: `Documents`.

### Chunk

Text segment derived from Document, unit for embedding.
- `Id` (Guid, PK; also LanceDB row `id`)
- `DocumentId` (Guid, FK → Document, indexed)
- `Ordinal` (int, 0-based order in document)
- `Text` (string, 512 tokens max, 50 overlap per FR-011)
- `TokenCount` (int)

*Constraints*: `Text` not empty, `Ordinal` unique per `DocumentId`. Stored in SQLite for audit and in LanceDB `text` column for citations. *SQLite table*: `Chunks`.

### Embedding (in LanceDB)

Vector for a Chunk; owned by LanceDB, not a separate SQLite row (SQLite holds Chunk; LanceDB holds vector+columns).
- `Id` (Guid = `Chunk.Id`, LanceDB row id)
- `Vector` (float[], dim 384 for `all-MiniLM`/`bge-micro-v2` or 768 for `nomic-embed-text`; vector index)
- `Columns`: `{ documentId, text, ordinal }`
- `ModelName` (string in metadata, e.g., `nomic-embed-text:768` or `bge-micro-v2:384`)

*LanceDB table*: `library` with HNSW `m=16, efConstruction=128`, scalar filter on `documentId`. No `company_id` (single-tenant).

### Query

Employee natural-language request and grounded response.
- `Id` (Guid, PK)
- `UserId` (string, from auth)
- `Prompt` (string, not empty)
- `RetrievedChunkIds` (Guid[], JSON in SQLite — topK=5)
- `Answer` (string, nullable until generated)
- `CitationIds` (Guid[], subset of Retrieved, JSON)
- `LatencyMs` (int)
- `CreatedAt` (DateTime, UTC)

*SQLite table*: `Queries`. Not stored in LanceDB. Used for eval `SC-003/004` and audit.

## Relationships Overview

```text
Library (1) ──< Document (N) ──< Chunk (N) ── Embedding (1:1 via Chunk.Id in LanceDB)
                                    │
                                    └─> Query.RetrievedChunkIds (references Chunks)
Query.CitationIds ⊆ RetrievedChunkIds
```

## State Transitions

- `Document.Status`: `Indexing` → `Ready` (success) | `Failed` (unsupported/corrupted, FR-006). Failed rows remain for error message.
- No deletion state machine — `DELETE /api/documents/{id}` removes `Document` + `Chunk` rows + LanceDB rows `filter: documentId==id`; subsequent queries exclude deleted chunks.

## Validation

- `Filename` not empty, `Mime` in allowed list (FR-010), `Size ≤100MB`, `Hash` unique (dedupe per Edge Cases), `Prompt` not empty, `RetrievedChunkIds` length ≤5.

## Storage Mapping

- SQLite `rag.db`: `Documents`, `Chunks`, `Queries`, `Library` (1 row).
- LanceDB file `data/lancedb` table `library`: row `id=Chunk.Id`, `vector`, columns `{ documentId, text, ordinal }`.
