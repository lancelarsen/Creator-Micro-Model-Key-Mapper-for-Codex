param([switch]$EncodeOnly)
$ErrorActionPreference = 'Stop'
$projectDirectory = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$captureDirectory = Join-Path $projectDirectory 'artifacts\readme-capture'
$mediaDirectory = Join-Path $projectDirectory 'docs\media'
$editor = Join-Path $projectDirectory 'publish\editor\CreatorMicroEditor.exe'
if (!(Get-Command ffmpeg -ErrorAction SilentlyContinue)) { throw 'Install FFmpeg and add it to PATH first.' }
if (!$EncodeOnly) {
    if (!(Test-Path -LiteralPath $editor)) { throw 'Publish the editor before capturing.' }
    # A fresh folder prevents stale frames from extending a later capture.
    $captureDirectory = Join-Path $projectDirectory ('artifacts\readme-capture-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
    Start-Process -FilePath $editor -ArgumentList '--capture-demo',('"' + $captureDirectory + '"') -WindowStyle Hidden -Wait
}
if (Test-Path -LiteralPath (Join-Path $captureDirectory 'error.txt')) { throw (Get-Content (Join-Path $captureDirectory 'error.txt') -Raw) }
if (!(Test-Path -LiteralPath (Join-Path $captureDirectory 'capture.txt'))) { throw 'The native capture did not finish.' }
New-Item -ItemType Directory -Path $mediaDirectory -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $captureDirectory 'main.png') -Destination (Join-Path $mediaDirectory 'editor.png') -Force
$settingsCapture = Join-Path $captureDirectory 'settings.png'
$cropLog = & ffmpeg -hide_banner -loop 1 -i $settingsCapture -t 0.2 -vf 'cropdetect=24:2:0' -f null - 2>&1
$cropMatch = [regex]::Match(($cropLog -join "`n"), 'crop=\d+:\d+:\d+:\d+')
if (!$cropMatch.Success) { throw 'Could not identify the Settings dialog bounds.' }
& ffmpeg -y -hide_banner -loglevel error -i $settingsCapture -vf $cropMatch.Value -frames:v 1 (Join-Path $mediaDirectory 'settings.png')
if ($LASTEXITCODE -ne 0) { throw 'Settings screenshot cropping failed.' }
$frames = Join-Path $captureDirectory 'frame%04d.png'
& ffmpeg -y -hide_banner -loglevel error -framerate 12 -i $frames -vf 'scale=960:-2:flags=lanczos' -c:v libx264 -crf 20 -pix_fmt yuv420p -movflags +faststart (Join-Path $mediaDirectory 'editor-demo.mp4')
if ($LASTEXITCODE -ne 0) { throw 'Video encoding failed.' }
& ffmpeg -y -hide_banner -loglevel error -i (Join-Path $mediaDirectory 'editor-demo.mp4') -filter_complex 'fps=10,scale=760:-2:flags=lanczos,split[a][b];[a]palettegen=max_colors=128:stats_mode=full[p];[b][p]paletteuse=dither=none:diff_mode=rectangle' -loop 0 (Join-Path $mediaDirectory 'editor-demo.gif')
if ($LASTEXITCODE -ne 0) { throw 'GIF encoding failed.' }
Get-Content (Join-Path $captureDirectory 'capture.txt')
Get-ChildItem -LiteralPath $mediaDirectory | Select-Object Name, Length
