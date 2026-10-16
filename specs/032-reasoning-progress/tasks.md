---

description: "Task list for Reasoning Progress While Waiting"
---

# Tasks: Reasoning Progress While Waiting

**Input**: Design documents from `/specs/032-reasoning-progress/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/](contracts/), [quickstart.md](quickstart.md)

**Tests**: **REQUIRED.** Constitution VI (Test-First, NON-NEGOTIABLE) and AGENTS.md mandate TDD — write these tests first, confirm they FAIL, then implement. Never delete a test to make a build pass.

**Organization**: Tasks are grouped by user story (US1–US3 from spec.md) so each is an independently testable increment. US1 is the MVP.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: US1–US3; omitted for Setup, Foundational, and Polish phases
- Every task names its exact file path

## Path Conventions

- Existing multi-project solution (**no new project, no new dependency**). Libraries `src/RAGGit.Core`, `src/RAGGit.Ingest`, `src/RAGGit.Retrieval`; host `src/RAGGit.Workstation.Api`; client `src/RAGGit.Client.Core` + `src/RAGGit.Client.WPF`; tests `tests/unit|contract|integration`.
- Behavioural source of truth: [contracts/api.yaml](contracts/api.yaml) (stream events), [contracts/ui-contracts.md](contracts/ui-contracts.md) (U1), [data-model.md](data-model.md) (entities + lifecycle).

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Establish a green baseline so every later failure is attributable to this feature.

- [x] T001 Confirm the baseline is green before any change: from repo root run `dotnet build RAGGit.Server.slnf -c Release`, then `dotnet test` for unit, contract, and the offline subset (`QueryOfflineTests|OfflineIdentityTests|OfflineHistoryTests|CrossUserIsolationTests`). Record any pre-existing failure so it is not later attributed to this feature.
- [x] T002 [P] Confirm `dotnet tool restore` then `dotnet csharpier check .` is clean at baseline (it formats XAML too), so later formatting diffs are attributable.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The shared wire contract and the single-pipeline refactor that every user story builds on. No user-story work can begin until this phase is complete.

**⚠️ CRITICAL**: `POST /api/queries` must behave **identically** after this phase (FR-010) — the extraction is a pure refactor of `QueryController.Post`, not a rewrite.

- [x] T003 [P] Add the `QueryStage` enum in `src/RAGGit.Core/Models/QueryStage.cs` with `[JsonStringEnumMemberName]` lowercase wire values exactly `understanding`, `searching`, `sources`, `composing` (members `Understanding = 0`, `Searching = 1`, `Sources = 2`, `Composing = 3`), matching the existing `QueryMode`/`QueryIntent` convention in `src/RAGGit.Core/Models/QueryIntent.cs`. Per data-model.md §1: exactly these four members, ordered as listed.
- [x] T004 [P] Add `QueryProgress` in `src/RAGGit.Core/Models/QueryProgress.cs` with required `QueryStage Stage` and required `string Text`, plus optional `int? SourceCount` and `IReadOnlyList<string>? DocumentNames`. Per data-model.md §2: `SourceCount`/`DocumentNames` are populated **only** on `stage: sources` and must be absent when retrieval found nothing.
- [x] T005 [P] Add the stream envelope in `src/RAGGit.Core/Models/QueryStreamEvent.cs`: a discriminated line type with `type` in `progress` | `answer` | `error`. The `progress` variant inlines the `QueryProgress` fields; the `answer` variant carries exactly the `QueryResponse` field set (`answer`, `citations`, `retrievedChunkIds`, `latencyMs`, `mode`, `suggestedPersons`); the `error` variant carries `error` (string) and `status` (int). Per data-model.md §3: unknown event types and unknown fields MUST be ignorable by the client.
- [x] T006 [P] Add `QueryOutcome` in `src/RAGGit.Retrieval/QueryOutcome.cs` carrying `Answer`, `Citations`, `RetrievedChunkIds`, `LatencyMs`, `Mode` (`QueryIntent?`), `SuggestedPersons`, and `RequestedMode` (`QueryMode`) — the in-memory equivalent of today's `QueryResponse` plus the effective intent (data-model.md §4). No new persisted fields.
- [x] T007 [P] Unit tests in `tests/unit/QueryStreamEventTests.cs`: `QueryStage` serializes to the four lowercase wire values and round-trips under the client's `JsonSerializerOptions`; a `progress` line deserializes with and without `sourceCount`/`documentNames`; an `answer` line deserializes into the same field set as `QueryResponse`; an unknown `type` is ignored rather than throwing; unknown fields are ignored.
- [x] T008 [P] Unit tests in `tests/unit/QueryPipelineTests.cs` for the extracted pipeline using the existing fake embedder/vector store/`ILlmClient` idioms: the observer receives stages in the order listed in data-model.md §1; `sources` is **never** emitted when retrieval returns zero chunks; no stage is observed after the terminal result; `ExecuteAsync` returns the same `Answer`/`Citations`/`RetrievedChunkIds`/`Mode` as today's controller path for the same fakes, and `LatencyMs` is measured across the whole pipeline.
- [x] T009 Extract `QueryPipeline` into `src/RAGGit.Retrieval/QueryPipeline.cs` with `Task<QueryOutcome> ExecuteAsync(QueryRequest request, Func<QueryProgress, CancellationToken, ValueTask>? onProgress, CancellationToken ct)`: move the `Math.Clamp(request.TopK, 1, 5)` clamp, `RetrieveWithIntentAsync`, the empty-result short-circuit (answer exactly `no relevant content found`, empty citations), `ResolveDocumentNamesAsync`, `GenerationService.GenerateAsync`, citation scoping, `PersonNameSuggester` did-you-mean assembly, and the latency stopwatch out of the controller. The LLM health probe stays in the controller so pre-stream failures keep their JSON status codes (T019). `onProgress` may be null (no-op).
- [x] T010 Rewire `QueryController.Post` in `src/RAGGit.Workstation.Api/Controllers/QueryController.cs` to delegate to `QueryPipeline.ExecuteAsync(request, onProgress: null, ct)` and return the identical `QueryResponse`, keeping the health probe and `SaveQueryAsync` in the controller exactly where they are. This is a pure refactor — no observable behaviour change (FR-010).
- [x] T011 Register `QueryPipeline` in `src/RAGGit.Workstation.Api/Program.cs` alongside the existing `RetrievalService`/`GenerationService` singletons, with no new configuration section.
- [x] T012 Verify no regression from the refactor: run `tests/contract/QueryContractTests.cs`, `tests/contract/QueryModeContractTests.cs`, `tests/integration/IntentRetrievalTests.cs`, `tests/integration/IntentGroundingTests.cs`, and the offline subset (`QueryOfflineTests|OfflineIdentityTests|OfflineHistoryTests|CrossUserIsolationTests`, which is FR-011's gate) unchanged. Any failure here is a refactor bug, not a new-feature bug.

**Checkpoint**: Shared contract and single pipeline ready — user-story work can begin.

---

## Phase 3: User Story 1 - The wait is explained, not blank (Priority: P1) 🎯 MVP

**Goal**: An employee asking a question sees a live, ordered account of the work — understanding the question, searching the library, the relevant sources found, composing the answer — which disappears when the answer arrives.

**Independent Test**: Ask a question against a real library and confirm more than one distinct progress line appears over time before the answer lands, that the last line corresponds to work actually performed, and that the finished exchange looks exactly as it does today (answer + Sources).

### Tests for User Story 1 (write first, confirm they fail)

- [x] T013 [P] [US1] Contract tests in `tests/contract/QueryStreamContractTests.cs` against `POST /api/queries/stream` per contracts/api.yaml: `200` with `Content-Type: application/x-ndjson`; event order is `progress(understanding)` → `progress(searching)` → optional `progress(sources)` → `progress(composing)` → exactly one terminal `answer`; each line is a standalone JSON object; `400` for an invalid query, `401` unauthenticated, and `503` when the LLM is unhealthy — all **before** the first event is written; and **parity**: for the same fakes the `answer` event equals the `POST /api/queries` `200` body.
- [x] T014 [P] [US1] Unit tests in `tests/unit/QueryStreamClientTests.cs` for the client stream reader using a stub `HttpMessageHandler`: events are yielded in order; a final line without a trailing newline is still parsed; a malformed line does not abort the stream; an unknown `type` is skipped; a stream that ends without a terminal event is surfaced as a failure rather than an empty answer.
- [x] T015 [P] [US1] Unit tests in `tests/unit/QueryViewModelReasoningTests.cs`: `ReasoningSteps` is populated in arrival order as events are consumed; it is empty once the answer is handled; the answer path still sets `Answer`, `Citations`, `HasCitations`, `SuggestedPersons`, `IsResultVisible`, and the existing `Answered in …ms …` status text exactly as today.
- [x] T016 [P] [US1] Unit tests in `tests/unit/ReasoningTextTests.cs` for the English baseline: each stage maps to a non-empty plain-language sentence; no sentence contains a UUID, a chunk/document identifier, a model name, configuration key, file-system path, or the assembled prompt.

### Implementation for User Story 1

- [x] T017 [US1] Implement `ReasoningText` in `src/RAGGit.Retrieval/ReasoningText.cs`: `string For(QueryStage stage, string query, int? sourceCount, IReadOnlyList<string>? documentNames)` returning the employee-facing sentence, with the `sources` variant naming the sources (e.g. `Found 2 relevant sources: policy.pdf, renewal.pdf`) and never claiming sources when `sourceCount` is null or 0 (FR-002, FR-006).
- [x] T018 [US1] Emit progress from `QueryPipeline` in `src/RAGGit.Retrieval/QueryPipeline.cs` via the existing `onProgress` callback: `understanding` at pipeline entry; `searching` when the vector search begins; `sources` with `SourceCount` and `DocumentNames` (resolved filenames, in first-appearance order) only when retrieval returned ≥1 chunk above `Retrieval:MinScore`; `composing` immediately before generation. Nothing is emitted after the terminal result (FR-002, FR-003, FR-004, FR-005).
- [x] T019 [US1] Implement `QueryController.PostStream` in `src/RAGGit.Workstation.Api/Controllers/QueryController.cs` per contracts/api.yaml: `[HttpPost("stream")]` with the same `[Authorize(Roles = "Employee,Admin")]` and `sub` handling as `Post`; set `Content-Type: application/x-ndjson` and `Cache-Control: no-store`; run the health probe and model validation **before** writing the first byte so `400`/`401`/`503` stay ordinary JSON; serialize each event with the app's JsonSerializerOptions (so `QueryStage`/`QueryIntent` stay lowercase), write it plus `"\n"`, and `await Response.Body.FlushAsync(ct)` after each; finish with the `answer` event and call the same `SaveQueryAsync` as `Post`. No new egress is introduced — the stream rides the existing connection (FR-001, FR-004, FR-011).
- [x] T020 [US1] Implement `QueryApiClient.StreamAsync(string query, int topK = 5, QueryMode mode = QueryMode.Auto, CancellationToken ct = default)` returning `IAsyncEnumerable<QueryStreamEvent>` in `src/RAGGit.Client.Core/Services/QueryApiClient.cs`: `HttpCompletionOption.ResponseHeadersRead`, `POST api/queries/stream`, reuse the existing `EnsureSuccessOrThrowAsync`/`IsMappedError` mapping for pre-stream statuses, read NDJSON line by line, and **do not** use `ConfigureAwait(false)` (research.md R5) so the ViewModel's continuations stay on the WPF UI thread (FR-003).
- [x] T021 [US1] Add `ReasoningStep` in `src/RAGGit.Client.Core/Models/ReasoningStep.cs` (`QueryStage Stage`, `string Text`, `IsCurrent`) and wire `QueryViewModel` in `src/RAGGit.Client.Core/ViewModels/QueryViewModel.cs`: add `[ObservableProperty] ObservableCollection<ReasoningStep> _reasoningSteps` plus a `HasReasoning` flag, switch `AskQuestionAsync` to `await foreach (var evt in _apiClient.StreamAsync(question, mode: QueryMode, cancellationToken: …))`, append `progress` rows in arrival order, and handle the terminal `answer` event through the existing post-answer code path unchanged.
- [x] T022 [US1] Replace the static busy line with the reasoning region in `src/RAGGit.Client.WPF/Components/ChatControl.xaml` and `src/RAGGit.Client.WPF/Components/ChatControl.xaml.cs`: remove the `Text="Searching the library and writing your answer…"` block, add `ReasoningSteps`/`HasReasoning` dependency properties (mirroring the existing `Messages`/`IsBusy` pattern), and render rows as `ui:TextBlock` inside a `ListBox` item template whose container carries `AutomationProperties.AutomationId="ReasoningProgressList"`; show a small `ui:ProgressRing` (14×14, `IsIndeterminate`) as the leading element of the newest row only; collapse the region when there are no steps (FR-001).
- [x] T023 [US1] Bind the region in `src/RAGGit.Client.WPF/Views/Pages/QueryPage.xaml`: pass `ReasoningSteps="{Binding ReasoningSteps}"` and `HasReasoning="{Binding HasReasoning}"` to `components:ChatControl`, leaving every existing binding and automation ID untouched.
- [x] T024 [US1] Integration test in `tests/integration/QueryStreamTests.cs`: drive `POST /api/queries/stream` end-to-end through the integration factory with fakes and assert (a) the observed event order and that `progress` events arrive before the `answer`, (b) the `sources` event names the uploaded document, (c) a streamed question still produces a history row, (d) no reasoning text is persisted with the query.

**Checkpoint**: US1 fully functional and independently testable — this is the MVP.

---

## Phase 4: User Story 2 - The reasoning is honest and safe (Priority: P2)

**Goal**: Every line shown describes work that actually happened, is in the question's language, leaks nothing, and leaves no misleading residue on failure or on an instant answer.

**Independent Test**: Script Greek questions, a no-match question, a request that fails mid-answer, and an instant answer; confirm lines are Greek for Greek questions, no `sources` line ever appears when the answer is `no relevant content found`, no line contains prompt/chunk text or another person's data, and no "still working" line survives a failure.

### Tests for User Story 2 (write first, confirm they fail)

- [x] T025 [P] [US2] Extend `tests/unit/ReasoningTextTests.cs`: a Greek question yields Greek sentences for **every** stage and an English question yields English ones (via the existing `AnswerLanguage` rule); the `sources` sentence with zero sources never claims a find; no sentence contains cross-person content.
- [x] T026 [P] [US2] Extend `tests/contract/QueryStreamContractTests.cs`: a **redaction** assertion that no field of any **progress** line contains a retrieved chunk's body text, the assembled prompt, a document file-system path, or a raw internal identifier (the `answer` event may carry the citation text the app already shows in Sources); and an assertion that a mid-stream failure produces a terminal `{"type":"error"}` event whose `error` string and `status` match what `POST /api/queries` returns for the same condition (FR-007, FR-012).
- [x] T027 [P] [US2] Extend `tests/unit/QueryViewModelReasoningTests.cs`: on a terminal `error` event, an unreachable workstation, and a stream that ends without a terminal event, `ReasoningSteps` is emptied and the status message is the same string the non-streaming path produces today (`model unavailable offline` / `cannot reach AI workstation: …`); and when the answer arrives before any `progress` event is handled, `ReasoningSteps` stays empty (no flicker, no placeholder — research.md R9) (FR-012, FR-013).

### Implementation for User Story 2

- [x] T028 [US2] Complete `ReasoningText` in `src/RAGGit.Retrieval/ReasoningText.cs`: render every stage in the question's language by reusing the existing language detection (the same rule `AnswerLanguage` applies to answers), keeping the four sentences free of prompt text, chunk bodies, identifiers, and configuration names (FR-006, FR-007).
- [x] T029 [US2] Harden emission in `src/RAGGit.Retrieval/QueryPipeline.cs`: guarantee `sources` is suppressed when nothing was retrieved, that `DocumentNames` contains only filenames the answer's own citations will expose and only from the caller's own retrieval, and that no stage is emitted once the terminal result is produced (FR-005, FR-007, FR-008).
- [x] T030 [US2] Emit the in-band failure in `src/RAGGit.Workstation.Api/Controllers/QueryController.cs`: if generation fails **after** the first event has been written, write a terminal `{"type":"error","error":…,"status":…}` event whose values match the non-streaming path's equivalent response, then end the stream; never append an `answer` event after it (FR-012).
- [x] T031 [US2] Implement client failure handling in `src/RAGGit.Client.Core/ViewModels/QueryViewModel.cs` and `src/RAGGit.Client.Core/Services/QueryApiClient.cs`: clear `ReasoningSteps` in a `finally` so no line survives any exit path; map a terminal `error` event, a non-2xx status, and an unreachable/timed-out workstation onto the existing status strings and severities without adding a new error vocabulary (FR-009, FR-012, FR-014).
- [x] T032 [US2] Integration test in `tests/integration/QueryStreamTests.cs`: a Greek question streams Greek progress sentences end-to-end; and a mid-stream generation failure (fake LLM throwing after the first stage) yields a terminal `error` event with the expected message and no `answer`.

**Checkpoint**: US1 and US2 both work independently.

---

## Phase 5: User Story 3 - Reasoning stays consistent with the finished answer (Priority: P3)

**Goal**: The finished exchange — answer, citations, Sources, copy action, conversation order, and history — is identical to today, with no reasoning anywhere.

**Independent Test**: Compare a conversation recorded before this feature with one recorded after: message order, answer text, citation list, status line, and the stored query row for each query are identical, and no reasoning is retrievable later.

### Tests for User Story 3 (write first, confirm they fail)

- [x] T033 [P] [US3] Unit tests in `tests/unit/QueryViewModelReasoningTests.cs`: `ReasoningSteps` rows never enter `Messages` or `ConversationStore`; the collection is cleared **before** the assistant message is appended; re-opening the exchange (a fresh ViewModel over the same `ConversationStore`) yields no reasoning rows.
- [x] T034 [P] [US3] Integration test in `tests/integration/QueryStreamTests.cs`: a question asked over `POST /api/queries/stream` produces the same `Queries` row as `POST /api/queries` for the same fakes (prompt, answer, retrieved ids, citation ids, mode); a `no relevant content found` answer over the stream stays exactly that string with empty citations; and no narration text is written to the persisted prompt or answer.
- [x] T035 [P] [US3] Verify the 005 per-person-history contracts are untouched: run `tests/contract/HistoryContractTests.cs`, `tests/contract/RecentDocumentsContractTests.cs`, and the offline subset (`QueryOfflineTests|OfflineIdentityTests|OfflineHistoryTests|CrossUserIsolationTests`) unchanged after the stream path exists.

### Implementation for User Story 3

- [x] T036 [US3] Guarantee history parity in `src/RAGGit.Workstation.Api/Controllers/QueryController.cs`: the stream path calls the **same** `SaveQueryAsync` with the same arguments (retrieved chunk ids, final answer including any did-you-mean suffix, citation ids, latency, effective mode) as `Post` — no stream-specific persistence path and no reasoning written anywhere.
- [x] T037 [US3] Guarantee client-side non-persistence in `src/RAGGit.Client.Core/ViewModels/QueryViewModel.cs`: reasoning rows are never added to `Messages`/`_conversation`, are not carried on `ChatMessage`, and the existing copy action continues to copy only the answer text.

**Checkpoint**: All three user stories are independently functional.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Formatting, end-to-end validation, and documentation.

- [x] T038 Run `dotnet csharpier format .` (it formats XAML too), then `dotnet csharpier check .` — must be clean.
- [x] T039 Run the full gate: `dotnet build RAGGit.Server.slnf -c Release` (and `dotnet build RAGGit.sln -c Release -p:Platform=x64` on Windows for the client), unit, contract, integration, and the offline subset (SC-005 grounding on the curated set, SC-008 offline suite).
- [x] T040 Execute [quickstart.md](quickstart.md) §2 (raw stream inspection with per-line timestamps), §3 (signed-in end-to-end: multi-line progress, Greek, no-match, failure, navigation-away, history re-open), and §4 (`winapp ui`: `ReasoningProgressList` present while in flight and absent afterwards; all 022/023 automation IDs still resolve). §2 also records SC-002's first-update latency.
- [ ] T041 [P] Measure SC-007 on the reference workstation: stream vs non-stream end-to-end time for the same questions, and record that the increase is ≤5% with the first update visible within 1 s (SC-002) and ≥2 updates on answers slower than 2 s (SC-001).
- [x] T042 [P] Document the feature's non-obvious behaviour in `AGENTS.md`: the new `POST /api/queries/stream` NDJSON endpoint, that `QueryPipeline` now owns the query orchestration for both transports, and that reasoning is transient (never persisted, never in history). Update the client-install runbook's description of the wait (`docs/runbooks/client-install.md`), which quoted the removed static line.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately.
- **Foundational (Phase 2)**: Depends on Setup — **BLOCKS all user stories**. The wire contract (T003–T007) and the pipeline extraction (T008–T012) are prerequisites for everything else.
- **US1 (Phase 3)**: Depends on Foundational. Delivers the endpoint, the client reader, and the UI region.
- **US2 (Phase 4)**: Depends on US1 (it hardens what US1 emits and how the client clears it).
- **US3 (Phase 5)**: Depends on US1; T036 is the parity guarantee for the endpoint US1 introduces.
- **Polish (Phase 6)**: Depends on all desired stories being complete.

### Within Each User Story

- Tests (T013–T016, T025–T027, T033–T035) MUST be written and FAIL before their implementation tasks.
- Models before services: T003–T006 before T009; T021 before T022.
- Services before endpoints: T009 before T010/T019; T017 before T018; T020 before T021.
- Core implementation before integration: T019/T020/T021 before T024.

### Story independence

- US1 alone is a viable MVP: it delivers the live reasoning region end-to-end.
- US2 adds language, redaction, and failure guarantees without touching US1's structure (it extends `ReasoningText`, adds the terminal `error` event, and hardens `finally`).
- US3 is a parity/guardrail story: T036/T037 are small, and its tests are the regression net for FR-010/FR-014.

---

## Parallel Opportunities

```bash
# Phase 2 — the four new types are separate files:
Task: "Add QueryStage enum in src/RAGGit.Core/Models/QueryStage.cs"          # T003
Task: "Add QueryProgress in src/RAGGit.Core/Models/QueryProgress.cs"         # T004
Task: "Add QueryStreamEvent envelope in src/RAGGit.Core/Models/QueryStreamEvent.cs"  # T005
Task: "Add QueryOutcome in src/RAGGit.Retrieval/QueryOutcome.cs"             # T006

