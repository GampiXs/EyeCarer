$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$output = Join-Path $root ('artifacts\package-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $output | Out-Null
& dotnet publish (Join-Path $root 'src\EyeCarer\EyeCarer.csproj') -c Release -r win-x64 --self-contained true -p:DebugType=None -p:DebugSymbols=false -o $output
if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
$runtime = Get-Content -LiteralPath (Join-Path $output 'EyeCarer.runtimeconfig.json') -Raw | ConvertFrom-Json
$cache = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget\packages' }
foreach ($framework in $runtime.runtimeOptions.includedFrameworks) {
    $package = if ($framework.name -eq 'Microsoft.NETCore.App') { 'microsoft.netcore.app.runtime.win-x64' } else { 'microsoft.windowsdesktop.app.runtime.win-x64' }
    $packageRoot = Join-Path $cache ($package + '\' + $framework.version)
    $licenses = @(Get-ChildItem -LiteralPath $packageRoot -File | Where-Object { $_.Name -match 'LICENSE|NOTICES' })
    if ($licenses.Count -eq 0) { throw "Missing redistribution license: $package" }
    foreach ($license in $licenses) { Copy-Item -LiteralPath $license.FullName -Destination (Join-Path $output ($package + '-' + $license.Name + '.txt')) }
}
Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination (Join-Path $output 'EyeCarer-LICENSE.txt')
@'
EyeCarer 1.0.0 — Windows 11 x64 (verified)
Extract every file into a stable writable folder and run EyeCarer.exe.
No separate .NET runtime or administrator rights required. This build is unsigned.
Start with Windows is enabled by default and creates a per-user Startup shortcut.
Double-click the tray icon for Settings. Esc dismisses a break.
To uninstall: disable Start with Windows, Exit EyeCarer, then remove this folder.
Full documentation and source: https://github.com/GampiXs/EyeCarer
EyeCarer is a break reminder, not a medical device.
'@ | Set-Content -LiteralPath (Join-Path $output 'START-HERE.txt') -Encoding UTF8
$zip = Join-Path $root 'artifacts\EyeCarer-win-x64.zip'
Compress-Archive -Path (Join-Path $output '*') -DestinationPath $zip -Force
$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  EyeCarer-win-x64.zip" | Set-Content -LiteralPath (Join-Path $root 'artifacts\SHA256SUMS.txt') -Encoding ASCII
Write-Output "Package: $zip"
Write-Output "SHA256: $hash"
