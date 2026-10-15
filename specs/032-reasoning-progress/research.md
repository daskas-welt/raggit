# Phase 0 Research: Reasoning Progress While Waiting

**Feature**: `032-reasoning-progress` | **Date**: 2026-10-15 | **Spec**: [spec.md](spec.md)

Every decision below closes an open question the spec left to planning (delivery mechanics, where the
stage narration is produced, how the client consumes it without breaking UI-thread affinity, and what
must stay byte-identical). Format: **Decision / Rationale / Alternatives considered**.

---

## R1 — Transport for live progress: NDJSON over the existing HTTPS POST

**Decision**: Add one endpoint, `POST /api/queries/stream`, that responds `200` with
`Content-Type: application/x-ndjson` and writes one JSON object per line, flushing after each line. The
request body is the existing `QueryRequest` (`query`, optional `topK`, optional `mode`), and
authorization is identical to `POST /api/queries` (`[Authorize(Roles = "Employee,Admin")]` plus the
existing `sub` claim handling).

**Rationale**: The workstation API is MVC-on-controllers, so a browser-oriented protocol buys nothing
here while a newline-delimited stream needs no framing spec and no parser beyond
`StreamReader.ReadLineAsync` + `JsonSerializer`. The client is a WPF app that can send an
`Authorization` header and read the response body incrementally with
`HttpCompletionOption.ResponseHeadersRead`, so the existing `BearerDelegatingHandler` and 10-minute
client timeout keep working unchanged. NDJSON is also trivially testable: unit tests feed a string of
lines through a stub `HttpMessageHandler`, contract tests read lines from `WebApplicationFactory`. No
new package is required in either direction.

**Alternatives considered**:
- **Server-Sent Events (`text/event-stream`)**: standard, and .NET 10 has server helpers, but for an MVC
  controller we would hand-roll `data:`/blank-line framing anyway; `EventSource` cannot set an
  `Authorization` header (it would force a cookie or query-string token, weakening the current auth
  model); and some LAN proxies buffer `text/event-stream` unless configured. Its advantages (browser
  reconnect, `Last-Event-ID`) do not apply to a single-shot desktop query.
- **WebSocket / SignalR**: adds a package and a second connection lifecycle (handshake, reconnect,
  back-pressure) to deliver what is a short, one-way, per-request message sequence.
- **Polling a status endpoint**: needs server-side per-query state keyed by a request id, plus expiry
  and cleanup — new stateful machinery on a stateless, singleton-service API, and it cannot show the
  `sources` stage any sooner than the next poll tick.
- **Long-poll of the existing endpoint with partial results**: cannot express ordering or "work has
  started" without inventing the same state as polling.

---

## R2 — One pipeline, two transports (extract `QueryPipeline`)

**Decision**: Move the query orchestration currently inlined in `QueryController.Post` — health probe,
intent-resolving retrieval, empty-result short-circuit, document-name resolution, generation, citation
scoping, did-you-mean suggestions, latency measurement — into `RAGGit.Retrieval.QueryPipeline` with the
signature `Task<QueryOutcome> ExecuteAsync(QueryRequest request, Func<QueryProgress, CancellationToken,
ValueTask>? onProgress, CancellationToken ct)`. `QueryController.Post` calls it with a no-op observer and
returns the same `QueryResponse` as today; `QueryController.PostStream` calls it with an observer that
writes events. History persistence (`IQueryRepository.AddAsync`) stays in the controller, exactly where
it is now.

**Rationale**: Two independent copies of the pipeline would drift, and drift here means either a
grounding regression or progress that no longer matches real work (FR-005). Progress is a *side channel*
of the existing pipeline, not a parallel implementation. Putting the orchestration in `RAGGit.Retrieval`
also satisfies Constitution III (library-first, independently testable) and makes the stage sequence
assertable in unit tests without an HTTP host. The non-streaming endpoint is a pure refactor: same
inputs, same outputs, same order of side effects.

