#!/usr/bin/env pwsh
# Teams Compliance Bot - Azure Monitoring and Diagnostics Script

param(
    [switch]$Live,
    [switch]$Logs,
    [switch]$Insights,
    [switch]$Health,
    [switch]$All
)

$webAppName = "teamsbot"
$resourceGroup = "Arandia-Apps"
$customDomain = "arandiateamsbot.ggunifiedtech.com"

Write-Host "🔍 Teams Compliance Bot - Azure Monitoring Dashboard" -ForegroundColor Green
Write-Host "====================================================" -ForegroundColor Green
Write-Host ""

if ($All -or $Health) {
    Write-Host "🏥 HEALTH CHECKS" -ForegroundColor Cyan
    Write-Host "=================" -ForegroundColor Cyan
    
    # Test bot endpoints
    $endpoints = @(
        "https://$customDomain",
        "https://$customDomain/api/messages",
        "https://$customDomain/api/notifications",
        "https://$customDomain/api/notifications/health",
        "https://$customDomain/api/subscriptions/health"
    )
    
    foreach ($endpoint in $endpoints) {
        try {
            Write-Host "Testing: $endpoint" -ForegroundColor Yellow
            $response = Invoke-WebRequest -Uri $endpoint -Method GET -TimeoutSec 10 -ErrorAction Stop
            Write-Host "  ✅ Status: $($response.StatusCode) $($response.StatusDescription)" -ForegroundColor Green
        } catch {
            Write-Host "  ❌ Error: $($_.Exception.Message)" -ForegroundColor Red
        }
    }
    Write-Host ""
}

if ($All -or $Logs) {
    Write-Host "📋 RECENT APP SERVICE LOGS" -ForegroundColor Cyan
    Write-Host "===========================" -ForegroundColor Cyan
    
    try {
        Write-Host "Fetching recent logs from Azure App Service..." -ForegroundColor Yellow
        $logs = az webapp log tail --name $webAppName --resource-group $resourceGroup --timeout 30 2>$null
        if ($logs) {
            Write-Host $logs -ForegroundColor White
        } else {
            Write-Host "No recent logs available. Trying to get deployment logs..." -ForegroundColor Yellow
            
            # Get deployment logs
            $deploymentLogs = az webapp deployment log show --name $webAppName --resource-group $resourceGroup 2>$null
            if ($deploymentLogs) {
                Write-Host $deploymentLogs -ForegroundColor White
            } else {
                Write-Host "No deployment logs available. App may not be deployed yet." -ForegroundColor Red
            }
        }
    } catch {
        Write-Host "❌ Error fetching logs: $($_.Exception.Message)" -ForegroundColor Red
    }
    Write-Host ""
}

if ($All -or $Insights) {
    Write-Host "📊 APPLICATION INSIGHTS DATA" -ForegroundColor Cyan
    Write-Host "============================" -ForegroundColor Cyan
    
    try {
        Write-Host "Querying Application Insights for recent activity..." -ForegroundColor Yellow
        
        # Get recent requests
        $query = @"
requests
| where timestamp > ago(1h)
| project timestamp, name, url, resultCode, duration
| order by timestamp desc
| take 20
"@
        
        $aiData = az monitor app-insights query --app "teamsbot" --analytics-query $query --resource-group $resourceGroup 2>$null | ConvertFrom-Json
        
        if ($aiData -and $aiData.tables) {
            Write-Host "Recent HTTP Requests (last hour):" -ForegroundColor Green
            $aiData.tables[0].rows | ForEach-Object {
                $timestamp = [DateTime]::Parse($_[0]).ToString("HH:mm:ss")
                $name = $_[1]
                $url = $_[2]
                $resultCode = $_[3]
                $duration = $_[4]
                Write-Host "  $timestamp | $resultCode | $name | ${duration}ms" -ForegroundColor White
            }
        } else {
            Write-Host "No recent Application Insights data found." -ForegroundColor Yellow
        }
        
        # Get recent exceptions
        $exceptionQuery = @"
exceptions
| where timestamp > ago(1h)
| project timestamp, type, outerMessage, method
| order by timestamp desc
| take 10
"@
        
        $exceptions = az monitor app-insights query --app "teamsbot" --analytics-query $exceptionQuery --resource-group $resourceGroup 2>$null | ConvertFrom-Json
        
        if ($exceptions -and $exceptions.tables -and $exceptions.tables[0].rows.Count -gt 0) {
            Write-Host ""
            Write-Host "Recent Exceptions (last hour):" -ForegroundColor Red
            $exceptions.tables[0].rows | ForEach-Object {
                $timestamp = [DateTime]::Parse($_[0]).ToString("HH:mm:ss")
                $type = $_[1]
                $message = $_[2]
                $method = $_[3]
                Write-Host "  $timestamp | $type | $method | $message" -ForegroundColor Red
            }
        }
        
    } catch {
        Write-Host "❌ Error querying Application Insights: $($_.Exception.Message)" -ForegroundColor Red
    }
    Write-Host ""
}

if ($Live) {
    Write-Host "🔴 LIVE MONITORING MODE" -ForegroundColor Red
    Write-Host "======================" -ForegroundColor Red
    Write-Host "Monitoring live logs... Press Ctrl+C to stop" -ForegroundColor Yellow
    Write-Host ""
    
    try {
        az webapp log tail --name $webAppName --resource-group $resourceGroup
    } catch {
        Write-Host "❌ Error starting live monitoring: $($_.Exception.Message)" -ForegroundColor Red
    }
}

if (-not ($Live -or $Logs -or $Insights -or $Health -or $All)) {
    Write-Host "Usage:" -ForegroundColor Yellow
    Write-Host "  .\monitor-bot.ps1 -All           # Run all checks" -ForegroundColor White
    Write-Host "  .\monitor-bot.ps1 -Health        # Test endpoints" -ForegroundColor White
    Write-Host "  .\monitor-bot.ps1 -Logs          # Get recent logs" -ForegroundColor White
    Write-Host "  .\monitor-bot.ps1 -Insights      # Query Application Insights" -ForegroundColor White
    Write-Host "  .\monitor-bot.ps1 -Live          # Live log monitoring" -ForegroundColor White
    Write-Host ""
    Write-Host "Example: .\monitor-bot.ps1 -All" -ForegroundColor Cyan
}

Write-Host ""
Write-Host "🔗 Quick Links:" -ForegroundColor Green
Write-Host "• GitHub Actions: https://github.com/Gabriel-0110/LawFirm-ComplianceBot/actions" -ForegroundColor Cyan
Write-Host "• Azure Portal: https://portal.azure.com/#@arandialawfirm.onmicrosoft.com/resource/subscriptions/b90a001e-0b0f-4114-8752-084c1babb416/resourceGroups/Arandia-Apps/providers/Microsoft.Web/sites/teamsbot" -ForegroundColor Cyan
Write-Host "• App Insights: https://portal.azure.com/#@arandialawfirm.onmicrosoft.com/resource/subscriptions/b90a001e-0b0f-4114-8752-084c1babb416/resourceGroups/Arandia-Apps/providers/microsoft.insights/components/teamsbot" -ForegroundColor Cyan
Write-Host "• Bot URL: https://$customDomain" -ForegroundColor Cyan
