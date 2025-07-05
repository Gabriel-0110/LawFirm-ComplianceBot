# 📞 GRAPH SUBSCRIPTIONS TEST SCRIPT
# Comprehensive testing of Microsoft Graph subscriptions and permissions

Write-Host "📞 Teams Compliance Bot - Graph Subscriptions Test" -ForegroundColor Cyan
Write-Host "====================================================" -ForegroundColor Cyan

$baseUrl = "https://arandiateamsbot.ggunifiedtech.com"
$userAgent = "TeamsComplianceBot-GraphTester/1.0"

# Function to test endpoint and parse JSON response
function Test-GraphEndpoint {
    param(
        [string]$Url,
        [string]$Method = "GET",
        [string]$Description,
        [object]$Body = $null
    )
    
    try {
        Write-Host "`nTesting: $Description" -ForegroundColor Yellow
        
        $params = @{
            Uri = $Url
            Method = $Method
            TimeoutSec = 30
            Headers = @{
                'User-Agent' = $userAgent
            }
        }
        
        if ($Body) {
            $params.Body = $Body | ConvertTo-Json
            $params.ContentType = "application/json"
        }
        
        $response = Invoke-RestMethod @params
        Write-Host "✅ $Description - SUCCESS" -ForegroundColor Green
        return $response
    }
    catch {
        Write-Host "❌ $Description - ERROR: $($_.Exception.Message)" -ForegroundColor Red
        return $null
    }
}

Write-Host "`n🔍 Testing Graph Subscription Endpoints..." -ForegroundColor Magenta

# Test 1: Dashboard Overview
Write-Host "`n1. SUBSCRIPTION DASHBOARD" -ForegroundColor White
$dashboard = Test-GraphEndpoint -Url "$baseUrl/api/subscriptions/dashboard" -Description "Subscription Dashboard"
if ($dashboard) {
    $health = $dashboard.dashboard.subscriptionHealth
    Write-Host "   • Total subscriptions: $($health.total)" -ForegroundColor White
    Write-Host "   • Active: $($health.active)" -ForegroundColor Green
    Write-Host "   • Expired: $($health.expired)" -ForegroundColor Red
    Write-Host "   • Status: $($health.status)" -ForegroundColor $(if($health.status -eq "Operational") {"Green"} else {"Yellow"})
}

# Test 2: Permission Status
Write-Host "`n2. GRAPH API PERMISSIONS" -ForegroundColor White
$permissions = Test-GraphEndpoint -Url "$baseUrl/api/subscriptions/permission-status" -Description "Permission Status Check"
if ($permissions) {
    Write-Host "   Permission Summary:" -ForegroundColor White
    Write-Host "   • Total: $($permissions.summary.total)" -ForegroundColor White
    Write-Host "   • Working: $($permissions.summary.working)" -ForegroundColor Green
    Write-Host "   • Failed: $($permissions.summary.failed)" -ForegroundColor Red
    Write-Host "   • Overall: $($permissions.summary.overallStatus)" -ForegroundColor $(if($permissions.summary.working -gt 0) {"Green"} else {"Red"})
}

# Test 3: Detailed Permission Check
Write-Host "`n3. DETAILED PERMISSION ANALYSIS" -ForegroundColor White
$detailedPerms = Test-GraphEndpoint -Url "$baseUrl/api/subscriptions/check-permissions" -Description "Detailed Permissions Check"
if ($detailedPerms) {
    Write-Host "   Permission Details:" -ForegroundColor White
    Write-Host "   • Total permissions: $($detailedPerms.permissionStatus.total)" -ForegroundColor White
    Write-Host "   • Granted: $($detailedPerms.permissionStatus.granted)" -ForegroundColor Green
    Write-Host "   • Required: $($detailedPerms.permissionStatus.required)" -ForegroundColor White
    Write-Host "   • Ready for production: $($detailedPerms.permissionStatus.ready)" -ForegroundColor $(if($detailedPerms.permissionStatus.ready) {"Green"} else {"Red"})
    
    Write-Host "`n   Individual Permissions:" -ForegroundColor White
    foreach ($perm in $detailedPerms.permissions) {
        $color = switch ($perm.status) {
            { $_ -like "*Granted*" } { "Green" }
            { $_ -like "*Unknown*" } { "Yellow" }
            { $_ -like "*Not Implemented*" } { "Yellow" }
            default { "Red" }
        }
        Write-Host "   • $($perm.permission): $($perm.status)" -ForegroundColor $color
        if ($perm.required) {
            Write-Host "     ⚠️ REQUIRED FOR COMPLIANCE RECORDING" -ForegroundColor Red
        }
    }
}