**Alternatives considered**:
- **A second controller action duplicating the pipeline**: shortest diff, but the two copies would need
  to be kept in lockstep forever, and the streaming copy would be the one that silently loses future
  fixes.
- **Progress events emitted from `RetrievalService`/`GenerationService` directly**: spreads transport
  concerns (and the failure/short-circuit branches) across library internals that today have no notion
  of a caller-visible stage.
- **Client-orchestrated progress** (client "knows" the phases and advances its own text): rejected by
  FR-004 — progress must be reported live, at the moment the work happens.

---

## R3 — What the stages are, and the honesty rules

**Decision**: Four stages, in this order, emitted only at the moment the corresponding work starts or
completes:

| Stage | Emitted when | Payload |
|-------|--------------|---------|
| `understanding` | the question enters the pipeline (intent resolution + embed begin) | text |
| `searching` | the vector search begins | text |
| `sources` | retrieval returned **one or more** chunks above `MinScore` | text, `sourceCount`, `documentNames` |
| `composing` | generation begins | text |

Rules: the `sources` stage is **not** emitted when retrieval returns zero chunks (no claim of found
sources when the answer will be "no relevant content found" — US2.2); `documentNames` carries only
filenames already surfaced by the answer's own Sources view; no stage is ever emitted after the answer
event; and the terminal `answer` event carries the same payload the non-streaming endpoint returns.

**Rationale**: These four are the real, observable boundaries of the existing pipeline, so every line
corresponds to work that actually ran (FR-005) and nothing has to be invented or timed. `documentNames`
is the piece that makes the wait feel informative without leaking anything new.

**Alternatives considered**: a single "working" line (no progress — the problem being fixed); a
per-chunk or token-level ticker (busy and meaningless); exposing retrieval *scores* (internal detail,
encourages score-lawyering, FR-006).

---

## R4 — Who writes the progress text: the workstation, localized to the question language

**Decision**: The workstation renders each stage's human-readable `text` in the question's language,
reusing the existing language detection (`AnswerLanguage.IsGreek`, already used to enforce the answer
language) via a small `RAGGit.Retrieval.ReasoningText` helper. Each event also carries the stable
`stage` value, which is language-independent.

**Rationale**: The workstation already decides the answer's language from the question, so narration and
answer cannot disagree (US3.1) and there is exactly one place where the Greek strings live. The stable
`stage` value keeps tests, automation, and any future styling independent of prose, so wording can change
without breaking assertions. The client stays a thin renderer, consistent with Constitution II.

**Alternatives considered**:
- **Client-side localization keyed off the question language**: forces the client to re-detect language
  (a second copy of the Greek/English rule) and splits one behaviour across two artifacts; the client has
  no localization framework today.
- **Stage identifier only, no text**: pushes all copy into the client for no gain, and makes the wording
  a client-build concern.

---

## R5 — Client consumption without breaking UI-thread affinity

**Decision**: `QueryApiClient.StreamAsync(...)` returns `IAsyncEnumerable<QueryStreamEvent>`; the
ViewModel consumes it with `await foreach` and mutates `ReasoningSteps` (an `ObservableCollection`)
inside the loop. The streaming enumerator deliberately **does not** use `ConfigureAwait(false)` — matching
every existing service in `RAGGit.Client.Core` — so each continuation resumes on the caller's
synchronization context (the WPF UI thread in the app, the test context in unit tests).

**Rationale**: `ObservableCollection` mutations must happen on the WPF UI thread or binding throws. The
codebase already relies on the awaited continuation returning to the UI thread (every `Messages.Add` in
`AskQuestionAsync` works this way), so following the same convention keeps the new code indistinguishable
from the surrounding style and avoids introducing a dispatcher dependency into the
framework-independent `Client.Core`.

**Alternatives considered**:
- **Callback API (`Action<QueryProgress>`)**: equally workable but harder to compose and to test for
  ordering, and it invites `ConfigureAwait(false)` in the callback path.
