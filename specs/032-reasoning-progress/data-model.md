# Phase 1 Data Model: Reasoning Progress While Waiting

**Feature**: `032-reasoning-progress` | **Date**: 2026-10-15 | **Spec**: [spec.md](spec.md) |
**Contracts**: [contracts/api.yaml](contracts/api.yaml), [contracts/ui-contracts.md](contracts/ui-contracts.md)

This feature adds **no persisted entity**. Everything below is either a transient wire value on the new
stream, a transient client-side row, or the (unchanged) query outcome carried on the stream's terminal
event. The `Queries` table, its columns, and the history/detail projections are untouched (FR-014,
R7 in [research.md](research.md)).

---

## 1. `QueryStage` (enum, wire) — NEW

The language-independent identity of a reasoning update. Lives in `RAGGit.Core.Models` beside
`QueryMode`/`QueryIntent`, with lowercase wire values via `[JsonStringEnumMemberName]` (repo convention).

| Member | Wire value | Meaning | Emitted when |
|--------|-----------|---------|--------------|
| `Understanding` | `understanding` | the question is being read and interpreted | the pipeline starts work on the question (intent resolution + embedding begin) |
| `Searching` | `searching` | the library is being searched | the vector search begins |
| `Sources` | `sources` | relevant sources were found | retrieval returned ≥1 chunk above `Retrieval:MinScore` |
| `Composing` | `composing` | the answer is being written | generation begins |

**Validation rules**

- Exactly these four members; no other stage may be emitted (FR-002).
- Ordered as listed; `sources` is skipped entirely when retrieval found nothing (FR-005, US2.2).
- A stage never appears after the terminal `answer`/`error` event (FR-009).

---

## 2. `QueryProgress` (wire payload) — NEW

The body of a `progress` stream event.

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `stage` | `QueryStage` | yes | language-independent identity (above) |
| `text` | string | yes | plain-language sentence for the employee, **rendered by the workstation in the question's language** (FR-006, R4) |
| `sourceCount` | integer, nullable | no | present only on `stage: sources`; count of distinct relevant sources |
| `documentNames` | string[], nullable | no | present only on `stage: sources`; filenames of those sources, in the order the answer's citations will use |

**Validation rules**

- `text` MUST contain no internal identifier, chunk body, prompt text, model name, or infrastructure
  name (FR-007); it is user-facing prose only.
- `text` MUST be in the question's language (FR-006, US2.1).
- `sourceCount`/`documentNames` MUST be absent (or empty) on stages other than `sources`, and MUST NOT be
  sent when retrieval found nothing (FR-005).
- `documentNames` MUST only contain filenames already reachable from the answer's own Sources view
  (FR-007) and MUST be scoped to the caller's own retrieval (FR-008).
- `sourceCount` is a count of relevant sources actually retrieved, never the candidate pool size.

---

## 3. Stream event envelope (wire) — NEW

One JSON object per line, `Content-Type: application/x-ndjson`. A single discriminated envelope keeps the
client parser trivial (R1).

| Variant | Discriminator | Carries |
|---------|---------------|---------|
| Progress | `"type": "progress"` | a `QueryProgress` (fields above, inlined) |
| Answer | `"type": "answer"` | the **same fields as the `POST /api/queries` `200` body** (`answer`, `citations`, `retrievedChunkIds`, `latencyMs`, `mode`, `suggestedPersons`) |
| Error | `"type": "error"` | `error` (string, the same message the non-streaming path would produce) and `status` (integer, the HTTP status the non-streaming path would have returned) |

**Validation rules**

