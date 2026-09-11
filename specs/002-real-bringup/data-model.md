# Data Model: Real Workstation Bring-Up

**Feature**: `002-real-bringup` | **Date**: 2026-09-11 | **Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Delta on**: [001 data-model.md](../001-offline-mode/data-model.md)

## Overview

This is a delta on `001-offline-mode/data-model.md`. No data-model version bump beyond the enforced LanceDB dimension; SQLite schema unchanged.

## Entities — changes from 001

### Library (Singleton) — unchanged

One company library per deployment. Not a multi-tenant table — one row (`id=1`) or implicit. No `CompanyId`. No schema change.

### Document — unchanged (hardening note)

Same fields: `Id` (Guid PK), `Filename`, `Mime` (`application/pdf`, `application/vnd.openxmlformats-officedocument.wordprocessingml.document`, `text/plain`, `text/markdown`), `Size` (reject >100MB), `Hash` (SHA-256 hex unique), `Status` (`Indexing` → `Ready`|`Failed`), `CreatedBy`, `CreatedAt`. Relationship `Library 1:N Document 1:N Chunk`. SQLite table `Documents`.

**Delta in this feature (FR-006)**: Corrupted/invalid `pdf`/`docx` returns `400 BadRequest` with no partial row and no vectors retained. Parser throws → catch in `DocumentsController` → 400 + delete any pre-inserted `Documents`/`Chunks` rows + skip `UpsertAsync`.

### Chunk — unchanged

`Id` (Guid PK = LanceDB row `id`), `DocumentId` (FK indexed), `Ordinal` (0-based unique per Document), `Text` (512 tokens max, 50 overlap), `TokenCount`. SQLite table `Chunks`.

### Embedding (in LanceDB) — now enforced

Vector for a Chunk; owned by LanceDB table `library` with HNSW `m=16, efConstruction=128`, cosine, scalar filter on `documentId`.

- `Id` (Guid = `Chunk.Id`)
- `Vector` (float[], **enforced dim 384 or 768**; was informational in 001, now validated)
- `Columns`: `{ documentId, text, ordinal }`
- `ModelName` (metadata string, e.g., `all-minilm:384` or `nomic-embed-text:768`)

**Enforcement (FR-001/SC-004/Q3)**:

- `VectorDb:VectorSize` (384|768) must equal the Arrow `FixedSizeList` size of the persisted `library` column `vector`. Guard at startup (normative) + first-request backstop for lazy table.
- Mismatch → startup fail-fast: `Configured VectorSize {configured} does not match existing collection dimension {stored} — delete data/lancedb or re-index/migrate` (message names both dims and recovery). `/health` then `vectorDb: down` when backstop fires.
- `Ollama:EmbedModel` must align: `all-minilm` ↔ 384, `nomic-embed-text` ↔ 768 (dev vs prod).
- Legacy `Qdrant:Path` (`./data/qdrant`) is not a source of truth; if it exists and disagrees with `VectorDb:Path`, startup log Warning.

*LanceDB table*: `library` row `id=Chunk.Id`, `vector FixedSizeList<float, VectorSize>`, columns `{ documentId, text, ordinal }`. No `company_id`.

### Query — unchanged

`Id` (Guid PK), `UserId` (string from auth), `Prompt` (not empty), `RetrievedChunkIds` (Guid[] JSON, topK=5), `Answer` (nullable), `CitationIds` (Guid[] subset), `LatencyMs`, `CreatedAt`. SQLite table `Queries`.

### ClientSession — new, client-memory only

Held by the thin MAUI client to gate UI; **not persisted** in SQLite or LanceDB.

- `WorkstationUrl` (string, required, from `Workstation:Url` or fallback `WorkstationUrl`; validated at launch)
- `ApiKey` (string, required, from `Workstation:ApiKey` or `Api:AdminKey`/`Api:EmployeeKey`; validated at launch)
- `IdentityType` (string, extensible, e.g., `ApiKey`) + `Role` (enum `Admin`|`Employee`) — discovered via `GET /api/auth/me` 200 `{identityType, role}` per Q2; client ignores unknown fields (forward-compat for 004)
- Derived: `IsAdmin` (bool, `Role == Admin`) drives `Upload`/`Delete` visibility

*Lifecycle*: Constructed in `MauiProgram` DI at launch; refreshed on `GET /api/auth/me`; on 401 → config error; on workstation unreachable → `AI workstation unavailable` / `cannot reach AI workstation` within timeout, no cloud fallback (FR-007).

## Relationships Overview — unchanged

```text
Library (1) ──< Document (N) ──< Chunk (N) ── Embedding (1:1 via Chunk.Id in LanceDB library)
                                     │
                                     └─> Query.RetrievedChunkIds (references Chunks)
Query.CitationIds ⊆ RetrievedChunkIds
ClientSession (memory) ──(GET /api/auth/me)──> Role (gates Document upload/delete)
```

## State Transitions — unchanged

- `Document.Status`: `Indexing` → `Ready` | `Failed` (Failed remains for error message).
- `DELETE /api/documents/{id}` removes `Document` + `Chunk` rows + LanceDB rows `filter: documentId==id`.

## Validation — delta

- `Filename` not empty, `Mime` in allowed list, `Size ≤100MB`, `Hash` unique, `Prompt` not empty, `RetrievedChunkIds` length ≤5 — same as 001.
- **New**: `VectorDb:VectorSize` ∈ {384, 768}; must equal persisted `library.vector` dim (enforced).
- **New**: `Ollama:EmbedModel` ↔ `VectorSize` alignment warning if mismatched (e.g., `all-minilm` with 768).
- **New**: Corrupted `pdf`/`docx` → 400, transaction rolled back, no orphan chunks/vectors.

## Storage Mapping — unchanged except enforcement

- SQLite `rag.db`: `Documents`, `Chunks`, `Queries`, `Library` (1 row).
- LanceDB file `data/lancedb` (VectorDb:Path) table `library`: row `id=Chunk.Id`, `vector`, columns `{ documentId, text, ordinal }` — dimension now enforced.

