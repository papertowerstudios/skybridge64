# Baut Skybridge64.exe und schnuert das Release-Paket.
#
# Voraussetzung: .NET Framework 4 (auf jedem Windows 10/11 vorhanden). Es wird nichts
# installiert und nichts heruntergeladen - alles Noetige liegt im Repository.
#
# WICHTIG, und der Grund fuer den Zuschnitt: die ViGEm-Bibliothek und das Treiber-Setup
# werden NICHT in die EXE eingebettet. Eine EXE, die eine zweite EXE in sich traegt, sie
# zur Laufzeit auf die Platte schreibt und mit Adminrechten startet - und die zusaetzlich
# eine DLL per Assembly.Load aus dem Speicher laedt - ist genau das Profil eines Droppers.
# Windows Defender hat die frueher so gebaute Fassung als Trojan:Win32/Sabsik.EN.B!ml
# eingestuft und beim Entpacken geloescht. Beide Dateien liegen deshalb sichtbar neben
# der EXE. Das ist unauffaelliger und ehrlicher: das ViGEmBus-Setup ist von Nefarius
# signiert und kann von jedem selbst geprueft werden.
#
# Ordner:
#   src/       Quelltext
#   assets/    Symbol und Foto - Bauzutaten
#   vendor/    fremde Binaerdateien (beide von Nefarius), wandern mit ins Paket
#   package/   alles, was unveraendert in die ZIP kommt
#   tools/     Hilfsprogramme, die Symbol und Foto erzeugt haben - nicht Teil des Builds
#
# Ergebnis:
#   Skybridge64.exe          rund 1 MB, nur das Foto ist einkompiliert
#   dist/Skybridge64-1.0.zip das Paket zum Weitergeben

$ErrorActionPreference = 'Stop'
$d   = $PSScriptRoot
$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$exe = Join-Path $d 'Skybridge64.exe'
$out = Join-Path $d 'dist'
$zip = Join-Path $out 'Skybridge64-1.0.zip'
$dll = Join-Path $d 'vendor\Nefarius.ViGEm.Client.dll'
$setup = Get-ChildItem (Join-Path $d 'vendor') -Filter 'ViGEmBus*.exe' | Select-Object -First 1

if (-not (Test-Path $csc)) { Write-Output "csc.exe nicht gefunden: $csc"; return }
if (-not (Test-Path $dll)) { Write-Output 'Nefarius.ViGEm.Client.dll fehlt in vendor/'; return }
if (-not $setup)           { Write-Output 'ViGEmBus-Setup fehlt in vendor/'; return }

Get-Process -Name 'Skybridge64' -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

& $csc /nologo /target:winexe /platform:x64 /win32icon:"$d\assets\skybridge.ico" `
    /resource:"$d\assets\n64-pad.png,n64-pad.png" `
    /out:"$exe" `
    /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Core.dll `
    /r:"$dll" `
    "$d\src\Skybridge64.cs" "$d\src\Skybridge64UI.cs" "$d\src\Skybridge64Main.cs"

if ($LASTEXITCODE -ne 0) { Write-Output 'Build fehlgeschlagen'; return }

if (-not (Test-Path $out)) { New-Item -ItemType Directory $out | Out-Null }
$stage = Join-Path $out 'package'
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory $stage | Out-Null

# Die DLL MUSS genau so heissen - der .NET-Loader sucht nach dem Assembly-Namen.
Copy-Item $exe $stage
Copy-Item $dll $stage
Copy-Item $setup.FullName $stage
Copy-Item (Join-Path $d 'package\*') $stage

if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -CompressionLevel Optimal
Remove-Item $stage -Recurse -Force

Write-Output ("EXE  " + [math]::Round((Get-Item $exe).Length/1KB) + " KB")
Write-Output ("OK  ->  " + $zip + "  (" + [math]::Round((Get-Item $zip).Length/1MB, 2) + " MB)")
