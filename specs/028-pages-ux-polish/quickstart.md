# Quickstart: All-Pages UX/UI Polish

**Feature**: `028-pages-ux-polish` | **Date**: 2026-10-02 | **Plan**: [plan.md](plan.md)

Validation guide for [contracts/ui-contracts.md](contracts/ui-contracts.md), the feature
outcomes in [spec.md](spec.md), and the existing regression suites.

## Prerequisites

- Windows 10 1809+ / Windows 11; .NET 10 SDK pinned by `global.json`.
- `winapp` CLI for UI Automation and screenshot walkthrough.
- Local workstation API credentials, a seeded library (documents in mixed states),
  saved questions (one long answer with citations), and one Admin + one non-admin
  account. A fresh empty library for the empty-state checks.

## Build, format, and regression gates

```powershell
dotnet tool restore
dotnet csharpier check .
dotnet build RAGGit.sln -c Release -p:Platform=x64
dotnet test tests/unit/RAGGit.Tests.Unit.csproj -c Release --filter "FullyQualifiedName~Tests.Unit"
dotnet test tests/contract/RAGGit.Tests.Contract.csproj -c Release --filter "FullyQualifiedName~Tests.Contract"
dotnet test tests/integration/RAGGit.Tests.Integration.csproj -c Release --filter "QueryOfflineTests|OfflineIdentityTests|OfflineHistoryTests|CrossUserIsolationTests"
```

> Environment note: tests resolve `./data/lancedb` relative to their **output**
> directory; a `DimensionMismatchException` is fixed by deleting
> `tests/*/bin/Release/net10.0/data/lancedb`. New unit tests for the Core seams
> (notification seam, pending-ask state, clear-conversation command, per-download
> busy) must exist and pass — TDD (constitution VI).

## Static design audits (from `specs/023-design-system-refinement/quickstart.md`, extended)

Run from the repo root under PowerShell 7 (`pwsh`). Each check must return no output
(presence checks report the IDs).

1. **No hard-coded colours** — `Select-String` for `#[0-9A-Fa-f]{6,8}|Color\.FromRgb|
   Colors\.` over `src/RAGGit.Client.WPF` (`*.xaml`, `*.cs`, excluding `obj|bin`).
2. **No `Opacity=`** — expect 0 hits in `*.xaml`.
3. **No ad-hoc `FontSize` on `ui:TextBlock`** — expect 0 hits.
4. **Valid `SymbolRegular` members** — every `Symbol="…"` resolves; expect no `MISS`.
5. **Valid `ThemeResource` keys** — every `{ui:ThemeResource X}` resolves; expect no
   `MISS`.
6. **Zero native message boxes** — `Select-String 'MessageBox\.Show'` over
   `src/RAGGit.Client.WPF` returns nothing.
7. **Status text never trims** — no `TextTrimming` on the shared status component's
   message text; History/DocumentsMine footers wrap.
8. **Row-action targets** — Library row download/delete buttons are ≥44 DIPs
   (`Width`/`Height` ≥44 or equivalent shared style).
9. **Frozen IDs** — every ID listed in contract U8.1 is present in its page's XAML;
   the new IDs in U8.2 exist exactly once.
10. **Spacing tokens** — the affected header cards, empty-state bodies, and dialog
    footer buttons resolve Padding/Margin/MinWidth from the shared Thickness
    resources (spot-check: no `Padding="12"` header card, no `0,8,0,0` empty-state
    body margin, no 90/128/136 dialog button drift).

## Interactive UI walkthrough

Run from the repo root after starting the workstation API and signing in. Capture each
state at a wide desktop size and at 800×600; cycle Light/Dark/High Contrast for each
surface (force a repaint after navigation and theme changes — see the 023 guide's
capture pitfall).

1. **Header parity (U1)** — walk Dashboard, Library, Ask, QueryDetail, History, My
   Documents, Admin, Settings: every page opens with the same header-card structure;
   Dashboard's profile card sits on the post-header row with the library summary; the
   suggestion-chip header is single-language; Ask shows the Clear action.
2. **Status + retry (U2)** — stop the API (or use an unreachable URL) and fail a load
   on every page: the shared status treatment with a working retry appears on
   Dashboard, Library, History, My Documents, Admin, and Login; restart the API and
   retry each. Long error messages wrap fully.
3. **Busy (U3)** — trigger refresh/load-more/download/re-check/sign-in/send: busy is
   in the initiating control with content visible; the Ask page never hides the
   conversation while answering.
4. **Feedback (U4)** — copy a chat message, the QueryDetail prompt/answer/citation,
   and both Settings values: a snackbar confirms each. Download a document (row ring
   → success snackbar → file opens) and force a download failure. Sign out and cancel
   an in-flight upload: both confirm first, and declining leaves everything untouched.
   Reset a password: the confirm renders in-window; no native message box appears
   anywhere.
5. **Conversation (U5)** — send 10+ questions: the newest message stays visible
   without scrolling. "Ask again" from History and from a saved answer: the Ask page
   opens with the question populated (not auto-sent), every time, including from a
   cold Ask page. Clear the conversation: empty state shows, input focused.
6. **Small window (U6)** — at 800×600 with the nav pane open, the Library table shows
   Filename/Status/actions without horizontal scrolling (Creator/Created hidden);
   widen past 720 DIPs content width and all columns return. Sign in as non-admin with
   an empty library: the empty state never suggests uploading. Measure row buttons
   ≥44 DIPs.
7. **Login & Settings (U7)** — press Enter in the username box and in the password
   box: sign-in submits; busy/error use the shared idioms. In Settings, re-check the
   connection (button busy → status row refreshes with the outcome); copy both
   displayed values.
8. **Accessibility/theme (U8)** — keyboard through every new affordance (retry,
   clear, re-check, copy buttons) in logical order with visible focus; screen-reader
   names on all new controls; all three themes legible with nothing clipped.

## Expected outcomes

| Check | Contract | Pass condition |
|-------|----------|----------------|
| Header parity | U1 | 8/8 content pages share one header structure; Dashboard profile on post-header row; single-language copy |
| Status + retry | U2 | one InfoBar-based idiom app-wide; retry on every async page; wrapped long errors; zero native message boxes |
| Busy | U3 | 100% in-control for action-initiated busy; conversation visible while answering; no duplicate dispatch |
| Feedback | U4 | 100% copy affordances confirm; download in-flight/success/failure all signaled; sign-out and cancel-mid-upload confirm |
| Conversation | U5 | auto-scroll after every exchange; ask-again populated on every attempt; clear works |
| Small window | U6 | no forced horizontal scroll at 800×600; ≥44-DIP row targets; role-aware empty states |
| Login & Settings | U7 | Enter submits from both boxes; re-check works; values copyable |
| Identity/theme/spacing | U8 | all frozen IDs preserved + new stable IDs; shared Thickness resources used; tokens only; three themes legible |
| Regression gates | U8 | CSharpier, full build, unit (incl. new seam tests)/contract/offline-integration suites pass with no unrelated assertion changes |
