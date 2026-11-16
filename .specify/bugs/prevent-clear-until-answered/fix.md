# Bug Fix: Clear is unavailable until the in-flight question ends

- **Slug**: prevent-clear-until-answered
- **Fixed**: 2026-11-16
- **Assessment**: ./assessment.md
- **Status**: applied

## Summary

The Clear-conversation action is now gated on the Ask surface being idle. While a question is in flight the header button is disabled and the command refuses `CanExecute`, so a clear can no longer empty the question and let the late answer land as an orphan bubble; the user stops the question first (Stop control in the input bar), after which Clear works as before.

## Changes

| File | Change | Notes |
|------|--------|-------|
| `src/RAGGit.Client.Core/ViewModels/QueryViewModel.cs` | modified | `ClearConversation` gains `CanExecute = nameof(CanClearConversation)` with `CanClearConversation => !IsBusy`, plus an in-method `if (IsBusy) return;` so direct `Execute` callers cannot bypass the gate; `OnIsBusyChanged` now re-raises `ClearConversationCommand`. |
| `src/RAGGit.Client.WPF/Views/Pages/QueryPage.xaml` | modified | `QueryClearConversationButton` gets `IsEnabled="{Binding IsBusy, Converter={StaticResource InverseBooleanConverter}}"` (app-level converter from `App.xaml`, same idiom as the other pages). Automation ID unchanged. |
| `tests/unit/QueryViewModelTests.cs` | modified | Two new tests (below). |

The existing `Click` handler in `QueryPage.xaml.cs` already checks `CanExecute` before executing, so it stays as the programmatic-call guard and needs no change; with `IsEnabled` false the click never fires in the first place.

## Diff Highlights

```csharp
[RelayCommand(CanExecute = nameof(CanClearConversation))]
private void ClearConversation()
{
    // CanExecute gates the UI; this keeps direct Execute callers honest.
    if (IsBusy)
    {
        return;
    }

    Messages.Clear();
    _conversation.ReplaceAll(Array.Empty<ChatMessage>());
    QueryText = string.Empty;
}

/// <summary>
/// Clearing while an answer is in flight would empty the question first and
/// let the late answer land in an empty conversation (an answer with no
/// question); leave the wait to Stop, then clear.
/// </summary>
private bool CanClearConversation => !IsBusy;
```

```xml
<ui:Button
    ...
    Click="OnClearConversationClicked"
    IsEnabled="{Binding IsBusy, Converter={StaticResource InverseBooleanConverter}}"
>
```

## Tests Added or Updated

- `tests/unit/QueryViewModelTests.cs::ClearConversation_WhileBusy_CannotExecute` — executable when idle, blocked while `IsBusy`, executable again afterwards.
- `tests/unit/QueryViewModelTests.cs::ClearConversation_StaysBlockedUntilTheAnswerEnds_ThenClears` — a hanging ask blocks Clear; stopping the question leaves the user message in `Messages` and the store; Clear then empties both and the input.
- Existing `ClearConversation_ClearsMessagesAndStore_AndResetsInput` and `ClearConversation_WhenEmpty_IsNoOp` still pass, proving the idle path did not become stricter.

## Local Verification

- Commands run:
  - `dotnet tool restore` + `dotnet csharpier format <3 touched files>` → formatted; `dotnet csharpier check .` → `Checked 312 files`, clean.
  - `dotnet test tests/unit/RAGGit.Tests.Unit.csproj -c Release --filter "FullyQualifiedName~ClearConversation_|FullyQualifiedName~StopAnswer_"` → **7 passed, 0 failed** (4 clear tests, 3 stop tests).
  - `dotnet test tests/unit/RAGGit.Tests.Unit.csproj -c Release --no-build` → **572 passed, 0 failed** (whole unit suite).
  - `dotnet test tests/client/RAGGit.Tests.Client.csproj -c Release` → **103 passed, 0 failed** (page construction + XAML lint; the new `IsEnabled` binding raises no binding error and the frozen Clear automation ID survives).
- Manual checks: none in a signed-in app; behaviour is pinned by the ViewModel tests and the client construction suite.

## Deviations from Assessment

None. The implementation follows the preferred remediation exactly, including keeping the `Click` handler and omitting an explanatory tooltip (the visible Stop control is the escape hatch, as the assessment recommended).

## Follow-ups

- `RelayCommand.Execute` called directly (bypassing `CanExecute`) still clears while busy; today the only production caller is `OnClearConversationClicked`, which guards. Any future keyboard shortcut or programmatic caller must check `CanExecute` or the orphan-answer race returns.
- `LoadHistoryAsync` shares `IsBusy`, so if it is ever wired into the Ask page, Clear will also be disabled while history loads — consistent with "wait for the view to settle", but worth remembering.
- The live signed-in UI reproduction was not exercised (same limitation as the previous fix's verification): stop → clear → ask is proven at the ViewModel level only.
- (Review follow-up, 2026-11-16) `ClearConversation` now also returns early when `IsBusy`, so the note above about direct `Execute` callers bypassing the gate no longer applies.
