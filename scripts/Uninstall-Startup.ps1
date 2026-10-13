$ErrorActionPreference = 'Stop'
$startupFolder = [Environment]::GetFolderPath('Startup')
$shortcutPath = Join-Path $startupFolder 'Micro Model Presets.lnk'
if (Test-Path -LiteralPath $shortcutPath -PathType Leaf) {
    Remove-Item -LiteralPath $shortcutPath
    Write-Output 'Automatic startup disabled.'
} else {
    Write-Output 'No Micro model preset startup shortcut is installed.'
}
