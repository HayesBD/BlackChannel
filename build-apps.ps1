<#
.SYNOPSIS
  Build the native app packages (Android APK + unpackaged Windows ZIP) into
  src/BlackChannel.Web/wwwroot/download/, where the /download page links to them and the
  normal web publish/deploy will pick them up.

.DESCRIPTION
  Decoupled from deploy.ps1 on purpose: building MAUI needs the maui/android workloads,
  which aren't needed for a routine web-only deploy. Run this when you want to refresh the
  downloadable apps, then deploy as usual.

  Requires: dotnet + the maui & android workloads ( dotnet workload install maui android ).

.PARAMETER ApiBaseUrl
  The deployed Function App API base the built apps should talk to (the BlazorWebView has
  no site origin to resolve "/api" against). Defaults to the value in MauiProgram.cs.

.EXAMPLE
  ./build-apps.ps1 -ApiBaseUrl https://func-blackchannel.azurewebsites.net/api/
#>
[CmdletBinding()]
param(
    [string]$ApiBaseUrl = ''
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition
$appProj  = Join-Path $repoRoot 'src/BlackChannel.App'
$download = Join-Path $repoRoot 'src/BlackChannel.Web/wwwroot/download'
New-Item -ItemType Directory -Force -Path $download | Out-Null

# Optionally point the native apps at a specific backend before building.
if ($ApiBaseUrl) {
    $mp = Join-Path $appProj 'MauiProgram.cs'
    $txt = Get-Content $mp -Raw
    $txt = [regex]::Replace($txt, 'private const string ApiBaseUrl = ".*?";', "private const string ApiBaseUrl = `"$ApiBaseUrl`";")
    Set-Content $mp $txt -Encoding utf8
    Write-Host "Set MAUI ApiBaseUrl = $ApiBaseUrl" -ForegroundColor Cyan
}

# ---- Android APK ----
Write-Host "Building Android APK (Release)..." -ForegroundColor Cyan
$androidOut = Join-Path $repoRoot 'publish/androidapp'
if (Test-Path $androidOut) { Remove-Item -Recurse -Force $androidOut }
dotnet publish $appProj -f net10.0-android -c Release -o $androidOut | Out-Null
$apk = Get-ChildItem $androidOut -Filter '*-Signed.apk' -Recurse |
       Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $apk) {
    # Some SDKs name it without the -Signed suffix; fall back to any .apk.
    $apk = Get-ChildItem $androidOut -Filter '*.apk' -Recurse |
           Sort-Object LastWriteTime -Descending | Select-Object -First 1
}
if (-not $apk) { throw "No APK produced under $androidOut." }
Copy-Item $apk.FullName (Join-Path $download 'blackchannel-android.apk') -Force
Write-Host "  -> wwwroot/download/blackchannel-android.apk" -ForegroundColor Green

# ---- Windows (unpackaged) ZIP ----
Write-Host "Building Windows (Release)..." -ForegroundColor Cyan
$winOut = Join-Path $repoRoot 'publish/winapp'
if (Test-Path $winOut) { Remove-Item -Recurse -Force $winOut }
dotnet publish $appProj -f net10.0-windows10.0.19041.0 -c Release -o $winOut | Out-Null
if (-not (Test-Path (Join-Path $winOut 'BlackChannel.App.exe'))) { throw "No Windows publish output (BlackChannel.App.exe) in $winOut." }
$zip = Join-Path $download 'blackchannel-windows.zip'
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $winOut '*') -DestinationPath $zip -Force
Write-Host "  -> wwwroot/download/blackchannel-windows.zip" -ForegroundColor Green

Write-Host "Done. Run ./deploy.ps1 (or push) to publish the site with these packages." -ForegroundColor Green
