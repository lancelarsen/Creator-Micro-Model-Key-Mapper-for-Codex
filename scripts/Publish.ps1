$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
& dotnet publish (Join-Path $root 'MicroPresets.csproj') -c Release -r win-x64 --self-contained false -o (Join-Path $root 'publish')
if ($LASTEXITCODE -ne 0) { throw 'Helper publish failed.' }
& dotnet publish (Join-Path $root 'Editor\CreatorMicroEditor.csproj') -c Release -r win-x64 --self-contained true -o (Join-Path $root 'publish\editor')
if ($LASTEXITCODE -ne 0) { throw 'Editor publish failed.' }
