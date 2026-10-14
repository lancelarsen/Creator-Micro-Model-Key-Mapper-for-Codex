param([string]$Source = (Join-Path (Split-Path $PSScriptRoot -Parent) 'publish'))
$ErrorActionPreference = 'Stop'
$Source = (Resolve-Path -LiteralPath $Source).Path
if (!(Test-Path -LiteralPath (Join-Path $Source 'editor\CreatorMicroEditor.exe'))) { throw 'Publish both projects before installing.' }
$target = Join-Path $env:LOCALAPPDATA 'CreatorMicroModelKeys'
$backup = Join-Path $env:LOCALAPPDATA ('CreatorMicroModelKeys-backup-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
$enableStartup = !(Test-Path -LiteralPath $target) -or (Test-Path -LiteralPath (Join-Path ([Environment]::GetFolderPath('Startup')) 'Creator Micro - Model Key Mapper for Codex.lnk'))
$helper = Join-Path $Source 'CodexMicroPresets.exe'
$stopProcess = Start-Process -FilePath $helper -ArgumentList '--stop' -WindowStyle Hidden -PassThru -Wait
if ($stopProcess.ExitCode -ne 0) { throw 'The running helper could not be stopped.' }
foreach ($process in Get-Process CreatorMicroEditor -ErrorAction SilentlyContinue) {
    if ($process.Path -eq (Join-Path $target 'editor\CreatorMicroEditor.exe')) {
        [void]$process.CloseMainWindow()
        if (!$process.WaitForExit(5000)) { throw 'Close the settings window before installing.' }
    }
}
if (Test-Path -LiteralPath $target) { Copy-Item -LiteralPath $target -Destination $backup -Recurse }
try {
    New-Item -ItemType Directory -Path $target -Force | Out-Null
    Get-ChildItem -LiteralPath $Source | Copy-Item -Destination $target -Recurse -Force
    if ($enableStartup) { & (Join-Path $PSScriptRoot 'Install-Startup.ps1') -ApplicationPath (Join-Path $target 'CodexMicroPresets.exe') }
    Start-Process -FilePath (Join-Path $target 'CodexMicroPresets.exe') -ArgumentList '--listen','--settings' -WindowStyle Hidden
    Write-Output "Installed: $target"
    if (Test-Path -LiteralPath $backup) { Write-Output "Previous installation: $backup" }
} catch {
    if (Test-Path -LiteralPath $backup) { Get-ChildItem -LiteralPath $backup | Copy-Item -Destination $target -Recurse -Force }
    throw
}
