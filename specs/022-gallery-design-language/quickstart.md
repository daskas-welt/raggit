# Quickstart: Gallery-Style Design Language

**Feature**: `022-gallery-design-language` | **Date**: 2026-09-29 | **Plan**: [plan.md](plan.md)

Validation guide proving the UI contracts in [contracts/ui-contracts.md](contracts/ui-contracts.md)
and the measurable outcomes in [spec.md](spec.md). It is a run/verify guide, not an implementation
spec.

## Prerequisites

- Windows 10 1809+ / Windows 11 desktop.
- .NET 10 SDK (pinned by `global.json`); `winapp` CLI (WinApp UI-automation harness) for the walkthrough.
- A reachable workstation API for the signed-in surfaces (Dashboard/Settings); the shell and Login
  render without it.

## Build, format, and regression gates

```powershell
dotnet tool restore
dotnet csharpier check .
dotnet build RAGGit.sln -c Release -p:Platform=x64   # Windows-only: includes the WPF client
```

Offline-safe regression gate (no Ollama required) — must stay green with zero assertion changes:

```powershell
dotnet test tests/unit/RAGGit.Tests.Unit.csproj -c Release --filter "FullyQualifiedName~Tests.Unit"
dotnet test tests/contract/RAGGit.Tests.Contract.csproj -c Release --filter "FullyQualifiedName~Tests.Contract"
dotnet test tests/integration/RAGGit.Tests.Integration.csproj -c Release --filter "QueryOfflineTests|OfflineIdentityTests|OfflineHistoryTests|CrossUserIsolationTests"
```

> Environment note: tests resolve `./data/lancedb` relative to their **output** directory. A
> `DimensionMismatchException` (configured 1024 vs stored 384) is fixed by deleting
> `tests/*/bin/Release/net10.0/data/lancedb` and re-running — not the API's `data/lancedb`.

## Static contract checks

Run from the repo root.

**C3 — no color literals (expect no output):**

```powershell
Select-String -Path (Get-ChildItem src/RAGGit.Client.WPF -Recurse -File -Include *.xaml,*.cs |
  Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' }).FullName `
  -Pattern '#[0-9A-Fa-f]{6,8}|Color\.FromRgb|Colors\.'
