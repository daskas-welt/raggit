# Quickstart: Dashboard Redesign — "Work overview"

**Feature**: `027-dashboard-redesign` | **Date**: 2026-10-01 | **Plan**: [plan.md](plan.md)

Validation guide for [contracts/ui-contracts.md](contracts/ui-contracts.md), the feature
outcomes in [spec.md](spec.md), and the existing regression suites.

## Prerequisites

- Windows 10 1809+ / Windows 11; .NET 10 SDK pinned by `global.json`.
- `winapp` CLI for UI Automation and screenshot walkthrough.
- Local workstation API credentials and a seeded library (documents in mixed states plus
  saved questions) for the populated walkthrough; a fresh empty library for the empty
  state; one Admin and one non-admin account.

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
> `tests/*/bin/Release/net10.0/data/lancedb`.

## Static design audits (from `specs/023-design-system-refinement/quickstart.md`)

Run from the repo root under PowerShell 7 (`pwsh`) for the reflection checks. Each check
must return no output (presence checks report the frozen IDs).

1. **No hard-coded colours** — `Select-String` for `#[0-9A-Fa-f]{6,8}|Color\.FromRgb|
   Colors\.` over `src/RAGGit.Client.WPF` (`*.xaml`, `*.cs`, excluding `obj|bin`).
2. **No `Opacity=`** — expect 0 hits in `*.xaml`.
3. **No ad-hoc `FontSize` on `ui:TextBlock`** — expect 0 hits.
4. **Valid `SymbolRegular` members** — every `Symbol="…"` resolves; expect no `MISS`.
5. **Valid `ThemeResource` keys** — every `{ui:ThemeResource X}` resolves; expect no
   `MISS`.
6. **Frozen IDs** — all 11 Dashboard IDs from contract U5 are present in
   `src/RAGGit.Client.WPF/Views/Pages/DashboardPage.xaml`, plus the four new
   `DashboardMetric{Ready,Indexing,Failed,Mine}` IDs.

## Interactive UI walkthrough

Run from the repo root after starting the workstation API and signing in. Capture each
state at a wide desktop size and at 800×600; force a repaint after navigation and after
each theme change before screenshots (see the 023 guide's capture pitfall).

1. **Header** — compact title plus supporting line; profile card, refresh, and labeled
   History / My Docs / Admin actions on the right. Sign in as non-admin and confirm no
   Admin action is offered.
2. **Metric cards** — six cards with icon, count, and label; activate each and confirm
   its destination (Documents/Ready/Indexing/Failed → Library, Questions → Ask,
   Mine → My Docs). Confirm zero cards still render and navigate.
3. **Failed refresh** — fail a refresh after a successful load; confirm last-known
   numbers stay beside the error status, and Retry reloads.
4. **Panels** — confirm side-by-side panels when wide and stacked panels at ≤720 DIPs
   content width (800×600 window); each panel's empty state appears when its content is
   empty.
5. **Whole-dashboard empty state** — fresh library, no saved questions: one empty state
   with "Open the library"; activate it and confirm the library opens. Add one document
   and confirm the empty state disappears.
6. **Accessibility/theme** — keyboard through header actions, cards, and panels; screen
   reader announces each card name and destination; cycle Light/Dark/High Contrast and
   confirm counts, labels, focus, and status stay legible with no clipping.

## Expected outcomes

| Check | Contract | Pass condition |
|-------|----------|----------------|
| Compact header | U1 | title + supporting line; profile, refresh, labeled actions; Admin only for admins; no hero |
| Metric cards | U2 | six cards with icon/count/label; 100% land on the agreed destination; ingestion states visible; last-known-good on failed refresh |
| Panels + stacking | U3 | Fluent cards; side by side when wide, stacked at ≤720 DIPs; per-panel empty states; nothing clipped at 800×600 |
| Dashboard empty state | U4 | single empty state only when fully empty; action opens the library |
| Identity/accessibility/theme | U5 | 11 frozen IDs preserved + 4 new stable IDs; keyboard/screen-reader operable; ≥44-DIP targets; Light/Dark/High Contrast legible |
| Regression gates | U5 | CSharpier, full build, unit/contract/offline-integration suites pass with no unrelated assertion changes |
