$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

$assets = Join-Path (Split-Path $PSScriptRoot -Parent) 'assets'
$source = Join-Path $assets "fish-icon.png"
$png256 = Join-Path $assets "fish-icon-256.png"
$ico = Join-Path $assets "fish.ico"

$image = [System.Drawing.Image]::FromFile($source)
$cleaned = New-Object System.Drawing.Bitmap -ArgumentList $image.Width, $image.Height, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$cleanedGraphics = [System.Drawing.Graphics]::FromImage($cleaned)
$cleanedGraphics.DrawImage($image, 0, 0, $image.Width, $image.Height)
$cleanedGraphics.Dispose()

$width = $cleaned.Width
$height = $cleaned.Height
$visited = New-Object 'bool[]' ($width * $height)
$queue = New-Object 'System.Collections.Generic.Queue[System.Drawing.Point]'

function Test-BackgroundPixel {
  param([System.Drawing.Color]$Color)
  return ($Color.A -gt 0 -and $Color.R -le 24 -and $Color.G -le 24 -and $Color.B -le 24)
}

function Add-Seed {
  param([int]$X, [int]$Y)

  $index = $Y * $width + $X
  if ($visited[$index]) {
    return
  }

  $visited[$index] = $true
  if (Test-BackgroundPixel $cleaned.GetPixel($X, $Y)) {
    $queue.Enqueue([System.Drawing.Point]::new($X, $Y))
  }
}

for ($x = 0; $x -lt $width; $x++) {
  Add-Seed $x 0
  Add-Seed $x ($height - 1)
}

for ($y = 0; $y -lt $height; $y++) {
  Add-Seed 0 $y
  Add-Seed ($width - 1) $y
}

while ($queue.Count -gt 0) {
  $point = $queue.Dequeue()
  $cleaned.SetPixel($point.X, $point.Y, [System.Drawing.Color]::Transparent)

  $neighbors = @(
    [System.Drawing.Point]::new($point.X - 1, $point.Y),
    [System.Drawing.Point]::new($point.X + 1, $point.Y),
    [System.Drawing.Point]::new($point.X, $point.Y - 1),
    [System.Drawing.Point]::new($point.X, $point.Y + 1)
  )

  foreach ($neighbor in $neighbors) {
    if ($neighbor.X -lt 0 -or $neighbor.Y -lt 0 -or $neighbor.X -ge $width -or $neighbor.Y -ge $height) {
      continue
    }

    $index = $neighbor.Y * $width + $neighbor.X
    if ($visited[$index]) {
      continue
    }

    $visited[$index] = $true
    if (Test-BackgroundPixel $cleaned.GetPixel($neighbor.X, $neighbor.Y)) {
      $queue.Enqueue($neighbor)
    }
  }
}

$bitmap = New-Object System.Drawing.Bitmap -ArgumentList 256, 256, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
$graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$graphics.Clear([System.Drawing.Color]::Transparent)
$graphics.DrawImage($cleaned, 0, 0, 256, 256)
$bitmap.Save($png256, [System.Drawing.Imaging.ImageFormat]::Png)
$graphics.Dispose()
$bitmap.Dispose()
$cleaned.Dispose()
$image.Dispose()

$pngBytes = [System.IO.File]::ReadAllBytes($png256)
$stream = [System.IO.File]::Create($ico)
$writer = New-Object System.IO.BinaryWriter($stream)

$writer.Write([UInt16]0)
$writer.Write([UInt16]1)
$writer.Write([UInt16]1)
$writer.Write([Byte]0)
$writer.Write([Byte]0)
$writer.Write([Byte]0)
$writer.Write([Byte]0)
$writer.Write([UInt16]1)
$writer.Write([UInt16]32)
$writer.Write([UInt32]$pngBytes.Length)
$writer.Write([UInt32]22)
$writer.Write($pngBytes)

$writer.Dispose()
$stream.Dispose()

Write-Host "Built $ico"
