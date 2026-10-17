param([string]$Source, [string]$Target, [string]$Output)
Add-Type -AssemblyName System.Drawing
$reference = [System.Drawing.Image]::FromFile($Source)
$implementation = [System.Drawing.Image]::FromFile($Target)
$comparison = [System.Drawing.Bitmap]::new(760,350)
$graphics = [System.Drawing.Graphics]::FromImage($comparison)
$font = [System.Drawing.Font]::new('Segoe UI',12)
try {
    $graphics.Clear([System.Drawing.Color]::White)
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.DrawString('Reference (green intentionally replaced)', $font, [System.Drawing.Brushes]::Black, 10, 8)
    $graphics.DrawString('Branch: black Codex / orange Claude', $font, [System.Drawing.Brushes]::Black, 390, 8)
    # Remove the supplied reference's surrounding white margin, not app content.
    $graphics.DrawImage($reference,[System.Drawing.Rectangle]::new(10,40,360,287),39,39,1328,1059,[System.Drawing.GraphicsUnit]::Pixel)
    $graphics.DrawImage($implementation,[System.Drawing.Rectangle]::new(390,40,$implementation.Width,$implementation.Height))
    $comparison.Save($Output,[System.Drawing.Imaging.ImageFormat]::Png)
} finally {
    $font.Dispose(); $graphics.Dispose(); $comparison.Dispose(); $implementation.Dispose(); $reference.Dispose()
}
