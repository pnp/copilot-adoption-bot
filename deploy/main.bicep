// =============================================================================
// Copilot Adoption Bot - infrastructure
// =============================================================================
// Single source of truth for the Azure resources. Invoked either by
// deploy/Deploy-AdoptionBot.ps1 (operator) or by the azure-provision workflow (CI).
//
// Deliberately does NOT create:
//   - the Entra app registrations (bot / Graph / web auth) - those need admin consent
//     and are documented in docs/SETUP.md
//   - tables, blob containers or queues - the application creates those on first run
//
// Secrets are passed as secure parameters and written to app settings. They are never
// defaulted in this file.
// =============================================================================

targetScope = 'resourceGroup'

// ---------------------------------------------------------------------------
// Parameters
// ---------------------------------------------------------------------------

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('Name of the App Service (also the default hostname).')
param appServiceName string

@description('Storage account name. 3-24 lowercase alphanumeric characters.')
@minLength(3)
@maxLength(24)
param storageAccountName string

@description('App Service Plan name.')
param appServicePlanName string = '${appServiceName}-plan'

@description('App Service Plan SKU.')
@allowed(['B1', 'B2', 'B3', 'S1', 'S2', 'S3', 'P0v3', 'P1v3', 'P2v3', 'P3v3'])
param appServicePlanSku string = 'B1'

@description('Application Insights resource name.')
param appInsightsName string = '${appServiceName}-insights'

@description('Deploy Application Insights.')
param deployAppInsights bool = true

// --- Bot / Graph identity (created out-of-band; see docs/SETUP.md) ------------

@description('Bot application (client) ID from the Teams Developer Portal.')
param botAppId string

@secure()
@description('Bot client secret.')
param botAppPassword string

@description('Bot tenant ID.')
param botTenantId string

@allowed(['SingleTenant', 'MultiTenant'])
@description('Bot application type.')
param botAppType string = 'SingleTenant'

@description('Teams app catalog ID, used to install the bot for a user.')
param appCatalogTeamAppId string = ''

@description('Graph application (client) ID.')
param graphClientId string

@secure()
@description('Graph client secret.')
param graphClientSecret string

@description('Graph tenant ID.')
param graphTenantId string

// --- Optional features -------------------------------------------------------

@description('Azure AI Foundry endpoint. Leave empty to disable Copilot Connected features.')
param aiFoundryEndpoint string = ''

@description('AI Foundry model deployment name.')
param aiFoundryDeploymentName string = 'gpt-4o'

@description('Enable the admin web UI sign-in (separate app registration to the bot).')
param webAuthEnabled bool = false

@description('Web auth application (client) ID.')
param webAuthClientId string = ''

@secure()
@description('Web auth client secret.')
param webAuthClientSecret string = ''

@description('Web auth tenant ID.')
param webAuthTenantId string = ''

@description('Web auth API audience (e.g. api://<clientId>).')
param webAuthApiAudience string = ''

// ---------------------------------------------------------------------------
// Storage
// ---------------------------------------------------------------------------

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    // RBAC only. Shared keys are a long-lived credential with full data-plane access;
    // the app authenticates with its managed identity instead.
    allowSharedKeyAccess: false
    allowBlobPublicAccess: false
  }
}

// ---------------------------------------------------------------------------
// Application Insights
// ---------------------------------------------------------------------------

resource appInsights 'Microsoft.Insights/components@2020-02-02' = if (deployAppInsights) {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    IngestionMode: 'ApplicationInsights'
  }
}

// ---------------------------------------------------------------------------
// App Service
// ---------------------------------------------------------------------------

resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: appServicePlanName
  location: location
  sku: {
    name: appServicePlanSku
  }
  properties: {
    reserved: false // Windows
  }
}

