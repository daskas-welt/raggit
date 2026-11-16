# Bug Verification: Clear is unavailable until the in-flight question ends

- **Slug**: prevent-clear-until-answered
- **Tested**: 2026-11-16
- **Assessment**: ./assessment.md
- **Fix**: ./fix.md
- **Result**: partial

## Summary

The automated equivalent of the reported state machine holds: while a question is in flight the Clear command refuses `CanExecute`, the question survives, and clearing works again immediately after the wait ends (Stop). No regressions were found in the unit or client suites. The original manual UI reproduction (signed-in WPF client, click Clear mid-answer) was **not** exercised — no workstation is running — so the disabled-header-button appearance is confirmed only by page construction/XAML lint and code inspection.

## Checks Performed

| Check | Command / Action | Result | Notes |
|-------|------------------|--------|-------|
| Reproduction (post-fix) | Live signed-in client: ask a slow question, click Clear during the wait (assessment steps 2–5) | skipped | No API listening on `8080`/`5001`; the flow needs a seeded library and a real model. Skipped as expensive/network-dependent per the command's rules. |
| Reproduction equivalent | `dotnet test tests/unit/RAGGit.Tests.Unit.csproj -c Release --filter "FullyQualifiedName~ClearConversation_\|FullyQualifiedName~StopAnswer_"` | pass | 7/7: gate blocks while busy, a hanging ask leaves `Messages`/store intact, and Clear works after Stop. |
| New / updated tests | same as above | pass | `Passed: 7, Failed: 0` in 128 ms. |
| Regression suite (unit) | `dotnet test tests/unit/RAGGit.Tests.Unit.csproj -c Release --no-build` | pass | `Passed: 570, Failed: 0, Skipped: 0` in 4 s; includes both pre-existing clear tests (idle clear, empty no-op) and every Ask/Stop/History test. |
| Regression suite (client UI) | `dotnet test tests/client/RAGGit.Tests.Client.csproj -c Release` | pass | `Passed: 103, Failed: 0` in 2 s; constructs every page/dialog and runs the XAML lint, so the new `IsEnabled` + `InverseBooleanConverter` binding resolves and `QueryClearConversationButton` keeps its automation ID. |
| Lint / format | `dotnet tool restore` + `dotnet csharpier check .` | pass | `Checked 311 files in 1326ms` — no C# or XAML drift. |
| Build (server filter) | `dotnet build RAGGit.Server.slnf -c Release` | not-run | No server files changed; the unit-test build compiles `Client.Core`, `Core`, `Retrieval`, and `Workstation.Api`, and the client-test build compiles the WPF app. |

## Output Excerpts

```
Passed!  - Failed: 0, Passed: 7, Skipped: 0, Total: 7, Duration: 128 ms - RAGGit.Tests.Unit.dll (net10.0)
Passed!  - Failed: 0, Passed: 570, Skipped: 0, Total: 570, Duration: 4 s - RAGGit.Tests.Unit.dll (net10.0)
Passed!  - Failed: 0, Passed: 103, Skipped: 0, Total: 103, Duration: 2 s - RAGGit.Tests.Client.dll (net10.0)
Checked 311 files in 1326ms.
```

## Residual Risks

- **Live UI not exercised.** The header Clear button's disabled visual and the `IsEnabled` toast/binding at runtime are unverified by eye; only construction and XAML lint prove the binding resolves.
- **Direct command execution bypasses the gate.** `ClearConversationCommand.Execute` called programmatically ignores `CanExecute`; the only production caller (`OnClearConversationClicked`) guards, but a future keyboard shortcut or automation that invokes `Execute` directly would reintroduce the orphan-answer race.
- **Gate shares `IsBusy`.** Any future operation that sets `IsBusy` (e.g. wiring `LoadHistoryAsync` into the page) will also disable Clear for its duration; acceptable but not explicitly designed for.
- **Failure/timeout window.** The gate covers the whole `IsBusy` window, so Clear is also disabled during a failing or timing-out ask until it settles — the assessment's open question assumed this, and it is consistent with the visible Stop control.
- **Two-step flow.** Clearing a waited-out answer still requires Stop → Clear; the single-click "clear cancels the ask" alternative was deliberately not implemented.

## Recommendation

Close the bug on the automated evidence, with the understanding that the disabled button was not seen in a running client. If a short live pass is possible, start the dev API with a seeded library, ask a broad question, and confirm Clear is greyed out until Stop — that would remove the only gap. Otherwise the state machine, regression suites, and format checks all pass, and the residual risks are documented.

## Addendum (post-verification review, 2026-11-16)

- The "direct command execution bypasses the gate" risk above is closed: `ClearConversation` now returns early when `IsBusy`, so a programmatic `Execute` cannot clear mid-answer.
- Re-verified after the review pass: unit **572 passed**, client **103 passed**, `dotnet csharpier check .` clean (312 files).
