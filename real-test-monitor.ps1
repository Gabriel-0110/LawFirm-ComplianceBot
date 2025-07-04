# Real-Time Teams Meeting Test Monitor
# This script monitors your bot during real Teams meeting tests
# Usage: .\real-test-monitor.ps1 -Duration "15m"

param(
    [string]$Duration = "15m"  # How long to monitor
)

Write-Host "🔍 Starting Real-Time Test Monitoring for $Duration..." -ForegroundColor Green
Write-Host "📞 Now create and start a Teams meeting to test your bot!" -ForegroundColor Yellow
Write-Host "⏰ This script will show you live activity from your bot..." -ForegroundColor Cyan
Write-Host ""

$durationMinutes = [int]$Duration.Replace('m','')
$endTime = (Get-Date).AddMinutes($durationMinutes)
$lastActivityCheck = Get-Date

while ((Get-Date) -lt $endTime) {
    $currentTime = Get-Date -Format 'HH:mm:ss'
    Write-Host "⏰ $currentTime - Checking bot activity..." -ForegroundColor Cyan
    
    try {
        # Quick health check (FIXED ENDPOINT)
        $healthResponse = Invoke-WebRequest -Uri "https://arandiabot-app.azurewebsites.net/health" -Method GET -UseBasicParsing -TimeoutSec 10
        $healthStatus = if ($healthResponse.StatusCode -eq 200) { "✅ HEALTHY" } else { "⚠️ WARNING" }
    } catch {
        $healthStatus = "❌ ERROR"
    }
    
    Write-Host "   Bot Status: $healthStatus" -ForegroundColor White
    
    # Check for recent call activity in Application Insights
    try {
        $recentCallsJson = az monitor app-insights query --app 2ba9f587-8009-489c-a50b-0bc1c0ce19f8 `
            --analytics-query "traces | where timestamp > ago(2m) and (message contains 'call' or message contains 'Graph' or message contains 'webhook' or message contains 'Teams') | count" `
            --output json 2>$null
        
        if ($recentCallsJson) {
            $recentCallsResult = $recentCallsJson | ConvertFrom-Json
            $activityCount = $recentCallsResult.tables[0].rows[0][0]
            
            if ($activityCount -gt 0) {
                Write-Host "   📞 ACTIVITY DETECTED: $activityCount recent call-related log entries!" -ForegroundColor Green
                
                # Show the actual recent activity
                $recentActivityJson = az monitor app-insights query --app 2ba9f587-8009-489c-a50b-0bc1c0ce19f8 `
                    --analytics-query "traces | where timestamp > ago(2m) and (message contains 'call' or message contains 'Graph' or message contains 'webhook' or message contains 'Teams') | project timestamp, message | order by timestamp desc | take 5" `
                    --output json 2>$null
                
                if ($recentActivityJson) {
                    $recentActivity = $recentActivityJson | ConvertFrom-Json
                    foreach ($row in $recentActivity.tables[0].rows) {
                        $logTime = [DateTime]::Parse($row[0]).ToString("HH:mm:ss")
                        $logMessage = $row[1]
                        Write-Host "     🔍 $logTime - $logMessage" -ForegroundColor Yellow
                    }
                }
            } else {
                Write-Host "   💤 No recent activity detected" -ForegroundColor Gray
            }
        }
    } catch {
        Write-Host "   ⚠️ Could not check Application Insights (expected if no recent activity)" -ForegroundColor DarkYellow
    }
    
    # Check for any recent errors
    try {
        $errorsJson = az monitor app-insights query --app 2ba9f587-8009-489c-a50b-0bc1c0ce19f8 `
            --analytics-query "traces | where timestamp > ago(2m) and severityLevel >= 3 | count" `
            --output json 2>$null
        
        if ($errorsJson) {
            $errorsResult = $errorsJson | ConvertFrom-Json
            $errorCount = $errorsResult.tables[0].rows[0][0]
            
            if ($errorCount -gt 0) {
                Write-Host "   ❌ ERRORS DETECTED: $errorCount recent errors!" -ForegroundColor Red
            }
        }
    } catch {
        # Ignore Application Insights query errors for error checking
    }
    
    $remainingMinutes = [math]::Round(($endTime - (Get-Date)).TotalMinutes, 1)
    Write-Host "   ⏳ $remainingMinutes minutes remaining..." -ForegroundColor DarkGray
    Write-Host ""
    
    Start-Sleep 30  # Check every 30 seconds
}

Write-Host "✅ Real-time monitoring completed!" -ForegroundColor Green
Write-Host ""
Write-Host "📊 To analyze your test results, run:" -ForegroundColor Cyan
Write-Host "   .\analyze-real-test.ps1" -ForegroundColor White
