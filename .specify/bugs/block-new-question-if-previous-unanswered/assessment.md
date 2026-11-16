# Bug Assessment: Ask screen locks the next question until the previous answer arrives

- **Slug**: block-new-question-if-previous-unanswered
- **Created**: 2026-11-16
- **Source**: pasted text
- **Verdict**: valid
- **Severity**: medium

## Report (verbatim)

> "Users cannot submit a new question until their previous question has been answered"

## Symptom

While a question is in flight on the Ask page, the send control refuses to run and the Enter key does nothing, so the employee cannot ask a follow-up or correction until the current answer (or a failure) arrives. Expected: the employee can either stop the in-flight question or immediately ask the next one, instead of waiting out a broad query that can legitimately run for minutes.

## Reproduction

1. Sign in as an Employee or Admin and open **Ask**.
2. Ask a broad question (a person summary or a "give me everything about…" prompt — the class of prompt AGENTS.md measures at 5–11 min on the reference Skylake box).
3. While the reasoning rows are still appearing, type a second question and press Enter, or click the send button.
4. Nothing happens: the send button is disabled and Enter is ignored; the input text is accepted but can never be submitted until the first answer arrives or the request fails/times out.
5. `[NEEDS CLARIFICATION: whether the reporter hit this on the streaming Ask page only, or also via "Ask again" from History (same gate, `ReaskAsync`)]`.

## Suspected Code Paths

- `src/RAGGit.Client.Core/ViewModels/QueryViewModel.cs:412-422` — `AskQuestionAsync` returns early when `IsBusy`, and flips `IsBusy`/`IsAskEnabled` for the whole request; there is no cancellation token anywhere in the path.
- `src/RAGGit.Client.Core/ViewModels/QueryViewModel.cs:596` — `CanAsk => IsAskEnabled && !IsBusy && !string.IsNullOrWhiteSpace(QueryText)`, so the send command reports `CanExecute == false` for the entire answer. `OnIsBusyChanged`/`OnIsAskEnabledChanged` (:129-139) re-raise it.
- `src/RAGGit.Client.Core/ViewModels/QueryViewModel.cs:545-550` — the `finally` block is the only place busy is cleared; it runs strictly after the stream completes, errors, or times out.
- `src/RAGGit.Client.Core/ViewModels/QueryViewModel.cs:577-594` — `ReaskAsync` has the same guard, so "Ask again" from History is equally blocked.
- `src/RAGGit.Client.WPF/Components/ChatControl.xaml:287-296` — the input box is **not** disabled, so most of the wait is spent accepting a draft that cannot be sent (the affordance mismatch that makes the lockout feel like a bug rather than a rule).
- `src/RAGGit.Client.WPF/Components/ChatControl.xaml:297-321` — the send button is bound to `SendCommand`, grayed by `CanExecute`, and only morphs its icon to a `ProgressRing`; it never becomes a stop action.
- `src/RAGGit.Client.WPF/Components/ChatControl.xaml.cs:198-206` — `InputBox_KeyDown` checks `SendCommand?.CanExecute(null)` and silently swallows Enter when it is false.
- `src/RAGGit.Client.Core/Services/QueryApiClient.cs:90-125` — `StreamAsync` already takes an `[EnumeratorCancellation] CancellationToken`, but the ViewModel calls it as `_apiClient.StreamAsync(question)` (`QueryViewModel.cs:440`) and passes nothing. The cancel branch at :122-125 lacks the `when (!cancellationToken.IsCancellationRequested)` filter the reader path already has (:204), so a deliberate cancel during `SendAsync` would surface as "request timed out".
- `src/RAGGit.Workstation.Api/Controllers/QueryController.cs:277-291` — the server persists a query only after `ExecuteAsync` completes, so an abandoned/cancelled question leaves no History row (an abandoned ask is clean to discard, and persistence is not a blocker for the fix).
- `specs/032-reasoning-progress/spec.md:70` — the current behaviour is a documented deferral ("What happens when the employee submits a second question while the first is still in flight? The behaviour must match today's busy-state rules…"), i.e. the gate is deliberate, not accidental.

## Root Cause Hypothesis

Confidence: **high**. The Ask surface has a single busy gate with no exit: `IsBusy` disables `AskCommand` for the full duration of `StreamAsync`, and the only cancellation in the query path is the DI-configured 10-minute `HttpClient.Timeout`. Nothing in the ViewModel, `QueryApiClient`, or `ChatControl` can abort an in-flight stream, and the input box keeps accepting text that the command layer will not submit. The streaming client and the workstation pipeline both already accept a `CancellationToken` end-to-end (`QueryApiClient.StreamAsync`, `QueryPipeline.ExecuteAsync`, `OllamaLlmClient.ChatAsync`), so the missing piece is client-side ownership of a per-question token and a UI affordance to use it. Feature 032's clarification deferred this question rather than answering it; the report is the user-visible consequence of that deferral on a box where a broad answer takes minutes.

## Proposed Remediation

**Preferred**: give the in-flight question an explicit stop, then let the input work normally.

