# Bug Assessment: Clear conversation is available while an answer is still in flight

- **Slug**: prevent-clear-until-answered
- **Created**: 2026-11-16
- **Source**: pasted text
- **Verdict**: valid
- **Severity**: low

## Report (verbatim)

> "Users should not be able to clear a conversation until their previous question has been answered"

## Symptom

While a question is in flight, the header's Clear action empties the conversation (view and in-session store). When the answer arrives it is appended to the now-empty conversation, producing an answer bubble with citations but no question above it, and the question text is gone until the exchange is re-read from History. Expected: Clear is unavailable until the in-flight question has been answered (or stopped), so a cleared conversation never gains a late answer.

## Reproduction

1. Sign in and open **Ask**.
2. Ask a broad question so the answer takes a while (the reasoning rows appear).
3. Before the answer arrives, click **Clear** in the page header (enabled; no confirmation).
4. The conversation empties and the empty state shows; the in-flight spinner/stop button keeps running.
5. When the answer arrives, an assistant bubble (with Sources) appears in the otherwise empty conversation. The original question is not in the view or the in-session store; only the server's History still has the pair.
6. `[NEEDS CLARIFICATION: whether the reporter also wants Clear disabled while the ask is failing/timing out (it is the same `IsBusy` window) or only while a normal answer is pending.]`

## Suspected Code Paths

- `src/RAGGit.Client.Core/ViewModels/QueryViewModel.cs:605-611` — `ClearConversation` has **no** `CanExecute` predicate; it unconditionally clears `Messages`, replaces the shared store with an empty set, and empties `QueryText` (discarding any next-question draft typed during the wait).
- `src/RAGGit.Client.Core/ViewModels/QueryViewModel.cs:141-146` — `OnIsBusyChanged` re-raises `AskCommand`, `UseSuggestedPersonCommand` and `StopAnswerCommand`, but nothing tells the clear command about `IsBusy`.
- `src/RAGGit.Client.Core/ViewModels/QueryViewModel.cs:442-505` — `AskQuestionAsync` appends the user message before awaiting the stream and appends the assistant message at completion; neither half is aware the conversation was cleared in between, so the late write is an orphan answer.
- `src/RAGGit.Client.WPF/Views/Pages/QueryPage.xaml:51-64` — the Clear button is bound to a `Click` handler (not to the command), so even a command-level `CanExecute` gate would not disable it by itself.
- `src/RAGGit.Client.WPF/Views/Pages/QueryPage.xaml.cs:33-43` — `OnClearConversationClicked` guards with `CanExecute` (so a command gate would make the handler a no-op) and then focuses the input unconditionally.
- `specs/028-pages-ux-polish/contracts/ui-contracts.md:79-83` — U5.3 requires a clear to empty view and store with no stale messages; an answer arriving after the clear is exactly the stale state the contract prohibits, while nothing in 028/032 forbids gating Clear on the busy state.
- `src/RAGGit.Client.Core/Services/ConversationStore.cs:39-46` — `ReplaceAll(empty)` is the store-side clear; nothing marks the in-flight question as abandoned, so the completion path still writes to it.

## Root Cause Hypothesis

Confidence: **high**. Clear is a pure view/store reset with no knowledge of the in-flight `AskQuestionAsync`, and the completion path is a fire-and-continue sequence that appends its answer whenever the stream ends. Any clear during the wait therefore races the answer: the clear wins for the question, the completion wins for the answer. Nothing in the ViewModel invalidates the in-flight ask (no generation/epoch), and the button is click-driven so it is always enabled. The Stop control added for the previous bug gives the natural sequencing — stop first, then clear — but Clear itself is still ungated.

## Proposed Remediation

**Preferred**: make Clear unavailable while a question is in flight, and let Stop be the way out.

1. In `QueryViewModel`, add `CanExecute = nameof(CanClearConversation)` to `[RelayCommand] ClearConversation` with `private bool CanClearConversation => !IsBusy;`, and call `ClearConversationCommand.NotifyCanExecuteChanged()` from `OnIsBusyChanged`.
2. In `QueryPage.xaml`, bind `IsEnabled="{Binding IsBusy, Converter={StaticResource InverseBooleanConverter}}"` on `QueryClearConversationButton` (`InverseBooleanConverter` is an app-level resource in `App.xaml`; no local declaration needed). Keep the frozen `AutomationId`. Add a tooltip while busy only if the team wants the reason surfaced — a disabled control's tooltip is not discoverable, so prefer relying on the visible Stop button in the input bar.
3. Keep the existing `Click` handler and its `CanExecute` guard; with `IsEnabled` false the click never fires, and the guard keeps programmatic callers honest. `Chat.FocusInput()` then only runs on a real clear.
4. Tests: gate test (`ClearConversationCommand.CanExecute` false while `IsBusy`, true before/after), a behavioral test that a clear attempted during the wait leaves `Messages`/store untouched and the answer still arrives, and a regression test that the normal clear-after-answer path still empties view, store and input.

**Alternatives**:

- **Clear cancels the in-flight ask, then empties.** One click, no dead-end; but it silently abandons the answer the user may still want and contradicts the report's "should not be able to clear until answered". Only worth it if product prefers a single destructive action over two.
- **Defer the clear until the stream terminates.** Keeps the single click but the conversation does not empty when clicked (worse feedback) and needs the same generation guard the supersede alternative needed.

**Files likely to change**:

- `src/RAGGit.Client.Core/ViewModels/QueryViewModel.cs`
- `src/RAGGit.Client.WPF/Views/Pages/QueryPage.xaml`
- `tests/unit/QueryViewModelTests.cs`
- `tests/client/ScreenLoadTests.cs` / the XAML lint (construction only; no change expected)

**Tests to add or update**:

- `QueryViewModelTests`: `ClearConversation_WhileBusy_CannotExecute`; `ClearConversation_WhileAnswering_LeavesConversationUntouched` (simulate `IsBusy`, assert `Messages` and store still hold the question, then complete the answer and assert the pair is intact).
- Keep `ClearConversation_ClearsMessagesAndStore_AndResetsInput` and `ClearConversation_WhenEmpty_IsNoOp` green (they prove the idle path did not become stricter).
- Client suite: the Clear button must still construct with its automation ID and the new `IsEnabled` binding must not raise a binding error.

## Risks & Considerations

- **History load shares `IsBusy`.** `LoadHistoryAsync` also sets `IsBusy` (currently no production caller), so the gate would also disable Clear during a history load. That is the same "wait for the view to settle" rule and is acceptable; note it if the method is wired later.
- **Wrong-checks-wrong**: gating on `IsBusy` assumes the only writer is the ask path. Any future background write must keep using `IsBusy` or move to an explicit "ask in flight" flag.
- **Accessibility**: a disabled control must have a discoverable reason; the Stop button in the same view is the escape hatch, so no extra dialog is needed. Do not use `Opacity` to dim — the theme raises the disabled visual.
- **Automation IDs are frozen**: `QueryClearConversationButton` must keep its ID and name, and remain reachable in UI automation while enabled.
- **No API or persistence change**: clears are client-side only; server History is untouched either way.

## Open Questions

- `[NEEDS CLARIFICATION: disable Clear for the whole `IsBusy` window (answer, failure, timeout, and any history load), or only while a normal answer is pending?]`
- `[NEEDS CLARIFICATION: after a failed or stopped ask, should Clear stay available immediately (this assessment assumes yes, since nothing is in flight)?]`
- `[NEEDS CLARIFICATION: should the disabled state explain itself (tooltip/aria text) or is the visible Stop button sufficient?]`
