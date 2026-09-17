<#
.SYNOPSIS
    Draws the Zone Champion icon (a 2x2 zone grid on a rounded tile) and writes a multi-size .ico.
.DESCRIPTION
    Small sizes are stored as 32-bit DIBs (read by every Windows API, including System.Drawing.Icon);
    256 px is stored as PNG. Run with Windows PowerShell 5.1 or PowerShell 7 on Windows.
#>
param(
    [string]$OutFile = (Join-Path $PSScriptRoot '..\src\ZoneChampion\Assets\ZoneChampion.ico')
)

Add-Type -AssemblyName System.Drawing

function New-RoundedPath([single]$x, [single]$y, [single]$w, [single]$h, [single]$r) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = [Math]::Min($r * 2, [Math]::Min($w, $h))
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $path.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $path.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-IconBitmap([int]$size) {
    $bitmap = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bitmap)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    $s = [single]$size
    $background = New-RoundedPath 0 0 $s $s ($s * 0.22)
    $g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 0x1E, 0x1E, 0x2E))), $background)

    $pad = [single]([Math]::Max(2.0, $s * 0.17))
    $gap = [single]([Math]::Max(1.0, $s * 0.07))
    $tile = [single](($s - 2 * $pad - $gap) / 2)
    $radius = [single]($tile * 0.24)
    $colors = @(
        [System.Drawing.Color]::FromArgb(255, 0x89, 0xB4, 0xFA),
        [System.Drawing.Color]::FromArgb(255, 0xCB, 0xA6, 0xF7),
        [System.Drawing.Color]::FromArgb(255, 0xA6, 0xE3, 0xA1),
        [System.Drawing.Color]::FromArgb(255, 0xF9, 0xE2, 0xAF)
    )
    for ($i = 0; $i -lt 4; $i++) {
        $col = $i % 2
        $row = [Math]::Floor($i / 2)
        $x = $pad + $col * ($tile + $gap)
        $y = $pad + $row * ($tile + $gap)
        $path = New-RoundedPath $x $y $tile $tile $radius
        $g.FillPath((New-Object System.Drawing.SolidBrush $colors[$i]), $path)
    }

    $g.Dispose()
    return $bitmap
}

function Get-DibBytes([System.Drawing.Bitmap]$bitmap) {
    $size = $bitmap.Width
    $stream = New-Object System.IO.MemoryStream
    $writer = New-Object System.IO.BinaryWriter $stream
    $maskRow = [int]([Math]::Ceiling($size / 32.0) * 4)

    $writer.Write([int]40)              # biSize
    $writer.Write([int]$size)           # biWidth
    $writer.Write([int]($size * 2))     # biHeight (color + mask)
    $writer.Write([int16]1)             # biPlanes
    $writer.Write([int16]32)            # biBitCount
    $writer.Write([int]0)               # biCompression
    $writer.Write([int]($size * $size * 4 + $maskRow * $size))
    $writer.Write([int]0); $writer.Write([int]0); $writer.Write([int]0); $writer.Write([int]0)

    # Pixel rows bottom-up, BGRA with straight alpha.
    for ($y = $size - 1; $y -ge 0; $y--) {
        for ($x = 0; $x -lt $size; $x++) {
            $c = $bitmap.GetPixel($x, $y)
            $writer.Write([byte]$c.B); $writer.Write([byte]$c.G); $writer.Write([byte]$c.R); $writer.Write([byte]$c.A)
        }
    }

    # AND mask: all zero; alpha does the work.
    $writer.Write((New-Object byte[] ($maskRow * $size)))
    $writer.Flush()
    return , $stream.ToArray() # comma keeps PowerShell from unrolling the array
}

$sizes = 16, 20, 24, 32, 40, 48, 64, 256
$images = foreach ($size in $sizes) {
    $bitmap = New-IconBitmap $size
    if ($size -eq 256) {
        $png = New-Object System.IO.MemoryStream
        $bitmap.Save($png, [System.Drawing.Imaging.ImageFormat]::Png)
        $bytes = $png.ToArray()
    }
    else {
        $bytes = Get-DibBytes $bitmap
    }
    $bitmap.Dispose()
    [pscustomobject]@{ Size = $size; Bytes = $bytes }
}

New-Item -ItemType Directory -Force -Path (Split-Path $OutFile) | Out-Null
$out = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter $out
$writer.Write([int16]0); $writer.Write([int16]1); $writer.Write([int16]$images.Count)

$offset = 6 + 16 * $images.Count
foreach ($image in $images) {
    $dimension = if ($image.Size -ge 256) { 0 } else { $image.Size }
    $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
    $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([int16]1); $writer.Write([int16]32)
    $writer.Write([int]$image.Bytes.Length); $writer.Write([int]$offset)
    $offset += $image.Bytes.Length
}
foreach ($image in $images) { $writer.Write($image.Bytes) }
$writer.Flush()

[System.IO.File]::WriteAllBytes((Resolve-Path -LiteralPath (Split-Path $OutFile)).Path + '\' + (Split-Path $OutFile -Leaf), $out.ToArray())
Write-Output "Wrote $OutFile ($($out.Length) bytes)"
