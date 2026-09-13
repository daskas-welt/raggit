# Data Model: 005-per-person-history

**Feature**: Per-Person Query & Document History — query-layer only, no new tables

## Overview

This feature is a **read-projection** over data already attributed by `004-identity`:
- `Queries` carries `UserId` (`TEXT`, UUID `sub` for person sessions, or `"admin"/"employee"` legacy)
- `Documents` carries `CreatedBy` (`TEXT`, same domain)

No `ALTER TABLE`, no `CREATE TABLE`, no migration. The new logic is two stores: `QueryHistoryStore` (queries) and `DocumentMineStore` (documents), each filtering by the caller's `sub` and excluding legacy literals.

## Existing Tables (read-only for this feature)

### `Users` (from 004 — owner, not written here)

| Column | Type | Constraints |
|--------|------|-------------|
| Id | TEXT PK | UUID |
| Username | TEXT | UNIQUE COLLATE NOCASE |
| DisplayName | TEXT |  |
| PasswordHash | TEXT | `PBKDF2-SHA256$310000$…` self-describing |
| Role | TEXT | `CHECK Role IN ('Admin','Employee')` |
| IsActive | INTEGER | 0/1 |
| FailedAccessCount, LockoutUntil, MustChangePassword, CreatedAt, LastLoginAt | — | |

### `Documents` (from 001/002, last touch 003 xlsx)

| Column | Type | Used by this feature |
|--------|------|----------------------|
| Id | TEXT PK | ✅ returned as `id` |
| FileName | TEXT | ✅ `filename` |
| FileSize | INTEGER | ✅ `size` |
| Status | TEXT | ✅ `status` (`Ready`, etc.) |
| CreatedBy | TEXT | ✅ filter `CreatedBy == @sub` AND `NOT IN ('admin','employee')` |
| CreatedAt | TEXT ISO-8601 | ✅ sort + `createdAt` |
| ContentType, StoragePath, … | — | not returned |

Indices: `IX_Documents_CreatedBy` (reused)

### `Queries` (from 001/002)

| Column | Type | Used by this feature |
|--------|------|----------------------|
| Id | TEXT PK | ✅ `id`, tiebreaker |
| UserId | TEXT | ✅ filter `UserId == @sub` AND `NOT IN ('admin','employee')` |
| Prompt | TEXT | ✅ list preview + detail full |
| Answer | TEXT | ✅ list preview + detail full |
| CreatedAt | TEXT ISO-8601 | ✅ `ORDER BY CreatedAt DESC, Id DESC` + `createdAt` |
| LatencyMs | INTEGER | ✅ `latencyMs` |
| CitationCount | INTEGER | ✅ `citationCount` (derived or stored) |

Related: `QueryCitations` (from 001) — `{ QueryId FK, DocumentId, ChunkId, Text, Ordinal }` joined only in detail path.

Indices: `IX_Queries_UserId` (ensure exists); proposed `IX_Queries_UserId_CreatedAt_Id (UserId, CreatedAt DESC, Id DESC)` if `EXPLAIN` shows scan (see R5).

## Derived Projections (not tables)

### QueryHistory Summary

**Shape** (one row per filtered `Queries` row):
- `Id: string` (uuid) — PK, not previewed
- `PromptPreview: string` — `length(Prompt)>120 ? substr(Prompt,1,120)+'…' : Prompt`
- `AnswerPreview: string` — same 120-char truncation, `…` if `no relevant content found` is not truncated (already short)
- `CitationCount: int` — `SELECT COUNT(*) FROM QueryCitations WHERE QueryId = Queries.Id` or cached column
- `LatencyMs: int`
- `CreatedAt: string ISO-8601`

**Query**: `SELECT … WHERE UserId = @sub AND UserId NOT IN ('admin','employee') ORDER BY CreatedAt DESC, Id DESC LIMIT @limit OFFSET @offset`  
**Count**: `SELECT COUNT(*) WHERE UserId = @sub AND UserId NOT IN ('admin','employee')` → `total`

**Validation**:
- `limit` clamped `1..100` default `20`, `offset` `≥0` default `0`
- Caller must have `sub` claim; otherwise `401`
- `total` reflects filtered set only

### Query Detail

**Shape**:
- `Id, Prompt, Answer (full, not truncated), LatencyMs, CreatedAt`
- `Citations: [{ documentId, chunkId, text, ordinal }] ORDER BY ordinal`

**Query**: `SELECT … FROM Queries WHERE Id=@id AND UserId=@sub AND UserId NOT IN ('admin','employee')` → `404` if not found/owned; `JOIN QueryCitations` for citations; `401` if unauth.

### Recent Documents (mine)

**Shape** (one row per filtered `Documents` row):
- `Id, Filename, Size (FileSize), Status, CreatedAt`

**Query**: `SELECT … WHERE CreatedBy = @sub AND CreatedBy NOT IN ('admin','employee') ORDER BY CreatedAt DESC, Id DESC LIMIT @limit OFFSET @offset`  
**Count**: same `WHERE` → `total`

**Validation**: same `limit/offset` clamping as history; `401` if unauth; `0` rows for new user is `200` with empty `items`.

## Pagination Envelope

```json
{
  "items": [ /* per projection */ ],
  "total": 42,
  "limit": 20,
  "offset": 0
}
```

- `total` is count before paging
- `offset >= total` → `items:[]` but `total` still correct
- Stable ordering guaranteed by `CreatedAt DESC, Id DESC`
- `limit`/`offset` always echoed back clamped

## Relationships

- `Queries.UserId` → `Users.Id` (logical FK, not enforced, UUID when from 004, literal when legacy) — history reads join not needed
- `Documents.CreatedBy` → `Users.Id` (same)
- No new FK or relationship for this feature

## State Transitions

No state machine for history (read-only). Underlying `Users.IsActive` and JWT expiry affect visibility via `OnTokenValidated`/`401` (existing from 004).

## DDL Impact

**None.** If `EXPLAIN QUERY PLAN` shows scan on `Queries` with `WHERE UserId`, optionally:

```sql
CREATE INDEX IF NOT EXISTS IX_Queries_UserId_CreatedAt_Id
ON Queries(UserId, CreatedAt DESC, Id DESC);
```

This is **optional**, additive, and not required for correctness; include only after benchmark shows `SCAN TABLE Queries`.

## Compatibility

- Existing API-key callers keep using `POST /api/query` and see `UserId="admin"/"employee"` rows only via direct DB, not via the new per-person endpoints (excluded by `NOT IN`).
- MAUI History screen reads only the new `/history` and `/{id}` endpoints; old clients ignore them (`1.4.0` additive).
