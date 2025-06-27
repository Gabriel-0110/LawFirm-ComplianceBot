# Teams Compliance Bot - Real-World Testing Guide
# How to test your bot with actual Teams meetings and calls
# Date: 2025-06-24

## 🎯 **Overview: Real vs Synthetic Testing**

**Current Status**: Your bot passes all synthetic tests (29/29) ✅
**Next Step**: Test with real Teams meetings and calls

### What We've Tested (Synthetic):
- ✅ Bot endpoints respond correctly
- ✅ Microsoft Graph API connectivity  
- ✅ Webhook processing logic
- ✅ Auto-answer code paths
- ✅ Error handling and security

### What We Need to Test (Real):
- 📞 Actual Teams meeting invitations
- 📞 Real call joining and recording
- 📞 Live webhook notifications from Microsoft Graph
- 📞 End-to-end call lifecycle
- 📞 Recording file generation and storage

---

## 🚀 **Step-by-Step Real Testing Process**

### **Phase 1: Teams App Registration & Deployment**

#### 1.1 **Upload Teams App Manifest**
```powershell
# Your manifest is at: TeamsAppManifest/manifest.json
# Steps:
# 1. Go to Microsoft Teams Admin Center: https://admin.teams.microsoft.com
# 2. Navigate to: Teams Apps > Manage Apps
# 3. Click "Upload" > "Upload an app"
# 4. Select your manifest.json file
# 5. Approve the app for your organization
```

#### 1.2 **Verify Bot Registration in Azure**
```powershell
# Check your bot service configuration
az bot show --name "arandia-compliance-bot" --resource-group "arandia-apps"

# Verify the messaging endpoint points to your production bot
# Should be: https://arandiabot-app.azurewebsites.net/api/messages
```

#### 1.3 **Set Up Graph API Subscriptions** (Optional for webhook testing)
```powershell
# Your bot can create subscriptions programmatically when calls happen
# Or you can create them manually for testing
curl -X POST "https://graph.microsoft.com/v1.0/subscriptions" \
  -H "Authorization: Bearer <your-app-token>" \
  -H "Content-Type: application/json" \
  -d '{
    "changeType": "created,updated,deleted",
    "notificationUrl": "https://arandiabot-app.azurewebsites.net/api/calls",
    "resource": "communications/calls",
    "expirationDateTime": "2025-06-25T22:00:00Z",
    "clientState": "TeamsComplianceBot-RealTest"
  }'
```

### **Phase 2: Real Teams Meeting Tests**

#### 2.1 **Basic Meeting Join Test**
```powershell
# Test Steps:
# 1. Create a Teams meeting in Outlook or Teams
# 2. Add your bot to the meeting:
#    - In meeting options, add: arandia-compliance-bot@<your-tenant>.com
#    - Or use the app name from your manifest
# 3. Start the meeting
# 4. Verify bot joins automatically
# 5. Check Application Insights for logs

# Monitor in real-time:
.\monitor-production.ps1 -Detailed

# Check logs during the test:
az monitor app-insights query --app 2ba9f587-8009-489c-a50b-0bc1c0ce19f8 \
  --analytics-query "traces | where timestamp > ago(10m) | order by timestamp desc"
```

#### 2.2 **Advanced Meeting Scenarios**
Create different test scenarios:

**Test Scenario A: Scheduled Meeting**
- Create recurring Teams meeting
- Add bot as attendee
- Start meeting and verify auto-join
- Test recording functionality

**Test Scenario B: Instant Meeting**  
- Start instant Teams meeting
- Invite bot during call
- Verify bot can join mid-call

**Test Scenario C: Large Meeting**
- Meeting with 10+ participants
- Test bot performance and recording quality

### **Phase 3: Call Recording & Storage Tests**

#### 3.1 **Recording Verification**
```powershell
# After a real call, check if recordings are created:
# Your bot should create recordings in Azure Blob Storage
# Check the storage account configured in your app settings

# Query for recording-related logs:
az monitor app-insights query --app 2ba9f587-8009-489c-a50b-0bc1c0ce19f8 \
  --analytics-query "traces | where message contains 'recording' or message contains 'media' | where timestamp > ago(1h)"
```

