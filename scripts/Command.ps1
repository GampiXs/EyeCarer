param([ValidateSet('status','rest','pause','resume','dismiss','menu','exit')][string]$Command = 'status')
$ErrorActionPreference = 'Stop'
$pipeName = 'EyeCarer.30F702CA.' + [Environment]::UserName
$client = New-Object System.IO.Pipes.NamedPipeClientStream('.', $pipeName, [System.IO.Pipes.PipeDirection]::InOut)
try {
    $client.Connect(2500)
    $writer = New-Object System.IO.StreamWriter($client)
    $writer.AutoFlush = $true
    $reader = New-Object System.IO.StreamReader($client)
    $writer.WriteLine($Command)
    $reader.ReadLine()
} finally { $client.Dispose() }
