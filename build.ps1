# Baut Skybridge64.exe und schnuert das Release-Paket.
#
# Voraussetzung: .NET Framework 4 (auf jedem Windows 10/11 vorhanden). Es wird nichts
# installiert und nichts heruntergeladen - alles Noetige liegt im Repository.
#
# Ergebnis:
#   Skybridge64.exe          eine einzige Datei, alles einkompiliert
#   dist/Skybridge64-1.0.zip das Paket zum Weitergeben

$ErrorActionPreference = 'Stop'
$d   = $PSScriptRoot
$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$exe = Join-Path $d 'Skybridge64.exe'
$out = Join-Path $d 'dist'
$zip = Join-Path $out 'Skybridge64-1.0.zip'

if (-not (Test-Path $csc)) { Write-Output "csc.exe nicht gefunden: $csc"; return }

Get-Process -Name 'Skybridge64' -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

# Foto, Symbol, ViGEm-Bibliothek und das Treiber-Setup werden als Ressourcen
# eingebettet - deshalb braucht die fertige EXE keine Begleitdateien.
& $csc /nologo /target:winexe /platform:x64 /win32icon:"$d\assets\skybridge.ico" `
    /resource:"$d\assets\n64-pad.png,n64-pad.png" `
    /resource:"$d\lib\Nefarius.ViGEm.Client.dll,Nefarius.ViGEm.Client.dll" `
    /resource:"$d\vendor\ViGEmBus_1.22.0_x64_x86_arm64.exe,ViGEmBus-Setup.exe" `
    /out:"$exe" `
    /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Core.dll `
    /r:"$d\lib\Nefarius.ViGEm.Client.dll" `
    "$d\src\Skybridge64.cs" "$d\src\Skybridge64UI.cs" "$d\src\Skybridge64Main.cs"

if ($LASTEXITCODE -ne 0) { Write-Output 'Build fehlgeschlagen'; return }

if (-not (Test-Path $out)) { New-Item -ItemType Directory $out | Out-Null }
$stage = Join-Path $out 'package'
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory $stage | Out-Null

Copy-Item $exe $stage
Copy-Item (Join-Path $d 'dist-readme\README.txt') $stage
Copy-Item (Join-Path $d 'third-party\ViGEmBus-LICENSE.txt') $stage
Copy-Item (Join-Path $d 'third-party\ViGEm.NET-LICENSE.txt') $stage

if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -CompressionLevel Optimal
Remove-Item $stage -Recurse -Force

Write-Output ("OK  ->  " + $zip + "  (" + [math]::Round((Get-Item $zip).Length/1MB, 2) + " MB)")