- **`Channel<T>` producer/consumer**: extra ceremony for a short per-request sequence.
- **Reintroducing a WPF dispatcher abstraction into `Client.Core`**: a new seam for a problem the existing
  continuation convention already solves.

---

## R6 — Streaming mechanics on the server: flush, no-store, and no interference from existing middleware

**Decision**: `PostStream` sets `Content-Type: application/x-ndjson`, `Cache-Control: no-store`, and
writes each event as `JsonSerializer.Serialize(event)` + `"\n"` to `Response.Body`, then
`await Response.Body.FlushAsync(ct)`. It checks the LLM health probe (and request validation) **before**
writing the first byte so failures can still return an ordinary JSON status (`400`, `401`, `503`) rather
than an in-band event.

**Rationale**: The API registers `UseResponseCaching()` and `UseHttpsRedirection()` but no response
compression, and response caching only acts on responses marked cacheable, so nothing buffers the stream
today; `Cache-Control: no-store` makes that explicit for proxies on the LAN. Flushing per event is what
makes progress *live*: without it, small writes would sit in the response buffer and defeat SC-002.
Returning status codes before the first write preserves today's client-side error mapping for
auth/offline/unhealthy cases.

**Alternatives considered**: `IAsyncEnumerable<QueryStreamEvent>` returned from the action (works, but
the MVC JSON output formatter is the wrong shape for NDJSON and would need a custom result anyway);
in-band-only errors (loses the existing `503 model unavailable offline` / `401` / `403` client mapping).

---

## R7 — Nothing new is persisted; the query row is still written

**Decision**: Progress events are never written to SQLite or any log sink beyond the existing structured
logging, and the `answer` event is not a new persisted shape. The streaming path calls the same
`SaveQueryAsync` as today, so a streamed question still appears in the employee's history with the same
prompt, answer, citations, mode, and latency.

**Rationale**: FR-014 (transient) and FR-010 (history unchanged) are both satisfied by *not* touching the
persistence layer. The alternative — storing reasoning — would change the `Queries` schema, the
`/api/queries/history` and `/api/queries/{id}` projections, and the 005 per-person-history contract for
no user-visible benefit the owner asked for.

**Alternatives considered**: persisting a run log for later review (rejected as out of scope by the
clarification, and it would widen the per-person data surface); storing nothing at all, including the
query row (rejected — it would silently break history for every streamed question).

---

## R8 — Ask-surface presentation and automation

