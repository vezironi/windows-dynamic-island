# One-time: creates the self-signed dev certificate used to sign the sparse package
# and trusts it on this machine. Must run in an elevated (Administrator) PowerShell.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $root 'artifacts'
New-Item -ItemType Directory -Force $artifacts | Out-Null

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host 'Bu script yonetici olarak calistirilmali. PowerShell''i "Yonetici olarak calistir" ile acip tekrar deneyin.' -ForegroundColor Yellow
    exit 1
}

$subject = 'CN=DynamicIslandDev'
$cert = Get-ChildItem Cert:\CurrentUser\My | Where-Object Subject -eq $subject | Select-Object -First 1
if (-not $cert) {
    $cert = New-SelfSignedCertificate -Type Custom -Subject $subject -KeyUsage DigitalSignature `
        -FriendlyName 'DynamicIsland Dev' -CertStoreLocation Cert:\CurrentUser\My `
        -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}') `
        -NotAfter (Get-Date).AddYears(10)
}

$pwd = ConvertTo-SecureString -String 'dev' -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath (Join-Path $artifacts 'dev.pfx') -Password $pwd | Out-Null
$cer = Join-Path $artifacts 'dev.cer'
Export-Certificate -Cert $cert -FilePath $cer | Out-Null
Import-Certificate -FilePath $cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null

Write-Host "Sertifika hazir: $artifacts\dev.pfx (makineye guvenilir olarak eklendi)" -ForegroundColor Green
