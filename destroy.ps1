<#
.SYNOPSIS
  Tear down everything by deleting the resource group.

  WARNING: this rotates the Static Web App subdomain on the next deploy, breaking any
  invite links already shared. Don't run it unless you mean to reset the environment.

.EXAMPLE
  ./destroy.ps1
  ./destroy.ps1 -Force
#>
[CmdletBinding()]
param(
    # Leave blank to use your current `az` subscription, or pass your own.
    [string]$SubscriptionId = '',
    [string]$ResourceGroup  = 'blackchannel',
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
if ($SubscriptionId) { az account set --subscription $SubscriptionId | Out-Null }

if (-not $Force) {
    $confirm = Read-Host "Delete resource group '$ResourceGroup' and EVERYTHING in it? Type the name to confirm"
    if ($confirm -ne $ResourceGroup) { Write-Host "Aborted."; exit 1 }
}

Write-Host "Deleting $ResourceGroup..." -ForegroundColor Yellow
az group delete -n $ResourceGroup --yes --no-wait
Write-Host "Delete started. Re-run ./deploy.ps1 once it completes." -ForegroundColor Green