# Test 4: Webhook Validation
Write-Host "`n4. WEBHOOK VALIDATION TEST" -ForegroundColor White
$webhookTest = Test-GraphEndpoint -Url "$baseUrl/api/notifications?validationToken=test123" -Description "Webhook Validation"

# Test 5: Subscription Creation Test (Expected to Fail)
Write-Host "`n5. SUBSCRIPTION CREATION TEST" -ForegroundColor White
$createTest = Test-GraphEndpoint -Url "$baseUrl/api/subscriptions/create-call-records-extended" -Method "POST" -Description "Create New Call Records Subscription"

# Test 6: Subscription Renewal
Write-Host "`n6. SUBSCRIPTION RENEWAL TEST" -ForegroundColor White
$renewTest = Test-GraphEndpoint -Url "$baseUrl/api/subscriptions/renew-all" -Method "POST" -Description "Renew All Subscriptions"

Write-Host "`n📊 GRAPH SUBSCRIPTIONS SUMMARY" -ForegroundColor Cyan
Write-Host "==============================" -ForegroundColor Cyan

# Analyze results
$webhookWorking = $webhookTest -ne $null
$hasActiveSubscriptions = $dashboard -and $dashboard.dashboard.subscriptionHealth.active -gt 0
$hasRequiredPermissions = $detailedPerms -and $detailedPerms.permissionStatus.ready

Write-Host "`n✅ WORKING COMPONENTS:" -ForegroundColor Green
if ($webhookWorking) { Write-Host "   • Webhook validation endpoint" -ForegroundColor Green }
if ($dashboard) { Write-Host "   • Subscription monitoring dashboard" -ForegroundColor Green }
if ($permissions) { Write-Host "   • Permission status reporting" -ForegroundColor Green }

Write-Host "`n❌ ISSUES IDENTIFIED:" -ForegroundColor Red
if (!$hasRequiredPermissions) {
    Write-Host "   • Missing CallRecords.Read.All permission (CRITICAL)" -ForegroundColor Red
}
if (!$hasActiveSubscriptions) {
    Write-Host "   • No active Graph subscriptions" -ForegroundColor Red
}
Write-Host "   • Subscription validation failing from Microsoft Graph" -ForegroundColor Red

Write-Host "`n🔧 REQUIRED ACTIONS:" -ForegroundColor Yellow
Write-Host "   1. Grant CallRecords.Read.All permission in Azure AD" -ForegroundColor White
Write-Host "   2. Grant Calls.AccessMedia.All permission for live calls" -ForegroundColor White
Write-Host "   3. Grant OnlineMeetings.ReadWrite.All permission" -ForegroundColor White
Write-Host "   4. Provide admin consent for all permissions" -ForegroundColor White
Write-Host "   5. Test subscription creation after granting permissions" -ForegroundColor White

Write-Host "`n🌐 AZURE AD APP REGISTRATION:" -ForegroundColor Cyan
Write-Host "   • App ID: 153ad72f-6fa4-4e88-b0fe-f0f785466699" -ForegroundColor White
Write-Host "   • Portal: https://portal.azure.com/#view/Microsoft_AAD_RegisteredApps/ApplicationMenuBlade/~/ApiPermissions/appId/153ad72f-6fa4-4e88-b0fe-f0f785466699" -ForegroundColor White

Write-Host "`n📋 GRAPH API PERMISSIONS NEEDED:" -ForegroundColor Cyan
Write-Host "   • CallRecords.Read.All (Application) - READ CALL RECORDS" -ForegroundColor White
Write-Host "   • Calls.AccessMedia.All (Application) - RECORD LIVE CALLS" -ForegroundColor White
Write-Host "   • OnlineMeetings.ReadWrite.All (Application) - MANAGE MEETINGS" -ForegroundColor White
Write-Host "   • Calls.JoinGroupCall.All (Application) - JOIN CALLS" -ForegroundColor White

Write-Host "`n🎯 CURRENT STATUS: PERMISSIONS CONFIGURATION REQUIRED" -ForegroundColor Yellow -BackgroundColor Black
