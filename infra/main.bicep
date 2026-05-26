// =============================================================================
// BlackChannel — Azure infrastructure
// Storage (+ tables) · Flex Consumption Functions · Azure SignalR (Serverless)
// · Static Web App · monthly budget alert.
// Idempotent — safe to re-run after deleting and recreating the resource group.
// =============================================================================
targetScope = 'resourceGroup'

@minLength(3)
@maxLength(16)
param appName string = 'blackchannel'

@description('Region for storage + function app + SignalR. Defaults to the RG location.')
param location string = resourceGroup().location

@description('Region for the Static Web App. SWA is not in Australia regions; East Asia is closest.')
param swaLocation string = 'eastasia'

@description('Origins allowed to call the Function API (the SWA hostname is added automatically).')
param allowedOrigins array = []

@description('Azure SignalR SKU. Free_F1 caps at 20 concurrent connections / 20k msgs per day.')
@allowed([ 'Free_F1', 'Standard_S1' ])
param signalRSku string = 'Free_F1'

@description('Monthly cost cap in USD. Email alerts at 50/80/100%.')
param budgetAmountUsd int = 25

@description('Days an UNDELIVERED ciphertext envelope is kept before auto-deletion. Lower = less data held. Delivered envelopes are deleted immediately on sync.')
@minValue(1)
@maxValue(90)
param undeliveredRetentionDays int = 14

@description('Email for budget alerts. Leave blank to skip creating the budget.')
param budgetEmail string = ''

// 6-char suffix from the RG id keeps globally-unique names (func/SWA/SignalR hostnames)
// distinct per deployment, so anyone can deploy their own without name collisions.
var suffix          = substring(uniqueString(resourceGroup().id, appName), 0, 6)
var storageName     = toLower('st${appName}${suffix}')
var funcName        = 'func-${appName}-${suffix}'
var swaName         = 'swa-${appName}-${suffix}'
var planName        = 'plan-${appName}-${suffix}'
var signalRName     = 'signalr-${appName}-${suffix}'
var deployContainer = 'deploymentpackage'
var envelopeContainer = 'envelopes'

// Table Storage tables. Public keys and invites only.
// NB: NO message plaintext, NO private keys — ever.
var tableNames = [
  'Users'      // public key bundle per user/device
  'Invites'    // one-time shareable join links
]

// -----------------------------------------------------------------------------
// Storage account + blob containers + tables
// -----------------------------------------------------------------------------
resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageName
  location: location
  sku: { name: 'Standard_LRS' }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    // Flex Consumption host needs shared-key access for its own deployment bookkeeping.
    // Application code uses the managed identity (MSI), not shared keys.
    allowSharedKeyAccess: true
    supportsHttpsTrafficOnly: true
    publicNetworkAccess: 'Enabled'
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storage
  name: 'default'
  properties: {
    deleteRetentionPolicy: { enabled: false }
    isVersioningEnabled: false
  }
}

resource envelopeContainerRes 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: envelopeContainer
  properties: { publicAccess: 'None' }
}

resource deployContainerRes 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: deployContainer
  properties: { publicAccess: 'None' }
}

resource tableService 'Microsoft.Storage/storageAccounts/tableServices@2023-05-01' = {
  parent: storage
  name: 'default'
}

resource tables 'Microsoft.Storage/storageAccounts/tableServices/tables@2023-05-01' = [for t in tableNames: {
  parent: tableService
  name: t
}]

// Lifecycle: undelivered ciphertext envelopes are a transient mailbox — expire
// them so the blind mailbox never accumulates blobs forever.
resource lifecycle 'Microsoft.Storage/storageAccounts/managementPolicies@2023-05-01' = {
  parent: storage
  name: 'default'
  properties: {
    policy: {
      rules: [
        {
          enabled: true
          name: 'expire-undelivered-envelopes'
          type: 'Lifecycle'
          definition: {
            filters: {
              blobTypes: [ 'blockBlob' ]
              prefixMatch: [ '${envelopeContainer}/' ]
            }
            actions: {
              baseBlob: {
                // Hard ceiling. The app deletes on delivery; this catches the rest.
                delete: { daysAfterModificationGreaterThan: undeliveredRetentionDays }
              }
            }
          }
        }
      ]
    }
  }
}