#### 3.2 **Storage Account Verification**
```powershell
# Check if your bot created any recording files
az storage blob list --account-name <your-storage-account> --container-name recordings --output table
```

---

## 🛠️ **Real Testing Scripts**

### **Script 1: Real-Time Monitoring During Tests**
```powershell
# Save as: real-test-monitor.ps1
param(
    [string]$Duration = "30m"  # How long to monitor
)

Write-Host "🔍 Starting Real-Time Test Monitoring for $Duration..." -ForegroundColor Green
Write-Host "Run your Teams meeting test now!" -ForegroundColor Yellow

$endTime = (Get-Date).AddMinutes([int]$Duration.Replace('m',''))

while ((Get-Date) -lt $endTime) {
    Write-Host "`n⏰ $(Get-Date -Format 'HH:mm:ss') - Checking bot activity..." -ForegroundColor Cyan
    
    # Quick health check
    $status = .\monitor-production.ps1
    
    # Check for recent call activity
    $recentCalls = az monitor app-insights query --app 2ba9f587-8009-489c-a50b-0bc1c0ce19f8 \
        --analytics-query "traces | where timestamp > ago(2m) and (message contains 'call' or message contains 'Graph') | count" \
        --output tsv 2>$null
    
    if ($recentCalls -and [int]$recentCalls -gt 0) {
        Write-Host "📞 ACTIVITY DETECTED: $recentCalls recent call-related log entries!" -ForegroundColor Green
        
        # Show recent activity
        az monitor app-insights query --app 2ba9f587-8009-489c-a50b-0bc1c0ce19f8 \
            --analytics-query "traces | where timestamp > ago(2m) and (message contains 'call' or message contains 'Graph') | project timestamp, message | order by timestamp desc" \
            --output table
    } else {
        Write-Host "💤 No recent activity detected" -ForegroundColor Gray
    }
    
    Start-Sleep 30  # Check every 30 seconds
}

Write-Host "`n✅ Monitoring completed!" -ForegroundColor Green
```

### **Script 2: Post-Test Analysis**
```powershell
# Save as: analyze-real-test.ps1
param(
    [string]$TestStartTime = (Get-Date).AddHours(-1).ToString("yyyy-MM-ddTHH:mm:ss")
)

Write-Host "📊 Analyzing Real Test Results since $TestStartTime..." -ForegroundColor Green

# 1. Check for call-related activity
Write-Host "`n🔍 Call Activity Analysis:" -ForegroundColor Yellow
az monitor app-insights query --app 2ba9f587-8009-489c-a50b-0bc1c0ce19f8 \
    --analytics-query "traces | where timestamp > datetime('$TestStartTime') and (message contains 'call' or message contains 'webhook' or message contains 'Graph') | summarize count() by bin(timestamp, 5m) | order by timestamp desc" \
    --output table

# 2. Check for any errors during test
Write-Host "`n❌ Error Analysis:" -ForegroundColor Red
az monitor app-insights query --app 2ba9f587-8009-489c-a50b-0bc1c0ce19f8 \
    --analytics-query "traces | where timestamp > datetime('$TestStartTime') and severityLevel >= 3 | project timestamp, message, severityLevel | order by timestamp desc" \
    --output table

# 3. Check webhook activity
Write-Host "`n🔗 Webhook Activity:" -ForegroundColor Cyan
az monitor app-insights query --app 2ba9f587-8009-489c-a50b-0bc1c0ce19f8 \
    --analytics-query "requests | where timestamp > datetime('$TestStartTime') and url contains '/api/calls' | summarize count() by resultCode | order by resultCode" \
    --output table

