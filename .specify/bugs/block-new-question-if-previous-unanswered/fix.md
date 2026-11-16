# Bug Fix: Stop an in-flight question instead of locking the Ask input

- **Slug**: block-new-question-if-previous-unanswered
- **Fixed**: 2026-11-16
- **Assessment**: ./assessment.md
- **Status**: applied

## Summary

The Ask surface no longer locks out the employee for the length of an answer: a Stop button appears beside the send control while a question is in flight, cancels the stream through a per-question `CancellationTokenSource`, clears the reasoning rows, and re-enables the input immediately. The query client now also distinguishes caller cancellation from a workstation timeout (cancel during `SendAsync` used to be reported as "request timed out").

## Changes

| File | Change | Notes |
|------|--------|-------|
| `src/RAGGit.Client.Core/ViewModels/QueryViewModel.cs` | modified | Owns `_askCts` per question; passes its token to `StreamAsync`; new `StopAnswerCommand` (`CanStop => IsBusy && _askCts is not null`); stops reading at the terminal `answer` event so a late stop cannot discard a delivered answer; new `catch (OperationCanceledException) when (cts.IsCancellationRequested)` sets "Question stopped." (Informational); `finally` clears/disposes the token source and notifies the stop command. |
| `src/RAGGit.Client.Core/Services/QueryApiClient.cs` | modified | Both `QueryAsync` and `StreamAsync` `TaskCanceledException` catches now carry `when (!cancellationToken.IsCancellationRequested)`, mirroring `ReadLineAsync`, so a deliberate stop propagates instead of becoming "AI workstation unavailable: request timed out". |
| `src/RAGGit.Workstation.Api/Controllers/QueryController.cs` | modified | A request aborted by the client (Stop, navigation) is logged at Information ("stopped by the client") instead of falling into the general `LogError` catch. |
| `src/RAGGit.Client.WPF/Components/ChatControl.xaml` | modified | The send button keeps `SendQuestionButton` and its 032 in-flight spinner; a `StopQuestionButton` (44×44, `Stop24`, "Stop answer") sits in a new column that is shown only while busy. |
| `src/RAGGit.Client.WPF/Components/ChatControl.xaml.cs` | modified | New `StopCommand` dependency property; `IsBusy` callback zeroes the stop gutter track when idle (no layout Margin). |
| `src/RAGGit.Client.WPF/Views/Pages/QueryPage.xaml` | modified | Binds `StopCommand="{Binding StopAnswerCommand}"`. |
| `tests/unit/HangingHandler.cs` | added | Shared handler that holds a request open until the caller cancels (used by both ViewModel and API-client tests). |
| `tests/unit/QueryViewModelTests.cs` | modified | Stop tests plus a handler-based ViewModel factory; direct-`Execute` clear guard covered. |
| `tests/unit/QueryViewModelReasoningTests.cs` | modified | New stop test plus `StallingBodyHandler`/`StallingStream`; new test that an answer is applied without waiting for the stream to close. |
| `tests/unit/QueryStreamClientTests.cs` | modified | Caller-cancellation tests for both `StreamAsync` and `QueryAsync`. |

## Diff Highlights

```csharp
// QueryViewModel.AskQuestionAsync
var cts = new CancellationTokenSource();
_askCts = cts;
StopAnswerCommand.NotifyCanExecuteChanged();
...
await foreach (
    var streamEvent in _apiClient.StreamAsync(question, cancellationToken: cts.Token)
)
```

```csharp
catch (OperationCanceledException) when (cts.IsCancellationRequested)
{
    // The workstation persists only completed queries, so nothing reaches
    // History; the prompt stays visible unanswered (same shape as a failed ask).
    SetStatusMessage("Question stopped.", "Informational");
}
```

```csharp
[RelayCommand(CanExecute = nameof(CanStop))]
private void StopAnswer() => _askCts?.Cancel();

private bool CanStop => IsBusy && _askCts is not null;
```

## Tests Added or Updated

- `tests/unit/QueryViewModelTests.cs::StopAnswer_CancelsTheInFlightQuestion_AndReenablesTheInput` — a hanging request is stopped; the VM reports busy false, input enabled, neutral status, stop no longer executable, prompt kept once, store untouched by an answer, and `AskCommand` executable for a follow-up.
- `tests/unit/QueryViewModelTests.cs::StopAnswer_WhenIdle_CannotExecute` — stop is gated while no question is in flight.
- `tests/unit/QueryViewModelReasoningTests.cs::StopAnswer_ClearsReasoning_AndLeavesThePromptUnanswered` — progress rows appear, then a stop clears them, leaves no answer, and shows the neutral status (FR-014: reasoning never survives the exchange).
- `tests/unit/QueryViewModelReasoningTests.cs::Ask_AppliesTheAnswerWithoutWaitingForTheStreamToClose` — the terminal `answer` is applied even when the connection stays open, so a stop during wind-down cannot discard a delivered answer.
- `tests/unit/QueryStreamClientTests.cs::StreamAsync_CallerCancellation_SurfacesAsCancellationNotATimeout` and `QueryAsync_CallerCancellation_SurfacesAsCancellationNotATimeout` — pin the cancellation filters: a caller-cancelled request throws `OperationCanceledException`, not the "request timed out" `HttpRequestException`.

## Local Verification

- Commands run:
  - `dotnet tool restore` + `dotnet csharpier format <changed files>` → formatted; `dotnet csharpier check .` → 312 files clean.
  - `dotnet test tests/unit/RAGGit.Tests.Unit.csproj -c Release --no-build` → **572 passed, 0 failed** (whole unit suite, 4 s).
  - `dotnet test tests/client/RAGGit.Tests.Client.csproj -c Release` → **103 passed, 0 failed** (every page/dialog constructed on an STA thread plus the XAML lint, so `StopQuestionButton`, `Stop24`, and the reflowed stop gutter parse and comply).
- Manual checks: none in the running app (no signed-in API session was started); the behaviour is covered by the ViewModel and client-screen tests.

## Deviations from Assessment

1. **The stopped prompt stays in the conversation.** The assessment proposed removing the unanswered user message from `Messages` and `_conversation`. Removing it would silently discard the text the employee had just typed (the input is cleared when the ask starts), which is a data-loss regression; the existing failure path already leaves the prompt unanswered (asserted by `Ask_TruncatedStream_FailsInsteadOfShowingAnEmptyAnswer`). The stopped question is kept and the status line says "Question stopped."
2. **The optional "cancel on page unload" item was not implemented.** It is a separate lifecycle concern (navigation), not part of the reported lockout; recorded as a follow-up instead of widening scope.

`QueryApiClient.StreamAsync` needed the cancellation filter recorded in the assessment's code-path list; no other files changed. A review pass later added the send-button spinner back (stop sits beside it), made the answer terminal stop the read loop, filtered `QueryAsync` the same way, and downgraded client-aborted log noise on the workstation; those are reflected above.

## Follow-ups

- Cancel the in-flight ask from `QueryPage` when the page unloads, so navigating away frees the workstation instead of letting an orphaned stream finish in a discarded transient ViewModel (the same window that motivated this bug).
- The initial greeting/test-count note in `AGENTS.md` still cites 549 unit tests; the suite now reports 572 (dated baseline, not updated by this fix).
- If product wants one-click interruption (submit the typed question and abandon the previous one in the same action), that is the assessment's "supersede" alternative and needs a generation guard so the abandoned run cannot clear the new run's busy state.
- Consider aligning `HttpClient.Timeout` (10 min) with `Ollama:TimeoutMs` (15 min) so a broad answer cannot end in the client-side timeout before the workstation gives up.
