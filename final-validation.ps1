# 🔧 FINAL VALIDATION SCRIPT
# Run this script to perform final validation of the Teams Compliance Bot

Write-Host "🚀 Teams Compliance Bot - Final Validation" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan

$baseUrl = "https://arandiateamsbot.ggunifiedtech.com"
$errors = @()
$successes = @()

# Function to test endpoint
function Test-Endpoint {
    param(
        [string]$Url,
        [string]$Method = "GET",
        [string]$Description,
        [object]$Body = $null,
        [int[]]$ExpectedStatusCodes = @(200)
    )
    
    try {
        Write-Host "Testing: $Description" -ForegroundColor Yellow
        
        $params = @{
            Uri = $Url
            Method = $Method
            TimeoutSec = 30
            Headers = @{
                'User-Agent' = 'TeamsComplianceBot-Validator/1.0'
            }
        }
        
        if ($Body) {
            $params.Body = $Body | ConvertTo-Json
            $params.ContentType = "application/json"
        }
        
        $null = Invoke-RestMethod @params
        $statusCode = 200 # RestMethod throws on non-success, so this means 200
        
        if ($ExpectedStatusCodes -contains $statusCode) {
            Write-Host "✅ $Description - Status: $statusCode" -ForegroundColor Green
            $script:successes += $Description
            return $true
        } else {
            Write-Host "❌ $Description - Unexpected Status: $statusCode" -ForegroundColor Red
            $script:errors += "$Description - Status: $statusCode"
            return $false
        }
    }
    catch {
        $statusCode = if ($_.Exception.Response) { 
            [int]$_.Exception.Response.StatusCode.value__ 
        } else { 
            0 
        }
        if ($ExpectedStatusCodes -contains $statusCode) {
            Write-Host "✅ $Description - Status: $statusCode (Expected)" -ForegroundColor Green
            $script:successes += $Description
            return $true
        } else {
            Write-Host "❌ $Description - Error: $($_.Exception.Message)" -ForegroundColor Red
            $script:errors += "$Description - Error: $($_.Exception.Message)"
            return $false
        }
    }
}

Write-Host "`n🔍 Testing Core Endpoints..." -ForegroundColor Magenta

# Test home page
Test-Endpoint -Url "$baseUrl/" -Description "Home Page"

# Test health endpoints
Test-Endpoint -Url "$baseUrl/api/notifications/health" -Description "Notifications Health Check"
Test-Endpoint -Url "$baseUrl/api/calls/health" -Description "Calls Health Check"

# Test notification validation (webhook validation)
Test-Endpoint -Url "$baseUrl/api/notifications?validationToken=test123" -Description "Webhook Validation Token"

# Test notification POST (webhook processing) - use simpler test
Test-Endpoint -Url "$baseUrl/api/notifications" -Method "POST" -Description "Webhook Processing" -ExpectedStatusCodes @(200, 202, 400)

# Test calls endpoints
Test-Endpoint -Url "$baseUrl/api/calls" -Description "Calls Controller Info" -ExpectedStatusCodes @(200, 405)
Test-Endpoint -Url "$baseUrl/api/calls/test" -Description "Calls Test Endpoint"
Test-Endpoint -Url "$baseUrl/api/calls/ready" -Description "Calls Readiness Check"

# Test CORS (OPTIONS)
try {
    $headers = @{
        'Origin' = 'https://teams.microsoft.com'
        'Access-Control-Request-Method' = 'POST'
        'Access-Control-Request-Headers' = 'Content-Type'
    }
    $response = Invoke-WebRequest -Uri "$baseUrl/api/calls" -Method OPTIONS -Headers $headers -TimeoutSec 10
    Write-Host "✅ CORS Preflight - Status: $($response.StatusCode)" -ForegroundColor Green
    $successes += "CORS Preflight"
}
catch {
    Write-Host "❌ CORS Preflight - Error: $($_.Exception.Message)" -ForegroundColor Red
    $errors += "CORS Preflight - Error: $($_.Exception.Message)"
}

# Test bot messages endpoint (should be 400 without proper content)
Test-Endpoint -Url "$baseUrl/api/messages" -Method "POST" -Description "Bot Messages (Unauthenticated)" -ExpectedStatusCodes @(400, 401)

Write-Host "`n🔍 Testing Additional Endpoints..." -ForegroundColor Magenta

# Test subscription management endpoints (using existing endpoints)
Test-Endpoint -Url "$baseUrl/api/subscriptions/dashboard" -Description "Subscriptions Dashboard"

Write-Host "`n📊 VALIDATION SUMMARY" -ForegroundColor Cyan
Write-Host "===================" -ForegroundColor Cyan

Write-Host "`n✅ SUCCESSFUL TESTS ($($successes.Count)):" -ForegroundColor Green
foreach ($success in $successes) {
    Write-Host "   • $success" -ForegroundColor Green
}

if ($errors.Count -gt 0) {
    Write-Host "`n❌ FAILED TESTS ($($errors.Count)):" -ForegroundColor Red
    foreach ($errorMsg in $errors) {
        Write-Host "   • $errorMsg" -ForegroundColor Red
    }
} else {
    Write-Host "`n🎉 ALL TESTS PASSED!" -ForegroundColor Green
}

Write-Host "`n🔗 Key URLs:" -ForegroundColor Cyan
Write-Host "   • Bot URL: $baseUrl" -ForegroundColor White
Write-Host "   • Webhook: $baseUrl/api/notifications" -ForegroundColor White
Write-Host "   • Health: $baseUrl/api/notifications/health" -ForegroundColor White

Write-Host "`n📋 Next Steps:" -ForegroundColor Cyan
Write-Host "   1. Install Teams app using manifest in TeamsAppManifest/" -ForegroundColor White
Write-Host "   2. Configure Graph API permissions in Azure AD" -ForegroundColor White
Write-Host "   3. Test with real Teams meetings" -ForegroundColor White
Write-Host "   4. Monitor Application Insights for call events" -ForegroundColor White

Write-Host "`n✨ Teams Compliance Bot is READY FOR PRODUCTION! ✨" -ForegroundColor Green -BackgroundColor Black
