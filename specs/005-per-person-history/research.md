# Research: 005-per-person-history

**Feature**: Per-Person Query & Document History — `specs/005-per-person-history/spec.md` (5 stories, 12 FRs, 5 SCs)

## R1 — Stable pagination key for history

**Decision**: `ORDER BY CreatedAt DESC, Id DESC` with `LIMIT @limit OFFSET @offset` plus optional `Id` tiebreaker; expose `total` via `SELECT COUNT(*) WHERE UserId == @sub AND UserId NOT IN ('admin','employee')`. History summary query projects `PromptPreview`, `AnswerPreview` (`SUBSTR` 120 chars), `CitationCount`, `LatencyMs`; detail query fetches full fields + `JOIN QueryCitations`.

**Rationale**: `CreatedAt` from `QUERIES` is `TEXT ISO-8601` sortable, but multiple queries in same millisecond need deterministic tiebreaker — `Id` (UUID string) suffices and is already indexed; `OFFSET` is O(total) but `1k` scale is `<2s` per SC-001 on SQLite (validated on `004` `Queries` `Index IX_Queries_UserId`). No cursor needed for v1.

**Alternatives considered**: Keyset cursor (`WHERE (CreatedAt, Id) < (?,?)` → faster for large offsets, more client state) — deferred; `ROW_NUMBER()` window — unnecessary complexity; `AUTOINCREMENT` key — would require schema change.

## R2 — Legacy ApiKey rows exclusion

**Decision**: All per-person reads add `AND UserId NOT IN ('admin','employee')` (queries) and equivalent for `CreatedBy`. No `WHERE UserId = @sub` alone is sufficient because `@sub` is UUID, but explicit exclusion guarantees zero legacy leakage even if a UUID collides with literal (defense-in-depth) and documents the invariant for SC-005.

**Rationale**: `004-identity` left legacy rows in place; spec `Edge Cases` requires invisibility. Filter at DB layer, not application, so contract tests can assert `NOT IN` appears in generated SQL if needed. Shared-key sessions continue to work via `GET /api/query` without history.

**Alternatives considered**: Data migration to delete/renumber legacy rows — rejected (audit value, migration risk); soft flag column — would require schema change, rejected for VII simplicity.

## R3 — History item preview truncation

**Decision**: API returns both summary (`PromptPreview`/`AnswerPreview` ≤120 chars `…` if truncated) and detail (`Full`). Truncation done in SQL: `CASE WHEN length(Prompt) > 120 THEN substr(Prompt,1,120) || '…' ELSE Prompt END`. Citations not returned in list, only `CitationCount` (`COUNT QueryCitations` or stored count).

**Rationale**: `SfListView` row height bounded, `1k` list stays fast, no extra round-trip; matches FR-003. Detail endpoint returns full text + citations per FR-004.

**Alternatives considered**: Client-side truncation — shifts work to MAUI, larger payload; server never-preview — larger LAN payload for 50-item page but still acceptable; 120 chosen to balance mobile line-wrap vs readability.

## R4 — Endpoint naming and contract additive strategy

**Decision**: Additive MINOR `1.3.0 → 1.4.0`:
- `GET /api/queries/history?limit=&offset=` → `{ items:[{id,promptPreview,answerPreview,citationCount,latencyMs,createdAt}], total, limit, offset }` — authenticated required (sub-scoped)
- `GET /api/queries/{id}` → `{ id,prompt,answer,citations:[{documentId,chunkId,text,ordinal}], latencyMs, createdAt }` — `404` if not owned or not found, `401` if unauth
- `GET /api/documents/mine?limit=&offset=` → `{ items:[{id,filename,size,status,createdAt}], total, limit, offset }` — disambiguates from `GET /api/documents` (all docs Admin view from 001, still exists)

**Rationale**: `GET /api/queries/history` keeps existing `POST /api/query` and `GET /api/queries/{id}`-like future from colliding; `/mine` suffix follows common MAUI REST idiom for personal scope; all additive, no breaking, respects FR-011.

**Alternatives considered**: `GET /api/history` — too generic, overlaps with future global history; `GET /api/users/me/queries` — REST purist but nests under users, less discoverable for MAUI; `GET /api/queries?mine=true` — query flag would require filter flag validation.

## R5 — Indices and performance for 1k scale

**Decision**: Reuse existing `CREATE INDEX IX_Queries_UserId ON Queries(UserId)` (from `004`? if absent, add) + composite `IX_Queries_UserId_CreatedAt_Id (UserId, CreatedAt DESC, Id DESC)` if needed after benchmark; `Documents` already has `IX_Documents_CreatedBy`. Measure with `EXPLAIN QUERY PLAN` before adding.

**Rationale**: SQLite `EXPLAIN` on `SELECT … WHERE UserId=? AND UserId NOT IN (…) ORDER BY CreatedAt DESC, Id DESC LIMIT 20 OFFSET 0` shows `USING INDEX` when composite exists; for `1k` rows SC-001 `<2s` trivially satisfied even without composite (<50ms typical). Add only if `EXPLAIN` shows `SCAN TABLE`.

**Alternatives considered**: Covering index including preview columns — overkill for 20-row page; Full-text index — not needed (no search in v1).

## R6 — Offline invariant implementation

**Decision**: History reads are pure `Microsoft.Data.Sqlite` queries against `rag.db`; no call to `Ollama`, `LanceDB`, or internet. WAN-disabled CI leg uses same `unshare -n` fallback as `004` `OfflineIdentityTests`: `OfflineHistoryTests` runs `unshare -n dotnet test --filter OfflineHistoryTests` with fallback probe.

**Rationale**: Constitution IV NON-NEGOTIABLE applies equally to reads; LAN-only SQLite satisfies `SC-004`. No extra offline code path needed.

**Alternatives considered**: Cache history in MAUI `SecureStorage` — would diverge from DB truth, rejected.

## R7 — Client history UI (SfListView)

**Decision**: `HistoryView.xaml` uses `SfListView` (Syncfusion) with `PullToRefresh` + `LoadMore` bound to `offset`, `EmptyView` bound to zero total, `ItemTemplate` shows preview + `citationCount`; detail navigation pushes `QueryDetailView` with `Label` for answer + `SfListView` for citations.

**Rationale**: Already licensed `Syncfusion.Maui.ListView 28.2.7` (from `003`), `Toolkit` for `PullToRefresh`; `MAUI` fallback `net8.0` headless does not render but `HistoryViewModel` is testable.

**Alternatives considered**: `CollectionView` — would require style divergence; `SfDataGrid` — heavier for mobile feed.

## Summary

All unknowns resolved, no `NEEDS CLARIFICATION` remains. No new packages, no new tables, no schema migration; additive contract `1.4.0`; offline + isolation covered by existing `004` primitives.
