// Teams Compliance Bot - Main Infrastructure Template
// This template creates all Azure resources needed for the Teams Compliance Bot
targetScope = 'resourceGroup'

@description('Environment name used for resource naming')
param environmentName string

@description('Primary location for all resources')
param location string = resourceGroup().location

@description('App Service Plan SKU')
@allowed(['F1', 'D1', 'B1', 'B2', 'B3', 'S1', 'S2', 'S3', 'P1', 'P2', 'P3'])
param appServicePlanSku string = 'B1'

@description('Storage account SKU')
@allowed(['Standard_LRS', 'Standard_GRS', 'Standard_RAGRS', 'Standard_ZRS', 'Premium_LRS'])
param storageAccountSku string = 'Standard_LRS'

@description('Microsoft App ID for the bot')
param microsoftAppId string

@description('Microsoft App Password for the bot')
@secure()
param microsoftAppPassword string

@description('Microsoft App Tenant ID')
param microsoftAppTenantId string

@description('Azure AD Client ID for Graph API')
param azureAdClientId string = microsoftAppId

@description('Azure AD Client Secret for Graph API')
@secure()
param azureAdClientSecret string = microsoftAppPassword

@description('Bot Display Name')
param botDisplayName string = 'Teams Compliance Bot'

@description('Bot Description')
param botDescription string = 'Enterprise compliance bot for Microsoft Teams call recording and management'

// Generate resource token for unique naming
var resourceToken = toLower(uniqueString(subscription().id, resourceGroup().id, environmentName))

// Resource naming
var resourceGroupName = resourceGroup().name
var logAnalyticsWorkspaceName = 'log-${resourceToken}'
var appInsightsName = 'appi-${resourceToken}'
var keyVaultName = 'kv-${resourceToken}'
var storageAccountName = 'st${resourceToken}'
var appServicePlanName = 'plan-${resourceToken}'
var appServiceName = 'app-${resourceToken}'
var botServiceName = 'bot-${resourceToken}'

// Tags for all resources
var commonTags = {
  'azd-env-name': environmentName
  Environment: 'Production'
  Application: 'TeamsComplianceBot'
  Purpose: 'CallRecording'
  ManagedBy: 'AzureDeveloperCLI'
}

// Log Analytics Workspace
resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsWorkspaceName
  location: location
  tags: commonTags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
    features: {
      enableLogAccessUsingOnlyResourcePermissions: true
    }
  }
}

// Application Insights
resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  tags: commonTags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalyticsWorkspace.id
    IngestionMode: 'LogAnalytics'
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

// User-assigned managed identity for secure access
resource userManagedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-${resourceToken}'
  location: location
  tags: commonTags
}

// Key Vault for secure credential storage
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  tags: commonTags
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enabledForTemplateDeployment: true
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      defaultAction: 'Allow'
      bypass: 'AzureServices'
    }
  }
}

// Storage Account for blob storage
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  tags: commonTags
  kind: 'StorageV2'
  sku: {
    name: storageAccountSku
  }
  properties: {
    accessTier: 'Hot'
    allowBlobPublicAccess: false
    allowSharedKeyAccess: true
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
    networkAcls: {
      defaultAction: 'Allow'
      bypass: 'AzureServices'
    }
    encryption: {
      services: {
        blob: {
          enabled: true
          keyType: 'Account'
        }
        file: {
          enabled: true
          keyType: 'Account'
        }
      }
      keySource: 'Microsoft.Storage'
    }
  }
}

// Role assignments for storage access
resource storageAccountRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, userManagedIdentity.id, 'ba92f5b4-2d11-453d-a403-e96b0029c9fe')
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe') // Storage Blob Data Contributor
    principalId: userManagedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// Key Vault role assignment for managed identity
resource keyVaultRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, userManagedIdentity.id, '4633458b-17de-408a-b874-0445c86b69e6')
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6') // Key Vault Secrets User
    principalId: userManagedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// App Service Plan
resource appServicePlan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: appServicePlanName
  location: location
  tags: commonTags
  kind: 'app'
  sku: {
    name: appServicePlanSku
  }
  properties: {
    reserved: false
  }
}

