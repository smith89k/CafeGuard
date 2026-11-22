# Draws the green CafeGuard shield into windows\CafeGuard.Shared\cafeguard.ico (16, 24, 32, 48, 64, 256 px).
# Same shield shape as Shield.cs. Two colors only, no edge: left half green #16A34A (normal), right half red #DC2626 (alert).
Add-Type -AssemblyName System.Drawing
$out = Join-Path $PSScriptRoot "..\windows\CafeGuard.Shared\cafeguard.ico"
$sizes = 16, 24, 32, 48, 64, 256
$shape = @(@(0.50,0.00), @(1.00,0.18), @(0.92,0.62), @(0.50,1.00), @(0.08,0.62), @(0.00,0.18))
$pngs = foreach ($s in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)
    $pad = [Math]::Max(1, $s * 0.06)
    $w = $s - 2 * $pad * 1.4; $h = $s - 2 * $pad
    $pts = foreach ($p in $shape) { New-Object System.Drawing.PointF ([float]($pad * 1.4 + $p[0] * $w)), ([float]($pad + $p[1] * $h)) }
    $g.FillPolygon((New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml('#16A34A'))), [System.Drawing.PointF[]]$pts)
    $g.SetClip((New-Object System.Drawing.RectangleF ([float]($s / 2)), 0, ([float]($s / 2)), ([float]$s)))
    $g.FillPolygon((New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml('#DC2626'))), [System.Drawing.PointF[]]$pts)
    $g.ResetClip()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    ,$ms.ToArray()
}
$fs = [System.IO.File]::Create($out)
$bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $len = $pngs[$i].Length
    $bw.Write([byte]($(if ($s -ge 256) { 0 } else { $s }))); $bw.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))
    $bw.Write([byte]0); $bw.Write([byte]0); $bw.Write([UInt16]1); $bw.Write([UInt16]32)
    $bw.Write([UInt32]$len); $bw.Write([UInt32]$offset); $offset += $len
}
foreach ($p in $pngs) { $bw.Write($p) }
$bw.Close()
"Wrote $out"
