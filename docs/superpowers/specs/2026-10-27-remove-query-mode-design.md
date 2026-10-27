# Remove query mode from the product — design

**Date:** 2026-10-27
**Status:** approved scope, awaiting implementation

## Intent and why

The Ask page exposes a `Mode` picker (`Auto` / `Broad` / `Specific`) bound to
`QueryViewModel.QueryMode`. It is an internal retrieval concept leaking into an
employee-facing surface; end users do not understand it, and the default (`Auto`)
is what everyone should use. The mode therefore disappears from the UI, from the
HTTP request/response/stream contract, and from persistence. Retrieval keeps
deciding broad vs granular automatically, because the rule-based classifier is
what makes two-level chunking reachable at all.

## Goals

- No `mode` on `POST /api/queries` or `POST /api/queries/stream` (request,
  response, or NDJSON `answer` event).
- No mode control or mode state in the WPF client.
- No `QueryMode` type, no request override, no persisted effective mode.

## Non-goals

- Changing the classifier rules, two-level chunking, broad retrieval, or
  citation/grounding behavior.
- Removing the effective intent from runtime decision-making: the classifier
  still runs on every query.
- Rejecting old clients that still send `mode`.

## Anti-goals

- Keeping an accepted-but-ignored `mode` parameter as a documented feature.
- Removing the classifier (broad retrieval would become unreachable).

## Constraints

- Unknown JSON fields are ignored by `System.Text.Json`; a legacy client sending
  `mode` gets classification, not a 400. We do not add rejection.
- Existing SQLite databases keep the now-unused `Queries.Mode` column and its
  historical values (no destructive `DROP COLUMN`). Fresh databases no longer
  create it, and nothing writes it anymore.

## Approaches considered

- **A — remove from the API, keep the audit:** keep writing effective intent to
  `Queries.Mode` for diagnostics. Rejected: the user chose full removal.
- **B — remove the audit too (chosen):** delete `Query.Mode`, the INSERT column,
  the fresh-schema column, and the `EnsureColumnAsync` upgrade step. No reader
  remains, so no writer either.
- **Keep `mode` accepted and ignore it:** dropped — a silently ignored parameter
  is worse than none.

## How (decided)

**Core models**
- Delete `QueryMode` (`QueryIntent.cs`); keep `QueryIntent` and
  `QueryIntentClassifier` as internal retrieval vocabulary.
- `QueryRequest`: drop `Mode`.
- `QueryResponse`: drop `Mode`.
- `QueryStreamEvent`: drop `Mode` and its mappings in `FromAnswer`/`ToResponse`.
- `Query`: drop `Mode`.

**Retrieval**
- `RetrievalService` keeps one entry point:
  `RetrieveAsync(query, topK, ct)`, classifying internally with
  `QueryIntentClassifier.Classify`.
- Delete `ResolveIntent`, the `QueryMode` overload, and
  `RetrieveWithIntentAsync` (its only caller no longer needs the intent).
- `QueryOutcome`: drop `Mode` and `RequestedMode`; `ToResponse` stops copying it.
- `QueryPipeline`: call `RetrieveAsync`; remove the intent plumbing.

**API**
- `QueryController.SaveQueryAsync` drops the `mode` parameter and assignment;
  everything else about persistence is unchanged.

**Persistence**
- `RagDbContext.GetTableCommands`: remove `Mode TEXT` from `Queries`.
- Remove the `EnsureColumnAsync("Queries", "Mode", ...)` upgrade block.
- `SqliteQueryRepository.AddAsync`: drop `Mode` from the INSERT and its value.

**Client**
- `QueryApiClient.QueryAsync` / `StreamAsync`: drop the `mode` parameter.
- `QueryViewModel`: drop the `QueryMode` property; `AskQuestionAsync` streams
  without a mode.
- `QueryPage.xaml`: remove the `Mode` label, the `ComboBox`, the `QueryModes`
  `ObjectDataProvider`, the unused `sys` namespace, and their gutter tracks;
  the Clear button stays in the header actions area.

**Tests**
- Delete `QueryModeClientTests` (its subject is gone).
- `QueryModeContractTests` becomes the removal contract: responses contain no
  `mode`, and a legacy `mode` field is ignored (200, classifier decides).
- Remove mode assertions/parameters from `QueryStreamContractTests`,
  `QueryStreamTests`, `EvalTests`, `IntentRetrievalTests`,
  `IntentGroundingTests`, `QueryPipelineTests`, `QueryStreamClientTests`,
  and `IntentRetrievalTests`' DB row reader.
- `RetrievalRoutingTests`: use the single-argument form; the broad cases already
  use classifier trigger phrases.
- No test asserts absence of the SQLite column; a fresh DB is covered by the
  existing schema bootstrap tests.

**Docs**
- Update the 030 bullet in `AGENTS.md` (mode is automatic now; no API field, no
  persisted mode).
- Leave `specs/030-*` and `specs/032-*` artifacts untouched as historical
  records; this document records the removal.

## Left to the builder

Exact test renames/splits, CSharpier formatting, and the usual filtered test
runs. No open product questions.
