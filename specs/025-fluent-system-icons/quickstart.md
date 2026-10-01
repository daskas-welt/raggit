# Quickstart: Fluent System Icons Across the Client

**Feature**: `025-fluent-system-icons` | **Date**: 2026-09-30 | **Plan**: [plan.md](plan.md)

Validation guide proving [contracts/ui-contracts.md](contracts/ui-contracts.md) and the measurable
outcomes in [spec.md](spec.md). Run/verify guide, not an implementation spec.

## Prerequisites

- Windows 10 1809+ / Windows 11 desktop; .NET 10 SDK (pinned by `global.json`).
- `winapp` CLI for the UI walkthrough; a reachable workstation API for the signed-in surfaces.
- The reflection audits (A1, A4) load the WPF-UI assembly and **MUST** run under PowerShell 7 (`pwsh`):
  Windows PowerShell 5.1 cannot resolve the `net10.0-windows` types, so `$asm.GetType(...)` returns
  null and every name is reported as missing. The `Select-String` checks run under either shell.

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

The `023` design audits (colour literals, `Opacity=`, ad-hoc `FontSize`, `ThemeResource` keys, automation
IDs) still apply unchanged — see
[../023-design-system-refinement/quickstart.md](../023-design-system-refinement/quickstart.md),
including its PowerShell 7 note.

## Static icon audits

Run from the repo root. Each check **MUST** report nothing except the presence lines it prints.

**A1 — every icon name is a valid `SymbolRegular` member, in XAML *and* C# (expect no `MISS`):**

```powershell
$dll = Join-Path $env:USERPROFILE '.nuget\packages\wpf-ui\4.3.0\lib\net10.0-windows7.0\Wpf.Ui.dll'
$names = [Enum]::GetNames(([Reflection.Assembly]::LoadFrom($dll)).GetType('Wpf.Ui.Controls.SymbolRegular'))
$files = Get-ChildItem src/RAGGit.Client.WPF -Recurse -File -Include *.xaml,*.cs |
  Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' }
foreach ($f in $files) { $c = Get-Content $f.FullName -Raw
  foreach ($m in [regex]::Matches($c, 'Symbol="([A-Za-z0-9_]+)"')) {
    if ($names -notcontains $m.Groups[1].Value) { "MISS $($f.Name): $($m.Groups[1].Value)" } }
  # State/Variant glyphs are also set through a Style setter; csharpier may put the
  # value on its own line, so this pattern allows whitespace (including newlines).
  foreach ($m in [regex]::Matches($c, '(?s)Property="Symbol"\s+Value="([A-Za-z0-9_]+)"')) {
    if ($names -notcontains $m.Groups[1].Value) { "MISS $($f.Name): $($m.Groups[1].Value)" } }
  foreach ($m in [regex]::Matches($c, 'SymbolRegular\.([A-Za-z0-9_]+)')) {
    if ($names -notcontains $m.Groups[1].Value) { "MISS $($f.Name): $($m.Groups[1].Value)" } } }
```

**A2 — the concept vocabulary is present (expect no `MISS`):**

```powershell
$want = 'ArrowEnterLeft24','PersonAdd24','PersonEdit24','KeyReset24','LockClosed24','LockOpen24',
        'ArrowDown24','ArrowRepeatAll24','ArrowLeft24','Eye24','TextQuote24','PlugDisconnected24',
        'Info24','Warning24','Checkmark24','Dismiss24'
$files = Get-ChildItem src/RAGGit.Client.WPF -Recurse -File -Include *.xaml,*.cs |
  Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' }
$found = foreach ($f in $files) { $c = Get-Content $f.FullName -Raw
  [regex]::Matches($c, 'Symbol="([A-Za-z0-9_]+)"') | ForEach-Object { $_.Groups[1].Value }
  [regex]::Matches($c, '(?s)Property="Symbol"\s+Value="([A-Za-z0-9_]+)"') | ForEach-Object { $_.Groups[1].Value }
  [regex]::Matches($c, 'SymbolRegular\.([A-Za-z0-9_]+)') | ForEach-Object { $_.Groups[1].Value } }
foreach ($w in $want) { if ($found -notcontains $w) { "MISS $w" } }
```

**A3 — the Ask suggestion chips stay text-only (expect `OK`):**

The clarification keeps the repeated person chips undecorated. Print the `SuggestionChips` template and
confirm it holds no `SymbolIcon`:

```powershell
$page = Get-Content src/RAGGit.Client.WPF/Views/Pages/QueryPage.xaml -Raw
$block = [regex]::Match($page, '(?s)x:Name="SuggestionChips".*?</ItemsControl>').Value
if ($block -match 'SymbolIcon') { 'MISS suggestion chips carry an icon' } else { 'OK suggestion chips text-only' }
```