1. In `QueryViewModel`, own a per-question `CancellationTokenSource` (`_askCts`, replaced at the start of each `AskQuestionAsync`, disposed in `finally`) and pass `_askCts.Token` into `_apiClient.StreamAsync(question, cancellationToken: _askCts.Token)`.
2. Add `[RelayCommand(CanExecute = nameof(CanStop))] private void StopAnswer()` that cancels `_askCts`; `CanStop => IsBusy && _askCts is not null`. Keep the existing `CanAsk` gate for the *send* action — this avoids `AsyncRelayCommand`'s default non-concurrent-execution guard entirely, because stopping is a separate synchronous command.
3. Classify cancellation distinctly in `AskQuestionAsync`: `catch (OperationCanceledException) when (_askCts?.IsCancellationRequested == true)` → clear reasoning, drop the unanswered user message from `Messages` **and** `_conversation` (the server persisted nothing, so the exchange never happened), set a neutral status ("Question stopped.") and re-enable the surface. Everything else keeps today's error vocabulary.
4. Add the matching `when (!cancellationToken.IsCancellationRequested)` filter to `QueryApiClient.StreamAsync`'s `TaskCanceledException` catch at :122, mirroring `ReadLineAsync` (:204).
5. In `ChatControl`, add a `StopCommand` dependency property and render a second 44 px `ui:Button` (`AutomationProperties.AutomationId="StopQuestionButton"`, visible only while `IsBusy`) in place of the send button. Keep `SendQuestionButton` and `ChatInputBox` IDs untouched — they are frozen. Because the button lives in the same 8 px gutter column as the send button, no margin is needed. Keep the input enabled so the user can compose the next question while waiting.
6. Optional but cheap and directly relevant: cancel the in-flight ask from `QueryPage` when the page unloads, so navigating away frees the workstation instead of letting an orphaned stream run to completion in a discarded transient ViewModel.

This preserves the existing conversation contracts (a stopped question leaves no dangling bubble), needs no API or schema change, and turns the blocking wait into "stop, then ask".

**Alternatives**:

- **Supersede on submit** — let a new submission cancel the previous stream and start the new one in the same click. Best matches the literal complaint, but requires `[RelayCommand(AllowConcurrentExecutions = true)]` plus an explicit generation/epoch counter: the abandoned run's `finally` would otherwise clear `IsBusy`/`IsAskEnabled` *after* the new run has set them, and its late answer must not overwrite the new answer. Higher risk for a small interaction gain; worth considering only if the product wants one-click interruption.
- **Queue the question** — accept the next question and run it when the current answer lands. Avoids cancellation semantics but doubles the wait on a box that already answers in minutes, needs a reasoning-account-per-queued-question separation, and is not what the reporter is asking for.

**Files likely to change**:

- `src/RAGGit.Client.Core/ViewModels/QueryViewModel.cs`
- `src/RAGGit.Client.Core/Services/QueryApiClient.cs`
- `src/RAGGit.Client.WPF/Components/ChatControl.xaml`
- `src/RAGGit.Client.WPF/Components/ChatControl.xaml.cs`
- `src/RAGGit.Client.WPF/Views/Pages/QueryPage.xaml` / `.xaml.cs` (only for the stop binding and the optional unload cancel)

**Tests to add or update**:

- `tests/unit/QueryViewModelTests.cs`: while `IsBusy`, `StopAnswerCommand` can execute and the ask command stays gated; a stopped question leaves one user message removed and no assistant message; status is neutral, `IsBusy == false`, `IsAskEnabled == true`.
- `tests/unit/QueryViewModelReasoningTests.cs`: a stalled stream (a handler that never completes until its token is cancelled) can be stopped, reasoning is cleared, and the stopped exchange never enters the conversation store (mirroring `Ask_TruncatedStream_FailsInsteadOfShowingAnEmptyAnswer`).
- `tests/unit/QueryStreamClientTests.cs`: caller cancellation surfaces as cancellation, not as "request timed out", including cancellation during the initial `SendAsync` (covers the missing filter at `QueryApiClient.cs:122`).
- `tests/client/ScreenLoadTests.cs` / the XAML lint: the new `StopQuestionButton` must carry an automation ID and the ChatControl must still construct.

## Risks & Considerations

- **Cancellation is cooperative**: the client abort propagates to Kestrel's `HttpContext.RequestAborted` and into `OllamaLlmClient.ChatAsync`, but Ollama may finish the current token batch before it stops; the UI must not imply instant server relief.
- **Shared state ordering**: the `finally` block clears busy unconditionally today. Any future supersede/queue implementation must guard it with a generation counter or a "still the owner of this ask" check, or an abandoned stream will clear the new question's busy state.
- **Automation IDs are frozen** (022/023/029/032): add a new ID for stop, do not rename `SendQuestionButton`/`ChatInputBox`.
- **Conversation store**: removing the stopped question must touch both `Messages` and `_conversation.Messages`, otherwise leaving and returning to Ask resurrects the unanswered prompt (`QueryViewModel` re-seeds from the store in its constructor).
- **Client timeout vs server timeout**: `HttpClient.Timeout` is 10 min while `Ollama:TimeoutMs` is 15 min, so a broad question can still end in the client-side "request timed out" message. Stopping is the user-facing fix; unequal timeouts remain a separate tuning concern.
- **No API breakage**: the streaming endpoint's contract is unchanged; an aborted stream simply never reaches its terminal event, which the server already handles (`catch (OperationCanceledException)` when the request is aborted).
- **History**: a stopped question is not persisted (`SaveQueryAsync` runs only after `ExecuteAsync`), so "stop" cannot create a partial History row.

## Open Questions

- `[NEEDS CLARIFICATION: which interaction is wanted — an explicit Stop control (preferred here), a "new question supersedes the old one" submission, or a queue?]`
- `[NEEDS CLARIFICATION: should a stopped question remain visible in the conversation as an unanswered prompt, or be removed? This assessment assumes removed, since the server recorded nothing.]`
- `[NEEDS CLARIFICATION: is navigating away mid-answer (which already abandons the question) expected to cancel it on the workstation, or is the orphaned stream acceptable?]`
