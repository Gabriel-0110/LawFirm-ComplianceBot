# Quick Start Real Teams Meeting Test
# This script helps you quickly test your bot with a real Teams meeting
# Usage: .\quick-real-test.ps1

Write-Host "🚀 TEAMS COMPLIANCE BOT - QUICK REAL TEST" -ForegroundColor Green
Write-Host "=" * 60
Write-Host ""

# 1. Pre-flight check (FIXED ENDPOINT)
Write-Host "🔍 Step 1: Pre-flight Bot Health Check..." -ForegroundColor Yellow
try {
    $healthResponse = Invoke-WebRequest -Uri "https://arandiateamsbot.ggunifiedtech.com/health" -Method GET -UseBasicParsing -TimeoutSec 15
    if ($healthResponse.StatusCode -eq 200) {
        Write-Host "   ✅ Bot is healthy and responding (FIXED ENDPOINT)" -ForegroundColor Green
    } else {
        Write-Host "   ⚠️ Bot responded with status: $($healthResponse.StatusCode)" -ForegroundColor Yellow
    }
} catch {
    Write-Host "   ❌ Bot health check failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "   🔧 Try running: .\monitor-production.ps1 to diagnose" -ForegroundColor Cyan
    exit 1
}

Write-Host ""

# 2. Start monitoring
Write-Host "🔍 Step 2: Starting Real-Time Monitoring..." -ForegroundColor Yellow
Write-Host "   📊 Monitoring will run for 10 minutes while you test" -ForegroundColor Cyan

# Start background monitoring
$monitoringJob = Start-Job -ScriptBlock {
    param($Duration)
    & "c:\Coding\Teams Recording\real-test-monitor.ps1" -Duration $Duration
} -ArgumentList "10m"

Write-Host "   ✅ Background monitoring started (Job ID: $($monitoringJob.Id))" -ForegroundColor Green
Write-Host ""

# 3. Instructions for user
Write-Host "📞 Step 3: CREATE YOUR TEST MEETING NOW!" -ForegroundColor Magenta
Write-Host "=" * 50
Write-Host ""
Write-Host "🎯 FOLLOW THESE STEPS:" -ForegroundColor White
Write-Host ""
Write-Host "   1. 📅 Open Microsoft Teams (desktop or web)" -ForegroundColor Cyan
Write-Host "   2. 🗓️ Go to Calendar > New Meeting" -ForegroundColor Cyan
Write-Host "   3. 📝 Title: 'Bot Test - $(Get-Date -Format 'HH:mm')'" -ForegroundColor Cyan
Write-Host "   4. 👥 Add your bot to the meeting:" -ForegroundColor Cyan
Write-Host "      • Option A: Add as attendee (if you have bot email)" -ForegroundColor DarkCyan
Write-Host "      • Option B: In meeting options, enable apps/bots" -ForegroundColor DarkCyan
Write-Host "   5. ▶️ Start the meeting immediately" -ForegroundColor Cyan
Write-Host "   6. 👀 Look for your bot in the participants list" -ForegroundColor Cyan
Write-Host ""
Write-Host "🔗 Bot Details:" -ForegroundColor Yellow
Write-Host "   • App ID: 153ad72f-6fa4-4e88-b0fe-f0f785466699" -ForegroundColor DarkYellow
Write-Host "   • Bot Name: Arandia Compliance Bot" -ForegroundColor DarkYellow
Write-Host "   • Webhook URL: https://arandiateamsbot.ggunifiedtech.com/api/calls" -ForegroundColor DarkYellow
Write-Host ""

# 4. Monitor progress
Write-Host "⏰ MONITORING YOUR TEST..." -ForegroundColor Green
Write-Host "   The background monitor will show activity as it happens" -ForegroundColor White
Write-Host "   Press Ctrl+C to stop monitoring early, or wait 10 minutes" -ForegroundColor Gray
Write-Host ""

# Wait for monitoring to complete or user to interrupt
try {
    Wait-Job -Job $monitoringJob -Timeout 600  # 10 minutes
    $monitorOutput = Receive-Job -Job $monitoringJob
    Remove-Job -Job $monitoringJob
    
    Write-Host $monitorOutput
} catch {
    Write-Host ""
    Write-Host "⏹️ Monitoring interrupted by user" -ForegroundColor Yellow
    Stop-Job -Job $monitoringJob
    Remove-Job -Job $monitoringJob
}

Write-Host ""

# 5. Analysis
Write-Host "📊 Step 4: Analyzing Test Results..." -ForegroundColor Yellow
Write-Host ""

# Run analysis
try {
    & "c:\Coding\Teams Recording\analyze-real-test.ps1"
} catch {
    Write-Host "❌ Could not run analysis: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# 6. What's Next
Write-Host "🎯 WHAT'S NEXT?" -ForegroundColor Magenta
Write-Host "=" * 50
Write-Host ""
Write-Host "If you saw bot activity:" -ForegroundColor Green
Write-Host "   ✅ Your bot is working with real Teams meetings!" -ForegroundColor Green
Write-Host "   📞 Try more advanced scenarios (recording, multiple participants)" -ForegroundColor Cyan
Write-Host "   🔄 Set up automated monitoring: .\setup-monitoring.ps1" -ForegroundColor Cyan
Write-Host ""
Write-Host "If you didn't see activity:" -ForegroundColor Yellow
Write-Host "   🔧 Check if the Teams app is uploaded to your tenant" -ForegroundColor Yellow
Write-Host "   📋 Verify bot permissions in Azure AD and Teams Admin Center" -ForegroundColor Yellow
Write-Host "   🔍 Review the complete guide: REAL-TESTING-GUIDE.md" -ForegroundColor Yellow
Write-Host ""
Write-Host "For ongoing monitoring:" -ForegroundColor Cyan
Write-Host "   📊 .\monitor-production.ps1 - Quick health checks" -ForegroundColor White
Write-Host "   🔍 .\real-test-monitor.ps1 - Live testing monitor" -ForegroundColor White
Write-Host "   📈 .\analyze-real-test.ps1 - Detailed post-test analysis" -ForegroundColor White
Write-Host ""
Write-Host "✅ Quick real test completed!" -ForegroundColor Green
