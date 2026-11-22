# Draws the green/red CafeGuard shield as the Microsoft Store pictures in windows\CafeGuard.Store\Assets.
# Same shield as make-icon.ps1: left half green (normal), right half red (alert), no edge. Transparent background.
Add-Type -AssemblyName System.Drawing
$outDir = Join-Path $PSScriptRoot "..\windows\CafeGuard.Store\Assets"
New-Item -ItemType Directory -Force $outDir | Out-Null
$shape = @(@(0.50,0.00), @(1.00,0.18), @(0.92,0.62), @(0.50,1.00), @(0.08,0.62), @(0.00,0.18))
$green = [System.Drawing.ColorTranslator]::FromHtml('#16A34A')
$red = [System.Drawing.ColorTranslator]::FromHtml('#DC2626')

function Save-Shield([string]$name, [int]$width, [int]$height, [double]$shieldFraction, [string]$dir = $outDir) {
    $bmp = New-Object System.Drawing.Bitmap $width, $height
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)
    $h = [Math]::Min($width, $height) * $shieldFraction
    $w = $h * 0.88
    $x0 = ($width - $w) / 2; $y0 = ($height - $h) / 2
    $pts = foreach ($p in $shape) { New-Object System.Drawing.PointF ([float]($x0 + $p[0] * $w)), ([float]($y0 + $p[1] * $h)) }
    $g.FillPolygon((New-Object System.Drawing.SolidBrush $green), [System.Drawing.PointF[]]$pts)
    $g.SetClip((New-Object System.Drawing.RectangleF ([float]($width / 2)), 0, ([float]($width / 2)), ([float]$height)))
    $g.FillPolygon((New-Object System.Drawing.SolidBrush $red), [System.Drawing.PointF[]]$pts)
    $g.ResetClip()
    $path = Join-Path $dir $name
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    "Wrote $path"
}

Save-Shield "Square44x44Logo.png"   44  44  0.86
Save-Shield "Square150x150Logo.png" 150 150 0.60
Save-Shield "Wide310x150Logo.png"   310 150 0.60
Save-Shield "StoreLogo.png"         50  50  0.86

# Logo for the README on GitHub
$docsImages = Join-Path $PSScriptRoot "..\docs\images"
New-Item -ItemType Directory -Force $docsImages | Out-Null
Save-Shield "logo.png" 256 256 0.92 $docsImages
