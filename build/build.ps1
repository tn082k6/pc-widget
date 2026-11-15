# Baut die fertige PC-Widget.exe (eine Datei, richtet sich beim ersten Start selbst ein)
# Voraussetzung: Rainmeter 4.5 installiert (https://www.rainmeter.net), .NET Framework 4.x (bei Windows dabei)
param([string]$Rainmeter = "$env:ProgramFiles\Rainmeter", [string]$Out = "$PSScriptRoot\..\dist")
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path "$PSScriptRoot\..").Path
New-Item -ItemType Directory -Force $Out | Out-Null
$Out = (Resolve-Path $Out).Path
$stage = Join-Path $Out 'payload'
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force "$stage\Rainmeter\Skins" | Out-Null

# Nutzlast: portables Rainmeter + Widget
Copy-Item "$Rainmeter\*" "$stage\Rainmeter\" -Recurse -Exclude 'uninst.exe'
Copy-Item "$root\skin\Rainmeter.ini" "$stage\Rainmeter\Rainmeter.ini"
Copy-Item "$root\skin\PCWidget" "$stage\Rainmeter\Skins\PCWidget" -Recurse
Copy-Item "$root\skin\Lies mich.txt" "$stage\Lies mich.txt"

Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
function New-Zip($files, $base, $zip) {
    if (Test-Path $zip) { Remove-Item $zip -Force }
    $fs = [IO.File]::Create($zip); $za = New-Object IO.Compression.ZipArchive($fs, [IO.Compression.ZipArchiveMode]::Create)
    foreach ($f in $files) { $rel = $f.FullName.Substring($base.Length + 1).Replace('\', '/'); [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($za, $f.FullName, $rel, [IO.Compression.CompressionLevel]::Optimal) }
    $za.Dispose(); $fs.Dispose()
}
$payload = Join-Path $Out 'payload.zip'
New-Zip (Get-ChildItem $stage -Recurse -File) $stage $payload

# exe mit eingebauter Nutzlast
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /platform:x64 /optimize+ `
    /win32icon:"$root\src\PCWidget.ico" /resource:"$payload,payload.zip" /out:"$Out\PC-Widget.exe" `
    /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:System.IO.Compression.dll "$root\src\PCWidget.cs"
if ($LASTEXITCODE) { throw 'Kompilieren fehlgeschlagen' }

# ZIP zum Verschicken: nur exe + Anleitung
Copy-Item "$root\skin\Lies mich.txt" "$Out\Lies mich.txt" -Force
New-Zip @((Get-Item "$Out\PC-Widget.exe"), (Get-Item "$Out\Lies mich.txt")) $Out (Join-Path $Out 'PC-Widget.zip')
"Fertig: $Out\PC-Widget.exe und $Out\PC-Widget.zip"
