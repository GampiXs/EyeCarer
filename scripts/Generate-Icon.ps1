$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path -Parent $PSScriptRoot
$assetDir = Join-Path $root 'assets'
New-Item -ItemType Directory -Force -Path $assetDir | Out-Null
$images = @()
foreach ($size in @(16, 24, 32, 48, 64, 128, 256)) {
    $bitmap = New-Object System.Drawing.Bitmap($size, $size)
    $g = [System.Drawing.Graphics]::FromImage($bitmap)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.ScaleTransform(($size / 64.0), ($size / 64.0))
    $dark = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 24, 20, 28))
    $red = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 176, 45, 73))
    $light = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 247, 237, 239), 3.6)
    $light.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $g.FillEllipse($dark, 1, 1, 62, 62)
    $eye = New-Object System.Drawing.Drawing2D.GraphicsPath
    $eye.AddBezier(8, 32, 20, 12, 44, 12, 56, 32)
    $eye.AddBezier(56, 32, 44, 52, 20, 52, 8, 32)
    $g.DrawPath($light, $eye)
    $g.FillEllipse($red, 22, 21, 22, 22)
    $g.FillEllipse($dark, 29, 17, 20, 22)
    $stream = New-Object System.IO.MemoryStream
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $images += ,@{ Size = $size; Bytes = $stream.ToArray() }
    $stream.Dispose(); $eye.Dispose(); $light.Dispose(); $red.Dispose(); $dark.Dispose(); $g.Dispose(); $bitmap.Dispose()
}
$file = [System.IO.File]::Create((Join-Path $assetDir 'EyeCarer.ico'))
$writer = New-Object System.IO.BinaryWriter($file)
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$images.Count)
    $offset = 6 + 16 * $images.Count
    foreach ($image in $images) {
        $dimension = if ($image.Size -eq 256) { 0 } else { $image.Size }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
        $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$image.Bytes.Length); $writer.Write([uint32]$offset)
        $offset += $image.Bytes.Length
    }
    foreach ($image in $images) { $writer.Write([byte[]]$image.Bytes) }
} finally { $writer.Dispose(); $file.Dispose() }
