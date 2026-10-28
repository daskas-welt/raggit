# Bug Verification: Page-level empty states share one card surface

- **Slug**: empty-state-consistency
- **Tested**: 2026-10-27
- **Assessment**: ./assessment.md
- **Fix**: ./fix.md
- **Result**: verified

## Summary

The symptom no longer reproduces: every page-level empty state (dashboard, library, my documents,
history, people management — both "no records" and "no matching" variants) now renders as the same
bordered, padded card. Verification rendered the real pages from the built client assembly with the
empty-state conditions satisfied, so the XAML and the shared style were exercised; the new static
guard and the full unit suite pass.

## Checks Performed

| Check | Command / Action | Result | Notes |
|-------|------------------|--------|-------|
| Reproduction (post-fix) | WPF harness (`%TEMP%\ragprobe`, `dotnet run -c Release`): instantiate the real `DashboardPage`/`LibraryPage`/`DocumentsMinePage`/`HistoryPage`/`AdminUsersPage` from `App.Services`, set a DataContext that satisfies each empty-state trigger, measure/arrange at 1280×800, `RenderTargetBitmap` → PNG | pass | 8 renders: 5 "no records" + 3 "no matching"; all show the shared bordered card. See `./evidence/`. |
| New / updated tests | `dotnet test tests/unit/RAGGit.Tests.Unit.csproj -c Release --filter "FullyQualifiedName~ClientEmptyStateCardStyleTests"` | pass | 3/3 — shared style + `BasedOn` chain + all nine empty-state automation IDs. |
| Regression suite | `dotnet test tests/unit/RAGGit.Tests.Unit.csproj -c Release --no-build` | pass | 557/557. |
| Lint / type-check | `dotnet csharpier check src/RAGGit.Client.WPF tests/unit/ClientEmptyStateCardStyleTests.cs` | pass | 52 files, clean. |
| Build | `dotnet build src/RAGGit.Client.WPF/RAGGit.Client.WPF.csproj -c Release -p:Platform=x64` | pass | 0 warnings, 0 errors. |
| Live signed-in client | launch client against the running API and log in | skipped | No credentials were supplied; the page-level rendering above replaces the manual walk-through for the style question. |
| UI automation (`winapp ui`) | target the empty-state automation IDs | skipped | Requires the signed-in client; IDs are in place for a future run. |

## Output Excerpts

```
dashboard: rendered
library: rendered
minedocs: rendered
history: rendered
admin: rendered
library-nomatch: rendered
minedocs-nomatch: rendered
history-nomatch: rendered
```

```
Passed!  - Failed:     0, Passed:   557, Skipped:     0, Total:   557  - RAGGit.Tests.Unit.dll (net10.0)
Checked 52 files in 635ms.
Build succeeded. 0 Warning(s) 0 Error(s)
```

Rendered evidence (1280×800, real page XAML, stub DataContext):

- `./evidence/dashboard.png` — 480 px bordered card centered below the metric cards (unchanged reference look).
- `./evidence/library.png`, `./evidence/minedocs.png`, `./evidence/history.png` — "no records" states with the same border, padding and corner radius.
- `./evidence/library-nomatch.png`, `./evidence/minedocs-nomatch.png`, `./evidence/history-nomatch.png` — "no matching" states that were bare panels before the fix; they now keep the card template via `BasedOn`.
- `./evidence/admin.png` — "No people yet" on the shared surface.

## Residual Risks

- Renders used a stub `DataContext` (no API/sign-in), so the surrounding data-bound chrome (pagination
  state, grid contents) is not part of the assertion — only the empty-state surface is.
- Admin's "No matching people" card is toggled from code-behind (`UpdateDirectorySummary`), so it was
  not rendered; its style wiring is covered by the unit guard and by the identical pattern proven for
  the other three no-match states.
- The shared style caps width (`MaxWidth=480`) rather than fixing it, so card widths still vary with
  content (e.g. 480 px on the dashboard, ~283 px for library). Border, padding and radius are identical.
- `PageHeaderCard` is still template-less (same root cause, deliberately out of scope). The flat page
  headers are visible in the evidence images and should be a separate decision.
- Nested placeholders (dashboard panels, query-detail citations, chat, upload dialog) remain borderless.

## Recommendation

Close the bug — the reported inconsistency is gone end-to-end at the page level, the fix is guarded by
tests, and no regressions appeared. Open the follow-up decisions recorded in `fix.md` (page headers,
nested placeholders, optional `EmptyStateControl`) separately if desired.
