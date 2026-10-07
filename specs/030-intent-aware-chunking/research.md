# Phase 0 Research: Intent-Aware Chunking

**Feature**: 030-intent-aware-chunking | **Date**: 2026-10-07 | **Plan**: [plan.md](plan.md)

All Technical Context unknowns are resolved below. The spec carried no `[NEEDS CLARIFICATION]`
markers (intent control and chunking model were settled during specification); the decisions here
shape *how* the confirmed behaviour is delivered.

---

## R1 — Two-level chunk representation (parent groups of children)

**Decision**: At ingest, keep the existing child chunking (`Chunker.ChunkText(text, doc, 512, 50)`)
and form **parent** units by grouping `IngestOptions.ParentGroupSize` (default **4**, ≈2048 tokens)
*consecutive* children. Parent text is the ordered concatenation of the group's child texts with
the known inter-child overlap trimmed. Each parent is persisted as its own `Chunk` row
(`Level = Parent`, `ParentId = null`) with a fresh `Guid`; each child carries `Level = Child` and
the parent's `Guid` in `ParentId`. **Only children are embedded** (choice 2b); the child vector
payload gains `parentId` so a child match maps to its parent without a re-embed.

**Rationale**: Matches the confirmed "grouped from children, not separately embedded" design — no
duplicate parent vectors and no storage doubling. Reuses the existing chunker; parent `Guid`s give
stable, meaningful citations (FR-004). Fixed grouping avoids heading/section detection, which the
current plain-text extractor has no structure for.

**Alternatives considered**:
- *(a) A second parent chunking pass* (`ParentChunkSize`/`ParentChunkOverlap`): cleaner boundaries,
  but two chunkers and a fuzzy child→parent mapping — rejected for v1 simplicity (VII).
- *(b) Embed parents too (2a)*: true parent-level similarity search, but doubles vectors/storage and
  the write path — rejected; revisit only if SC-002 synthesis quality proves insufficient.
- *(c) Derive parent text at query time from `Ordinal` ranges*: no parent rows, so no stable parent
  citation id — rejected (weakens V).

**Overlap handling**: consecutive children overlap by `ChunkOverlap` (50) tokens; when
concatenating a group the first `min(ChunkOverlap, childTokens-1)` tokens of every child after the
first are dropped, so no sentence is duplicated in the parent text. The final (short) child of a
document is handled the same way.

---

## R2 — Intent classification (offline, deterministic)

**Decision**: Add `RAGGit.Retrieval.QueryIntentClassifier` — a **rule-based** classifier returning
`QueryIntent.Broad | QueryIntent.Granular` from a small phrase lexicon:
- **Broad triggers**: `summarize`, `summary`, `overview`, `compare`, `comparison`, `contrast`,
  `difference(s) between`, `list all`, `all the steps`, `end-to-end`, `explain how`, `walk me
  through`, `pros and cons`, `high-level`, `guide`.
- **Granular** is the default; explicit granular cues (`what is the`, `when was`, `how many`,
  `how much`, `value of`, `the date`, `the name`, `the setting`, `deadline`, `threshold`, `limit`)
  reinforce it but are not required.