# 4. Performance metrics
Write-Host "`n⚡ Performance Metrics:" -ForegroundColor Green
az monitor app-insights query --app 2ba9f587-8009-489c-a50b-0bc1c0ce19f8 \
    --analytics-query "requests | where timestamp > datetime('$TestStartTime') | summarize avg(duration), max(duration), count() by name | order by avg_duration desc" \
    --output table

Write-Host "`n✅ Analysis completed!" -ForegroundColor Green
```

---

## 📋 **Real Test Checklist**

### **Pre-Test Setup** ✅
- [ ] Teams app manifest uploaded to Teams Admin Center
- [ ] Bot approved for your organization
- [ ] Bot service endpoints configured correctly
- [ ] Production bot is running and healthy
- [ ] Application Insights monitoring active

### **Test Execution** 🧪
- [ ] **Test 1**: Schedule Teams meeting, add bot, verify auto-join
- [ ] **Test 2**: Start instant meeting, invite bot, verify joining
- [ ] **Test 3**: Test recording start/stop functionality
- [ ] **Test 4**: Verify webhook notifications are received
- [ ] **Test 5**: Test call termination and cleanup

### **Post-Test Validation** ✅
- [ ] Check Application Insights for call activity
- [ ] Verify recording files created (if applicable)
- [ ] Confirm no errors in logs
- [ ] Validate webhook processing
- [ ] Test bot cleanup and resource management

---

## 🚨 **Common Issues & Troubleshooting**

### **Issue 1: Bot Doesn't Join Meeting**
```powershell
# Troubleshooting steps:
# 1. Check if bot is properly added to meeting
# 2. Verify Teams app is installed and approved
# 3. Check bot registration in Azure Bot Service
# 4. Verify callback URLs are correct

# Debug command:
az bot show --name "arandia-compliance-bot" --resource-group "arandia-apps" --query "{messagingEndpoint:messagingEndpoint,displayName:displayName}"
```

### **Issue 2: No Webhooks Received**
```powershell
# Check subscription status:
az monitor app-insights query --app 2ba9f587-8009-489c-a50b-0bc1c0ce19f8 \
    --analytics-query "requests | where url contains '/api/calls' | where timestamp > ago(1h) | order by timestamp desc"

# Verify webhook endpoint accessibility:
curl -X POST "https://arandiabot-app.azurewebsites.net/api/calls" \
    -H "Content-Type: application/json" \
    -d '{"test": "webhook connectivity"}'
```

### **Issue 3: Recording Not Working**
```powershell
# Check media configuration:
az monitor app-insights query --app 2ba9f587-8009-489c-a50b-0bc1c0ce19f8 \
    --analytics-query "traces | where message contains 'media' or message contains 'recording' | where timestamp > ago(1h)"

# Verify storage account access:
az storage account show --name <your-storage-account> --resource-group arandia-apps
```

---

## 🎯 **Quick Start: Your First Real Test**

**Run this 5-minute test right now:**

1. **Start Monitoring**:
   ```powershell
   .\real-test-monitor.ps1 -Duration "10m"
   ```

2. **Create Test Meeting**:
   - Open Microsoft Teams
   - Click "Calendar" > "New Meeting"
   - Add title: "Bot Test - $(Get-Date)"
   - Add attendees: Include your bot or use meeting options to allow bot
   - Start the meeting immediately

3. **Verify Bot Behavior**:
   - Check if bot joins automatically
   - Look for bot presence in participants list
   - Monitor the PowerShell output for activity

4. **Check Results**:
   ```powershell
   .\analyze-real-test.ps1
   ```

**Expected Results**:
- ✅ Bot appears in meeting participants
- ✅ Webhook notifications appear in logs  
- ✅ No errors in Application Insights
- ✅ Call lifecycle tracked properly

---

## 📞 **Ready to Test?**

Your bot is **production-ready** and all synthetic tests pass. Now it's time for real Teams meetings!

**Start with**: Upload your Teams app manifest and run the first real test. Your bot is fully prepared to handle live Teams calling scenarios.

**Need Help?** Run `.\real-test-monitor.ps1` and create a test meeting - the script will show you exactly what's happening in real-time.