# Phase 3 — the test files are separate:
Task: "Contract tests POST /api/queries/stream in tests/contract/QueryStreamContractTests.cs"  # T013
Task: "Unit tests NDJSON reader in tests/unit/QueryStreamClientTests.cs"                       # T014
Task: "Unit tests ViewModel reasoning lifecycle in tests/unit/QueryViewModelReasoningTests.cs" # T015
Task: "Unit tests progress text baseline in tests/unit/ReasoningTextTests.cs"                  # T016

# Phase 5 — independent verification work:
Task: "Unit tests non-persistence in tests/unit/QueryViewModelReasoningTests.cs"   # T033
Task: "Integration test history parity in tests/integration/QueryStreamTests.cs"   # T034
Task: "Verify 005 history contracts unchanged"                                     # T035
```

`tests/unit/QueryPipelineTests.cs` (T008) cannot run in parallel with T009; `tests/contract/QueryStreamContractTests.cs` is extended by T013 → T026 (sequential, same file).

---

## Implementation Strategy

### MVP First (User Story 1 only)

1. Phase 1 — setup and baseline.
2. Phase 2 — wire contract + pipeline extraction (the refactor must leave `POST /api/queries` identical).
3. Phase 3 — US1: stream endpoint, client reader, reasoning region.
4. **STOP and VALIDATE**: quickstart §2–§4 — multiple distinct lines before the answer, region gone on completion, history unchanged.
5. Demo/deploy — the wait is explained, which is the whole point of the feature.

### Incremental delivery

1. Setup + Foundational → one pipeline, one contract.
2. + US1 → live reasoning region (MVP).
3. + US2 → Greek text, redaction, failure and fast-answer behaviour.
4. + US3 → parity guarantees for history, citations, and conversation.
5. + Polish → formatting, full gate, SC-001/002/007 measurements, AGENTS.md.

---

## Notes

- [P] tasks touch different files and have no dependency on an incomplete task.- Every task names its exact file path; tests are written first and must fail before implementation (Constitution VI).
- `POST /api/queries` must remain byte-for-byte compatible after Phase 2 — T012 and T034 are the guards.
- Reasoning is transient (FR-014): any task that appears to persist it, log it, or add it to a history projection is out of scope — revisit [research.md](research.md) R7 instead.
- Keep all 022/023 automation IDs stable; add IDs only for new interactive elements (the reasoning region is non-interactive and needs only its container ID).
- Run `dotnet csharpier format .` after touching `.xaml`, and read `%LOCALAPPDATA%\RAGGit\crash.log` first when a page "won't open" (XAML parse errors fail silently).

---

## Verification log (2026-10-15)

Recorded evidence for the tasks left open above, and for the pre-existing failure T001 asked to identify.

**Gate (T039)** — `dotnet build RAGGit.Server.slnf -c Release` and `dotnet build RAGGit.sln -c Release -p:Platform=x64`: 0 errors. Unit **549/549**, contract **102/102**, offline subset **12/12**. Integration **82/83**.

**Pre-existing failure (not this feature)** — `RealLoopTests.RealLoop_PersistChunkRetrieve_CitedAnswer` fails with "Document ingestion failed" (the 50-page PDF reaches `Failed` during ingest). Reproduced identically from a **clean worktree at `3aeb195`** (no feature-032 changes present), and `git status` shows no ingest/retrieval file modified by this work. Environmental; unrelated to the reasoning stream.

**T040 §2 — real stream liveness (done, scratch instance)** — A scratch API was run against a **temp** database/LanceDB (dev data untouched) with real Ollama (`snowflake-arctic-embed2` + `qwen2.5:3b`), one small document uploaded. Observed arrival times: headers `65 ms`, `understanding`/`searching` `82 ms`, `sources` `283 ms` ("Found 1 relevant source: policy.txt", `sourceCount` 1), `composing` `283 ms`, `answer` **`23 793 ms`**. The progress lines therefore arrive *live*, ~23.5 s before the answer — the claim the in-process test host cannot prove.

**T040 §3 — signed-in end-to-end (done)** — Real-stack checks through the API: English question → stages then a cited answer (`mode: granular`); **Greek question → every progress line Greek** (Διαβάζω την ερώτησή σας…, Αναζητώ στη βιβλιοθήκη…, Βρέθηκε 1 σχετική πηγή: policy.txt, Συντάσσω την απάντηση…) with a Greek cited answer (FR-006, US2.1); **no-match question → no `sources` stage at all** and exactly `no relevant content found` with empty citations (FR-005, US2.2, US3.2).

**T040 §3/§4 — signed-in WPF client (`winapp ui`, done)** — A scratch HTTPS workstation on a temp database with real Ollama (`snowflake-arctic-embed2` + `qwen2.5:3b`), the client's Release output pointed at it, signed in as a local person account. `ui-tests.ps1` (kept in the session temp dir with the screenshots and `ui-results.json`) reports **18/18 PASS** against the Post-032 build: the region is absent when idle, appears while waiting, renders the exact sources line (`Found 1 relevant source: policy.txt`), is removed once the answer lands, leaves no residue after navigating away mid-answer, the cited answer bubble with its Sources expander renders, and the streamed question appears in per-person History. Frozen IDs verified live: `ChatInputBox`, `SendQuestionButton`, `QueryModeCombo`, `QueryClearConversationButton`, `NavAsk`/`NavDashboard`/`NavHistory`. A Greek question through the UI rendered Greek rows (`Διαβάζω την ερώτησή σας…`, `Βρέθηκε 1 σχετική πηγή: policy.txt`) and cleared on completion. Visual review of the in-flight screenshot: four ordered rows, older rows aligned on the 22 px gutter, spinner on the newest row only, no clipping/overlap/scrollbars, and `%LOCALAPPDATA%\RAGGit\crash.log` gained no entries (so no page-construction XAML error). Server log corroborates: the fresh build issues `POST /api/queries/stream → 200 application/x-ndjson`, while the stale build used earlier issued `POST /api/queries` — which is why the first UI attempt showed no region at all.

**Known pre-existing UI facts observed (not defects of this feature)** — `Sources` expanders start collapsed (so the citation text is not in the automation tree until opened), `RaggitBrandLabel` is not surfaced to UIA, and nav items expose `SelectionItemPattern` only (they answer to `click`, not `invoke`).

**Still outstanding** — **T041** (SC-007 benchmark on the reference workstation). Best evidence so far: the stream's own overhead is negligible — a scratch run measured wall `23 794 ms` against the pipeline's own `latencyMs` `23 748 ms` (~0.2%), the first update lands at ~82 ms, and the UI runs above show first feedback well under 1 s on a real answer taking 3–23 s.

