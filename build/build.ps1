# Baut PC-Widget.zip: App kompilieren + portables Rainmeter + Widget
# Voraussetzung: Rainmeter 4.5 installiert (https://www.rainmeter.net), .NET Framework 4.x (bei Windows dabei)
param([string]$Rainmeter = "$env:ProgramFiles\Rainmeter", [string]$Out = "$PSScriptRoot\..\dist")
$ErrorActionPreference = 'Stop'
$root = Resolve-Path "$PSScriptRoot\.."
$stage = Join-Path $Out 'PC-Widget'
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force "$stage\Rainmeter" | Out-Null

& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /platform:x64 /optimize+ `
    /win32icon:"$root\src\PCWidget.ico" /out:"$stage\PC-Widget.exe" `
    /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll "$root\src\PCWidget.cs"
if ($LASTEXITCODE) { throw 'Kompilieren fehlgeschlagen' }

Copy-Item "$Rainmeter\*" "$stage\Rainmeter\" -Recurse -Exclude 'uninst.exe'
Copy-Item "$root\skin\Rainmeter.ini" "$stage\Rainmeter\Rainmeter.ini"
New-Item -ItemType Directory -Force "$stage\Rainmeter\Skins" | Out-Null
Copy-Item "$root\skin\PCWidget" "$stage\Rainmeter\Skins\PCWidget" -Recurse
Copy-Item "$root\skin\Lies mich.txt" "$stage\Lies mich.txt"

$zip = Join-Path $Out 'PC-Widget.zip'
if (Test-Path $zip) { Remove-Item $zip -Force }
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$fs = [IO.File]::Create($zip); $za = New-Object IO.Compression.ZipArchive($fs, [IO.Compression.ZipArchiveMode]::Create)
Get-ChildItem $Out -Recurse -File | Where-Object { $_.FullName -ne $zip } | ForEach-Object {
    $rel = $_.FullName.Substring((Resolve-Path $Out).Path.Length + 1).Replace('\', '/')
    [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($za, $_.FullName, $rel, [IO.Compression.CompressionLevel]::Optimal)
}
$za.Dispose(); $fs.Dispose()
"Fertig: $zip"