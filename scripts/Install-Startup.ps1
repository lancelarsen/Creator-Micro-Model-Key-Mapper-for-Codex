param([string]$ApplicationPath)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ApplicationPath)) {
    $ApplicationPath = Join-Path (Split-Path $PSScriptRoot -Parent) 'CodexMicroPresets.exe'
}
$resolvedApp = Resolve-Path -LiteralPath $ApplicationPath -ErrorAction SilentlyContinue
if (-not $resolvedApp -or -not (Test-Path -LiteralPath $resolvedApp.ProviderPath -PathType Leaf)) {
    throw 'Build the app first, then pass the executable path with -ApplicationPath.'
}
$appPath = $resolvedApp.ProviderPath

$startupFolder = [Environment]::GetFolderPath('Startup')
$shortcutPath = Join-Path $startupFolder 'Micro Model Presets.lnk'
$shell = New-Object -ComObject WScript.Shell
foreach ($legacyName in @('Codex Micro Model Presets.lnk')) {
    $legacy = Join-Path $startupFolder $legacyName
    if (Test-Path -LiteralPath $legacy) {
        $target = $shell.CreateShortcut($legacy).TargetPath
        if ([IO.Path]::GetFileName($target) -eq 'CodexMicroPresets.exe') { Remove-Item -LiteralPath $legacy }
    }
}
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $appPath
$shortcut.Arguments = '--listen'
$shortcut.WorkingDirectory = Split-Path $appPath -Parent
$shortcut.WindowStyle = 7
$shortcut.Description = 'Start the Micro model preset keys after sign-in'
$shortcut.Save()
Write-Output "Automatic startup enabled: $shortcutPath"
