# 🤖 Teams Compliance Bot - GitHub Deployment Ready

A Microsoft Teams bot for compliance recording that **automatically joins calls** and records them with secure storage, Microsoft Graph integration, and full compliance features.

## 🚀 Recent Critical Fixes

- **✅ Compilation errors fixed** - Code now builds successfully
- **✅ Automatic call joining** - Bot receives webhooks and joins calls automatically  
- **✅ Graph subscriptions** - Real-time notifications when calls start
- **✅ Enhanced services** - All missing components implemented
- **✅ GitHub deployment** - Ready for CI/CD deployment

## 🔧 GitHub Secrets Required

Set these in GitHub Settings → Secrets and variables → Actions:

### Core Bot Configuration
```
MICROSOFT_APP_ID
MICROSOFT_APP_PASSWORD  
MICROSOFT_APP_TENANT_ID
```

### Azure Deployment
```
AZURE_CREDENTIALS
AZURE_RESOURCE_GROUP
```

### Storage & Endpoints
```
BLOB_STORAGE_CONNECTION_STRING
APPLICATION_INSIGHTS_CONNECTION_STRING
BOT_NOTIFICATION_URL
BOT_CALLBACK_URL
NOTIFICATION_CLIENT_STATE
```

## Features

- **Compliance Recording**: Automatic recording of Teams calls and meetings
- **Secure Storage**: Azure Blob Storage integration for secure file storage
- **Transcription**: Audio transcription capabilities for compliance records
- **User Management**: Role-based access control (Admin, Super Admin, Viewer)
- **Notifications**: Real-time alerts for recording events
- **Policy Management**: Configurable retention and deletion policies
- **Encryption**: Certificate-based encryption for sensitive data

## Prerequisites

- Microsoft Teams environment
- Azure subscription
- Bot registration in Azure Bot Service
- Application registration in Azure AD

## Configuration

Before deployment, configure the following settings in your Azure App Service:

### Required Application Settings

```
MicrosoftAppId=your-bot-app-id
MicrosoftAppPassword=your-bot-app-password
MicrosoftAppTenantId=your-tenant-id
```

### Azure AD Settings

```
AzureAd__TenantId=your-tenant-id
AzureAd__ClientId=your-client-id
AzureAd__ClientSecret=your-client-secret
```

### Storage Configuration

```
ConnectionStrings__BlobStorage=your-storage-connection-string
Azure__StorageAccount=your-storage-account
```

### Application Insights

```
ApplicationInsights__ConnectionString=your-application-insights-connection-string
```

## Deployment

1. Deploy the infrastructure using the Bicep templates in the `infra/` folder
2. Configure the application settings in Azure App Service  
3. Deploy the application code using GitHub Actions (automatic on push to main)
4. Update the Teams app manifest with your bot's App ID
5. Install the Teams app in your tenant

**Live Deployment:**
- Azure App Service: `teamsbot-cxawdtgqcqh2a3hd.eastus2-01.azurewebsites.net`
- Custom Domain: `arandiateamsbot.ggunifiedtech.com`

## Teams App Manifest

Update the `TeamsComplianceBot/TeamsAppManifest/manifest.json` file with:
- Your bot's App ID in the `id` and `botId` fields
- Your company information in the `developer` section
- Your domain in the `validDomains` section (e.g., `arandiateamsbot.ggunifiedtech.com`)

## Security Notes

⚠️ **Important**: This repository contains placeholder values for all sensitive configuration. Never commit actual secrets, connection strings, or API keys to source control.

All sensitive values should be configured directly in your Azure App Service application settings or Azure Key Vault.

## Support

For support and questions, please refer to the documentation or create an issue in this repository.

## License

This project is licensed under the MIT License.