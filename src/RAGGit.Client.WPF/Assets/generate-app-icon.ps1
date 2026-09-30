<#
.SYNOPSIS
    Generates the Raggit application icon: the multi-size .ico the client ships, and an optional
    review sheet showing every frame over light and dark swatches.

.DESCRIPTION
    The mark is defined here and nowhere else (feature 024-client-app-icon, FR-008): a rounded-square
    tile in the product's fixed brand colour carrying a white page with a folded corner. Below the
    small-frame threshold the fold is dropped; the tile, colour and silhouette stay identical.

    Runs under Windows PowerShell 5.1 or PowerShell 7 (GDI+ only, no WPF assemblies).

.EXAMPLE
    pwsh -File src/RAGGit.Client.WPF/Assets/generate-app-icon.ps1
    pwsh -File src/RAGGit.Client.WPF/Assets/generate-app-icon.ps1 -ReviewSheet "$env:TEMP\icon-review.png"
#>
[CmdletBinding()]
param(
    [string]$Output = (Join-Path $PSScriptRoot 'RaggitIcon.ico'),
    [string]$ReviewSheet
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

# --- The mark, defined once -----------------------------------------------------------------
$BrandColour = '#0F6CBD'                                  # the product's first fixed brand colour
$GlyphColour = '#FFFFFF'
$FrameSizes = 16, 20, 24, 32, 40, 48, 64, 256             # every frame the Windows shell may ask for
$FoldMinFrame = 24                                        # below this the folded corner is dropped
$TileRadius = 0.22                                        # tile corner radius, as a fraction of the frame
$PageLeft = 0.24; $PageTop = 0.21; $PageRight = 0.76; $PageBottom = 0.79
$PageRadius = 0.07                                        # page corner radius, as a fraction
$PageFold = 0.16                                          # folded corner size, as a fraction
$LightSwatch = '#F3F3F3'
$DarkSwatch = '#202020'

function New-RoundedTilePath {
    param([single]$Size)
    $radius = [single][Math]::Max(2, [Math]::Round($Size * $TileRadius))
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $path.AddArc(0, 0, 2 * $radius, 2 * $radius, 180, 90)
    $path.AddArc($Size - 2 * $radius, 0, 2 * $radius, 2 * $radius, 270, 90)
    $path.AddArc($Size - 2 * $radius, $Size - 2 * $radius, 2 * $radius, 2 * $radius, 0, 90)
    $path.AddArc(0, $Size - 2 * $radius, 2 * $radius, 2 * $radius, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-PagePath {
    param([single]$X, [single]$Y, [single]$W, [single]$H, [single]$Radius, [single]$Fold)
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $path.AddArc($X, $Y, 2 * $Radius, 2 * $Radius, 180, 90)                  # top-left corner
    $path.AddLine($X + $W - $Fold, $Y, $X + $W, $Y + $Fold)                  # the fold line
    $path.AddLine($X + $W, $Y + $Fold, $X + $W, $Y + $H - $Radius)           # right edge
    $path.AddArc($X + $W - 2 * $Radius, $Y + $H - 2 * $Radius, 2 * $Radius, 2 * $Radius, 0, 90)
    $path.AddArc($X, $Y + $H - 2 * $Radius, 2 * $Radius, 2 * $Radius, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-MarkBitmap {
    param([int]$Size)

    $bitmap = [System.Drawing.Bitmap]::new($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $tileBrush = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml($BrandColour))
    $glyphBrush = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml($GlyphColour))
    try {
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.Clear([System.Drawing.Color]::Transparent)

        $tile = New-RoundedTilePath -Size $Size
        try { $graphics.FillPath($tileBrush, $tile) } finally { $tile.Dispose() }

        $x = [single]($Size * $PageLeft)
        $y = [single]($Size * $PageTop)
        $w = [single]($Size * ($PageRight - $PageLeft))
        $h = [single]($Size * ($PageBottom - $PageTop))
        $radius = [single][Math]::Max(1, $Size * $PageRadius)
        $fold = [single][Math]::Max(2, $Size * $PageFold)

        $page = New-PagePath -X $x -Y $y -W $w -H $h -Radius $radius -Fold $fold
        try { $graphics.FillPath($glyphBrush, $page) } finally { $page.Dispose() }

        if ($Size -ge $FoldMinFrame) {
            # The turned-back corner: brand colour showing through the cut corner, so the mark keeps
            # exactly two colours at every size.
            $turn = [System.Drawing.Drawing2D.GraphicsPath]::new()
            try {
                $points = [System.Drawing.PointF[]]@(
                    [System.Drawing.PointF]::new(($x + $w - $fold), $y),
                    [System.Drawing.PointF]::new(($x + $w), ($y + $fold)),
                    [System.Drawing.PointF]::new(($x + $w - $fold), ($y + $fold))
                )
                $turn.AddPolygon($points)
                $graphics.FillPath($tileBrush, $turn)
            } finally { $turn.Dispose() }
        }
    } finally {
        $tileBrush.Dispose()
        $glyphBrush.Dispose()
        $graphics.Dispose()
    }
    return $bitmap
}

function ConvertTo-DibBytes {
    param([System.Drawing.Bitmap]$Bitmap)

    $w = $Bitmap.Width
    $h = $Bitmap.Height
    $locked = $Bitmap.LockBits(
        [System.Drawing.Rectangle]::new(0, 0, $w, $h),
        [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $stride = $locked.Stride
        $pixels = [byte[]]::new($stride * $h)
        [System.Runtime.InteropServices.Marshal]::Copy($locked.Scan0, $pixels, 0, $pixels.Length)
    } finally {
        $Bitmap.UnlockBits($locked)
    }

    # The AND mask is required by the container even for 32-bit frames; the alpha channel decides
    # transparency, so an empty mask is correct.
    $maskStride = [int]([Math]::Floor(($w + 31) / 32) * 4)
    $mask = [byte[]]::new($maskStride * $h)

    $stream = [System.IO.MemoryStream]::new()
    $writer = [System.IO.BinaryWriter]::new($stream)
    try {
        $writer.Write([uint32]40)                       # BITMAPINFOHEADER
        $writer.Write([int32]$w)
        $writer.Write([int32]($h * 2))                  # XOR + AND
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]0)                        # BI_RGB
        $writer.Write([uint32]$pixels.Length)           # XOR image size; the AND mask follows it
        $writer.Write([int32]0); $writer.Write([int32]0)
        $writer.Write([uint32]0); $writer.Write([uint32]0)
        for ($y = $h - 1; $y -ge 0; $y--) { $writer.Write($pixels, $y * $stride, $w * 4) }   # DIBs are bottom-up
        $writer.Write($mask)
        $writer.Flush()
        return , $stream.ToArray()
    } finally {
        $writer.Dispose()
        $stream.Dispose()
    }
}

function ConvertTo-PngBytes {
    param([System.Drawing.Bitmap]$Bitmap)
    $stream = [System.IO.MemoryStream]::new()
    try {
        $Bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        return , $stream.ToArray()
    } finally {
        $stream.Dispose()
    }
}

function Write-IcoFile {
    param([string]$Path, [object[]]$Images)

    $stream = [System.IO.MemoryStream]::new()
    $writer = [System.IO.BinaryWriter]::new($stream)
    try {
        $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$Images.Count)
        $offset = 6 + (16 * $Images.Count)
        foreach ($image in $Images) {
            $dimension = if ($image.Size -ge 256) { 0 } else { $image.Size }
            # The largest frame is a PNG; the container's plane and bit-count fields are ignored for
            # it, and the convention there is zero for both.
            $planes = if ($image.IsPng) { 0 } else { 1 }
            $bitCount = if ($image.IsPng) { 0 } else { 32 }
            $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
            $writer.Write([byte]0); $writer.Write([byte]0)
            $writer.Write([uint16]$planes); $writer.Write([uint16]$bitCount)
            $writer.Write([uint32]$image.Data.Length)
            $writer.Write([uint32]$offset)
            $offset += $image.Data.Length
        }
        foreach ($image in $Images) { $writer.Write($image.Data) }
        $writer.Flush()
        [System.IO.File]::WriteAllBytes($Path, $stream.ToArray())
    } finally {
        $writer.Dispose()
        $stream.Dispose()
    }
}

function Write-ReviewSheet {
    param([string]$Path, [object[]]$Marks)

    $cell = 288
    $margin = 16
    $zoom = 4
    $zoomSizes = @(16, 20)
    $zoomRowHeight = ($zoomSizes | Measure-Object -Maximum).Maximum * $zoom
    $width = ($cell * 2) + ($margin * 3)
    $height = ($margin * 2) + (($cell + $margin) * $Marks.Count) + $zoomRowHeight + $margin
    $swatches = @(
        [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml($LightSwatch)),
        [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml($DarkSwatch))
    )
    $canvas = [System.Drawing.Bitmap]::new($width, $height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($canvas)
    try {
        $graphics.Clear([System.Drawing.Color]::White)
        $row = $margin
        foreach ($mark in $Marks) {
            for ($column = 0; $column -lt 2; $column++) {
                $x = $margin + ($column * ($cell + $margin))
                $graphics.FillRectangle($swatches[$column], $x, $row, $cell, $cell)
                $graphics.DrawImage(
                    $mark.Bitmap,
                    $x + [int](($cell - $mark.Bitmap.Width) / 2),
                    $row + [int](($cell - $mark.Bitmap.Height) / 2))
            }
            $row += $cell + $margin
        }
        # Nearest-neighbour zoom of the smallest frames, where legibility actually has to hold.
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
        $row += $margin
        $x = $margin
        foreach ($size in $zoomSizes) {
            $mark = $Marks | Where-Object { $_.Size -eq $size }
            for ($column = 0; $column -lt 2; $column++) {
                $side = $size * $zoom
                $graphics.FillRectangle($swatches[$column], $x, $row, $side, $side)
                $graphics.DrawImage($mark.Bitmap, $x, $row, $side, $side)
                $x += $side + $margin
            }
        }
        $canvas.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    } finally {
        $graphics.Dispose()
        $swatches | ForEach-Object { $_.Dispose() }
        $canvas.Dispose()
    }
}

$images = [System.Collections.Generic.List[object]]::new()
$marks = [System.Collections.Generic.List[object]]::new()
foreach ($size in $FrameSizes) {
    $bitmap = New-MarkBitmap -Size $size
    $marks.Add([pscustomobject]@{ Size = $size; Bitmap = $bitmap })
    $isPng = $size -ge 256
    $data = if ($isPng) { ConvertTo-PngBytes -Bitmap $bitmap } else { ConvertTo-DibBytes -Bitmap $bitmap }
    $images.Add([pscustomobject]@{ Size = $size; IsPng = $isPng; Data = $data })
}

Write-IcoFile -Path $Output -Images $images.ToArray()
Write-Output "wrote $Output ($([Math]::Round((Get-Item $Output).Length / 1KB, 1)) KB, $($images.Count) frames: $($FrameSizes -join ', '))"

if ($ReviewSheet) {
    Write-ReviewSheet -Path $ReviewSheet -Marks $marks.ToArray()
    Write-Output "wrote review sheet $ReviewSheet"
}

foreach ($mark in $marks) { $mark.Bitmap.Dispose() }
