# Quickstart: Application Icon

**Feature**: `024-client-app-icon` | **Date**: 2026-09-30 | **Plan**: [plan.md](plan.md)

Validation guide proving [contracts/ui-contracts.md](contracts/ui-contracts.md) and the measurable
outcomes in [spec.md](spec.md). Run/verify guide, not an implementation spec.

## Prerequisites

- Windows 10 1809+ / Windows 11 desktop; .NET 10 SDK (pinned by `global.json`).
- `winapp` CLI for the screenshot pass.
- The identity audits below use GDI+ only, which loads under **both** Windows PowerShell 5.1 and
  PowerShell 7 — that is different from the `023` design audits, which need `pwsh` for their reflection
  checks. Use whichever shell is convenient here.

## Build, format, and regression gates

```powershell
dotnet tool restore
dotnet csharpier check .
dotnet build RAGGit.sln -c Release -p:Platform=x64
dotnet test tests/unit/RAGGit.Tests.Unit.csproj -c Release
dotnet test tests/contract/RAGGit.Tests.Contract.csproj -c Release
dotnet test tests/integration/RAGGit.Tests.Integration.csproj -c Release
```

The `023` design audits (colour literals, `Opacity=`, icon names, `ThemeResource` keys, automation IDs)
still apply unchanged — see [../023-design-system-refinement/quickstart.md](../023-design-system-refinement/quickstart.md),
including its PowerShell 7 note.

## Static identity audits

Run from the repo root. Each **MUST** report nothing except the presence lines it prints.

**A1 — the asset carries exactly the documented frames (expect no `MISS`, no `EXTRA`):**

```powershell
$bytes = [System.IO.File]::ReadAllBytes("src/RAGGit.Client.WPF/Assets/RaggitIcon.ico")
$count = [BitConverter]::ToUInt16($bytes, 4)
$frames = for ($i = 0; $i -lt $count; $i++) {
  $o = 6 + ($i * 16)
  $w = if ($bytes[$o] -eq 0) { 256 } else { $bytes[$o] }
  $h = if ($bytes[$o + 1] -eq 0) { 256 } else { $bytes[$o + 1] }
  "${w}x${h}"
}
$expected = '16x16','20x20','24x24','32x32','40x40','48x48','64x64','256x256'
foreach ($e in $expected) { if ($frames -notcontains $e) { "MISS frame $e" } }
foreach ($f in $frames) { if ($expected -notcontains $f) { "EXTRA frame $f" } }
```

The listing above reads directory entries only, and the largest frame is a PNG rather than a DIB (that
entry carries zero plane and bit-count fields, the convention for PNG frames), so it does not prove the
frame is readable. Continuing from that snippet, decode it and confirm the committed asset is what the
generator produces:

```powershell
Add-Type -AssemblyName System.Drawing
$offset = 0; $length = 0
for ($i = 0; $i -lt $count; $i++) {
  $o = 6 + ($i * 16)
  if ($bytes[$o] -eq 0) { $length = [BitConverter]::ToUInt32($bytes, $o + 8); $offset = [BitConverter]::ToUInt32($bytes, $o + 12) }
}
$stream = [System.IO.MemoryStream]::new([byte[]]($bytes[$offset..($offset + $length - 1)]))
try {
  $image = [System.Drawing.Image]::FromStream($stream)
  if ($image.Width -ne 256 -or $image.Height -ne 256) { "MISS the 256 frame decodes as $($image.Width)x$($image.Height)" }
  $image.Dispose()
} finally { $stream.Dispose() }

# The committed asset must be reproducible from the committed generator, in either shell.
$temp = Join-Path $env:TEMP 'raggit-icon-check.ico'
pwsh -NoProfile -File src/RAGGit.Client.WPF/Assets/generate-app-icon.ps1 -Output $temp | Out-Null
if ((Get-FileHash $temp -Algorithm SHA256).Hash -ne (Get-FileHash src/RAGGit.Client.WPF/Assets/RaggitIcon.ico -Algorithm SHA256).Hash) {
  "MISS the committed asset is not what the generator produces"
}
```

**A2 — one brand colour, defined once (expect no `MISS`):**

```powershell
$generator = 'src/RAGGit.Client.WPF/Assets/generate-app-icon.ps1'
$brand = [regex]::Match((Get-Content $generator -Raw), "\`$BrandColour\s*=\s*'(#[0-9A-Fa-f]{6})'").Groups[1].Value
if (-not $brand) { "MISS no brand colour in $generator" }
$inlined = Select-String -Path (Get-ChildItem src/RAGGit.Client.WPF -Recurse -File -Include *.xaml,*.cs |
  Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' }).FullName -Pattern $brand
if ($inlined) { "MISS brand colour inlined on a surface: $($inlined.Path -join ', ')" }
```

**A3 — the executable carries *the* mark, not the placeholder (expect no `MISS`):**

A bare "the executable has an icon" check is not enough: an executable carrying the framework's
placeholder icon would pass it, so this check has to look for the brand colour inside the icon the shell
would show.

```powershell
Add-Type -AssemblyName System.Drawing
$brand = ([regex]::Match((Get-Content 'src/RAGGit.Client.WPF/Assets/generate-app-icon.ps1' -Raw), '#[0-9A-Fa-f]{6}')).Value
$want = [System.Drawing.ColorTranslator]::FromHtml($brand)
$exe = (Resolve-Path "src/RAGGit.Client.WPF/bin/Release/net10.0-windows10.0.17763.0/RAGGit.Client.WPF.exe").Path
$icon = [System.Drawing.Icon]::ExtractAssociatedIcon($exe)
if (-not $icon) { "MISS executable icon missing" } else {
  $bmp = $icon.ToBitmap(); $hits = 0
  for ($y = 0; $y -lt $bmp.Height; $y++) {
    for ($x = 0; $x -lt $bmp.Width; $x++) {
      $p = $bmp.GetPixel($x, $y)
      if ($p.A -gt 128 -and [Math]::Abs($p.R - $want.R) -le 24 -and [Math]::Abs($p.G - $want.G) -le 24 -and
          [Math]::Abs($p.B - $want.B) -le 24) { $hits++ }
    }
  }
  if ($hits -lt 40) { "MISS executable icon carries no brand colour (placeholder still in place?)" }
}
```

