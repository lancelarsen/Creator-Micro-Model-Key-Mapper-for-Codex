param([string]$InputDirectory = (Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts\codex-demo'))
$ErrorActionPreference = 'Stop'
$projectDirectory = Split-Path $PSScriptRoot -Parent
$mediaDirectory = Join-Path $projectDirectory 'docs\media'
$events = Get-Content (Join-Path $InputDirectory 'events.json') -Raw | ConvertFrom-Json
$duration = [double]::Parse((Get-Content (Join-Path $InputDirectory 'end.txt') -Raw), [Globalization.CultureInfo]::InvariantCulture)
function Number($value) { ([double]$value).ToString('0.000', [Globalization.CultureInfo]::InvariantCulture) }
$font = "fontfile='C\:/Windows/Fonts/segoeui.ttf'"
$filters = [Collections.Generic.List[string]]::new()
$filters.Add('pad=iw:ih+140:0:140:color=0x17181C')
$filters.Add("drawtext=$font`:text='CREATOR MICRO  /  LIVE CODEX':x=30:y=16:fontsize=20:fontcolor=0xABAEBC")
for ($index = 0; $index -lt 3; $index++) {
    $x = 30 + $index * 80
    $key = @('H','I','J')[$index]
    $filters.Add("drawbox=x=$x`:y=52:w=62:h=62:color=0x292A31:t=fill")
    $filters.Add("drawbox=x=$x`:y=52:w=62:h=62:color=0x45464D:t=2")
    $event = $events[$index]
    if ($event.key -ne $key -or $event.exitCode -ne 0) { throw 'The recording must contain verified H, I, J selections in order.' }
    $start = Number $event.indication
    $end = if ($index -lt 2) { Number $events[$index + 1].indication } else { Number $duration }
    $enabled = "enable='between(t,$start,$end)'"
    $filters.Add("drawbox=x=$x`:y=52:w=62:h=62:color=0x7566DC:t=fill:$enabled")
    $filters.Add("drawtext=$font`:text='$key':x=$($x+20):y=65:fontsize=32:fontcolor=white")
    $label = @('Luna / Extra High','6.1 Sol / High','6 Astra / Low')[$index]
    $filters.Add("drawtext=$font`:text='$label':x=296:y=58:fontsize=30:fontcolor=white:$enabled")
    $filters.Add("drawtext=$font`:text='Preset trigger':x=296:y=99:fontsize=18:fontcolor=0xABAEBC:$enabled")
}
$filters.Add('scale=1120:-2:flags=lanczos')
$outputVideo = Join-Path $mediaDirectory 'codex-presets.mp4'
& ffmpeg -y -hide_banner -loglevel error -i (Join-Path $InputDirectory 'raw.mp4') -t (Number $duration) -vf ($filters -join ',') -c:v libx264 -crf 18 -pix_fmt yuv420p -movflags +faststart $outputVideo
if ($LASTEXITCODE -ne 0) { throw 'Codex demo video encoding failed.' }
& ffmpeg -y -hide_banner -loglevel error -i $outputVideo -filter_complex 'fps=12,scale=960:-2:flags=lanczos,split[a][b];[a]palettegen=max_colors=128:stats_mode=full[p];[b][p]paletteuse=dither=none:diff_mode=rectangle' -loop 0 (Join-Path $mediaDirectory 'codex-presets.gif')
if ($LASTEXITCODE -ne 0) { throw 'Codex demo GIF encoding failed.' }
Get-Item $outputVideo, (Join-Path $mediaDirectory 'codex-presets.gif') | Select-Object Name, Length
