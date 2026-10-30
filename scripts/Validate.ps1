$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root 'publish\EyeCarer.exe'
$configPath = Join-Path $root 'publish\config.json'
$results = [System.Collections.Generic.List[string]]::new()
function Command([string]$name) { (& (Join-Path $PSScriptRoot 'Command.ps1') $name) | ConvertFrom-Json }
function Assert([bool]$ok, [string]$message) {
    if (-not $ok) { throw "FAIL $message" }
    $results.Add("PASS $message"); Write-Output "PASS $message"
}
function Stop-App {
    $running = Get-Process -Name EyeCarer -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe }
    if ($running) { $null = Command 'exit'; foreach ($p in $running) { if (-not $p.WaitForExit(5000)) { throw 'Exit timed out' } } }
}
$original = Get-Content -LiteralPath $configPath -Raw
try {
    Stop-App
    $config = $original | ConvertFrom-Json
    $config.IntervalSeconds = 30; $config.DurationSeconds = 5; $config.StartImmediately = $false
    $config | ConvertTo-Json | Set-Content -LiteralPath $configPath -Encoding UTF8
    $process = Start-Process -FilePath $exe -ArgumentList '--diagnostics' -WindowStyle Hidden -PassThru
    Start-Sleep -Milliseconds 1200
    $initial = Command 'status'
    Assert ($initial.OverlayCount -eq 0 -and $initial.NextInSeconds -gt 25) 'No immediate break; initial 30-second interval'
    Start-Process -FilePath $exe -WindowStyle Hidden -Wait
    $instances = @(Get-Process -Name EyeCarer | Where-Object { $_.Path -eq $exe })
    Assert ($instances.Count -eq 1) 'Second launch exits; single instance remains'
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $starts = [System.Collections.Generic.List[double]]::new()
    $ends = [System.Collections.Generic.List[double]]::new()
    $wasActive = $false
    while ($timer.Elapsed.TotalSeconds -lt 72 -and $ends.Count -lt 2) {
        $state = Command 'status'
        $active = $state.OverlayCount -gt 0
        if ($active -and -not $wasActive) { $starts.Add($timer.Elapsed.TotalSeconds) }
        if (-not $active -and $wasActive) { $ends.Add($timer.Elapsed.TotalSeconds) }
        $wasActive = $active
        Start-Sleep -Milliseconds 200
    }
    Assert ($starts.Count -eq 2 -and $ends.Count -eq 2) 'Two real automatic breaks appear and disappear'
    Assert ([Math]::Abs(($starts[1] - $starts[0]) - 30) -lt 1) 'Consecutive automatic starts are 30 seconds apart'
    Assert ([Math]::Abs(($ends[0] - $starts[0]) - 5) -lt 1 -and [Math]::Abs(($ends[1] - $starts[1]) - 5) -lt 1) 'Both overlays last 5 seconds including fades'
    $paused = Command 'pause'
    Assert ($paused.Paused -and $paused.OverlayCount -eq 0 -and $paused.NextInSeconds -gt 3600) 'Pause for one hour'
    $manual = Command 'rest'
    Assert ($manual.OverlayCount -gt 0 -and $manual.Paused) 'Manual rest remains available while paused'
    $null = Command 'dismiss'
    $resumed = Command 'resume'
    Assert (-not $resumed.Paused -and $resumed.NextInSeconds -gt 29) 'Resume resets a full interval'
    $null = Command 'pause'
    $samples = @()
    for ($i = 1; $i -le 20; $i++) {
        $null = Command 'rest'; Start-Sleep -Milliseconds 120
        $state = Command 'dismiss'; Start-Sleep -Milliseconds 120
        Assert ($state.OverlayCount -eq 0) "Resource cycle $i closes all overlays"
        $process.Refresh()
        $samples += [pscustomobject]@{ Cycle=$i; WorkingSetMB=[Math]::Round($process.WorkingSet64 / 1MB, 1); Handles=$process.HandleCount }
    }
    $samples | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $root 'tests\resource-samples.json') -Encoding UTF8
    $samples | Format-Table | Out-String | Write-Output
    Assert (($samples[-1].Handles - $samples[9].Handles) -lt 40) 'No sustained handle accumulation over last ten cycles'
    $cpuBefore = $process.TotalProcessorTime.TotalSeconds
    Start-Sleep -Seconds 3
    $process.Refresh()
    $cpuDelta = $process.TotalProcessorTime.TotalSeconds - $cpuBefore
    $results.Add("INFO Paused idle CPU seconds over 3 seconds: $cpuDelta")
    Stop-App
    Assert (-not (Get-Process -Name EyeCarer -ErrorAction SilentlyContinue)) 'Exit terminates the process'
    $shell = New-Object -ComObject WScript.Shell
    $link = $shell.CreateShortcut((Join-Path ([Environment]::GetFolderPath('Startup')) 'EyeCarer.lnk'))
    Assert ($link.TargetPath -eq $exe -and (Test-Path -LiteralPath $link.TargetPath)) 'Startup shortcut targets the published executable'
}
finally {
    Stop-App
    # Always restore production values even if validation fails.
    $production = $original | ConvertFrom-Json
    $production.IntervalSeconds = 900; $production.DurationSeconds = 15; $production.StartImmediately = $false
    $production | ConvertTo-Json | Set-Content -LiteralPath $configPath -Encoding UTF8
    $results | Set-Content -LiteralPath (Join-Path $root 'tests\functional-results.txt') -Encoding UTF8
    Start-Process -FilePath $exe -WorkingDirectory (Join-Path $root 'publish') -WindowStyle Hidden
}
