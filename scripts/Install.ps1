param([switch]$NoLaunch)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root 'publish\EyeCarer.exe'
$configPath = Join-Path $root 'publish\config.json'
$existingConfig = if (Test-Path -LiteralPath $configPath) { Get-Content -LiteralPath $configPath -Raw } else { $null }
# Stop only an instance published at this exact path before replacing its files.
Get-Process -Name EyeCarer -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe } | ForEach-Object {
    $process = $_
    Start-Process -FilePath $exe -ArgumentList '--exit' -WindowStyle Hidden -Wait
    if (-not $process.WaitForExit(5000)) { throw 'EyeCarer did not exit; publication cancelled.' }
}
if (-not (Test-Path -LiteralPath (Join-Path $root 'assets\EyeCarer.ico'))) { & (Join-Path $PSScriptRoot 'Generate-Icon.ps1') }
& dotnet publish (Join-Path $root 'src\EyeCarer\EyeCarer.csproj') -c Release -r win-x64 --self-contained true -o (Join-Path $root 'publish')
if ($LASTEXITCODE -ne 0) { throw 'Release publish failed.' }
if (-not (Test-Path -LiteralPath $exe)) { throw 'Published executable is missing.' }
if ($existingConfig) {
    [IO.File]::WriteAllText(($configPath + '.tmp'), $existingConfig)
    [IO.File]::Replace(($configPath + '.tmp'), $configPath, ($configPath + '.bak'))
}
try { $startWithWindows = (Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json).StartWithWindows }
catch { $startWithWindows = $true }
$startup = [Environment]::GetFolderPath('Startup')
$shell = New-Object -ComObject WScript.Shell
$linkPath = Join-Path $startup 'EyeCarer.lnk'
if ($startWithWindows) {
$link = $shell.CreateShortcut($linkPath)
$link.TargetPath = $exe
$link.WorkingDirectory = Join-Path $root 'publish'
$link.IconLocation = (Join-Path $root 'publish\EyeCarer.ico') + ',0'
$link.Description = 'EyeCarer eye rest reminder'
$link.Save()
$verified = $shell.CreateShortcut($linkPath)
if ($verified.TargetPath -ne $exe) { throw 'Startup shortcut target mismatch.' }
} elseif (Test-Path -LiteralPath $linkPath) {
    $previousLink = $shell.CreateShortcut($linkPath)
    if ($previousLink.TargetPath -eq $exe) { Remove-Item -LiteralPath $linkPath }
}
Write-Output "Installed: $exe"
Write-Output "Autostart: $linkPath"
if (-not $NoLaunch) { Start-Process -FilePath $exe -WorkingDirectory (Join-Path $root 'publish') -WindowStyle Hidden }
