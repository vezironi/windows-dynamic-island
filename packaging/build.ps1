# Publishes the app to %LOCALAPPDATA%\DynamicIsland\app.
# -WithIdentity additionally builds + signs a sparse identity package and registers it
# (only needed if a future Windows build stops allowing UserNotificationListener for
# unpackaged apps; on Windows 11 26200 it works without identity — see spec §10).
param([string]$Configuration = 'Release', [switch]$WithIdentity)
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $root 'artifacts'
$appDir = Join-Path $env:LOCALAPPDATA 'DynamicIsland\app'
$sdkBin = 'C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64'
$makeappx = Join-Path $sdkBin 'makeappx.exe'
$signtool = Join-Path $sdkBin 'signtool.exe'
$pfx = Join-Path $artifacts 'dev.pfx'

if ($WithIdentity -and -not (Test-Path $pfx)) { throw "Sertifika yok. Once packaging\create-cert.ps1'i yonetici olarak calistirin." }

Get-Process DynamicIsland -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 300

dotnet publish (Join-Path $root 'src\DynamicIsland\DynamicIsland.csproj') -c $Configuration -r win-x64 --self-contained false -o $appDir
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish basarisiz' }

if (-not $WithIdentity) {
    Write-Host "Yayinlandi. Calistir: $appDir\DynamicIsland.exe" -ForegroundColor Green
    return
}

# Stage only the manifest + assets (packaging/ also holds these scripts).
$stage = Join-Path $artifacts 'pkg'
if (Test-Path $stage) { Remove-Item -Recurse -Force $stage }
New-Item -ItemType Directory -Force $stage | Out-Null
Copy-Item (Join-Path $PSScriptRoot 'AppxManifest.xml') $stage
Copy-Item (Join-Path $PSScriptRoot 'Assets') $stage -Recurse

$msix = Join-Path $artifacts 'DynamicIsland.msix'
& $makeappx pack /d $stage /p $msix /nv /o
if ($LASTEXITCODE -ne 0) { throw 'makeappx basarisiz' }
& $signtool sign /fd SHA256 /f $pfx /p dev $msix
if ($LASTEXITCODE -ne 0) { throw 'signtool basarisiz' }

Get-AppxPackage -Name 'DynamicIsland' | Remove-AppxPackage
Add-AppxPackage -Path $msix -ExternalLocation $appDir

Write-Host "Kuruldu. Calistir: $appDir\DynamicIsland.exe" -ForegroundColor Green
