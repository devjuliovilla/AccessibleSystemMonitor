param(
    [string]$OutputPath = (Join-Path $PSScriptRoot '..\assets\ASM.ico')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$resolvedPath = [System.IO.Path]::GetFullPath($OutputPath)
[System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($resolvedPath)) | Out-Null
$bitmap = [System.Drawing.Bitmap]::new(64, 64, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
$graphics.Clear([System.Drawing.Color]::Transparent)

$background = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
    [System.Drawing.Rectangle]::new(2, 2, 60, 60),
    [System.Drawing.Color]::FromArgb(15, 38, 64),
    [System.Drawing.Color]::FromArgb(0, 130, 150),
    45.0
)
$path = [System.Drawing.Drawing2D.GraphicsPath]::new()
$path.AddArc(2, 2, 16, 16, 180, 90)
$path.AddArc(46, 2, 16, 16, 270, 90)
$path.AddArc(46, 46, 16, 16, 0, 90)
$path.AddArc(2, 46, 16, 16, 90, 90)
$path.CloseFigure()
$graphics.FillPath($background, $path)

$accent = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(80, 235, 205), 3.5)
$accent.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$accent.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$accent.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
$points = [System.Drawing.PointF[]]@(
    [System.Drawing.PointF]::new(9, 15),
    [System.Drawing.PointF]::new(19, 15),
    [System.Drawing.PointF]::new(25, 8),
    [System.Drawing.PointF]::new(31, 21),
    [System.Drawing.PointF]::new(37, 13),
    [System.Drawing.PointF]::new(54, 13)
)
$graphics.DrawLines($accent, $points)

$font = [System.Drawing.Font]::new('Segoe UI', 20, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$format = [System.Drawing.StringFormat]::new()
$format.Alignment = [System.Drawing.StringAlignment]::Center
$format.LineAlignment = [System.Drawing.StringAlignment]::Center
$graphics.DrawString('ASM', $font, [System.Drawing.Brushes]::White, [System.Drawing.RectangleF]::new(3, 25, 58, 31), $format)

$hIcon = $bitmap.GetHicon()
try {
    $icon = [System.Drawing.Icon]::FromHandle($hIcon)
    $stream = [System.IO.File]::Create($resolvedPath)
    try { $icon.Save($stream) } finally { $stream.Dispose(); $icon.Dispose() }
}
finally {
    Add-Type -Namespace Native -Name IconMethods -MemberDefinition '[System.Runtime.InteropServices.DllImport("user32.dll")] public static extern bool DestroyIcon(System.IntPtr handle);' -ErrorAction SilentlyContinue
    [Native.IconMethods]::DestroyIcon($hIcon) | Out-Null
    $format.Dispose()
    $font.Dispose()
    $accent.Dispose()
    $path.Dispose()
    $background.Dispose()
    $graphics.Dispose()
    $bitmap.Dispose()
}

Write-Output "Icono creado: $resolvedPath"