**A4 — the `023` audits still pass over the touched files:**

- No hard-coded colour: `Select-String -Pattern '#[0-9A-Fa-f]{6,8}|Color\.FromRgb|Colors\.'` over
  `*.xaml,*.cs` (expect nothing).
- No `Opacity=`: `Select-String -Pattern 'Opacity="'` over `*.xaml` (expect nothing).
- Every `{ui:ThemeResource X}` key is a valid enum member (the `023` reflection check).
- Every automation ID is present: the `023` D7 inventory is a **superset** of the frozen 22, including
  the seven nav IDs set in `ViewModels/MainWindowViewModel.cs`.

## UI walkthrough (screenshots)

Launch the API and the client, then capture each affected surface and review the PNGs. Static checks
cannot see clipping, contrast or wrong theming; the PNGs can.

```powershell
$Pid = (Get-Process RAGGit.Client.WPF).Id
winapp ui screenshot -a $Pid -o "login.png"                                              # sign-in glyph
winapp ui click "NavLibrary"   -a $Pid; winapp ui screenshot -a $Pid -o "library.png"    # retry + error glyph
winapp ui click "NavHistory"   -a $Pid; winapp ui screenshot -a $Pid -o "history.png"    # refresh/view/ask-again/load-more
winapp ui click "NavMyDocs"    -a $Pid; winapp ui screenshot -a $Pid -o "mydocs.png"     # refresh/load-more
winapp ui click "NavAdmin"     -a $Pid; winapp ui screenshot -a $Pid -o "admin.png"      # role/active/locked/create/reset/status
winapp ui click "NavSettings"  -a $Pid; winapp ui screenshot -a $Pid -o "settings.png"   # sign-out + connection state
winapp ui click "NavAsk"       -a $Pid; winapp ui screenshot -a $Pid -o "ask.png"        # Sources disclosure; chips text-only
```

> **Capture pitfall**: `winapp ui screenshot --capture-screen` can return a stale frame for a page
> navigated to immediately before the capture, so a theme capture may show the previous theme. Force a
> repaint first (minimize and restore, or change the theme while the page is already on screen) and
> review that PNG. A dark page also compresses far smaller than a light one — treat an unexpectedly
> large PNG as a hint to re-check, not as proof.

## Manual checks

1. **Themes (F3, SC-006)** — cycle Light / Dark / High Contrast on every page above; confirm every new
   glyph stays visible and correctly coloured, with no icon disappearing into its surface. Force a
   repaint before each capture.
2. **Layout (F2, F6, SC-006)** — at the minimum window size (800×600) confirm no labelled control with a
   new glyph clips, wraps badly or displaces its neighbours, and the admin table still tracks rows.
3. **Coverage & consistency (F1, F5, SC-001, SC-002, SC-003)** — walk each surface in the action table
   and confirm the agreed glyph is shown; compare a concept across pages (refresh on four pages, copy on
   chat/prompt/answer/citation) and confirm one glyph; confirm active/locked and connection states swap
   glyph as well as colour.
4. **Accessibility (F4, SC-005)** — with a screen reader, confirm every icon-only control announces its
   action and no control announces a raw glyph; confirm labelled controls still announce their label.
5. **No regressions (F7, SC-008, SC-009)** — confirm no label was removed and no control became
   icon-only, and that the automation-ID inventory is unchanged.

## Expected outcomes

| Check | Contract | Pass condition |
|-------|----------|----------------|
| Icon names | F1/SC-007 | A1 reports 0 invalid names (XAML + C#) |
| Coverage | F1/SC-001 | A2 reports 0 missing; every action-table control shows its glyph |
| Concept consistency | F1/SC-002 | one glyph per concept across every surface |
| Sizing | F2/SC-006 | every glyph on the documented scale; no ad-hoc `FontSize`; 800×600 unclipped |
| Colour/theme | F3/SC-004, SC-006 | 0 hard-coded colours, 0 `Opacity=`; glyphs visible in all three themes |
| Accessibility | F4/SC-005 | every icon-only control has a name + tooltip; labels retained |
| State | F5/SC-003 | every stateful surface shows a glyph as well as colour |
| Placement | F6/FR-009 | suggestion chips text-only (A3 `OK`); no icon on headers/page numbers |
| Documentation | F7/FR-010 | new concept→glyph entries present in `023`'s design-system reference |
| Behaviour freeze | F7/SC-008, SC-009 | automation IDs unchanged; build/format/tests green, zero assertion changes |