// -----------------------------------------------------------------------------
// Azure SignalR Service (Serverless mode — required for Functions bindings)
// -----------------------------------------------------------------------------
resource signalR 'Microsoft.SignalRService/signalR@2024-03-01' = {
  name: signalRName
  location: location
  sku: {
    name: signalRSku
    tier: split(signalRSku, '_')[0]
    capacity: 1
  }
  kind: 'SignalR'
  properties: {
    // Data minimisation: connectivity + messaging logs OFF. We don't want a record of
    // who connected, when, or that messages flowed. No diagnostic settings are attached
    // to this resource, so nothing is exported to Log Analytics either.
    features: [
      { flag: 'ServiceMode', value: 'Serverless' }
      { flag: 'EnableConnectivityLogs', value: 'false' }
      { flag: 'EnableMessagingLogs', value: 'false' }
    ]
    publicNetworkAccess: 'Enabled'
    // Connection string auth keeps the Functions binding simple. Tighten to AAD later.
    disableLocalAuth: false
    cors: {
      allowedOrigins: union(allowedOrigins, [ 'https://${swa.properties.defaultHostname}' ])
    }
  }
}

// -----------------------------------------------------------------------------
// Flex Consumption plan + Function App
// -----------------------------------------------------------------------------
resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: planName
  location: location
  sku: { name: 'FC1', tier: 'FlexConsumption' }
  properties: { reserved: true }
}

resource func 'Microsoft.Web/sites@2023-12-01' = {
  name: funcName
  location: location
  kind: 'functionapp,linux'
  identity: { type: 'SystemAssigned' }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    functionAppConfig: {
      deployment: {
        storage: {
          type: 'blobContainer'
          value: '${storage.properties.primaryEndpoints.blob}${deployContainer}'
          authentication: { type: 'SystemAssignedIdentity' }
        }
      }
      runtime: { name: 'dotnet-isolated', version: '8.0' }
      scaleAndConcurrency: {
        maximumInstanceCount: 5
        instanceMemoryMB: 2048
      }
    }
    siteConfig: {
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      cors: {
        // SWA hostname pulled straight from the swa resource so CORS is correct
        // the moment the Function App goes live.
        allowedOrigins: union(allowedOrigins, [ 'https://${swa.properties.defaultHostname}' ])
        supportCredentials: false
      }
      appSettings: [
        { name: 'AzureWebJobsStorage__accountName', value: storage.name }
        { name: 'STORAGE_ACCOUNT_NAME', value: storage.name }
        { name: 'ENVELOPE_CONTAINER', value: envelopeContainer }
        // SignalR connection string for the Functions output binding.
        { name: 'AzureSignalRConnectionString', value: signalR.listKeys().primaryConnectionString }
        // Entra External ID — left blank so the app runs in dev-user mode until set.
        // Fill these (ideally as Key Vault refs) to require real Entra External ID sign-in.
        { name: 'ENTRA_AUTHORITY', value: '' }
        { name: 'ENTRA_AUDIENCE', value: '' }
      ]
    }
  }
  dependsOn: [
    envelopeContainerRes
    deployContainerRes
  ]
}

// RBAC — Function MSI: read/write blobs + table data on the storage account.
var blobDataContributorRoleId  = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
var tableDataContributorRoleId = '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3'

resource roleBlob 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storage.id, func.id, blobDataContributorRoleId)
  scope: storage
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', blobDataContributorRoleId)
    principalId: func.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource roleTable 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storage.id, func.id, tableDataContributorRoleId)
  scope: storage
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', tableDataContributorRoleId)
    principalId: func.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// -----------------------------------------------------------------------------
// Static Web App (Free) — hosts the Blazor WASM client
// -----------------------------------------------------------------------------
resource swa 'Microsoft.Web/staticSites@2023-12-01' = {
  name: swaName
  location: swaLocation
  sku: { name: 'Free', tier: 'Free' }
  properties: {
    allowConfigFileUpdates: true
  }
}

// -----------------------------------------------------------------------------
// Monthly budget with email alerts at 50/80/100%. Created only if an email is supplied.
// -----------------------------------------------------------------------------
resource budget 'Microsoft.Consumption/budgets@2023-05-01' = if (!empty(budgetEmail)) {
  name: 'monthly-cap'
  properties: {
    category: 'Cost'
    amount: budgetAmountUsd
    timeGrain: 'Monthly'
    timePeriod: {
      startDate: '2026-05-01T00:00:00Z'
      endDate:   '2030-12-01T00:00:00Z'
    }
    notifications: {
      actualOver50:  { enabled: true, operator: 'GreaterThan', threshold: 50,  contactEmails: [ budgetEmail ], thresholdType: 'Actual' }
      actualOver80:  { enabled: true, operator: 'GreaterThan', threshold: 80,  contactEmails: [ budgetEmail ], thresholdType: 'Actual' }
      actualOver100: { enabled: true, operator: 'GreaterThan', threshold: 100, contactEmails: [ budgetEmail ], thresholdType: 'Actual' }
    }
  }
}

output functionName   string = func.name
output functionHost   string = func.properties.defaultHostName
output staticSiteName  string = swa.name
output staticSiteHost  string = swa.properties.defaultHostname
output storageAccount  string = storage.name
output signalRName     string = signalR.name