resource appService 'Microsoft.Web/sites@2023-12-01' = {
  name: appServiceName
  location: location
  identity: {
    // The app authenticates to Storage with this identity; role assignments below.
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      netFrameworkVersion: 'v10.0'

      // 64-bit: .NET 10 has no reason to run 32-bit, and a 32-bit worker caps the
      // process at ~2GB of address space, which this workload can reach.
      use32BitWorkerProcess: false

      // The app exposes /health; without this App Service never checks it, so an
      // unhealthy instance is never detected or recycled.
      healthCheckPath: '/health'

      http20Enabled: true
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'

      // NOTE: alwaysOn is deliberately NOT set here. It is governed by policy in the
      // target environment. See docs/SCALING.md section 7 for how background work is
      // built to tolerate the worker being unloaded.

      appSettings: concat([
        // --- Bot identity ---
        { name: 'MicrosoftAppId', value: botAppId }
        { name: 'MicrosoftAppPassword', value: botAppPassword }
        { name: 'MicrosoftAppTenantId', value: botTenantId }
        { name: 'MicrosoftAppType', value: botAppType }
        { name: 'AppCatalogTeamAppId', value: appCatalogTeamAppId }

        // --- Graph ---
        { name: 'GraphConfig__ClientId', value: graphClientId }
        { name: 'GraphConfig__ClientSecret', value: graphClientSecret }
        { name: 'GraphConfig__TenantId', value: graphTenantId }
        { name: 'GraphConfig__ApiAudience', value: 'https://graph.microsoft.com' }

        // --- Storage (RBAC via the managed identity above) ---
        { name: 'StorageAuthConfig__StorageAccountName', value: storageAccountName }
        { name: 'StorageAuthConfig__UseRBAC', value: 'true' }

        { name: 'DevMode', value: 'false' }
        { name: 'WEBSITE_RUN_FROM_PACKAGE', value: '1' }
      ],
      deployAppInsights ? [
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsights!.properties.ConnectionString }
        { name: 'ApplicationInsightsAgent_EXTENSION_VERSION', value: '~3' }
      ] : [],
      empty(aiFoundryEndpoint) ? [] : [
        { name: 'AIFoundryConfig__Endpoint', value: aiFoundryEndpoint }
        { name: 'AIFoundryConfig__DeploymentName', value: aiFoundryDeploymentName }
      ],
      !webAuthEnabled ? [] : [
        { name: 'WebAuthConfig__ClientId', value: webAuthClientId }
        { name: 'WebAuthConfig__ClientSecret', value: webAuthClientSecret }
        { name: 'WebAuthConfig__TenantId', value: webAuthTenantId }
        { name: 'WebAuthConfig__ApiAudience', value: webAuthApiAudience }
        { name: 'WebAuthConfig__Authority', value: '${environment().authentication.loginEndpoint}${webAuthTenantId}' }
      ])
    }
  }
}

// ---------------------------------------------------------------------------
// Role assignments - the app's managed identity needs data-plane access to Storage
// ---------------------------------------------------------------------------

// Built-in role definition IDs (stable across tenants).
var storageBlobDataContributor = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
var storageTableDataContributor = '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3'
var storageQueueDataContributor = '974c5e8b-45b9-4653-ba55-5f855dd0fb88'

resource blobRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storage.id, appService.id, storageBlobDataContributor)
  scope: storage
  properties: {
    principalId: appService.identity.principalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataContributor)
    principalType: 'ServicePrincipal'
  }
}

resource tableRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storage.id, appService.id, storageTableDataContributor)
  scope: storage
  properties: {
    principalId: appService.identity.principalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageTableDataContributor)
    principalType: 'ServicePrincipal'
  }
}

resource queueRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storage.id, appService.id, storageQueueDataContributor)
  scope: storage
  properties: {
    principalId: appService.identity.principalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageQueueDataContributor)
    principalType: 'ServicePrincipal'
  }
}

// ---------------------------------------------------------------------------
// Outputs
// ---------------------------------------------------------------------------

output appServiceName string = appService.name
output appServiceHostName string = appService.properties.defaultHostName
output messagingEndpoint string = 'https://${appService.properties.defaultHostName}/api/messages'
output storageAccountName string = storage.name
output managedIdentityPrincipalId string = appService.identity.principalId
output appInsightsConnectionString string = deployAppInsights ? appInsights!.properties.ConnectionString : ''