```

**C7 — no literal glyph content (expect no output):**

```powershell
Select-String -Path (Get-ChildItem src/RAGGit.Client.WPF -Recurse -File -Include *.xaml |
  Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' }).FullName `
  -Pattern 'Content="[«‹›»←→↑↓]"'
```

**C7 — every icon name is a valid `SymbolRegular` member (expect no `MISS`):**

```powershell
$dll = "$env:USERPROFILE\.nuget\packages\wpf-ui\4.3.0\lib\net10.0-windows7.0\Wpf.Ui.dll"
$names = [Enum]::GetNames(([Reflection.Assembly]::LoadFrom($dll)).GetType('Wpf.Ui.Controls.SymbolRegular'))
Get-ChildItem src/RAGGit.Client.WPF -Recurse -File -Include *.xaml |
  Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' } |
  ForEach-Object { $f=$_.FullName; $n=0; Get-Content $f | ForEach-Object { $n++
    [regex]::Matches($_, 'Symbol="([A-Za-z0-9_]+)"') | ForEach-Object {
      if ($names -notcontains $_.Groups[1].Value) { "MISS $f`:$n $($_.Groups[1].Value)" } } } }
```

**C3 — every `{ui:ThemeResource X}` key is valid (expect no `MISS`; the markup extension is
enum-keyed and throws `XamlParseException` at load for an unknown key):**

```powershell
$dll = "$env:USERPROFILE\.nuget\packages\wpf-ui\4.3.0\lib\net10.0-windows7.0\Wpf.Ui.dll"
$keys = [Enum]::GetNames(([Reflection.Assembly]::LoadFrom($dll)).GetType('Wpf.Ui.Markup.ThemeResource'))
Get-ChildItem src/RAGGit.Client.WPF -Recurse -File -Include *.xaml |
  Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' } |
  ForEach-Object { $f=$_.FullName; $n=0; Get-Content $f | ForEach-Object { $n++
    [regex]::Matches($_, 'ui:ThemeResource\s+([A-Za-z0-9_]+)') | ForEach-Object {
      if ($keys -notcontains $_.Groups[1].Value) { "MISS $f`:$n $($_.Groups[1].Value)" } } } }
```

**C7 — no `Icon="…"` string form on `ui:Button` (expect no output; `IconElementConverter`
silently yields an empty icon, so use the `<ui:Button.Icon><ui:SymbolIcon …/></ui:Button.Icon>`
element form):**

```powershell
Select-String -Path (Get-ChildItem src/RAGGit.Client.WPF -Recurse -File -Include *.xaml |
  Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' }).FullName -Pattern 'Icon="[A-Za-z]'
```

**C4 — frozen automation IDs retained (expect a superset of the preserved list in
[data-model.md](data-model.md)):**

```powershell
Select-String -Path (Get-ChildItem src/RAGGit.Client.WPF -Recurse -File -Include *.xaml |
  Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' }).FullName -Pattern 'AutomationId="'
```

**C6 — no `ScrollViewer` wrapping a virtualizing collection:** review the `ScrollViewer` sites and
confirm none directly parents a `ListView`/`ListBox`/`GridView`/`DataGrid`.

## UI-automation walkthrough

Launch the API, then the client, capture its PID, and drive the surfaces with `winapp ui`
(see the `winui-ui-testing` skill). Assertions:

```powershell
# C1/C2 — shell
winapp ui wait-for "NavDashboard" -a $Pid -t 5000
winapp ui wait-for "NavSettings" -a $Pid -t 5000        # Settings pinned in footer

# C8 — Dashboard tiles (C2/SC-002)
winapp ui wait-for "DashboardMetricDocuments" -a $Pid -t 5000   # Library tile
winapp ui invoke "DashboardMetricQueries" -a $Pid               # Ask tile opens Ask
winapp ui wait-for "DashboardTileHistory" -a $Pid -t 5000

# C4 — accessibility coverage (all app controls have an automation id)
$els = (winapp ui inspect -a $Pid -i --json | ConvertFrom-Json).elements
@($els | Where-Object { $_.type -match 'Button|Card|Edit|ListItem' -and -not $_.automationId }).Count  # expect 0

# C5 — minimum size reflows without clipping (resize to 800x600, then re-assert tiles)
winapp ui screenshot -a $Pid -o "dashboard-800x600.png"
```

Capture screenshots after each major state (shell collapsed/expanded, Dashboard hero+tiles, Settings
rows) and review them — UIA returns PASS while the app can still be visually clipped or mis-themed.

## Manual checks

1. **Themes (C3, SC-006)** — cycle Windows between Light, Dark, and a High Contrast theme. On the
   shell, Dashboard (hero + tiles), and Settings, confirm no fixed color survives, hero text stays
   legible, and the gradient follows the accent.
2. **Layout (C5)** — resize to 800×600; confirm tiles wrap and nothing is clipped or horizontally
   scrolled.
3. **Keyboard (C4, FR-015)** — traverse the shell, Dashboard, and Settings with the keyboard only;
   confirm visible focus and that Ctrl+F reaches the nav search.
4. **Behavior freeze (C6)** — run a query end-to-end and a document upload; confirm answers,
   citations, and upload states are unchanged.

## Expected outcomes

| Check | Contract | Pass condition |
|-------|----------|----------------|
| Window chrome | C1 | Mica backdrop, centered, pane toggle works |
| Navigation | C2 | destinations listed in order; footer Settings present; no search field |
| Hero gradient | C3 | accent-token gradient; no literals; legible in all themes |
| Dashboard tiles | C8/SC-002 | icon + title + description; one activation to navigate |
| Settings rows | C8 | icon + title + description + control per row |
| Automation IDs | C4/SC-004 | superset of the preserved list |
| Icon names | C7/FR-012 | zero invalid `SymbolRegular` names |
| Minimum size | C5/SC-005 | reflow, no clipping at 800×600 |
| Build/tests | SC-007 | build + suites green, zero assertion changes, formatter clean |
