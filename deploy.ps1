<#
.SYNOPSIS
  One-shot, idempotent deploy of BlackChannel to Azure.

.DESCRIPTION
  - Logs in / sets subscription
  - Creates the resource group if missing
  - Deploys infra (Bicep) — storage, SignalR, Flex Consumption Function App, Static Web App
  - Builds + publishes the Function App (zip deploy — Flex-compatible path)
  - Builds the Blazor WASM site with the correct API + SignalR hosts baked in
  - Deploys the Blazor site to the Static Web App (SWA CLI; auto-installs)
  - Updates Function CORS to allow the SWA hostname

  PREFER `git push origin main` (CI). This is a local fallback only.

.EXAMPLE
  ./deploy.ps1
#>
[CmdletBinding()]
param(
    # Leave blank to use your current `az` subscription, or pass your own.
    [string]$SubscriptionId = '',
    [string]$ResourceGroup  = 'blackchannel',
    [string]$Location       = 'australiaeast',
    [string]$AppName        = 'blackchannel',
    [string]$SwaLocation    = 'eastasia',
    [switch]$SkipInfra,
    [switch]$SkipFunctions,
    [switch]$SkipWeb
)

$ErrorActionPreference = 'Stop'
$repoRoot   = Split-Path -Parent $MyInvocation.MyCommand.Definition
$publishDir = Join-Path $repoRoot 'publish'

function Require-Command($cmd, $hint) {
    if (-not (Get-Command $cmd -ErrorAction SilentlyContinue)) { throw "Required command '$cmd' not found. $hint" }
}
Require-Command az     "Install: https://learn.microsoft.com/cli/azure/install-azure-cli"
Require-Command dotnet "Install the .NET 8 SDK: https://dotnet.microsoft.com/download"
Require-Command npm    "Install Node.js: https://nodejs.org/"

# -------- Azure login + subscription --------
$account = az account show -o json 2>$null | ConvertFrom-Json
if (-not $account) {
    az login --only-show-errors | Out-Null
    $account = az account show -o json | ConvertFrom-Json
}
if ($SubscriptionId) {
    az account set --subscription $SubscriptionId
} else {
    $SubscriptionId = (az account show --query id -o tsv)
}
Write-Host "Subscription: $SubscriptionId" -ForegroundColor Green

# -------- Resource group --------
if (-not (az group exists -n $ResourceGroup | ConvertFrom-Json)) {
    az group create -n $ResourceGroup -l $Location | Out-Null
}

# -------- Infra (Bicep) --------
if (-not $SkipInfra) {
    Write-Host "Deploying Bicep..." -ForegroundColor Cyan
    $deployName = "blackchannel-$(Get-Date -Format yyyyMMddHHmmss)"
    $deployJson = az deployment group create -g $ResourceGroup -n $deployName `
        --template-file (Join-Path $repoRoot 'infra/main.bicep') `
        --parameters appName=$AppName swaLocation=$SwaLocation -o json
    if ($LASTEXITCODE -ne 0) { throw "Bicep deployment failed." }
    $outputs = ($deployJson | ConvertFrom-Json).properties.outputs
} else {
    $last = az deployment group list -g $ResourceGroup --query "[?properties.provisioningState=='Succeeded'] | [0]" -o json | ConvertFrom-Json
    if (-not $last) { throw "No prior successful deployment. Run without -SkipInfra first." }
    $outputs = $last.properties.outputs
}

$functionName   = $outputs.functionName.value
$functionHost   = $outputs.functionHost.value
$staticSiteName = $outputs.staticSiteName.value
$staticSiteHost = $outputs.staticSiteHost.value
$signalRName    = $outputs.signalRName.value
$apiBaseUrl     = "https://$functionHost/api"
$swaOrigin      = "https://$staticSiteHost"

Write-Host "Function: $functionName ($functionHost)" -ForegroundColor Green
Write-Host "SWA:      $staticSiteName ($staticSiteHost)" -ForegroundColor Green

