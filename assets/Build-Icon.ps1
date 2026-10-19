$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$frames = @()
foreach ($size in @(16, 20, 24, 32, 40, 48, 64, 128, 256)) {
    $bitmap = [Drawing.Bitmap]::new($size, $size)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.ScaleTransform($size / 256.0, $size / 256.0)
    $bubble = [Drawing.Drawing2D.GraphicsPath]::new()
    $bubble.AddArc(12, 12, 88, 88, 180, 90)
    $bubble.AddArc(156, 12, 88, 88, 270, 90)
    $bubble.AddArc(156, 132, 88, 88, 0, 90)
    $bubble.AddLine(108, 220, 56, 250)
    $bubble.AddLine(56, 250, 56, 220)
    $bubble.AddArc(12, 132, 88, 88, 90, 90)
    $bubble.CloseFigure()
    $gradient = [Drawing.Drawing2D.LinearGradientBrush]::new([Drawing.Point]::new(0, 0), [Drawing.Point]::new(256, 256), [Drawing.Color]::FromArgb(31, 92, 224), [Drawing.Color]::FromArgb(65, 44, 171))
    $graphics.FillPath($gradient, $bubble)
    $white = [Drawing.SolidBrush]::new([Drawing.Color]::White)
    $cyan = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(78, 229, 235))
    $graphics.FillRectangle($white, 76, 58, 27, 122)
    $graphics.FillRectangle($white, 76, 153, 98, 27)
    $graphics.FillEllipse($cyan, 157, 61, 30, 30)
    $stream = [IO.MemoryStream]::new()
    $bitmap.Save($stream, [Drawing.Imaging.ImageFormat]::Png)
    $frames += [pscustomobject]@{ Size = $size; Bytes = $stream.ToArray() }
    $stream.Dispose(); $cyan.Dispose(); $white.Dispose(); $gradient.Dispose(); $bubble.Dispose(); $graphics.Dispose(); $bitmap.Dispose()
}
$iconFile = [IO.File]::Create((Join-Path $PSScriptRoot 'Lexora.ico'))
$writer = [IO.BinaryWriter]::new($iconFile)
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$frames.Count)
    $offset = 6 + 16 * $frames.Count
    foreach ($frame in $frames) {
        $dimension = if ($frame.Size -eq 256) { 0 } else { $frame.Size }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
        $writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$frame.Bytes.Length); $writer.Write([uint32]$offset)
        $offset += $frame.Bytes.Length
    }
    foreach ($frame in $frames) { $writer.Write([byte[]]$frame.Bytes) }
} finally { $writer.Dispose() }
