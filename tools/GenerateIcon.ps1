Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class IconNativeMethods {
    [DllImport("user32.dll", SetLastError = true)] public static extern bool DestroyIcon(IntPtr handle);
}
'@

$assetDirectory = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\assets'))
$iconPath = Join-Path $assetDirectory 'codex-micro-presets.ico'
$previewPath = Join-Path $assetDirectory 'codex-micro-presets.png'
$bitmap = [System.Drawing.Bitmap]::new(64, 64, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([System.Drawing.Color]::Transparent)

function Fill-RoundedRectangle($Graphics, [System.Drawing.Color]$Color, [float]$X, [float]$Y, [float]$Width, [float]$Height, [float]$Radius) {
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $diameter = 2 * $Radius
    $path.AddArc($X, $Y, $diameter, $diameter, 180, 90)
    $path.AddArc($X + $Width - $diameter, $Y, $diameter, $diameter, 270, 90)
    $path.AddArc($X + $Width - $diameter, $Y + $Height - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($X, $Y + $Height - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    $brush = [System.Drawing.SolidBrush]::new($Color)
    $Graphics.FillPath($brush, $path)
    $brush.Dispose()
    $path.Dispose()
}

Fill-RoundedRectangle $graphics '#202632' 3 3 58 58 15
$letters = @('H', 'I', 'J')
$colors = @('#58d4bd', '#73a8ff', '#f2ba65')
$starts = @(6, 24.5, 43)
$white = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(244, 246, 249))
$font = [System.Drawing.Font]::new('Segoe UI', 10, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$center = [System.Drawing.StringFormat]::new()
$center.Alignment = [System.Drawing.StringAlignment]::Center
$center.LineAlignment = [System.Drawing.StringAlignment]::Center

for ($index = 0; $index -lt 3; $index++) {
    $x = [float]$starts[$index]
    Fill-RoundedRectangle $graphics '#2d3543' ($x - 3) 14 21 37 5
    Fill-RoundedRectangle $graphics '#4b5565' $x 17 15 31 4.5
    Fill-RoundedRectangle $graphics '#2d3543' $x 17 15 31 4.5
    Fill-RoundedRectangle $graphics $colors[$index] ($x + 3) 21 9 3 1.5
    $graphics.DrawString($letters[$index], $font, $white, [System.Drawing.RectangleF]::new($x, 25, 15, 18), $center)
}

$bitmap.Save($previewPath, [System.Drawing.Imaging.ImageFormat]::Png)
$handle = $bitmap.GetHicon()
try {
    $icon = [System.Drawing.Icon]::FromHandle($handle)
    $stream = [System.IO.File]::Create($iconPath)
    try { $icon.Save($stream) } finally { $stream.Dispose() }
    $icon.Dispose()
} finally {
    [IconNativeMethods]::DestroyIcon($handle) | Out-Null
    $center.Dispose()
    $font.Dispose()
    $white.Dispose()
    $graphics.Dispose()
    $bitmap.Dispose()
}

Write-Output "Wrote $iconPath"
