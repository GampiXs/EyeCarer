$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root 'publish\EyeCarer.exe'
Get-Process -Name EyeCarer -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe } | ForEach-Object {
    $process = $_
    Start-Process -FilePath $exe -ArgumentList '--exit' -WindowStyle Hidden -Wait
    if (-not $process.WaitForExit(5000)) { throw 'EyeCarer did not exit.' }
}
$linkPath = Join-Path ([Environment]::GetFolderPath('Startup')) 'EyeCarer.lnk'
if (Test-Path -LiteralPath $linkPath) {
    $shell = New-Object -ComObject WScript.Shell
    $link = $shell.CreateShortcut($linkPath)
    if ($link.TargetPath -eq $exe) { Remove-Item -LiteralPath $linkPath }
}
$configPath = Join-Path $root 'publish\config.json'
if (Test-Path -LiteralPath $configPath) {
    $config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
    $config.StartWithWindows = $false
    $config | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $configPath -Encoding UTF8
}
Write-Output 'EyeCarer stopped and its autostart removed. Source, binaries, configuration and logs retained.'