// App Service for hosting the bot
resource appService 'Microsoft.Web/sites@2024-04-01' = {
  name: appServiceName
  location: location
  tags: union(commonTags, {
    'azd-service-name': 'teams-compliance-bot'
  })
  kind: 'app'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${userManagedIdentity.id}': {}
    }
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    clientAffinityEnabled: false
    publicNetworkAccess: 'Enabled'
    siteConfig: {
      netFrameworkVersion: 'v8.0'
      alwaysOn: appServicePlanSku != 'F1' && appServicePlanSku != 'D1'
      use32BitWorkerProcess: false
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      http20Enabled: true
      healthCheckPath: '/health'
      cors: {
        allowedOrigins: ['*']
        supportCredentials: false
      }
      appSettings: [
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsights.properties.ConnectionString
        }
        {
          name: 'ApplicationInsightsAgent_EXTENSION_VERSION'
          value: '~3'
        }
        {
          name: 'XDT_MicrosoftApplicationInsights_Mode'
          value: 'Recommended'
        }
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        {
          name: 'MicrosoftAppId'
          value: microsoftAppId
        }
        {
          name: 'MicrosoftAppPassword'
          value: microsoftAppPassword
        }
        {
          name: 'MicrosoftAppTenantId'
          value: microsoftAppTenantId
        }
        {
          name: 'MicrosoftAppType'
          value: 'MultiTenant'
        }
        {
          name: 'AzureAd__Instance'
          value: environment().authentication.loginEndpoint
        }
        {
          name: 'AzureAd__TenantId'
          value: microsoftAppTenantId
        }
        {
          name: 'AzureAd__ClientId'
          value: azureAdClientId
        }
        {
          name: 'AzureAd__ClientSecret'
          value: azureAdClientSecret
        }
        {
          name: 'Azure__StorageAccount'
          value: storageAccount.name
        }
        {
          name: 'Compliance__DefaultRetentionDays'
          value: '2555'
        }
        {
          name: 'Compliance__AutoDelete'
          value: 'true'
        }
        {
          name: 'Compliance__PolicyVersion'
          value: '1.0'
        }
        {
          name: 'AZURE_CLIENT_ID'
          value: userManagedIdentity.properties.clientId
        }
      ]
      connectionStrings: [
        {
          name: 'BlobStorage'
          connectionString: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'
          type: 'Custom'
        }
        {
          name: 'ApplicationInsights'
          connectionString: appInsights.properties.ConnectionString
          type: 'Custom'
        }
      ]
    }
  }
}

// Bot Service
resource botService 'Microsoft.BotService/botServices@2022-09-15' = {
  name: botServiceName
  location: 'global'
  tags: commonTags
  kind: 'azurebot'
  sku: {
    name: 'F0'
  }
  properties: {
    displayName: botDisplayName
    description: botDescription
    endpoint: 'https://${appService.properties.defaultHostName}/api/messages'
    msaAppId: microsoftAppId
    msaAppType: 'MultiTenant'
    isStreamingSupported: false
    disableLocalAuth: false
    publicNetworkAccess: 'Enabled'
    schemaTransformationVersion: '1.3'
  }
}

// Teams Channel for the bot
resource teamsChannel 'Microsoft.BotService/botServices/channels@2022-09-15' = {
  parent: botService
  name: 'MsTeamsChannel'
  location: 'global'
  properties: {
    channelName: 'MsTeamsChannel'
    properties: {
      enableCalling: true
      callingWebhook: 'https://${appService.properties.defaultHostName}/api/calls'
      isEnabled: true
    }
  }
}

// Outputs for deployment
output AZURE_LOCATION string = location
output AZURE_TENANT_ID string = subscription().tenantId
output AZURE_RESOURCE_GROUP string = resourceGroupName
output RESOURCE_GROUP_ID string = resourceGroup().id

output APPLICATIONINSIGHTS_CONNECTION_STRING string = appInsights.properties.ConnectionString
output APPLICATIONINSIGHTS_NAME string = appInsights.name

output AZURE_KEY_VAULT_NAME string = keyVault.name
output AZURE_KEY_VAULT_ENDPOINT string = keyVault.properties.vaultUri

output AZURE_STORAGE_ACCOUNT_NAME string = storageAccount.name
output AZURE_STORAGE_ACCOUNT_ID string = storageAccount.id

output SERVICE_TEAMS_COMPLIANCE_BOT_NAME string = appService.name
output SERVICE_TEAMS_COMPLIANCE_BOT_RESOURCE_EXISTS bool = true
output SERVICE_TEAMS_COMPLIANCE_BOT_ENDPOINT_URL string = 'https://${appService.properties.defaultHostName}'
output WEBSITE_HOSTNAME string = appService.properties.defaultHostName

output BOT_SERVICE_NAME string = botService.name
output BOT_SERVICE_ENDPOINT string = 'https://${appService.properties.defaultHostName}/api/messages'

output AZURE_CLIENT_ID string = userManagedIdentity.properties.clientId
output AZURE_MANAGED_IDENTITY_NAME string = userManagedIdentity.name
