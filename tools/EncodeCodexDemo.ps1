param([string]$InputDirectory = (Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts\codex-demo-full'))
$ErrorActionPreference = 'Stop'
$projectDirectory = Split-Path $PSScriptRoot -Parent
$mediaDirectory = Join-Path $projectDirectory 'docs\media'
$events = Get-Content (Join-Path $InputDirectory 'events.json') -Raw | ConvertFrom-Json
$duration = [double]::Parse((Get-Content (Join-Path $InputDirectory 'end.txt') -Raw), [Globalization.CultureInfo]::InvariantCulture)
function Number($value) { ([double]$value).ToString('0.000', [Globalization.CultureInfo]::InvariantCulture) }
$font = "fontfile='C\:/Windows/Fonts/segoeui.ttf'"
$filters = [Collections.Generic.List[string]]::new()
$filters.Add('[0:v]scale=960:-2:flags=lanczos,pad=1400:820:440:80:color=0x17181C[canvas]')
$filters.Add('[1:v]scale=360:360:flags=lanczos[idle]')
$filters.Add('[canvas][idle]overlay=30:210[base0]')
for ($index = 0; $index -lt 3; $index++) {
    $event = $events[$index]
    if ($event.key -ne @('H','I','J')[$index] -or $event.exitCode -ne 0) { throw 'Expected verified preset selections in order.' }
    $start = Number $event.indication
    $end = if ($index -lt 2) { Number $events[$index + 1].indication } else { Number $duration }
    $filters.Add("[$($index+2):v]scale=360:360:flags=lanczos[key$index]")
    $filters.Add("[base$index][key$index]overlay=30:210:enable='between(t,$start,$end)'[base$($index+1)]")
}
$labels = [Collections.Generic.List[string]]::new()
$labels.Add("drawtext=$font`:text='Creator Micro':x=30:y=60:fontsize=30:fontcolor=white")
$labels.Add("drawtext=$font`:text='Press a mapped key':x=30:y=110:fontsize=22:fontcolor=0xABAEBC")
$labels.Add("drawtext=$font`:text='Live Codex':x=460:y=28:fontsize=28:fontcolor=white")
for ($index = 0; $index -lt 3; $index++) {
    $key = @('H','I','J')[$index]
    $event = $events[$index]
    if ($event.key -ne $key -or $event.exitCode -ne 0) { throw 'The recording must contain verified H, I, J selections in order.' }
    $start = Number $event.indication
    $end = if ($index -lt 2) { Number $events[$index + 1].indication } else { Number $duration }
    $enabled = "enable='between(t,$start,$end)'"
    $model = @('GPT-6 Luna','GPT-6.1 Sol','GPT-6 Astra')[$index]
    $effort = @('Extra High','High','Low')[$index]
    $labels.Add("drawtext=$font`:text='$model':x=30:y=610:fontsize=28:fontcolor=white:$enabled")
    $labels.Add("drawtext=$font`:text='$effort':x=30:y=655:fontsize=24:fontcolor=0xB1A4FF:$enabled")
}
$filters.Add('[base3]' + ($labels -join ',') + '[out]')
$outputVideo = Join-Path $mediaDirectory 'codex-presets.mp4'
$keypadDirectory = Join-Path $projectDirectory 'assets\demo-keypad'
& ffmpeg -y -hide_banner -loglevel error -i (Join-Path $InputDirectory 'raw.mp4') -loop 1 -i (Join-Path $keypadDirectory 'idle.png') -loop 1 -i (Join-Path $keypadDirectory 'luna.png') -loop 1 -i (Join-Path $keypadDirectory 'sol.png') -loop 1 -i (Join-Path $keypadDirectory 'astra.png') -t (Number $duration) -filter_complex ($filters -join ';') -map '[out]' -c:v libx264 -crf 18 -pix_fmt yuv420p -movflags +faststart $outputVideo
if ($LASTEXITCODE -ne 0) { throw 'Codex demo video encoding failed.' }
& ffmpeg -y -hide_banner -loglevel error -i $outputVideo -filter_complex 'fps=12,scale=1100:-2:flags=lanczos,split[a][b];[a]palettegen=max_colors=128:stats_mode=full[p];[b][p]paletteuse=dither=none:diff_mode=rectangle' -loop 0 (Join-Path $mediaDirectory 'codex-presets.gif')
if ($LASTEXITCODE -ne 0) { throw 'Codex demo GIF encoding failed.' }
Get-Item $outputVideo, (Join-Path $mediaDirectory 'codex-presets.gif') | Select-Object Name, Length