# Make the public site URL available to the Function (used to build invite links).
az functionapp config appsettings set -g $ResourceGroup -n $functionName `
    --settings "PUBLIC_SITE_URL=$swaOrigin" | Out-Null

# -------- Functions: build, zip, deploy (Flex-compatible) --------
if (-not $SkipFunctions) {
    Write-Host "Publishing Functions..." -ForegroundColor Cyan
    $funcPublish = Join-Path $publishDir 'functions'
    if (Test-Path $funcPublish) { Remove-Item -Recurse -Force $funcPublish }
    dotnet publish (Join-Path $repoRoot 'src/BlackChannel.Functions') -c Release -o $funcPublish | Out-Null

    $funcZip = Join-Path $publishDir 'functions.zip'
    if (Test-Path $funcZip) { Remove-Item -Force $funcZip }
    Compress-Archive -Path (Join-Path $funcPublish '*') -DestinationPath $funcZip -Force

    az functionapp deployment source config-zip -g $ResourceGroup -n $functionName --src $funcZip | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Function zip deploy failed." }
}

# -------- CORS --------
$cors = az functionapp cors show -g $ResourceGroup -n $functionName -o json | ConvertFrom-Json
if (-not ($cors.allowedOrigins -contains $swaOrigin)) {
    az functionapp cors add -g $ResourceGroup -n $functionName --allowed-origins $swaOrigin | Out-Null
}

# -------- Blazor WASM: bake config, patch CSP, publish, deploy --------
if (-not $SkipWeb) {
    Write-Host "Publishing Blazor WASM (apiBaseUrl=$apiBaseUrl)..." -ForegroundColor Cyan
    $webProj = Join-Path $repoRoot 'src/BlackChannel.Web'

    $appSettingsPath = Join-Path $webProj 'wwwroot/appsettings.json'
    $appSettings = Get-Content $appSettingsPath -Raw | ConvertFrom-Json
    $appSettings.apiBaseUrl = "$apiBaseUrl/"
    ($appSettings | ConvertTo-Json -Depth 10) | Set-Content $appSettingsPath -Encoding utf8

    # Pin CSP connect-src to the real function + SignalR hosts.
    $signalRHost = "$signalRName.service.signalr.net"
    $swaConfigPath = Join-Path $webProj 'wwwroot/staticwebapp.config.json'
    $swaConfig = Get-Content $swaConfigPath -Raw | ConvertFrom-Json
    $csp = "default-src 'self'; script-src 'self' 'wasm-unsafe-eval'; worker-src 'self' blob:; style-src 'self' 'unsafe-inline'; img-src 'self' data:; connect-src 'self' https://$functionHost wss://$signalRHost https://$signalRHost; frame-ancestors 'none'; base-uri 'self'; form-action 'self'"
    $swaConfig.globalHeaders.'Content-Security-Policy' = $csp
    ($swaConfig | ConvertTo-Json -Depth 10) | Set-Content $swaConfigPath -Encoding utf8

    $webPublish = Join-Path $publishDir 'web'
    if (Test-Path $webPublish) { Remove-Item -Recurse -Force $webPublish }
    dotnet publish $webProj -c Release -o $webPublish | Out-Null

    if (-not (Get-Command swa -ErrorAction SilentlyContinue)) {
        npm install -g @azure/static-web-apps-cli | Out-Null
    }
    $swaToken = az staticwebapp secrets list -n $staticSiteName -g $ResourceGroup --query "properties.apiKey" -o tsv
    if (-not $swaToken) { throw "Could not read SWA deployment token." }

    swa deploy (Join-Path $webPublish 'wwwroot') --deployment-token $swaToken --env production
    if ($LASTEXITCODE -ne 0) { throw "SWA deploy failed." }
}

Write-Host ""
Write-Host "Done." -ForegroundColor Green
Write-Host "Site: $swaOrigin"
Write-Host "API:  $apiBaseUrl"