- Any broad trigger ⇒ Broad; otherwise ⇒ **Granular** (the safe default per FR-008, and identical
  to today's behaviour).

**Rationale**: Fully offline (IV), deterministic, O(query length) so SC-005 holds trivially, and
directly unit-testable (VI) and tunable against the eval sets (SC-001/002). Keeps the workstation
contract simple.

**Alternatives considered**: LLM-based labelling (adds a chat round-trip, latency, and
nondeterminism, risking SC-005); embedding-based classification (needs labelled examples to tune);
hybrid (rule-first then LLM) — deferred, not needed for v1.

---

## R3 — Retrieval routing

**Decision**: Extend `RetrievalService.RetrieveAsync` to `RetrieveAsync(query, topK, mode, ct)`:
1. Resolve the effective intent: explicit Broad/Specific override wins; `Auto` ⇒ run
   `QueryIntentClassifier`.
2. **Granular**: exactly today's path — embed the query, search child vectors, order by score,
   apply `RetrievalOptions.MinScore`, take `topK`.
3. **Broad**: search child vectors with a wider candidate limit
   (`min(topK × RetrievalOptions.BroadCandidateMultiplier, RetrievalOptions.MaxCandidates)`, defaults
   4 and 50), group the results by their `parentId`, fetch the distinct parent rows through
   `IDocumentRepository`, keep each parent's best child score, apply `MinScore`, and take `topK`
   parents. Return the parents as `SearchResult`s (`ChunkId = parent.Id`, `Text = parent text`,
   `Ordinal = parent ordinal`).

`GenerationService` is unchanged — it consumes `SearchResult`s regardless of level, so citations
work for both. `QueryController` only forwards the resolved mode.

**Rationale**: One vector search either way; broad retrieval adds a bounded SQLite lookup (local,
sub-millisecond) and needs no parent embeddings (2b). Keeps topK and the relevance floor
(FR-009).

**Alternatives considered**: independent parent embeddings (needs 2a, rejected in R1); query-time
re-chunking (too slow, risks SC-005); returning raw child hits for broad queries (that is the
current behaviour the feature exists to fix).

---

## R4 — Query override surface (spec FR-007, US3)

**Decision**: Add an **optional** `mode` field to `QueryRequest`
(`auto | broad | specific`, default `auto`) and echo the **effective** mode
(`broad | granular`) as an optional field on `QueryResponse`. The client shows a three-way selector
on the Ask surface, default **Auto**, persisted on `QueryViewModel`; `QueryApiClient.QueryAsync`
carries the value.

**Rationale**: Additive and optional ⇒ fully backward compatible (clients that omit `mode` behave
exactly as today). Default Auto preserves the "just ask" flow; the override is the cheap recovery
from misclassification that the spec's US3 asks for.

**Alternatives considered**: a required mode (breaks the current flow and the contract — rejected);
no override (no recovery; rejected by the spec's resolution of FR-007).

---

## R5 — Existing-library re-ingestion (FR-011)

**Decision**: Add a `ReindexBackfillService` (hosted service) that runs once after the schema is
ensured. It finds **Ready** documents whose chunks are not yet two-level (no `Level = Parent` row)
and re-processes each through the existing background path from its stored original
(`IDocumentContentStore`), i.e. re-chunk → re-embed → upsert → persist, observing
`Uploading → Queued → Indexing → Ready|Failed`. Documents already two-level are skipped, so the pass
is idempotent. Work is published through `IngestWorkQueue` so `LanceDbMaintenanceWorker` still
compacts/indexes afterwards.

**Rationale**: Reuses the proven background ingest and the stored originals (009) — no bespoke
migration machinery, no data loss, and the lifecycle is retained (FR-011). Because re-ingest
re-chunks from the original bytes, no vector read-back from LanceDB is required.

**Alternatives considered**: in-place payload mutation of existing vectors (requires reading vectors
back — LanceDB-specific and fragile — rejected); leaving old documents single-level (violates
FR-011).

**Operational note**: this mirrors the documented `VectorDb:VectorSize` recreate workflow — a full
re-ingest — but is automatic and scoped to stale documents rather than a manual wipe.

---

## R6 — Citation semantics

**Decision**: Granular answers cite **child** chunks; broad answers cite **parent** chunks (parent
`Guid`, parent text, parent ordinal). The `Citation` / `QueryAnswer` shape is unchanged; the client
already renders whatever chunk id/text it receives.

**Rationale**: Keeps the contract stable (no schema change) while making citations meaningful at
both levels; the parent text a broad answer cites is the exact context the model saw.

**Alternatives considered**: broad answers citing the several children behind the parent (noisy,
over-fragmented — rejected).

---

## R7 — Configuration

**Decision**: Two additive options, both with safe defaults so an unconfigured deployment behaves
sensibly:
- `IngestOptions.ParentGroupSize` (**4**) — children grouped per parent (≈2048 tokens at 512/child).
- `RetrievalOptions.BroadCandidateMultiplier` (**4**) and `RetrievalOptions.MaxCandidates` (**50**)
  — how wide broad retrieval searches before grouping.

**Rationale**: Keeps tunables in the existing options classes (config-driven, no rebuild) and gives
the eval harness knobs for SC-001/002. Validation: `ParentGroupSize ≥ 1`; a value of `1` degrades
gracefully to parent == child.

**Alternatives considered**: hard-coded constants (no tuning, rejected); a `ParentChunkSize` in
tokens (indirect and needs conversion — rejected in favour of a direct group count).

---

## Summary of resolved unknowns

| Unknown | Resolution |
|---------|-----------|
| How are parents represented? | Groups of consecutive children; own row + `Guid`; not embedded (R1) |
| How is intent decided? | Rule-based `QueryIntentClassifier`; Broad trigger else Granular (R2) |
| How does retrieval route? | `mode`/classifier ⇒ child search or group-to-parent (R3) |
| How does the user override? | Optional `mode` on the request; Auto default (R4) |
| How is the existing library upgraded? | Idempotent re-ingest backfill from stored originals (R5) |
| What do broad answers cite? | The parent chunk (R6) |
| What is configurable? | `ParentGroupSize`, `BroadCandidateMultiplier`, `MaxCandidates` (R7) |
