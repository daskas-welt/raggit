# Bug Verification: Stop an in-flight question instead of locking the Ask input

- **Slug**: block-new-question-if-previous-unanswered
- **Tested**: 2026-11-16
- **Assessment**: ./assessment.md
- **Fix**: ./fix.md
- **Result**: partial

## Summary

The automated equivalent of the reported interaction now passes end-to-end at the ViewModel/transport layer: a stalled ask can be stopped, the reasoning rows clear, the prompt stays visible, and the send action becomes available for the next question immediately. The original manual reproduction (signed-in WPF client against a live workstation) was **not** exercised, so the user-facing Stop button and the real Ollama cancellation path are confirmed only by construction/XAML-lint tests and code inspection.

## Checks Performed

| Check | Command / Action | Result | Notes |
|-------|------------------|--------|-------|
| Reproduction (post-fix) | Live signed-in WPF client: ask a broad question, stop it, ask again (assessment steps 1–5) | skipped | No workstation is running (`8080`/`5001` closed) and the flow needs a seeded library plus a real model; skipped as expensive per the command's rules. The automated equivalent below covers the same state machine. |
| Reproduction equivalent | `dotnet test tests/unit/RAGGit.Tests.Unit.csproj -c Release --filter "FullyQualifiedName~StopAnswer_\|FullyQualifiedName~StreamAsync_CallerCancellation"` | pass | 4/4: stop cancels a hanging request, re-enables the ask, clears reasoning, and is gated when idle; caller cancellation no longer maps to a timeout. |
| New / updated tests | same as above | pass | `Passed: 4, Failed: 0` in 122 ms. |
| Regression suite (unit) | `dotnet test tests/unit/RAGGit.Tests.Unit.csproj -c Release --no-build` | pass | `Passed: 568, Failed: 0, Skipped: 0` in 4 s; includes the pre-existing busy-gate tests (`Ask_WhileBusy_CannotExecute`, `Reask_WhileBusy_DoesNothing`) and every other Ask/History test. |
| Regression suite (client UI) | `dotnet test tests/client/RAGGit.Tests.Client.csproj -c Release` | pass | `Passed: 103, Failed: 0` in 3 s; constructs every page/dialog on an STA thread and runs the XAML lint, so `StopQuestionButton`, `Stop24`, and the new DP parse and bind without a `XamlParseException`. |
| Lint / format | `dotnet tool restore` + `dotnet csharpier check .` | pass | `Checked 311 files in 1431ms` — no formatting drift in C# or XAML. |
| Build (server filter) | `dotnet build RAGGit.Server.slnf -c Release` | not-run | No server/API files changed; the unit-test build already compiles `RAGGit.Client.Core`, `RAGGit.Core`, `RAGGit.Retrieval`, and `RAGGit.Workstation.Api`, and the client-test build compiles the WPF app. |

## Output Excerpts

```
Passed!  - Failed: 0, Passed: 4, Skipped: 0, Total: 4, Duration: 122 ms - RAGGit.Tests.Unit.dll (net10.0)
Passed!  - Failed: 0, Passed: 568, Skipped: 0, Total: 568, Duration: 4 s - RAGGit.Tests.Unit.dll (net10.0)
Passed!  - Failed: 0, Passed: 103, Skipped: 0, Total: 103, Duration: 3 s - RAGGit.Tests.Client.dll (net10.0)
Checked 311 files in 1431ms.
```

Environment at test time: Ollama reachable on `127.0.0.1:11434`, no API listening on `8080`/`5001`, `winapp` available.

## Residual Risks

- **Live UI not exercised.** The Stop button's visual swap (send hidden, stop shown in the same 44 px slot) is only proven by the construction/lint suite; an actual click-through in a signed-in client has not been done, so layout aesthetics at runtime and automation-ID discoverability remain unverified.
- **Server-side cancellation timing not observed.** The token flows `QueryApiClient` → Kestrel → `QueryPipeline` → `OllamaLlmClient.ChatAsync`, but this run did not measure how quickly Ollama frees the runner or whether the workstation logs a cancelled (rather than failed) query. A stuck generation still returns a neutral status to the employee, so the symptom is fixed regardless.
- **Interaction is two steps.** The report's complaint ("cannot submit until the previous is answered") is resolved by Stop → ask; Enter while busy still does nothing. If product wants a single click that supersedes the in-flight question, that is the assessment's alternative and was not implemented.
- **Fast-answer window.** On a fast answer the stop button may be visible for only a moment; the tests cover the stopped state, not the timing of the visual swap.
- **Timeout parity.** `HttpClient.Timeout` (10 min) remains shorter than `Ollama:TimeoutMs` (15 min); a very broad question can still end in the client's "request timed out" error instead of the new stop path.

## Recommendation

Hold at **partial** until one short live check is done: start the dev API with a seeded library, sign in with the WPF client, ask a broad question, click Stop, and confirm the input accepts the next question — that closes the only gap between the automated evidence and the reported user experience. If the environment cannot be brought up, the automated evidence is strong enough to close the bug on its own, with the residual risks above accepted.

## Addendum (post-verification review, 2026-11-16)

A review pass after this report changed the UI shape and two robustness points; the report above is the record of the earlier build:

- The send button keeps its 032 in-flight spinner; Stop now sits beside it in a column whose 8 px gutter is zeroed while idle, so the "visual swap" risk above no longer describes the layout.
- The VM stops reading at the terminal `answer`, so a stop during connection wind-down cannot discard a delivered answer (pinned by `Ask_AppliesTheAnswerWithoutWaitingForTheStreamToClose`).
- The workstation logs a client-aborted stream at Information instead of `LogError`; `QueryAsync` now has the same cancellation filter as `StreamAsync`; the duplicated test handler moved to `tests/unit/HangingHandler.cs`.
- Re-verified: unit 572 passed, client 103 passed, `dotnet csharpier check .` clean (312 files).
