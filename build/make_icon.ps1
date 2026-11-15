# Erzeugt das App-Icon (iOS-Stil): Squircle, Mini-Widget mit Ring, Musik-Balken und Werten.
# Wird 4-fach groesser gezeichnet und herunterskaliert -> saubere Kanten. Nur ASCII.
Add-Type -AssemblyName System.Drawing
$dir = $PSScriptRoot

function Squircle([float]$x, [float]$y, [float]$w, [float]$h, [float]$n = 5) {
    # Superellipse |x|^n + |y|^n = 1 (wie iOS-App-Icons)
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $pts = New-Object 'System.Collections.Generic.List[System.Drawing.PointF]'
    $cx = $x + $w / 2; $cy = $y + $h / 2; $a = $w / 2; $b = $h / 2
    for ($i = 0; $i -lt 360; $i++) {
        $t = $i * [Math]::PI / 180; $c = [Math]::Cos($t); $s = [Math]::Sin($t)
        $px = $cx + $a * [Math]::Sign($c) * [Math]::Pow([Math]::Abs($c), 2 / $n)
        $py = $cy + $b * [Math]::Sign($s) * [Math]::Pow([Math]::Abs($s), 2 / $n)
        $pts.Add((New-Object System.Drawing.PointF([float]$px, [float]$py)))
    }
    $p.AddPolygon($pts.ToArray()); return $p
}
function RoundRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath; $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90); $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90); $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90); $p.CloseFigure(); return $p
}
function C($a, $r, $g, $b) { [System.Drawing.Color]::FromArgb($a, $r, $g, $b) }

