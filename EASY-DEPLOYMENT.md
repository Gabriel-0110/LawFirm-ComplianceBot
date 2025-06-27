# 🚀 Easy Deployment Guide: Adding Secrets to GitHub

> **📋 Get Your Actual Values**: Use the values from your local `SECRETS-BACKUP-PRIVATE.txt` file to replace the placeholders below.

There are **3 easy ways** to securely deploy your Teams Compliance Bot with secrets:

## 🎯 Option 1: GitHub Secrets + Actions (Recommended)

### Step 1: Add Secrets to GitHub
1. Go to your repo: https://github.com/Gabriel-0110/LawFirm-ComplianceBot
2. Click **Settings** → **Secrets and variables** → **Actions** 
3. Click **New repository secret** and add these one by one:

| Secret Name | Value |
|-------------|-------|
| `MICROSOFT_APP_ID` | `your-bot-app-id` |
| `MICROSOFT_APP_PASSWORD` | `your-bot-app-password` |
| `MICROSOFT_APP_TENANT_ID` | `your-tenant-id` |
| `AZURE_AD_CLIENT_SECRET` | `your-client-secret` |
| `BLOB_STORAGE_CONNECTION` | `your-storage-connection-string` |
| `APPLICATION_INSIGHTS_CONNECTION` | `your-application-insights-connection-string` |
| `AZURE_WEBAPP_PUBLISH_PROFILE` | *Get this from Azure App Service → Deployment Center → Download publish profile* |

### Step 2: Push the Workflow
I've created `.github/workflows/deploy.yml` - just commit and push it:

```bash
git add .github/workflows/deploy.yml
git commit -m "Add automated deployment workflow"
git push origin main
```

### Step 3: Automatic Deployment! 🎉
- Every time you push to `main`, it automatically deploys with your secrets
- Secrets are encrypted and never visible in logs
- No manual configuration needed

---

## 🎯 Option 2: Azure App Service Configuration (Manual)

### Quick Setup in Azure Portal:
1. Go to your Azure App Service → **Configuration** → **Application settings**
2. Add these settings:

```
MicrosoftAppId = your-bot-app-id
MicrosoftAppPassword = your-bot-app-password
MicrosoftAppTenantId = your-tenant-id
AzureAd__TenantId = your-tenant-id
AzureAd__ClientId = your-client-id
AzureAd__ClientSecret = your-client-secret
ConnectionStrings__BlobStorage = your-storage-connection-string
ApplicationInsights__ConnectionString = your-application-insights-connection-string
Azure__StorageAccount = your-storage-account
Storage__ConnectionString = your-storage-connection-string
```

3. **Save** and **Restart** the app service

---

## 🎯 Option 3: Azure Key Vault (Enterprise)

For production environments, use Azure Key Vault:
1. Create an Azure Key Vault
2. Add secrets to Key Vault
3. Configure App Service to reference Key Vault secrets
4. Use `@Microsoft.KeyVault(VaultName=myvault;SecretName=mysecret)` syntax

---

## 🎯 Recommended: Use Option 1 (GitHub Secrets)

**Why GitHub Secrets is best:**
- ✅ Fully automated deployment
- ✅ Secrets never appear in code or logs  
- ✅ Easy to manage and update
- ✅ Works with your existing GitHub repository
- ✅ Free with GitHub

**Next Steps:**
1. Add the secrets to GitHub (takes 5 minutes)
2. Commit the workflow file
3. Push to main
4. Watch it deploy automatically! 🚀

Your secrets will be secure and your deployments will be automatic!
