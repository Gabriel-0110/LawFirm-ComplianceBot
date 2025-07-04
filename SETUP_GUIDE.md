# Teams Compliance Bot - Setup Guide

## Critical Issues Fixed

### 1. ✅ Compilation Error Fixed
- Fixed missing `ProcessCallRecordForComplianceAsync` method
- Added proper async/await handling

### 2. ✅ Missing Service Registration Fixed
- Added `ICallJoiningService` registration in DI container
- Added `SubscriptionSetupService` for automatic Graph subscription setup

### 3. ✅ Microsoft Graph Webhooks Added
- Created `GraphWebhookController` to receive call notifications
- Added automatic call joining when notifications are received

## What Was Missing (Critical for Bot Function)

### 1. **Microsoft Graph Subscriptions**
Your bot wasn't receiving notifications when calls started. The new `SubscriptionSetupService` now automatically:
- Creates subscriptions for `/communications/calls` 
- Creates subscriptions for `/communications/callRecords`
- Sets up webhooks to receive real-time notifications

### 2. **Automatic Call Joining**
The `GraphWebhookController` now:
- Receives notifications when calls start
- Automatically attempts to join the call
- Starts recording after successful join

### 3. **Proper Graph API Permissions Needed**
Your bot needs these specific permissions in Azure AD:

**Application Permissions Required:**
```
- Calls.AccessMedia.All
- Calls.Initiate.All  
- Calls.JoinGroupCall.All
- Calls.JoinGroupCallAsGuest.All
- CallRecords.Read.All
- OnlineMeetings.Read.All
- OnlineMeetings.ReadWrite.All
```

## Configuration Setup Required

### 1. Update appsettings.json with Real Values

Replace placeholder values in `appsettings.json`:

```json
{
  "MicrosoftAppId": "YOUR_ACTUAL_BOT_APP_ID",
  "MicrosoftAppPassword": "YOUR_ACTUAL_BOT_APP_PASSWORD", 
  "MicrosoftAppTenantId": "YOUR_ACTUAL_TENANT_ID",
  "AzureAd": {
    "TenantId": "YOUR_ACTUAL_TENANT_ID",
    "ClientId": "YOUR_ACTUAL_BOT_APP_ID", 
    "ClientSecret": "YOUR_ACTUAL_BOT_APP_PASSWORD"
  },
  "Recording": {
    "NotificationUrl": "https://YOUR_ACTUAL_BOT_DOMAIN.com/api/graphwebhook",
    "NotificationClientState": "YOUR_SECURE_RANDOM_TOKEN_FOR_SECURITY"
  },
  "ConnectionStrings": {
    "BlobStorage": "YOUR_ACTUAL_STORAGE_CONNECTION_STRING",
    "ApplicationInsights": "YOUR_ACTUAL_APP_INSIGHTS_CONNECTION_STRING"
  }
}
```

### 2. Azure AD App Registration Setup

**Step 1: Go to Azure Portal > Azure Active Directory > App Registrations**

**Step 2: Find your bot app registration and add API Permissions:**
- Microsoft Graph > Application Permissions
- Add all the permissions listed above
- Grant admin consent for your organization

**Step 3: Enable Public Client Flow:**
- Go to Authentication tab
- Enable "Allow public client flows"

**Step 4: Add Redirect URIs:**
- Add `https://token.botframework.com/.auth/web/redirect`
- Add your bot's domain: `https://YOUR_BOT_DOMAIN.com/api/messages`

### 3. Bot Framework Registration

**In Azure Portal > Bot Channels Registration:**
- Set Messaging Endpoint: `https://YOUR_BOT_DOMAIN.com/api/messages` 
- Enable Microsoft Teams channel
- Set the App ID and Password

### 4. Teams App Manifest Updates

Update your `manifest.json`:

```json
{
  "webApplicationInfo": {
    "id": "YOUR_ACTUAL_BOT_APP_ID",
    "resource": "https://graph.microsoft.com/"
  },
  "authorization": {
    "permissions": {
      "resourceSpecific": [
        {
          "name": "Calls.AccessMedia.Chat",
          "type": "Application"
        },
        {
          "name": "Calls.JoinGroupCall.Chat", 
          "type": "Application"
        },
        {
          "name": "OnlineMeeting.ReadBasic.Chat",
          "type": "Application"
        }
      ]
    }
  }
}
```

## Testing Your Bot

### 1. Build and Deploy
```bash
cd "c:\Coding\Teams Recording"
dotnet build
# Deploy to Azure App Service
```

### 2. Test Webhook Endpoint
```bash
# Test that webhook validation works:
curl "https://YOUR_BOT_DOMAIN.com/api/graphwebhook?validationToken=test123"
# Should return: test123
```

### 3. Install Bot in Teams
- Upload the Teams app package to Teams
- Add the bot to a team or chat
- Start a Teams call/meeting
- Check logs to see if bot receives notifications and joins

### 4. Check Logs
Monitor these logs:
- "Creating subscription for resource: /communications/calls"
- "Call created notification received"
- "Attempting to join call"
- "Successfully joined call"
- "Recording started for call"

## Microsoft Compliance Requirements

### 1. **Data Retention Policies**
- Current setting: 2555 days (7 years) - compliant with most regulations
- Auto-delete enabled for expired recordings

### 2. **Security Measures**
- All credentials stored in Azure Key Vault (via infrastructure)
- Storage encrypted at rest and in transit
- Access logging enabled via Application Insights

### 3. **User Consent**
- Bot announces recording when it joins calls
- Participants are notified that recording is in progress
- Compliance with Teams recording notification requirements

### 4. **Access Controls**
- Admin users defined in configuration
- Role-based access to recordings and compliance data
- Audit trail for all access and operations

## Next Steps

1. **Configure Real Values**: Replace all placeholder values with actual ones
2. **Deploy Infrastructure**: Use the provided Bicep templates to deploy Azure resources
3. **Set Permissions**: Configure the Microsoft Graph API permissions
4. **Test Thoroughly**: Start with a small group before organization-wide deployment
5. **Monitor Compliance**: Set up alerting for failed recordings or compliance violations

## Troubleshooting

If the bot still doesn't join calls:

1. **Check Permissions**: Verify all Graph API permissions are granted and consented
2. **Check Webhooks**: Test the webhook endpoint responds correctly
3. **Check Logs**: Look for error messages in Application Insights
4. **Check Network**: Ensure bot can reach Microsoft Graph APIs
5. **Check Bot Registration**: Verify bot is properly registered in Bot Framework

The bot should now automatically join calls and start recording for compliance purposes.
