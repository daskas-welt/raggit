# Quickstart: Design-System Refinement

**Feature**: `023-design-system-refinement` | **Date**: 2026-09-29 | **Plan**: [plan.md](plan.md)

Validation guide proving [contracts/ui-contracts.md](contracts/ui-contracts.md) and the measurable
outcomes in [spec.md](spec.md). Run/verify guide, not an implementation spec.

## Prerequisites

- Windows 10 1809+ / Windows 11 desktop; .NET 10 SDK (pinned by `global.json`).
- `winapp` CLI for the UI walkthrough; a reachable workstation API for the signed-in surfaces.

## Build, format, and regression gates

```powershell
dotnet tool restore
dotnet csharpier check .
dotnet build RAGGit.sln -c Release -p:Platform=x64
dotnet test tests/unit/RAGGit.Tests.Unit.csproj -c Release --filter "FullyQualifiedName~Tests.Unit"
dotnet test tests/contract/RAGGit.Tests.Contract.csproj -c Release --filter "FullyQualifiedName~Tests.Contract"
dotnet test tests/integration/RAGGit.Tests.Integration.csproj -c Release --filter "QueryOfflineTests|OfflineIdentityTests|OfflineHistoryTests|CrossUserIsolationTests"
```

> Environment note: tests resolve `./data/lancedb` relative to their **output** directory; a
> `DimensionMismatchException` is fixed by deleting `tests/*/bin/Release/net10.0/data/lancedb`.

## Static design audits

Run from the repo root. Each **MUST** return no output (except the presence checks, which report).

**D2 — no hard-coded colours:**

```powershell
Select-String -Path (Get-ChildItem src/RAGGit.Client.WPF -Recurse -File -Include *.xaml,*.cs |
  Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' }).FullName `
  -Pattern '#[0-9A-Fa-f]{6,8}|Color\.FromRgb|Colors\.'
```

**D2 — no `Opacity=` (expect 0):**

```powershell
Select-String -Path (Get-ChildItem src/RAGGit.Client.WPF -Recurse -File -Include *.xaml |
  Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' }).FullName -Pattern 'Opacity="'
```

**D1 — no ad-hoc `FontSize` on `ui:TextBlock` (expect 0):**

```powershell
$files = Get-ChildItem src/RAGGit.Client.WPF -Recurse -File -Include *.xaml |
  Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' }
foreach ($f in $files) { $c = Get-Content $f.FullName -Raw
  if ($c -match '(?s)<ui:TextBlock\b(?:(?!>).)*FontSize="') { $f.FullName } }
```

**D3 — every icon name is a valid `SymbolRegular` member (expect no `MISS`):**

```powershell
$dll = "$env:USERPROFILE\.nuget\packages\wpf-ui\4.3.0\lib\net10.0-windows7.0\Wpf.Ui.dll"
$asm = [Reflection.Assembly]::LoadFrom($dll)
$names = [Enum]::GetNames($asm.GetType('Wpf.Ui.Controls.SymbolRegular'))
Get-ChildItem src/RAGGit.Client.WPF -Recurse -File -Include *.xaml |
  Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' } |
  ForEach-Object { $f=$_.FullName; $n=0; Get-Content $f | ForEach-Object { $n++
    [regex]::Matches($_, 'Symbol="([A-Za-z0-9_]+)"') | ForEach-Object {
      if ($names -notcontains $_.Groups[1].Value) { "MISS $f`:$n $($_.Groups[1].Value)" } } } }
```

**D2 — every `{ui:ThemeResource X}` key is a valid enum member (expect no `MISS`):**

```powershell
$keys = [Enum]::GetNames(([Reflection.Assembly]::LoadFrom($dll)).GetType('Wpf.Ui.Markup.ThemeResource'))
Get-ChildItem src/RAGGit.Client.WPF -Recurse -File -Include *.xaml |
  Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' } |
  ForEach-Object { $f=$_.FullName; $n=0; Get-Content $f | ForEach-Object { $n++
    [regex]::Matches($_, 'ui:ThemeResource\s+([A-Za-z0-9_]+)') | ForEach-Object {
      if ($keys -notcontains $_.Groups[1].Value) { "MISS $f`:$n $($_.Groups[1].Value)" } } } }
```

**D7 — automation IDs preserved (expect a superset of the frozen list in [data-model.md](data-model.md)):**

```powershell
Select-String -Path (Get-ChildItem src/RAGGit.Client.WPF -Recurse -File -Include *.xaml |
  Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' }).FullName -Pattern 'AutomationId="'
```

**D1/D5 — page headers and empty states present:** for each page in the data-model's Page table,
confirm the header has a `PageTitleText` and a `PageSubtitleText`, and each listed empty state has an
`EmptyStateGlyph`. Review by inspection or the screenshot walkthrough.

## UI walkthrough (screenshots)

Launch the API and the client, then capture each affected surface and review the PNGs:

```powershell
winapp ui wait-for "DashboardMetricDocuments" -a $Pid -t 8000
winapp ui screenshot -a $Pid -o "dashboard.png"        # hero, tiles, empty states
winapp ui click "NavLibrary"   -a $Pid; winapp ui screenshot -a $Pid -o "library.png"   # header, table separation
winapp ui click "NavHistory"   -a $Pid; winapp ui screenshot -a $Pid -o "history.png"   # header, empty state
winapp ui click "NavMyDocs"    -a $Pid; winapp ui screenshot -a $Pid -o "mydocs.png"
winapp ui click "NavAdmin"     -a $Pid; winapp ui screenshot -a $Pid -o "admin.png"     # table separation
winapp ui click "NavSettings"  -a $Pid; winapp ui screenshot -a $Pid -o "settings.png"  # header, rows
winapp ui click "NavDashboard" -a $Pid; winapp ui screenshot -a $Pid -o "active-nav.png" # filled active glyph
```

Review each screenshot against D1–D6. UIA assertions do not see clipping, overlap, wrong theming or
spacing — the PNGs do.

## Manual checks

1. **Themes (D2, SC-003)** — cycle Light / Dark / High Contrast on every page; confirm supporting
   text, status glyphs, row separators and the active nav destination all stay legible.
2. **Layout (FR-015)** — at 800×600 confirm empty-state glyphs and separators do not clip.
3. **Consistency (D1, D3)** — compare every page header and every repeated concept glyph across
   pages; confirm one treatment, one symbol.

## Expected outcomes

| Check | Contract | Pass condition |
|-------|----------|----------------|
| Page headers | D1/SC-002 | every primary page: title + supporting line |
| Secondary treatment | D1/SC-004 | one treatment, served by `SecondaryText`/`PageSubtitleText` |
| No opacity | D2/SC-001 | 0 `Opacity=` |
| No literals | D2/SC-003 | 0 hard-coded colours |
| Icon names/sizes | D3/SC-005 | 0 invalid names; sizes on scale |
| Table separation | D5/SC-006 | rows separated, header distinct |
| Empty states | D5/SC-006 | glyph + title + hint |
| Active destination | D4/SC-007 | distinguishable in all themes |
| Automation IDs | D7/SC-008 | superset of the frozen list |
| Build/tests/format | SC-009 | green, zero assertion changes |
