# Phase 1 Data Model: Intent-Aware Chunking

**Feature**: 030-intent-aware-chunking | **Date**: 2026-10-07 | **Plan**: [plan.md](plan.md)

Storage is unchanged in kind — SQLite `rag.db` for metadata and chunks, LanceDB `./data/lancedb` for
child vectors. This feature adds **structure** to the existing `Chunk` record (two levels) and a
nullable mode on the request/response. `VectorDb:VectorSize` stays 1024 (this is a chunk-size
change, not a vector-dimension change).

---

## Entity overview

```text
Document 1 ──────── * Chunk (Level = Child)   ← embedded, searched
    │                     │
    │                     └── ParentId ──┐  (self-reference)
    └────── * Chunk (Level = Parent) ←───┘  ← NOT embedded, context + citation
```

- A **Document** owns all of its chunks (unchanged aggregate boundary: chunk access is only through
  `IDocumentRepository`).
- A **child chunk** points at the **parent chunk** that groups it via `ParentId`.
- A **parent chunk** has `ParentId = null` and is the group of `ParentGroupSize` consecutive
  children.
- Only child chunks have vectors in LanceDB; the vector payload carries `parentId` so a child hit
  maps to its parent.

---

## Entity: `Document` (unchanged)

No change. For reference: `Id` (Guid), `Filename`, `Mime` (`DocumentMimeType`), `Size`, `Hash`
(SHA-256, unique), `Status` (`DocumentStatus`), `CreatedBy`, `CreatedByName?`, `FailureReason?`,
`CreatedAt`.

**Lifecycle (unchanged, retained by FR-011)**: `Uploading → Queued → Indexing → Ready | Failed`
(`Failed` carries a user-safe `FailureReason`; re-upload re-stages a `Failed` document).

---

## Entity: `Chunk` (extended)

| Field | Type | Change | Notes |
|-------|------|--------|-------|
| `Id` | `Guid` | — | Stable; a citation id (child for granular, parent for broad) |
| `DocumentId` | `Guid` | — | Owning document |
| `Ordinal` | `int` | — | Child: position among children. Parent: its group index (0-based) |
| `Text` | `string` | — | Child: ~512-token segment. Parent: ordered concatenation of its children with overlap trimmed |
| `TokenCount` | `int` | — | Approximate whitespace token count |
| `Level` | `ChunkLevel` | **NEW** | `Child` (0) or `Parent` (1) |
| `ParentId` | `Guid?` | **NEW** | Child ⇒ the owning parent's `Id`; Parent ⇒ `null` |

**Validation rules**

- A `Child` row MUST have a non-null `ParentId` that refers to a `Parent` row in the same document
  (FR-010).
- A `Parent` row MUST have `ParentId = null`.
- Parent text MUST NOT duplicate the child overlap (each child after the first contributes its
  `min(ChunkOverlap, childTokens-1)` non-overlapping suffix tokens).
- A document MUST contain at least one `Parent` iff it contains children (empty documents stay
  empty — the existing no-extractable-content path is unchanged).

**New enum — `ChunkLevel`**

```csharp
public enum ChunkLevel { Child = 0, Parent = 1 }
```

---

## Entity: `SearchResult` (extended)

Add `ParentId` (`string?`) sourced from the vector payload. `ChunkId`, `DocumentId`, `Text`,
`Ordinal`, `Score` are unchanged. For granular retrievals `ParentId` is informational; for broad
retrievals the service resolves parents through it.

---

## Entity: `VectorRecord` (unchanged shape)

`Id`, `Vector`, `Payload`. The ingest payload gains a `"parentId"` key alongside the existing
`"documentId"`, `"text"`, `"ordinal"`. Only `Child` vectors are written.

---

## Entity: `Query` / request / response (extended)

| Field | Type | Where | Change |
|-------|------|-------|--------|
| `Mode` | `QueryMode` (`Auto`\|`Broad`\|`Specific`) | `QueryRequest` | **NEW**, optional, default `Auto` |
| `Mode` | `QueryIntent` (`Broad`\|`Granular`) | `QueryResponse` | **NEW**, optional; the effective intent actually used |
| `Mode` | `string?` (`"Broad"`\|`"Granular"`) | `Query` (persisted) | **NEW**, nullable column, for history/audit |

**New enums — `QueryMode` and `QueryIntent`**

```csharp
public enum QueryMode   { Auto = 0, Broad = 1, Specific = 2 } // user's request / override
public enum QueryIntent { Granular = 0, Broad = 1 }            // resolved classification
```

**Resolution rule**: `Specific` ⇒ `Granular`; `Broad` ⇒ `Broad`; `Auto` ⇒ classifier result
(default `Granular`).

---

## Entity: `Document` re-ingestion state (derived, not stored)

A `Ready` document is **two-level** when it has at least one `Chunk` row with `Level = Parent`.
The backfill selects `Ready` documents lacking such a row and re-enqueues them (see
[research.md](research.md) R5). This is derived on demand; no new column is added.

---

## SQLite schema changes

Applied in `RagDbContext` via the existing probe-then-`ALTER TABLE` pattern
(`EnsureDocumentColumnAsync` today), because SQLite has no portable `ADD COLUMN IF NOT EXISTS`:

```sql
-- Chunks
ALTER TABLE Chunks ADD COLUMN Level INTEGER NOT NULL DEFAULT 0;   -- 0=Child, 1=Parent
ALTER TABLE Chunks ADD COLUMN ParentId TEXT;                      -- child → parent Guid; parent → NULL

-- Queries
ALTER TABLE Queries ADD COLUMN Mode TEXT;                         -- "Broad" | "Granular" | NULL (legacy)
```

Fresh databases get the columns in `GetSchemaCommands()` directly. Indexes:

```sql
CREATE INDEX IF NOT EXISTS IX_Chunks_Document_Level ON Chunks (DocumentId, Level);
CREATE INDEX IF NOT EXISTS IX_Chunks_Parent          ON Chunks (ParentId);
```

The `IX_Chunks_Document_Level` index backs the backfill's "documents without a parent row" query;
`IX_Chunks_Parent` backs parent resolution during broad retrieval.

---

## Configuration additions

| Option | Default | Purpose |
|--------|---------|---------|
| `IngestOptions.ParentGroupSize` | `4` | Children grouped per parent (≈2048 tokens at 512/child) |
| `RetrievalOptions.BroadCandidateMultiplier` | `4` | Child candidates fetched for broad queries = `topK × multiplier` |
| `RetrievalOptions.MaxCandidates` | `50` | Upper bound on broad candidate fetch |

---

## Requirement traceability

| Entity / rule | Requirement |
|---------------|-------------|
| Two-level `Chunk` (`Level`, `ParentId`) | FR-010 |
| Parent text without overlap duplication | FR-002, SC-002 |
| Child-only embedding + `parentId` payload | FR-003, SC-001 |
| Parent citation for broad, child for granular | FR-004 |
| Unchanged "no relevant content found" path | FR-005 |
| Local classifier/grouping (no egress) | FR-006 |
| Optional `Mode`, Auto default | FR-007 |
| `Auto` ⇒ safe default `Granular` | FR-008 |
| Retained `topK` + `MinScore` | FR-009 |
| Idempotent re-ingest backfill | FR-011, SC-006 |
