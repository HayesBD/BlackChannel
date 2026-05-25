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
dotnet publish $appProj -f net10.0-android -c Release | Out-Null
$apk = Get-ChildItem (Join-Path $appProj 'bin/Release/net10.0-android') -Filter '*-Signed.apk' -Recurse |
       Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $apk) { throw "No signed APK produced." }
Copy-Item $apk.FullName (Join-Path $download 'blackchannel-android.apk') -Force
Write-Host "  -> wwwroot/download/blackchannel-android.apk" -ForegroundColor Green

# ---- Windows (unpackaged) ZIP ----
Write-Host "Building Windows (Release)..." -ForegroundColor Cyan
dotnet publish $appProj -f net10.0-windows10.0.19041.0 -c Release | Out-Null
$winDir = Get-ChildItem (Join-Path $appProj 'bin/Release/net10.0-windows10.0.19041.0') -Directory |
          Where-Object { Test-Path (Join-Path $_.FullName 'BlackChannel.App.exe') } |
          Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $winDir) { throw "No Windows publish output found." }
$zip = Join-Path $download 'blackchannel-windows.zip'
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $winDir.FullName '*') -DestinationPath $zip -Force
Write-Host "  -> wwwroot/download/blackchannel-windows.zip" -ForegroundColor Green

Write-Host "Done. Run ./deploy.ps1 (or push) to publish the site with these packages." -ForegroundColor Green
