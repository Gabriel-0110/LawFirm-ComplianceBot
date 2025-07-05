# 🔍 WEBHOOK VALIDATION DEBUG SCRIPT
# Deep dive into webhook validation issues

Write-Host "🔍 Teams Compliance Bot - Webhook Validation Debug" -ForegroundColor Cyan
Write-Host "===================================================" -ForegroundColor Cyan

$baseUrl = "https://arandiateamsbot.ggunifiedtech.com"

# Function to test webhook validation with different parameters
function Test-WebhookValidation {
    param(
        [string]$ValidationToken,
        [string]$UserAgent,
        [string]$Description
    )
    
    try {
        Write-Host "`nTesting: $Description" -ForegroundColor Yellow
        Write-Host "  • Token: $ValidationToken" -ForegroundColor Gray
        Write-Host "  • User-Agent: $UserAgent" -ForegroundColor Gray
        
        $params = @{
            Uri = "$baseUrl/api/notifications?validationToken=$ValidationToken"
            Method = "GET"
            TimeoutSec = 30
            Headers = @{
                'User-Agent' = $UserAgent
            }
        }
        
        $response = Invoke-WebRequest @params
        $responseBody = $response.Content
        
        Write-Host "  ✅ Status: $($response.StatusCode)" -ForegroundColor Green
        Write-Host "  ✅ Response: $responseBody" -ForegroundColor Green
        Write-Host "  ✅ Content-Type: $($response.Headers['Content-Type'])" -ForegroundColor Green
        
        # Validate response matches token
        if ($responseBody -eq $ValidationToken) {
            Write-Host "  ✅ TOKEN MATCH: Response correctly echoes validation token" -ForegroundColor Green
            return $true
        } else {
            Write-Host "  ❌ TOKEN MISMATCH: Expected '$ValidationToken', got '$responseBody'" -ForegroundColor Red
            return $false
        }
    }
    catch {
        Write-Host "  ❌ ERROR: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

Write-Host "`n🧪 Testing Webhook Validation Scenarios..." -ForegroundColor Magenta

# Test different validation scenarios
$tests = @(
    @{
        Token = "test123"
        UserAgent = "Microsoft-Graph-ChangeNotifications/1.0"
        Description = "Standard Graph validation (typical)"
    },
    @{
        Token = "abc-123-def"
        UserAgent = "Microsoft-Graph-ChangeNotifications/1.0"
        Description = "Graph validation with hyphens"
    },
    @{
        Token = "validation_token_12345"
        UserAgent = "Microsoft-Graph-ChangeNotifications/1.0"
        Description = "Graph validation with underscores"
    },
    @{
        Token = "short"
        UserAgent = "Microsoft-Graph-ChangeNotifications/1.0"
        Description = "Short validation token"
    },
    @{
        Token = "VeryLongValidationTokenWithManyCharacters123456789"
        UserAgent = "Microsoft-Graph-ChangeNotifications/1.0"
        Description = "Long validation token"
    },
    @{
        Token = "test123"
        UserAgent = "Microsoft-BotFramework/3.0"
        Description = "Bot Framework user agent"
    },
    @{
        Token = "test123"
        UserAgent = "curl/8.0"
        Description = "Curl user agent"
    },
    @{
        Token = ""
        UserAgent = "Microsoft-Graph-ChangeNotifications/1.0"
        Description = "Empty validation token"
    }
)

$passedTests = 0
$totalTests = $tests.Count

foreach ($test in $tests) {
    $result = Test-WebhookValidation -ValidationToken $test.Token -UserAgent $test.UserAgent -Description $test.Description
    if ($result) {
        $passedTests++
    }
}

Write-Host "`n📊 WEBHOOK VALIDATION RESULTS" -ForegroundColor Cyan
Write-Host "=============================" -ForegroundColor Cyan
Write-Host "  • Total Tests: $totalTests" -ForegroundColor White
Write-Host "  • Passed: $passedTests" -ForegroundColor Green
Write-Host "  • Failed: $($totalTests - $passedTests)" -ForegroundColor Red

if ($passedTests -eq $totalTests) {
    Write-Host "`n✅ ALL WEBHOOK TESTS PASSED!" -ForegroundColor Green
    Write-Host "The webhook validation endpoint is working correctly." -ForegroundColor Green
} elseif ($passedTests -gt 0) {
    Write-Host "`n⚠️ PARTIAL SUCCESS" -ForegroundColor Yellow
    Write-Host "Some validation scenarios work, others don't." -ForegroundColor Yellow
} else {
    Write-Host "`n❌ ALL WEBHOOK TESTS FAILED!" -ForegroundColor Red
    Write-Host "The webhook validation endpoint has issues." -ForegroundColor Red
}

Write-Host "`n🔍 NEXT DEBUGGING STEPS:" -ForegroundColor Cyan
Write-Host "1. Check Azure App Service logs for webhook validation attempts" -ForegroundColor White
Write-Host "2. Monitor Application Insights for Graph validation requests" -ForegroundColor White
Write-Host "3. Test subscription creation with minimal resource scope" -ForegroundColor White
Write-Host "4. Verify HTTPS/SSL certificate configuration" -ForegroundColor White

Write-Host "`n🌐 Graph Subscription Creation:" -ForegroundColor Cyan
Write-Host "Microsoft Graph sends validation requests to the webhook URL." -ForegroundColor White
Write-Host "The endpoint must respond with 200 OK and echo the validation token." -ForegroundColor White
Write-Host "If this fails, Graph refuses to create the subscription." -ForegroundColor White

Write-Host "`n📋 Webhook URL: $baseUrl/api/notifications" -ForegroundColor White
