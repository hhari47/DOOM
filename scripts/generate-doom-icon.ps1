param(
    [string]$OutputPath = "src/DOOM.App/Assets/DOOM.ico"
)

Add-Type -AssemblyName System.Drawing

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$pngStreams = @()

$dir = Split-Path -Parent $OutputPath
if (-not (Test-Path $dir)) {
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
}

foreach ($size in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    $margin = [Math]::Max(1, [int]($size * 0.06))
    $rect = New-Object System.Drawing.Rectangle($margin, $margin, ($size - 2 * $margin), ($size - 2 * $margin))
    $cornerRadius = [Math]::Max(2, [int]($size * 0.22))

    # Path for rounded rectangle
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $diameter = $cornerRadius * 2
    $arcRect = New-Object System.Drawing.Rectangle($rect.X, $rect.Y, $diameter, $diameter)
    $path.AddArc($arcRect, 180, 90)
    $arcRect.X = $rect.Right - $diameter
    $path.AddArc($arcRect, 270, 90)
    $arcRect.Y = $rect.Bottom - $diameter
    $path.AddArc($arcRect, 0, 90)
    $arcRect.X = $rect.Left
    $path.AddArc($arcRect, 90, 90)
    $path.CloseFigure()

    # Fill background with dark gradient #09090D -> #16161F
    $bgBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $rect,
        [System.Drawing.Color]::FromArgb(255, 14, 14, 20),
        [System.Drawing.Color]::FromArgb(255, 24, 24, 34),
        45.0
    )
    $g.FillPath($bgBrush, $path)
    $bgBrush.Dispose()

    # Border glow / stroke #10B981 (Emerald accent)
    $penWidth = [Math]::Max(1.0, ($size * 0.05))
    $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(220, 16, 185, 129), $penWidth)
    $g.DrawPath($pen, $path)
    $pen.Dispose()
    $path.Dispose()

    # Draw central stylized Monogram "D"
    $fontSize = [float]($size * 0.58)
    $fontFamily = [System.Drawing.FontFamily]::GenericSansSerif
    $font = New-Object System.Drawing.Font($fontFamily, $fontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)

    $sf = New-Object System.Drawing.StringFormat
    $sf.Alignment = [System.Drawing.StringAlignment]::Center
    $sf.LineAlignment = [System.Drawing.StringAlignment]::Center

    # Text offset
    $textRect = New-Object System.Drawing.RectangleF(0, ($size * 0.02), $size, $size)

    # Soft shadow
    $shadowBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(160, 0, 0, 0))
    $shadowRect = New-Object System.Drawing.RectangleF(($size * 0.02), ($size * 0.04), $size, $size)
    $g.DrawString("D", $font, $shadowBrush, $shadowRect, $sf)
    $shadowBrush.Dispose()

    # White text
    $textBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 243, 244, 246))
    $g.DrawString("D", $font, $textBrush, $textRect, $sf)
    $textBrush.Dispose()

    # Emerald spark / dot in the upper right quadrant
    if ($size -ge 32) {
        $sparkSize = [Math]::Max(3, [int]($size * 0.12))
        $sparkX = [int]($size * 0.68)
        $sparkY = [int]($size * 0.22)
        $sparkBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 16, 185, 129))
        $g.FillEllipse($sparkBrush, $sparkX, $sparkY, $sparkSize, $sparkSize)
        $sparkBrush.Dispose()
    }

    $font.Dispose()
    $sf.Dispose()
    $g.Dispose()

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    $pngStreams += @{
        Size = $size
        Bytes = $ms.ToArray()
    }
    $ms.Dispose()
}

# Assemble ICO binary format
$fs = [System.IO.File]::Create($OutputPath)
$bw = New-Object System.IO.BinaryWriter($fs)

# ICONDIR
$bw.Write([uint16]0) # Reserved
$bw.Write([uint16]1) # Resource Type: 1 = Icon
$bw.Write([uint16]$pngStreams.Count) # Image count

# Calculate offsets
# Header size = 6 bytes + (16 bytes * count)
$offset = 6 + (16 * $pngStreams.Count)

foreach ($entry in $pngStreams) {
    $width = if ($entry.Size -ge 256) { 0 } else { [byte]$entry.Size }
    $height = if ($entry.Size -ge 256) { 0 } else { [byte]$entry.Size }
    $bytes = $entry.Bytes

    $bw.Write([byte]$width)
    $bw.Write([byte]$height)
    $bw.Write([byte]0)     # ColorCount
    $bw.Write([byte]0)     # Reserved
    $bw.Write([uint16]1)   # Color Planes
    $bw.Write([uint16]32)  # Bits per pixel
    $bw.Write([uint32]$bytes.Length) # Image byte count
    $bw.Write([uint32]$offset)       # Image data offset

    $offset += $bytes.Length
}

# Write PNG bytes
foreach ($entry in $pngStreams) {
    $bw.Write($entry.Bytes)
}

$bw.Flush()
$bw.Close()
$fs.Close()

Write-Host "Generated DOOM icon at: $OutputPath (Size: $((Get-Item $OutputPath).Length) bytes, $($pngStreams.Count) resolutions)"

