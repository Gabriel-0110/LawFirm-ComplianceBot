#!/usr/bin/env pwsh
# Teams Bot Monitoring Script - Check deployment and logs

Write-Host "🔍 Teams Compliance Bot - Deployment & Monitoring Check" -ForegroundColor Green
Write-Host "=====================================================" -ForegroundColor Green
Write-Host ""

$appName = "teamsbot"
$resourceGroup = "Arandia-Apps"
$botUrl = "https://arandiateamsbot.ggunifiedtech.com"

# Function to test endpoint
function Test-Endpoint {
    param($url, $description)
    
    try {
        $response = Invoke-WebRequest -Uri $url -Method GET -TimeoutSec 10 -UseBasicParsing
        $status = $response.StatusCode
        
        if ($status -eq 200) {
            Write-Host "✅ $description : $status OK" -ForegroundColor Green
        } else {
            Write-Host "⚠️  $description : $status" -ForegroundColor Yellow
        }
    } catch {
        Write-Host "❌ $description : ERROR - $($_.Exception.Message)" -ForegroundColor Red
    }
}

# Check GitHub Actions deployment status
Write-Host "🚀 Checking GitHub Actions Deployment..." -ForegroundColor Cyan
Write-Host "Monitor at: https://github.com/Gabriel-0110/LawFirm-ComplianceBot/actions" -ForegroundColor Blue
Write-Host ""

# Test bot endpoints
Write-Host "🌐 Testing Bot Endpoints..." -ForegroundColor Cyan
Test-Endpoint "$botUrl" "Main Bot URL"
Test-Endpoint "$botUrl/api/messages" "Bot Messages Endpoint"
Test-Endpoint "$botUrl/api/notifications" "Webhook Notifications Endpoint"
Test-Endpoint "$botUrl/api/notifications/health" "Notifications Health Check"
Test-Endpoint "$botUrl/api/calls" "Calls Endpoint"

Write-Host ""

# Check Azure App Service status
Write-Host "☁️  Checking Azure App Service..." -ForegroundColor Cyan
try {
    $appStatus = az webapp show --name $appName --resource-group $resourceGroup --query "state" -o tsv 2>$null
    if ($appStatus -eq "Running") {
        Write-Host "✅ App Service Status: $appStatus" -ForegroundColor Green
    } else {
        Write-Host "⚠️  App Service Status: $appStatus" -ForegroundColor Yellow
    }
} catch {
    Write-Host "❌ Could not check App Service status. Make sure Azure CLI is logged in." -ForegroundColor Red
}

Write-Host ""

# Get recent logs
Write-Host "📋 Recent Application Logs..." -ForegroundColor Cyan
Write-Host "Getting last 20 log entries..." -ForegroundColor Gray

try {
    $logs = az webapp log tail --name $appName --resource-group $resourceGroup --provider application --timeout 5 2>$null
    if ($logs) {
        Write-Host $logs -ForegroundColor White
    } else {
        Write-Host "⚠️  No recent logs found or unable to access logs" -ForegroundColor Yellow
    }
} catch {
    Write-Host "❌ Could not retrieve logs. Check Azure CLI login." -ForegroundColor Red
}

Write-Host ""

# Application Insights link
Write-Host "📊 Application Insights Monitoring:" -ForegroundColor Cyan
Write-Host "Go to: https://portal.azure.com/#@arandialawfirm.com/resource/subscriptions/b90a001e-0b0f-4114-8752-084c1babb416/resourceGroups/Arandia-Apps/providers/microsoft.insights/components/teamsbot/overview" -ForegroundColor Blue
Write-Host ""

# Next steps
Write-Host "🎯 NEXT STEPS:" -ForegroundColor Green
Write-Host "1. If endpoints are failing, check GitHub Actions logs"
Write-Host "2. Check Application Insights for runtime errors"
Write-Host "3. Verify all GitHub secrets are correctly set"
Write-Host "4. Test bot registration in Teams Admin Center"
Write-Host "5. Create test meeting to verify automatic joining"
Write-Host ""

Write-Host "📞 Test Bot Functionality:" -ForegroundColor Yellow
Write-Host "1. Start a Teams meeting in your organization"
Write-Host "2. Check Application Insights for call join attempts"
Write-Host "3. Verify recordings appear in blob storage"
Write-Host ""

# Open monitoring URLs
Write-Host "🌐 Opening monitoring dashboards..." -ForegroundColor Green
Start-Process "https://github.com/Gabriel-0110/LawFirm-ComplianceBot/actions"
Start-Process "https://portal.azure.com/#@arandialawfirm.com/resource/subscriptions/b90a001e-0b0f-4114-8752-084c1babb416/resourceGroups/Arandia-Apps/providers/microsoft.insights/components/teamsbot/overview"