function Draw([int]$size) {
    $S = $size * 4                       # Supersampling
    $big = New-Object System.Drawing.Bitmap($S, $S, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($big); $g.SmoothingMode = 'AntiAlias'; $g.Clear([System.Drawing.Color]::Transparent)
    $u = $S / 100.0                      # 1 Einheit = 1 % der Icongroesse
    $detail = $size -ge 40

    # Hintergrund: tiefes Blau-Schwarz mit sanftem Verlauf
    $bg = Squircle (1 * $u) (1 * $u) (98 * $u) (98 * $u)
    $br = New-Object System.Drawing.Drawing2D.LinearGradientBrush((New-Object System.Drawing.PointF(0, 0)), (New-Object System.Drawing.PointF(0, $S)), (C 255 30 36 58), (C 255 6 7 12))
    $g.FillPath($br, $bg)
    # Farbschein oben links (wie der Cover-Schimmer im Widget)
    $glow = New-Object System.Drawing.Drawing2D.GraphicsPath; $glow.AddEllipse(-30 * $u, -35 * $u, 110 * $u, 100 * $u)
    $pg = New-Object System.Drawing.Drawing2D.PathGradientBrush($glow); $pg.CenterColor = (C 120 10 132 255); $pg.SurroundColors = @((C 0 10 132 255))
    $g.SetClip($bg); $g.FillPath($pg, $glow); $g.ResetClip()

    if ($detail) {
        # Mini-Widget-Karte
        $card = RoundRect (14 * $u) (20 * $u) (72 * $u) (60 * $u) (14 * $u)
        $g.FillPath((New-Object System.Drawing.SolidBrush((C 235 0 0 0))), $card)
        $g.DrawPath((New-Object System.Drawing.Pen((C 60 255 255 255), [float](1.2 * $u))), $card)
        # Ring (wie CPU-Ring) links
        $rr = 13 * $u; $cx = 34 * $u; $cy = 50 * $u; $th = 5.2 * $u
        $track = New-Object System.Drawing.Pen((C 45 255 255 255), [float]$th)
        $g.DrawEllipse($track, $cx - $rr, $cy - $rr, 2 * $rr, 2 * $rr)
        $ring = New-Object System.Drawing.Pen((C 255 10 132 255), [float]$th); $ring.StartCap = 'Round'; $ring.EndCap = 'Round'
        $g.DrawArc($ring, $cx - $rr, $cy - $rr, 2 * $rr, 2 * $rr, -90, 250)
        # Equalizer-Balken rechts oben
        $eq = @(9, 15, 6, 12)
        for ($i = 0; $i -lt 4; $i++) {
            $h = $eq[$i] * $u; $x = (54 + $i * 6) * $u
            $g.FillPath((New-Object System.Drawing.SolidBrush((C 255 235 245 255))), (RoundRect $x (45 * $u - $h) (3.4 * $u) $h (1.7 * $u)))
        }
        # zwei Wertebalken unten rechts
        $g.FillPath((New-Object System.Drawing.SolidBrush((C 60 255 255 255))), (RoundRect (54 * $u) (53 * $u) (24 * $u) (3.6 * $u) (1.8 * $u)))
        $g.FillPath((New-Object System.Drawing.SolidBrush((C 255 235 245 255))), (RoundRect (54 * $u) (53 * $u) (16 * $u) (3.6 * $u) (1.8 * $u)))
        $g.FillPath((New-Object System.Drawing.SolidBrush((C 60 255 255 255))), (RoundRect (54 * $u) (61 * $u) (24 * $u) (3.6 * $u) (1.8 * $u)))
        $g.FillPath((New-Object System.Drawing.SolidBrush((C 255 160 172 188))), (RoundRect (54 * $u) (61 * $u) (10 * $u) (3.6 * $u) (1.8 * $u)))
    } else {
        # klein: nur ein kraeftiger Ring, gut erkennbar
        $rr = 27 * $u; $th = 13 * $u
        $g.DrawEllipse((New-Object System.Drawing.Pen((C 60 255 255 255), [float]$th)), 50 * $u - $rr, 50 * $u - $rr, 2 * $rr, 2 * $rr)
        $ring = New-Object System.Drawing.Pen((C 255 10 132 255), [float]$th); $ring.StartCap = 'Round'; $ring.EndCap = 'Round'
        $g.DrawArc($ring, 50 * $u - $rr, 50 * $u - $rr, 2 * $rr, 2 * $rr, -90, 250)
    }
    # feiner heller Rand oben (Glas-Kante)
    $g.DrawPath((New-Object System.Drawing.Pen((C 40 255 255 255), [float](0.8 * $u))), $bg)
    $g.Dispose()

    $out = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g2 = [System.Drawing.Graphics]::FromImage($out); $g2.InterpolationMode = 'HighQualityBicubic'; $g2.PixelOffsetMode = 'HighQuality'; $g2.SmoothingMode = 'AntiAlias'
    $g2.DrawImage($big, 0, 0, $size, $size); $g2.Dispose(); $big.Dispose()
    return $out
}

$sizes = 256, 128, 64, 48, 32, 24, 16
$pngs = foreach ($s in $sizes) {
    $b = Draw $s; $ms = New-Object IO.MemoryStream; $b.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    if ($s -eq 256) { $b.Save("$dir\icon_preview.png") }; if ($s -eq 32) { $b.Save("$dir\icon_preview32.png") }
    $b.Dispose(); , $ms.ToArray()
}
$o = New-Object IO.MemoryStream; $w = New-Object IO.BinaryWriter($o)
$w.Write([UInt16]0); $w.Write([UInt16]1); $w.Write([UInt16]$sizes.Count)
$off = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $d = $pngs[$i]; $bs = if ($s -ge 256) { 0 } else { $s }
    $w.Write([byte]$bs); $w.Write([byte]$bs); $w.Write([byte]0); $w.Write([byte]0); $w.Write([UInt16]1); $w.Write([UInt16]32)
    $w.Write([UInt32]$d.Length); $w.Write([UInt32]$off); $off += $d.Length
}
foreach ($d in $pngs) { $w.Write($d) }
[IO.File]::WriteAllBytes("$dir\PCWidget.ico", $o.ToArray())
"icon ok"
