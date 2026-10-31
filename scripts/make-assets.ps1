# Renders Ayr's mark (three lines of moving air) into the few small bitmaps the app embeds. The mark is defined right
# here as geometry on a 24-unit grid, so the app never needs an image editor or an SVG engine.
# Needs Windows PowerShell 5.1, which ships WPF:  powershell -NoProfile -File scripts\make-assets.ps1
[CmdletBinding()]
param([string]$OutDir)
$ErrorActionPreference = 'Stop'
# Windows PowerShell 5.1 does not populate $PSScriptRoot inside param() defaults.
if (-not $OutDir) { $OutDir = Join-Path $PSScriptRoot '..\src\Ayr\Ui\Assets' }
Add-Type -AssemblyName PresentationCore, WindowsBase

$Grid = 24.0
$MarkPaths = @(
    'M2,8 H11 A2.5,2.5 0 1 0 8.5,5.5',
    'M2,12 H17 A3,3 0 1 0 14,9',
    'M2,16 H13 A2.5,2.5 0 1 1 10.5,18.5'
)
$Accent = [System.Windows.Media.Color]::FromRgb(232, 95, 84)
$White = [System.Windows.Media.Colors]::White

function New-Mark([double]$size, [double]$markShare, [double]$strokeUnits, [System.Windows.Media.Color]$color) {
    $group = New-Object System.Windows.Media.DrawingGroup
    $scale = $size * $markShare / $Grid
    $offset = ($size - $Grid * $scale) / 2
    $group.Transform = New-Object System.Windows.Media.MatrixTransform $scale, 0, 0, $scale, $offset, $offset
    $pen = New-Object System.Windows.Media.Pen (New-Object System.Windows.Media.SolidColorBrush $color), $strokeUnits
    $pen.StartLineCap = 'Round'; $pen.EndLineCap = 'Round'; $pen.LineJoin = 'Round'
    foreach ($path in $MarkPaths) {
        $group.Children.Add((New-Object System.Windows.Media.GeometryDrawing $null, $pen, ([System.Windows.Media.Geometry]::Parse($path))))
    }
    return $group
}

function Render-Png([int]$size, [bool]$onTile, [double]$strokeUnits) {
    $visual = New-Object System.Windows.Media.DrawingVisual
    $context = $visual.RenderOpen()
    if ($onTile) {
        $radius = $size * 0.22
        $context.DrawRoundedRectangle((New-Object System.Windows.Media.SolidColorBrush $Accent), $null, (New-Object System.Windows.Rect 0, 0, $size, $size), $radius, $radius)
        $context.DrawDrawing((New-Mark $size 0.72 $strokeUnits $White))
    } else {
        $context.DrawDrawing((New-Mark $size 0.96 $strokeUnits $White))
    }
    $context.Close()
    $bitmap = New-Object System.Windows.Media.Imaging.RenderTargetBitmap $size, $size, 96, 96, ([System.Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = New-Object System.IO.MemoryStream
    $encoder.Save($stream)
    return ,$stream.ToArray()
}

# Multi-size .ico with PNG-compressed entries (supported since Windows Vista).
function Write-Ico([string]$name, [int[]]$sizes, [bool]$onTile, [double]$strokeUnits) {
    $images = foreach ($size in $sizes) { ,(Render-Png $size $onTile $strokeUnits) }
    $out = New-Object System.IO.MemoryStream
    $writer = New-Object System.IO.BinaryWriter $out
    $writer.Write([UInt16]0); $writer.Write([UInt16]1); $writer.Write([UInt16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $size = $sizes[$i]; $length = $images[$i].Length
        $writer.Write([byte]($size % 256)); $writer.Write([byte]($size % 256)); $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([UInt16]1); $writer.Write([UInt16]32); $writer.Write([UInt32]$length); $writer.Write([UInt32]$offset)
        $offset += $length
    }
    foreach ($image in $images) { $writer.Write($image) }
    $writer.Flush()
    [IO.File]::WriteAllBytes((Join-Path $OutDir $name), $out.ToArray())
}

New-Item -ItemType Directory -Force $OutDir | Out-Null
Get-ChildItem $OutDir -File | Remove-Item

# App icon (exe, window, taskbar): the white mark on an accent tile.
Write-Ico 'app.ico' @(16, 20, 24, 32, 40, 48, 64, 256) $true 2.2
# Tray icon: the bare white mark, like Windows' own monochrome tray icons, at every small-icon DPI size.
Write-Ico 'tray.ico' @(16, 20, 24, 28, 32, 40, 48) $false 2.4
# The mark alone at the exact sizes the in-app badge uses, so it is drawn 1:1 and stays crisp.
foreach ($size in 24, 32, 40, 48) {
    [IO.File]::WriteAllBytes((Join-Path $OutDir "mark-$size.png"), (Render-Png $size $false 2.2))
}

Get-ChildItem $OutDir | Select-Object Name, Length
