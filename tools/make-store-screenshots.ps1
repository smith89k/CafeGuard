# Makes 1920x1080 Store screenshots from the real card pictures (docs\store\screenshots\card-*.png).
# Run the app with --render-card first (see README); this only places them on a plain background with a headline.
Add-Type -AssemblyName System.Drawing
$dir = Join-Path $PSScriptRoot "..\docs\store\screenshots"
$shots = @(
    @{ Card = 'card-off.png';   Out = 'screenshot-1-off.png';   Title = 'Going to the bathroom?';      Text = 'One click turns Cafe Guard ON.' },
    @{ Card = 'card-on.png';    Out = 'screenshot-2-on.png';    Title = 'Cafe Guard is watching';      Text = 'Charger, lid, sleep, shutdown and headphones.' },
    @{ Card = 'card-alarm.png'; Out = 'screenshot-3-alarm.png'; Title = 'Touch it and everyone hears'; Text = 'A loud siren and voice. Only your Windows PIN stops it.' }
)
$green = [System.Drawing.ColorTranslator]::FromHtml('#16A34A')
foreach ($s in $shots) {
    $bmp = New-Object System.Drawing.Bitmap 1920, 1080
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.TextRenderingHint = 'AntiAliasGridFit'; $g.InterpolationMode = 'HighQualityBicubic'
    $card = [System.Drawing.Image]::FromFile((Join-Path $dir $s.Card))
    # Same color as the card picture's own backdrop, so the card blends in.
    $g.Clear(([System.Drawing.Bitmap]$card).GetPixel(2, 2))
    $h = [Math]::Min(960, $card.Height); $w = [int]($card.Width * $h / $card.Height)
    $g.DrawImage($card, 1920 - $w - 90, [int]((1080 - $h) / 2), $w, $h)
    $titleFont = New-Object System.Drawing.Font 'Bahnschrift', 64, ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
    $textFont = New-Object System.Drawing.Font 'Bahnschrift', 36, ([System.Drawing.FontStyle]::Regular), ([System.Drawing.GraphicsUnit]::Pixel)
    $nameFont = New-Object System.Drawing.Font 'Bahnschrift', 40, ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
    $g.DrawString('Cafe Guard', $nameFont, (New-Object System.Drawing.SolidBrush $green), 120, 330)
    $area = New-Object System.Drawing.RectangleF 120, 400, (1920 - $w - 300), 400
    $g.DrawString($s.Title, $titleFont, [System.Drawing.Brushes]::Black, $area)
    $area2 = New-Object System.Drawing.RectangleF 120, 560, (1920 - $w - 300), 300
    $g.DrawString($s.Text, $textFont, (New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml('#374151'))), $area2)
    $card.Dispose()
    $bmp.Save((Join-Path $dir $s.Out), [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    "Wrote $($s.Out)"
}
