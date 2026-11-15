# PC Widget - Musik-Helfer
# Liest ueber Windows (Medien-Steuerung) aus, was Spotify gerade spielt,
# schreibt Titel/Kuenstler/Cover fuer Rainmeter in eine Datei und fuehrt
# Klicks auf Play/Pause/Weiter/Zurueck aus.
param([string]$Dir = $PSScriptRoot, [switch]$Gpu)

$ErrorActionPreference = 'SilentlyContinue'

# Nur eine Instanz
$mutexName = 'Local\PCWidgetMedia_' + ([BitConverter]::ToString([System.Security.Cryptography.MD5]::Create().ComputeHash([Text.Encoding]::UTF8.GetBytes($Dir.ToLower()))) -replace '-', '')
$mutex = New-Object System.Threading.Mutex($false, $mutexName)
try { if (-not $mutex.WaitOne(0)) { exit } } catch [System.Threading.AbandonedMutexException] { }

Add-Type -AssemblyName System.Runtime.WindowsRuntime
$null = [Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager, Windows.Media.Control, ContentType = WindowsRuntime]
$null = [Windows.Storage.Streams.IRandomAccessStreamWithContentType, Windows.Storage.Streams, ContentType = WindowsRuntime]
$null = [Windows.Storage.Streams.IInputStream, Windows.Storage.Streams, ContentType = WindowsRuntime]
$asStream = [System.IO.WindowsRuntimeStreamExtensions].GetMethod('AsStreamForRead', [Type[]]@([Windows.Storage.Streams.IInputStream]))

