$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$projectDirectory = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$assetDirectory = Join-Path $projectDirectory 'assets'
$logoPath = Join-Path $projectDirectory 'Editor\Resources\Images\codex.png'
$logo = [Drawing.Image]::FromFile($logoPath)

function New-RoundedPath([float]$X, [float]$Y, [float]$Width, [float]$Height, [float]$Radius) {
    $path = [Drawing.Drawing2D.GraphicsPath]::new()
    $diameter = 2 * $Radius
    $path.AddArc($X, $Y, $diameter, $diameter, 180, 90)
    $path.AddArc($X + $Width - $diameter, $Y, $diameter, $diameter, 270, 90)
    $path.AddArc($X + $Width - $diameter, $Y + $Height - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($X, $Y + $Height - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}
function New-KeycapBitmap([int]$Size) {
    # Supersampling keeps the rim and logo crisp at tray sizes.
    $renderSize = $Size * 4
    $canvas = [Drawing.Bitmap]::new($renderSize, $renderSize, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [Drawing.Graphics]::FromImage($canvas)
    $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.Clear([Drawing.Color]::Transparent)
    $graphics.ScaleTransform($renderSize / 128.0, $renderSize / 128.0)
    $shadowPath = New-RoundedPath 3 4 122 122 25
    $shadowBrush = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(70, 0, 0, 0))
    $graphics.FillPath($shadowBrush, $shadowPath)
    $keyPath = New-RoundedPath 3 2 122 122 25
    $fill = [Drawing.Drawing2D.LinearGradientBrush]::new(
        [Drawing.PointF]::new(3, 2), [Drawing.PointF]::new(125, 124),
        [Drawing.ColorTranslator]::FromHtml('#25272C'), [Drawing.ColorTranslator]::FromHtml('#1C1E22'))
    $rimWidth = if ($Size -le 32) { 2.4 } else { 1.5 }
    $rim = [Drawing.Pen]::new([Drawing.ColorTranslator]::FromHtml('#4B4D56'), $rimWidth)
    $graphics.FillPath($fill, $keyPath)
    $graphics.DrawPath($rim, $keyPath)
    $logoSize = if ($Size -le 32) { 110.0 } else { 104.0 }
    $graphics.DrawImage($logo, [Drawing.RectangleF]::new((128 - $logoSize) / 2, 63 - $logoSize / 2, $logoSize, $logoSize))
    $graphics.Dispose()
    $shadowBrush.Dispose(); $shadowPath.Dispose(); $keyPath.Dispose(); $fill.Dispose(); $rim.Dispose()
    $result = [Drawing.Bitmap]::new($Size, $Size, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $downsample = [Drawing.Graphics]::FromImage($result)
    $downsample.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $downsample.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $downsample.DrawImage($canvas, 0, 0, $Size, $Size)
    $downsample.Dispose(); $canvas.Dispose()
    return $result
}
try {
    $preview = New-KeycapBitmap 512
    try { $preview.Save((Join-Path $assetDirectory 'codex-micro-presets.png'), [Drawing.Imaging.ImageFormat]::Png) }
    finally { $preview.Dispose() }
    Copy-Item -LiteralPath (Join-Path $assetDirectory 'codex-micro-presets.png') -Destination (Join-Path $projectDirectory 'Editor\Resources\Images\mapper_icon.png') -Force
    Copy-Item -LiteralPath (Join-Path $assetDirectory 'codex-micro-presets.png') -Destination (Join-Path $projectDirectory 'Editor\Resources\AppIcon\appicon.png') -Force
    # Windows selects a matching size instead of shrinking one 64 px image.
    $sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
    $images = foreach ($size in $sizes) {
        $bitmap = New-KeycapBitmap $size
        $memory = [IO.MemoryStream]::new()
        try {
            $bitmap.Save($memory, [Drawing.Imaging.ImageFormat]::Png)
            ,$memory.ToArray()
        } finally { $memory.Dispose(); $bitmap.Dispose() }
    }
    $writer = [IO.BinaryWriter]::new([IO.File]::Create((Join-Path $assetDirectory 'codex-micro-presets.ico')))
    try {
        $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
        $offset = 6 + 16 * $sizes.Count
        for ($index = 0; $index -lt $sizes.Count; $index++) {
            $dimension = if ($sizes[$index] -eq 256) { 0 } else { $sizes[$index] }
            $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
            $writer.Write([byte]0); $writer.Write([byte]0)
            $writer.Write([uint16]1); $writer.Write([uint16]32)
            $writer.Write([uint32]$images[$index].Length); $writer.Write([uint32]$offset)
            $offset += $images[$index].Length
        }
        foreach ($bytes in $images) { $writer.Write([byte[]]$bytes) }
    } finally { $writer.Dispose() }
    $logoData = [Convert]::ToBase64String([IO.File]::ReadAllBytes($logoPath))
    $svg = @"
<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="512" height="512" viewBox="0 0 128 128">
  <defs><linearGradient id="key" x1="0" y1="0" x2="1" y2="1"><stop stop-color="#25272C"/><stop offset="1" stop-color="#1C1E22"/></linearGradient></defs>
  <rect x="3" y="4" width="122" height="122" rx="25" fill="#000" opacity=".27"/>
  <rect x="3" y="2" width="122" height="122" rx="25" fill="url(#key)" stroke="#4B4D56" stroke-width="1.5"/>
  <image x="12" y="11" width="104" height="104" xlink:href="data:image/png;base64,$logoData"/>
</svg>
"@
    [IO.File]::WriteAllText((Join-Path $assetDirectory 'codex-micro-presets.svg'), $svg)
    [IO.File]::WriteAllText((Join-Path $projectDirectory 'Editor\Resources\AppIcon\appicon.svg'), $svg)
} finally { $logo.Dispose() }
Write-Output 'Generated keycap preview, MAUI assets, SVG, and nine Windows icon sizes.'