- Exactly one terminal event (`answer` or `error`); at most one `error`, and it is last (FR-012).
- Unknown event types and unknown fields MUST be ignored by the client (forward compatibility, matching
  the existing "clients MUST ignore unknown fields" rule from 030's contract).
- No event may contain chunk text, the assembled prompt, or any other person's data (FR-007, FR-008).
- Events are ordered; the client renders them in arrival order (FR-003).

---

## 4. `QueryOutcome` (library return value) — NEW (refactor, not new data)

What `QueryPipeline.ExecuteAsync` returns, and what the terminal `answer` event is built from. It is the
in-memory equivalent of today's `QueryResponse` plus the effective intent — no new persisted fields.

| Field | Type | Notes |
|-------|------|-------|
| `Answer` | string | the grounded answer, or exactly `no relevant content found` |
| `Citations` | list of citation | unchanged shape (`documentId`, `documentName`, `chunkId`, `text`, `ordinal`) |
| `CitationIds` | list of id | the citation ids as generation produced them (the value the history row stores) |
| `RetrievedChunkIds` | list of id | unchanged (empty when nothing was retrieved) |
| `LatencyMs` | integer | measured across the whole pipeline, as today |
| `Mode` | `QueryIntent?` | effective intent, as today |
| `SuggestedPersons` | list | did-you-mean hints, as today |
| `RequestedMode` | `QueryMode` | the caller's override, used to build the persisted query row's mode as today |

**Validation rules**

- The `answer` stream event and the non-streaming `200` body MUST be derived from the same fields, so a
  parity test can assert equality for identical inputs (R12).
- `Answer` is `no relevant content found` with an empty `Citations` list when nothing was retrieved —
  unchanged (FR-010, US3.2).

---

## 5. `ReasoningStep` (client-side row) — NEW, transient

The renderable row the Ask surface binds to. Never serialized, never sent to the server, never stored.

| Field | Type | Notes |
|-------|------|-------|
| `Stage` | `QueryStage` | carried through from the event, for tests/automation/styling |
| `Text` | string | the workstation-rendered sentence, displayed verbatim |
| `IsCurrent` | bool | true only for the newest row, which shows the activity indicator |

**Validation rules**

- Rows exist only while a question is in flight; the collection is emptied the moment the answer or an
  error is presented (FR-009, FR-014).
- Rows are appended in arrival order and are never rewritten or reordered (FR-003).
- The collection is never repopulated after the question completes — revisiting the exchange later shows
  no reasoning (FR-014, US3.4).
- At most one reasoning account exists at a time: a second submission cannot interleave with a first
  (existing busy rules are unchanged).

---

## 6. `ReasoningAccount` (lifecycle) — NEW, transient state machine

The ordered sequence of `ReasoningStep`s for one in-flight question. It has no storage, no id, and no
existence outside the client's memory.

```text
        submit
Idle ─────────────► Streaming ──────────► Complete      (answer event → region cleared)
  ▲                    │
  │                    └──────────────► Failed          (error event, HTTP status, or timeout → region cleared)
  │                                             │
  └─────────────────────────────────────────────┘  (next question starts a fresh account)
```

| State | Entered by | Region visible? | Exits to |
|-------|-----------|-----------------|----------|
| `Idle` | page load, answer delivered, failure shown, conversation cleared | no | `Streaming` (submit) |
| `Streaming` | question submitted; first `progress` event rendered | yes, rows appended as events arrive | `Complete` or `Failed` |
| `Complete` | terminal `answer` event handled | no — cleared before the assistant bubble is shown AND the state is fully mutable | `Idle` |
| `Failed` | terminal `error` event, non-2xx status, unreachable workstation, timeout, or a stream that ends without a terminal event | no — cleared in `finally` | `Idle` |

**Validation rules**

- `Complete` clears the account *before* the assistant message is added, so no reasoning row can survive
  into the finished exchange (FR-009, US1.3).
- `Failed` clears the account in `finally`, so a failure never leaves a "still working" impression
  (FR-012, US2.5).
- Navigating away mid-`Streaming` disposes the request; the account is discarded and is not re-shown on
  return (US1.4, R10).
- An answer that completes before any event is rendered passes straight from `Idle` to `Complete` with no
  visible region (FR-013, R9).

---

## 7. Unchanged entities (explicitly out of scope)

| Entity | Why it is listed | Change |
|--------|------------------|--------|
| `Query` (persisted row) | FR-010 requires history to be identical | **none** — written by the same `SaveQueryAsync` path, same fields, same ordering rules |
| `HistoryItem` / `QueryDetail` / `HistoryCitation` | 005 per-person-history projections | **none** — no reasoning is added to any projection (FR-014) |
| `Citation` | grounding contract | **none** — same shape, same `ordinal` ordering, same parent/child meaning from 030 |
| `Document` / `Chunk` | ingest | **none** — retrieval is untouched |
| `PersonSuggestion` | did-you-mean hints | **none** — still delivered on the terminal `answer` event with the same shape |