**A4 — the asset folder contains only the identity and its generator (expect nothing):**

```powershell
$assets = Get-ChildItem src/RAGGit.Client.WPF/Assets -File | Select-Object -ExpandProperty Name
$expected = 'RaggitIcon.ico','generate-app-icon.ps1'
if (Compare-Object $assets $expected) { "MISS asset inventory: $($assets -join ', ')" }
```

**A5 — the window's own icons carry the mark (expect `OK` for both):**

This is the check for the surfaces a screenshot can miss: the shell draws the taskbar button and the
Alt-Tab entry from the window's icons, so reading them back is stronger evidence than a picture. It also
proves the small frame is used, not a downscaled large one.

```powershell
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class WindowIconProbe {
    [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);
}
"@
$brand = [regex]::Match((Get-Content 'src/RAGGit.Client.WPF/Assets/generate-app-icon.ps1' -Raw), "\`$BrandColour\s*=\s*'(#[0-9A-Fa-f]{6})'").Groups[1].Value
$want = [System.Drawing.ColorTranslator]::FromHtml($brand)
$hwnd = (Get-Process RAGGit.Client.WPF).MainWindowHandle
foreach ($kind in @(@{ Name = 'small (taskbar/title)'; Flag = 0 }, @{ Name = 'large (Alt-Tab)'; Flag = 1 })) {
    $handle = [WindowIconProbe]::SendMessage($hwnd, 0x007F, [IntPtr]$kind.Flag, [IntPtr]::Zero)
    if ($handle -eq [IntPtr]::Zero) { "$($kind.Name): MISS - window has no icon"; continue }
    $icon = [System.Drawing.Icon]::FromHandle($handle)
    $bmp = $icon.ToBitmap(); $hits = 0
    for ($y = 0; $y -lt $bmp.Height; $y++) { for ($x = 0; $x -lt $bmp.Width; $x++) {
        $p = $bmp.GetPixel($x, $y)
        if ($p.A -gt 128 -and [Math]::Abs($p.R - $want.R) -le 24 -and [Math]::Abs($p.G - $want.G) -le 24 -and
            [Math]::Abs($p.B - $want.B) -le 24) { $hits++ } } }
    "$($kind.Name): $($bmp.Width)x$($bmp.Height) brand pixels=$hits $(if ($hits -lt 20) { 'MISS' } else { 'OK' })"
    $bmp.Dispose()
}
```

## Identity review (visual)

The mark cannot be judged from scripts alone. Generate the review sheet straight from the generator's
geometry — every size over light and dark swatches — and read it:

```powershell
pwsh -File src/RAGGit.Client.WPF/Assets/generate-app-icon.ps1 -ReviewSheet "$env:TEMP\icon-review.png"
```

Then run the client and capture the two shell surfaces the user actually sees:

```powershell
$hwnd = (Get-Process RAGGit.Client.WPF).MainWindowHandle   # the running client
winapp ui screenshot --capture-screen --window $hwnd -o "identity-window.png"
winapp ui screenshot --capture-screen -o "identity-shell.png"
```

- `identity-window.png` covers the window icon and the shell's custom title bar — apply the capture
  pitfall from the `023` guide (force a repaint before capturing, or the PNG can show a stale frame).
- `identity-shell.png` must have the taskbar in frame. The taskbar may be vertical, on the secondary
  edge, or auto-hidden: capture the strip where it actually is (park the pointer on that edge first), and
  bring the client to the foreground so its button is the active one — otherwise the running button is
  one anonymous icon among many. Audit A5 is the objective check for these two surfaces; the strip is
  the human-readable one.

Review each against I1–I5. Confirm: the mark reads at taskbar size, the tile and silhouette carry the
identity when colour is ignored, the mark sits beside the product wordmark rather than replacing it, and
no surface shows a generic placeholder.

## Published build

```powershell
dotnet publish src/RAGGit.Client.WPF -c Release -p:Platform=x64 -o ./out/desktop
```

Re-run A3 against `./out/desktop/RAGGit.Client.WPF.exe`, and open `./out/desktop` in the file system to
confirm the file icon matches the running application's.

## Expected outcomes

| Check | Contract | Pass condition |
|-------|----------|----------------|
| Asset frames | I1 | exactly 16 · 20 · 24 · 32 · 40 · 48 · 64 · 256 px, no missing or extra |
| Executable icon | I1/SC-001 | icon present **and** contains the brand colour |
| Window icons | I2/SC-001 | small (16) and large (32) icons both contain the brand colour |
| Surface coverage | I2/SC-001 | file, pinned, taskbar, Alt-Tab and title bar show the mark; no placeholder |
| Legibility | I3/SC-002 | review sheet readable at 16 px and clean at 256 px, on light and dark |
| Brand consistency | I4/SC-004 | one brand colour, defined once, not inlined on any surface; asset folder holds only the asset and its generator |
| Published build | I1/SC-003 | published executable passes A3 and its file icon matches |
| Behaviour freeze | I5/SC-005 | 22+ frozen automation IDs present, titles unchanged, zero assertion changes |
| Build/format/tests | SC-005 | green |