$asTaskGeneric = ([System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object {
    $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1'
})[0]
function Await($op, [Type]$type) {
    $task = $asTaskGeneric.MakeGenericMethod($type).Invoke($null, @($op))
    $null = $task.Wait(3000)
    $task.Result
}

$mgrType = [Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager]
$mgr = Await ($mgrType::RequestAsync()) $mgrType

$outFile = Join-Path $Dir 'nowplaying.txt'
$cmdFile = Join-Path $Dir 'cmd.ini'
$utf8 = New-Object System.Text.UTF8Encoding($false)
$lastKey = ''
$coverPath = ''
$coverToggle = 0
$tick = 0

function Get-Session {
    $sessions = $mgr.GetSessions()
    $s = $sessions | Where-Object { $_.SourceAppUserModelId -like '*Spotify*' } | Select-Object -First 1
    if (-not $s) { $s = $mgr.GetCurrentSession() }
    $s
}

function Save-Cover($props) {
    if (-not $props.Thumbnail) { return '' }
    try {
        $ras = Await ($props.Thumbnail.OpenReadAsync()) ([Windows.Storage.Streams.IRandomAccessStreamWithContentType])
        $src = $asStream.Invoke($null, @($ras))
        $script:coverToggle = 1 - $script:coverToggle
        $path = Join-Path $Dir ("cover_{0}.png" -f $script:coverToggle)
        $fs = [System.IO.File]::Create($path)
        $src.CopyTo($fs); $fs.Close(); $src.Close()
        return $path
    } catch { return '' }
}

# Spotify-Lautstaerke ueber den Windows-Lautstaerkemischer (pro App)
Add-Type -TypeDefinition @'
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
namespace PCW {
  [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class MMDeviceEnumerator {}
  [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
  interface IMMDeviceEnumerator { int _a(); [PreserveSig] int GetDefaultAudioEndpoint(int flow, int role, out IMMDevice dev); }
  [Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
  interface IMMDevice { [PreserveSig] int Activate(ref Guid iid, int ctx, IntPtr p, [MarshalAs(UnmanagedType.IUnknown)] out object o); }
  [Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
  interface IAudioSessionManager2 { int _a(); int _b(); [PreserveSig] int GetSessionEnumerator(out IAudioSessionEnumerator e); }
  [Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
  interface IAudioSessionEnumerator { [PreserveSig] int GetCount(out int c); [PreserveSig] int GetSession(int i, out IAudioSessionControl2 s); }
  [Guid("bfb7ff88-7239-4fc9-8fa2-07c950be9c6d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
  interface IAudioSessionControl2 { int _1(); int _2(); int _3(); int _4(); int _5(); int _6(); int _7(); int _8(); int _9(); int _10(); int _11();
    [PreserveSig] int GetProcessId(out uint pid); }
  [Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
  interface ISimpleAudioVolume { [PreserveSig] int SetMasterVolume(float v, ref Guid ctx); [PreserveSig] int GetMasterVolume(out float v); }

  public static class AppVolume {
    static System.Collections.Generic.List<ISimpleAudioVolume> Find(string proc) {
      var list = new System.Collections.Generic.List<ISimpleAudioVolume>();
      var en = (IMMDeviceEnumerator)(new MMDeviceEnumerator());
      IMMDevice dev; if (en.GetDefaultAudioEndpoint(0, 1, out dev) != 0) return list;
      Guid iid = typeof(IAudioSessionManager2).GUID; object o;
      if (dev.Activate(ref iid, 23, IntPtr.Zero, out o) != 0) return list;
      IAudioSessionEnumerator se; ((IAudioSessionManager2)o).GetSessionEnumerator(out se);
      int n; se.GetCount(out n);
      for (int i = 0; i < n; i++) {
        IAudioSessionControl2 s; se.GetSession(i, out s); uint pid; s.GetProcessId(out pid);
        try { if (pid != 0 && Process.GetProcessById((int)pid).ProcessName.Equals(proc, StringComparison.OrdinalIgnoreCase)) list.Add((ISimpleAudioVolume)s); } catch {}
      }
      return list;
    }
    // -1 = Spotify hat gerade keine Audio-Sitzung
    public static int Get(string proc) {
      foreach (var v in Find(proc)) { float f; if (v.GetMasterVolume(out f) == 0) return (int)Math.Round(f * 100); }
      return -1;
    }
    public static int Set(string proc, int pct) {
      pct = Math.Max(0, Math.Min(100, pct)); Guid g = Guid.Empty; int done = -1;
      foreach (var v in Find(proc)) { v.SetMasterVolume(pct / 100f, ref g); done = pct; }
      return done;
    }
  }
}
'@
$volume = -1

# GPU-Auslastung (Summe aller 3D-Engines), nur mit -Gpu
$gpuPct = ''
$gpuCounters = @()
$gpuRefresh = 0
function Update-Gpu {
    try {
        if ($script:gpuRefresh -le 0) {
            foreach ($c in $script:gpuCounters) { try { $c.Dispose() } catch { } }
            $cat = New-Object System.Diagnostics.PerformanceCounterCategory('GPU Engine')
            $script:gpuCounters = @($cat.GetInstanceNames() | Where-Object { $_ -like '*engtype_3D' } | ForEach-Object {
                $pc = New-Object System.Diagnostics.PerformanceCounter('GPU Engine', 'Utilization Percentage', $_, $true)
                try { $null = $pc.NextValue() } catch { }
                $pc
            })
            $script:gpuRefresh = 10
        }
        $script:gpuRefresh--
        $sum = 0.0
        foreach ($c in $script:gpuCounters) { try { $sum += $c.NextValue() } catch { } }
        $script:gpuPct = [string][Math]::Min(100, [Math]::Round($sum))
    } catch { $script:gpuPct = '0' }
}

Add-Type -AssemblyName System.Drawing
$defaultColor = '235,245,255'
$coverColor = $defaultColor

# Kraeftige Hauptfarbe vom Cover, hell genug fuer schwarzen Hintergrund
function Get-CoverColor($path) {
    if (-not $path -or -not (Test-Path $path)) { return $defaultColor }
    try {
        $img = [System.Drawing.Image]::FromFile($path)
        $bmp = New-Object System.Drawing.Bitmap($img, 24, 24)
        $img.Dispose()
        $sr = 0.0; $sg = 0.0; $sb = 0.0; $sw = 0.0
        for ($x = 0; $x -lt 24; $x++) { for ($y = 0; $y -lt 24; $y++) {
            $c = $bmp.GetPixel($x, $y)
            $s = $c.GetSaturation(); $l = $c.GetBrightness()
            $w = $s * $s * (1 - [Math]::Abs($l - 0.5) * 1.6) + 0.001
            if ($w -lt 0) { $w = 0.001 }
            $sr += $c.R * $w; $sg += $c.G * $w; $sb += $c.B * $w; $sw += $w
        } }
        $bmp.Dispose()
        $col = [System.Drawing.Color]::FromArgb([int]($sr / $sw), [int]($sg / $sw), [int]($sb / $sw))
        $h = $col.GetHue(); $s = $col.GetSaturation()
        if ($s -lt 0.15) { return $defaultColor }   # fast grau -> Eis-Weiss
        # HSL -> RGB mit fester Helligkeit 0.68 und mind. 0.55 Saettigung
        $s = [Math]::Max($s, 0.55); $l = 0.68
        $q = if ($l -lt 0.5) { $l * (1 + $s) } else { $l + $s - $l * $s }; $p = 2 * $l - $q
        $rgb = foreach ($t in ($h / 360 + 1/3), ($h / 360), ($h / 360 - 1/3)) {
            if ($t -lt 0) { $t += 1 }; if ($t -gt 1) { $t -= 1 }
            $v = if ($t -lt 1/6) { $p + ($q - $p) * 6 * $t } elseif ($t -lt 1/2) { $q } elseif ($t -lt 2/3) { $p + ($q - $p) * (2/3 - $t) * 6 } else { $p }
            [int][Math]::Round($v * 255)
        }
        return ($rgb -join ',')
    } catch { return $defaultColor }
}

while ($true) {
    # Klick-Befehle aus Rainmeter
    # Datei muss existieren, sonst kann Rainmeter nicht reinschreiben
    if (-not (Test-Path $cmdFile)) { [System.IO.File]::WriteAllText($cmdFile, "[Cmd]`r`nAction=`r`n") }
    $action = ''
    try {
        foreach ($line in [System.IO.File]::ReadAllLines($cmdFile)) {
            if ($line -match '^\s*Action\s*=\s*(\w+)') { $action = $Matches[1] }
        }
    } catch { }
    if ($action) {
        [System.IO.File]::WriteAllText($cmdFile, "[Cmd]`r`nAction=`r`n")
        # Lautstaerke: volup / voldown / vol<0-100>
        if ($action -eq 'volmute') {
            $cur = [PCW.AppVolume]::Get('Spotify')
            if ($cur -gt 0) { $script:volBeforeMute = $cur; $null = [PCW.AppVolume]::Set('Spotify', 0) }
            elseif ($cur -eq 0) { $r = if ($script:volBeforeMute) { $script:volBeforeMute } else { 50 }; $null = [PCW.AppVolume]::Set('Spotify', $r) }
            $action = ''; $tick = 0
        }
        if ($action -match '^vol(up|down|(\d+))$') {
            $cur = [PCW.AppVolume]::Get('Spotify')
            if ($cur -ge 0) {
                $target = if ($Matches[1] -eq 'up') { $cur + 5 } elseif ($Matches[1] -eq 'down') { $cur - 5 } else { [int]$Matches[2] }
                $null = [PCW.AppVolume]::Set('Spotify', $target)
            }
            $action = ''; $tick = 0
        }
        $s = Get-Session
        if ($s) {
            switch ($action) {
                'playpause' { $null = Await ($s.TryTogglePlayPauseAsync()) ([bool]) }
                'next'      { $null = Await ($s.TrySkipNextAsync()) ([bool]) }
                'prev'      { $null = Await ($s.TrySkipPreviousAsync()) ([bool]) }
            }
            $tick = 0
        }
    }

    if ($tick -le 0) {
        $tick = 4   # alle ~1 Sekunde
        $title = 'Nichts läuft'; $artist = 'Spotify'; $status = 'Stopped'; $pct = 0; $posTxt = ''; $durTxt = ''
        $s = Get-Session
        if ($s) {
            $props = Await ($s.TryGetMediaPropertiesAsync()) ([Windows.Media.Control.GlobalSystemMediaTransportControlsSessionMediaProperties])
            $info = $s.GetPlaybackInfo()
            $status = "$($info.PlaybackStatus)"
            if ($props -and $props.Title) {
                $title = $props.Title; $artist = $props.Artist
                $key = "$title|$artist"
                if ($key -ne $lastKey) { $lastKey = $key; $coverPath = Save-Cover $props; $coverColor = Get-CoverColor $coverPath }
                $tl = $s.GetTimelineProperties()
                $dur = $tl.EndTime.TotalSeconds
                $pos = $tl.Position.TotalSeconds
                if ($status -eq 'Playing') { $pos += ([DateTimeOffset]::Now - $tl.LastUpdatedTime).TotalSeconds }
                if ($dur -gt 0) {
                    $pos = [Math]::Max(0, [Math]::Min($dur, $pos))
                    $pct = [Math]::Round($pos / $dur * 100, 1)
                    $posTxt = '{0}:{1:00}' -f [int][Math]::Floor($pos / 60), [int][Math]::Floor($pos % 60)
                    $durTxt = '{0}:{1:00}' -f [int][Math]::Floor($dur / 60), [int][Math]::Floor($dur % 60)
                }
            }
        } else { $lastKey = ''; $coverPath = '' }
        if ($lastKey -eq '') { $coverPath = ''; $coverColor = $defaultColor }
        try { $volume = [PCW.AppVolume]::Get('Spotify') } catch { $volume = -1 }
        if ($Gpu) { Update-Gpu }
        $line = '{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}|{8}|{9}|' -f ($title -replace '\|', '/'), ($artist -replace '\|', '/'), $status, ($pct.ToString([Globalization.CultureInfo]::InvariantCulture)), $coverPath, $coverColor, $posTxt, $durTxt, $volume, $gpuPct
        [System.IO.File]::WriteAllText($outFile, $line, $utf8)
    }
    $tick--

    # Beenden, wenn Rainmeter zu ist
    if ((Get-Date).Second % 10 -eq 0 -and $tick -eq 3) {
        if (-not (Get-Process Rainmeter -ErrorAction SilentlyContinue)) { break }
    }
    Start-Sleep -Milliseconds 250
}
