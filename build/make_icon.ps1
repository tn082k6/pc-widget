Add-Type -AssemblyName System.Drawing
$dir = $PSScriptRoot
function Draw([int]$s) {
    $bmp = New-Object System.Drawing.Bitmap($s, $s, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.Clear([System.Drawing.Color]::Transparent)
    $r = $s * 0.22; $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $w = $s - 1
    $p.AddArc(0, 0, $r * 2, $r * 2, 180, 90); $p.AddArc($w - $r * 2, 0, $r * 2, $r * 2, 270, 90)
    $p.AddArc($w - $r * 2, $w - $r * 2, $r * 2, $r * 2, 0, 90); $p.AddArc(0, $w - $r * 2, $r * 2, $r * 2, 90, 90); $p.CloseFigure()
    $br = New-Object System.Drawing.Drawing2D.LinearGradientBrush((New-Object System.Drawing.Point(0, 0)), (New-Object System.Drawing.Point(0, $s)), [System.Drawing.Color]::FromArgb(255, 44, 46, 54), [System.Drawing.Color]::FromArgb(255, 4, 4, 6))
    $g.FillPath($br, $p)
    # drei Ringe wie im Widget (Eis-Weiss, Blau, Grau)
    $cx = $s / 2; $cy = $s / 2
    $rings = @(@(0.34, 235, 245, 255, 300), @(0.245, 10, 132, 255, 220), @(0.15, 160, 172, 188, 150))
    $th = [Math]::Max(1.5, $s * 0.075)
    foreach ($ring in $rings) {
        $rr = $s * $ring[0]
        $track = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(45, $ring[1], $ring[2], $ring[3]), $th)
        $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, $ring[1], $ring[2], $ring[3]), $th)
        $pen.StartCap = 'Round'; $pen.EndCap = 'Round'
        $g.DrawEllipse($track, $cx - $rr, $cy - $rr, $rr * 2, $rr * 2)
        $g.DrawArc($pen, $cx - $rr, $cy - $rr, $rr * 2, $rr * 2, -90, $ring[4])
    }
    $g.Dispose(); return $bmp
}
$sizes = 256, 64, 48, 32, 16
$pngs = foreach ($s in $sizes) { $b = Draw $s; $ms = New-Object IO.MemoryStream; $b.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png); if ($s -eq 256) { $b.Save("$dir\icon_preview.png") }; $b.Dispose(); , $ms.ToArray() }
$out = New-Object IO.MemoryStream; $bw = New-Object IO.BinaryWriter($out)
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $d = $pngs[$i]
    $bw.Write([byte]($(if ($s -ge 256) { 0 } else { $s }))); $bw.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))
    $bw.Write([byte]0); $bw.Write([byte]0); $bw.Write([UInt16]1); $bw.Write([UInt16]32)
    $bw.Write([UInt32]$d.Length); $bw.Write([UInt32]$offset); $offset += $d.Length
}
foreach ($d in $pngs) { $bw.Write($d) }
[IO.File]::WriteAllBytes("$dir\PCWidget.ico", $out.ToArray())
"icon ok"
