# Quickstart: WPF-UI Modernization

**Feature**: `021-wpfui-modernization` | **Date**: 2026-09-29 | **Plan**: [plan.md](plan.md)

Validation guide for the modernization. It proves the UI contracts in
[contracts/ui-contracts.md](contracts/ui-contracts.md) and the measurable outcomes in
[spec.md](spec.md). It is a run/verify guide, not an implementation spec.

## Prerequisites

- Windows 10 1809+ / Windows 11 desktop.
- .NET 10 SDK (pinned by `global.json`).
- A reachable workstation API for live screens (optional for static render checks); the client also
  renders Login/Settings without it.

## Build and format gates

```powershell
dotnet tool restore
dotnet csharpier check .
dotnet build RAGGit.sln -c Release -p:Platform=x64   # Windows-only: includes the WPF client
```

Expected: format check clean; solution builds with no WPF/XAML errors.

Offline-safe regression gate (no Ollama required):

```powershell
dotnet test --filter "FullyQualifiedName~Tests.Unit"
dotnet test --filter "FullyQualifiedName~Tests.Contract"
dotnet test --filter "QueryOfflineTests|OfflineIdentityTests|OfflineHistoryTests|CrossUserIsolationTests"
```

Expected: all green with zero assertion changes (C6).

## Static contract checks

Run from the repo root.

**C2 — no color literals (expect no output):**

```powershell
Select-String -Path (Get-ChildItem src/RAGGit.Client.WPF -Recurse -Include *.xaml,*.cs).FullName `
  -Pattern '#[0-9A-Fa-f]{6,8}|Color\.FromRgb|Colors\.'
```

**C3 — no literal icon-glyph content (expect no output):**

```powershell
Select-String -Path (Get-ChildItem src/RAGGit.Client.WPF -Recurse -Include *.xaml).FullName `
  -Pattern 'Content="[«‹›»←→↑↓]"'
```

**C3 — icons present (expect matches for the pager):**

```powershell
Select-String -Path src/RAGGit.Client.WPF/Components/PaginationFooterControl.xaml -Pattern 'SymbolIcon|Chevron'
```

**C4 — frozen automation IDs retained (expect all 52 literal IDs found):**

```powershell
$frozen = (Get-Content specs/021-wpfui-modernization/data-model.md | Select-String '^\s*(\w+,?){6,}' )  # informational
Select-String -Path (Get-ChildItem src/RAGGit.Client.WPF -Recurse -Include *.xaml).FullName `
  -Pattern 'AutomationId="'
```

Compare the emitted IDs against the frozen list in [data-model.md](data-model.md); the result MUST be
a superset.

**C6 — no `ScrollViewer` wrapping a virtualizing collection:** review the 4 `ScrollViewer` sites and
confirm none directly parents a `ListView`/`ListBox`/`GridView`/`DataGrid`.

**C1 — mapping catalog complete:** every `StyleOnly`/`Structural` family in
[data-model.md](data-model.md) has a rationale; every `Convert` family has zero raw occurrences.

## Manual UI walkthrough

1. **Theme (C2, SC-004)** — launch the client (Login → Dashboard). Switch Windows between Light, Dark,
   and a High Contrast theme. On every page and with `UploadDialog` open, confirm no stale or
   fixed-color element remains, all text is legible, and the status chip and chat bubble recolor.
2. **Icons (C3, SC-003/SC-006)** — on a list with more than one page (Library or History), confirm the
   pager shows Fluent chevrons (first/prev/next/last), themed and correctly enabled/disabled. Confirm
   icons appear on a machine without any font installed by the user (Windows 10 1809 check).
3. **Keyboard & accessibility (C4, C5, SC-005)** — traverse every primary screen with the keyboard only;
   confirm visible focus and that icon-only controls announce a name (Narrator). Confirm dialogs return
   focus to the invoking control.
4. **Layout (C5, SC-005)** — resize to the minimum window size (800×600); confirm all primary content is
   reachable and unclipped.
5. **Behavior freeze (C6)** — run a query end-to-end and a document upload; confirm answers/citations and
   upload states are unchanged from before the restyle.

## Expected outcomes

| Check | Contract | Pass condition |
|-------|----------|----------------|
| Color literals | C2 | zero matches |
| Literal glyphs | C3 | zero matches |
| Fluent icons | C3 | pager + status use `SymbolIcon`/`SymbolRegular` |
| Automation IDs | C4 | superset of the frozen list |
| Lists virtualized | C6 | no `ScrollViewer`-wrapped virtualizing collection |
| Mapping catalog | C1 | all families dispositioned; exceptions have rationale |
| Themes | C2 | Light/Dark/High Contrast all legible |
| Build/tests | C6 | build + suites green, zero assertion changes |