**Decision**: `ChatControl` gains an `ReasoningSteps` dependency property bound to
`QueryViewModel.ReasoningSteps`; the existing static busy `ui:TextBlock` ("Searching the library and
writing your answer…") is replaced by that region. Each row is a `ui:TextBlock` inside a `ListBox`
item template; the newest (in-flight) row shows a small `ui:ProgressRing` (`16×16`, `IsIndeterminate`) as
its leading element. The region container carries
`AutomationProperties.AutomationId="ReasoningProgressList"`, and the region is collapsed whenever there
are no steps. Icons are not used, so no `SymbolRegular` member has to be validated.

**Rationale**: A `ListBox` container (rather than `ui:Card`/`Border`) is used because the `winapp ui`
harness surfaces ListBox automation IDs and does not surface `Card`/`Border` IDs (AGENTS "Verifying UI"
trap) — this keeps the feature's own acceptance check runnable. `ui:ProgressRing` is already used in the
send button, so no new glyph-name risk is introduced, and dropping icons avoids guessing at
`SymbolRegular` members that "compile fine, fail at runtime" (AGENTS WPF-UI trap). The existing
send-button spinner and `IsBusy` behaviour stay exactly as they are.

**Alternatives considered**: a single updating `ui:TextBlock` (cheapest, but "updates progressively"
becomes unverifiable and history is invisible); `ui:CardExpander` per step (visual noise for a
half-second-lived region); symbolic icons per stage (nice, but each `Symbol="…"` must be verified against
`SymbolRegular` at implementation time and buys nothing measurable here).

---

## R9 — Fast answers must not flicker

**Decision**: The client renders the reasoning region only from the first event it receives, and clears it
in a `finally` when the answer or error arrives; there is no client-side minimum display time and no
pre-seeded placeholder row. If the answer completes before any event is processed, the user sees nothing
extra at all.

**Rationale**: FR-013 asks for no misleading or contradictory progress. Because the first event
(`understanding`) is emitted at pipeline entry, the region naturally appears within the SC-002 budget
without any timer-driven theatre; adding an artificial minimum would make a fast answer feel slower,
against SC-007. Rejecting the pre-seeded row also means the "0 relevant sources" case can never show a
"sources found" line that did not happen.

**Alternatives considered**: seeding "Searching…" locally before the first event (double-renders against
the server's own `searching`/`understanding` text and can contradict it); holding the region for a fixed
minimum (adds latency for nothing).

---

## R10 — Mid-stream failure and disconnect behaviour

**Decision**: Failures surface as they do today: validation/auth/unhealthy failures arrive as ordinary
HTTP statuses before the stream starts (client maps them with the existing message strings); a failure
*after* the stream has started is delivered as a terminal `{"type":"error"}` event carrying the same
message the non-streaming endpoint would produce, and the client shows it through the existing status
banner. If the employee navigates away or closes the window mid-answer, the request aborts,
`HttpContext.RequestAborted` cancels the pipeline, and — exactly as today — no history row is written.

**Rationale**: Keeping both failure positions mapped to the same user-visible strings means the new
endpoint cannot invent a new error vocabulary, and the abort path is unchanged because the same
cancellation token already flows through the pipeline.

**Alternatives considered**: treating a truncated stream (no terminal event) as success (would silently
lose the answer); retrying the query automatically after a mid-stream failure (double work, duplicate
history rows, and a nondeterministic UX).

---

## R11 — Compatibility, rollout, and what is deliberately *not* built

**Decision**: `POST /api/queries` remains available and unchanged; the client uses only the streaming
endpoint for new questions, and no runtime fallback to the non-streaming endpoint is implemented. No
configuration switch is added. Neither per-token answer streaming nor the model's internal thinking text
is built.

**Rationale**: Client and workstation ship as one artifact set (031), so there is no version skew to
bridge, and Simplicity (VII) argues against a dual path that would only ever be exercised in tests. The
clarifications explicitly excluded internal thinking text and progressive answer text, so building either
would add scope the owner declined — and the local models in use emit no separated reasoning to show.

**Alternatives considered**: a `Streaming:Enabled` flag (an untested second path forever); SSE fallback
(two client parsers); exposing Ollama's `think` output (declined in the spec; leak risk per FR-007).

---

## R12 — Offline invariant and test strategy

**Decision**: No new egress is introduced and no new test category is needed. Unit tests cover stage
ordering/honesty, text language, NDJSON parsing, and the ViewModel lifecycle with no Ollama and no
network. Contract tests exercise the endpoint through `WebApplicationFactory` with the existing
`FakeLlmClient`/`FakeVectorStore`, including a parity assertion that the `answer` event payload equals
`POST /api/queries`'s `200` body for the same fakes (modulo `latencyMs`), and a redaction assertion that
no event field contains chunk text or prompt text. Integration tests cover the stream end-to-end, assert a
history row still appears, and confirm no reasoning is retrievable from history/detail. The existing
offline subset (`QueryOfflineTests|OfflineIdentityTests|OfflineHistoryTests|CrossUserIsolationTests`)
stays green.

**Rationale**: The invariant is about query-time egress, which this feature does not add; the parity test
is the strongest available guard that the refactor in R2 preserved the non-streaming contract, and the
redaction test is the executable form of FR-007.

**Alternatives considered**: a dedicated offline stream test (redundant with the existing gate, which
already runs the query path); snapshot-testing the prose (brittle against wording changes; the stage
enum is the stable thing to assert).
