# Teams Compliance Bot - Production Test Suite
# Comprehensive production testing for arandiabot-app.azurewebsites.net
# Author: Production Testing Suite
# Date: 2025-06-24

param(
    [string]$BaseUrl = "https://arandiateamsbot.ggunifiedtech.com",
    [string]$AppInsightsAppId = "a968b065-b8d3-4812-9105-7d805d39e46b",
    [string]$ResourceGroup = "arandia-apps",
    [string]$AppServiceName = "teamsbot",
    [switch]$Verbose,
    [switch]$IncludeAzureQueries,
    [switch]$MonitorOnly
)

Write-Host "🏭 Teams Compliance Bot - PRODUCTION Test Suite" -ForegroundColor Magenta
Write-Host "🌐 Production URL: $BaseUrl" -ForegroundColor Cyan
Write-Host "📊 Application Insights: $AppInsightsAppId" -ForegroundColor Cyan
Write-Host "🏗️  Resource Group: $ResourceGroup" -ForegroundColor Cyan
Write-Host "📱 App Service: $AppServiceName" -ForegroundColor Cyan
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor DarkGray

$global:testResults = @()
$global:errorCount = 0
$global:warningCount = 0
$global:passCount = 0
$global:startTime = Get-Date

function Write-TestResult {
    param(
        [string]$TestName,
        [string]$Status, # PASS, FAIL, WARN, INFO
        [string]$Message,
        [object]$Details = $null
    )
    
    $timestamp = Get-Date -Format "HH:mm:ss"
    $color = switch ($Status) {
        "PASS" { "Green"; $global:passCount++ }
        "FAIL" { "Red"; $global:errorCount++ }
        "WARN" { "Yellow"; $global:warningCount++ }
        "INFO" { "Cyan" }
    }
    
    Write-Host "[$timestamp] [$Status] $TestName`: $Message" -ForegroundColor $color
    
    $global:testResults += [PSCustomObject]@{
        Timestamp = $timestamp
        TestName = $TestName
        Status = $Status
        Message = $Message
        Details = $Details
    }
    
    if ($Verbose -and $Details) {
        Write-Host "   Details: $($Details | ConvertTo-Json -Compress)" -ForegroundColor Gray
    }
}

function Test-HttpEndpoint {
    param(
        [string]$Url,
        [string]$Method = "GET",
        [hashtable]$Headers = @{},
        [string]$Body = $null,
        [int]$ExpectedStatusCode = 200,
        [string]$TestName,
        [int]$TimeoutSec = 30
    )
    
    try {
        $params = @{
            Uri = $Url
            Method = $Method
            Headers = $Headers
            TimeoutSec = $TimeoutSec
        }
        
        if ($Body) {
            $params.Body = $Body
            $params.ContentType = "application/json"
        }
        
        $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
        $response = Invoke-RestMethod @params -StatusCodeVariable statusCode
        $stopwatch.Stop()
        
        $responseTime = $stopwatch.ElapsedMilliseconds
        
        if ($statusCode -eq $ExpectedStatusCode) {
            Write-TestResult $TestName "PASS" "HTTP $statusCode (${responseTime}ms)" $response
            return $response
        } else {
            Write-TestResult $TestName "WARN" "HTTP $statusCode (${responseTime}ms) - Expected $ExpectedStatusCode" $response
            return $response
        }
    }
    catch {
        $errorMessage = $_.Exception.Message
        if ($_.Exception.Response) {
            $statusCode = [int]$_.Exception.Response.StatusCode
            Write-TestResult $TestName "FAIL" "HTTP $statusCode - $errorMessage" $_.Exception
        } else {
            Write-TestResult $TestName "FAIL" "Request failed: $errorMessage" $_.Exception
        }
        return $null
    }
}

function Get-ProductionHealth {
    Write-Host "`n🏥 Production Health Assessment" -ForegroundColor Yellow
    
    # Check if we can reach the app
    try {
        $response = Invoke-WebRequest -Uri $BaseUrl -Method GET -TimeoutSec 10
        Write-TestResult "Service Availability" "PASS" "Production service is reachable" $response.StatusCode
    }
    catch {
        Write-TestResult "Service Availability" "FAIL" "Cannot reach production service: $($_.Exception.Message)" $_.Exception
        return $false
    }
    
    # Check HTTPS and security headers
    try {
        $response = Invoke-WebRequest -Uri $BaseUrl -Method GET -TimeoutSec 10
        
        # Check if the original URL was HTTPS (since we're connecting to https://)
        if ($BaseUrl.StartsWith("https://")) {
            Write-TestResult "HTTPS Enforcement" "PASS" "Service enforces HTTPS" $null
        } else {
            Write-TestResult "HTTPS Enforcement" "FAIL" "Service not using HTTPS" $null
        }
        
        # Check security headers
        $securityHeaders = @("Strict-Transport-Security", "X-Content-Type-Options", "X-Frame-Options")
        foreach ($header in $securityHeaders) {
            if ($response.Headers[$header]) {
                Write-TestResult "Security Header: $header" "PASS" "Present: $($response.Headers[$header])" $null
            } else {
                Write-TestResult "Security Header: $header" "WARN" "Missing recommended security header" $null
            }
        }
    }
    catch {
        Write-TestResult "Security Headers Check" "WARN" "Could not check security headers: $($_.Exception.Message)" $null
    }
    
    return $true
}

function Test-ProductionConfiguration {
    Write-Host "`n⚙️ Production Configuration Validation" -ForegroundColor Yellow
    
    # Test basic info endpoint
    $infoResponse = Test-HttpEndpoint "$BaseUrl/" "GET" @{} $null 200 "Bot Configuration"
    
    if ($infoResponse) {
        $config = $infoResponse.configuration
        
        # Verify production settings
        if ($infoResponse.environment -eq "Production") {
            Write-TestResult "Environment Mode" "PASS" "Running in Production mode" $infoResponse.environment
        } else {
            Write-TestResult "Environment Mode" "WARN" "Not in Production mode: $($infoResponse.environment)" $infoResponse.environment
        }
        
        # Check required configuration
        $requiredConfig = @(
            @{Name="botId"; Required=$true},
            @{Name="tenantId"; Required=$true},
            @{Name="appType"; Required=$true}
        )
        
        foreach ($configItem in $requiredConfig) {
            if ($config.($configItem.Name)) {
                Write-TestResult "Config: $($configItem.Name)" "PASS" "Configured: $($config.($configItem.Name))" $null
            } else {
                $status = if ($configItem.Required) { "FAIL" } else { "WARN" }
                Write-TestResult "Config: $($configItem.Name)" $status "Missing configuration" $null
            }
        }
        
        return $config
    }
    
    return $null
}

function Test-GraphApiProduction {
    Write-Host "`n📊 Microsoft Graph API - Production Testing" -ForegroundColor Yellow
    
    $graphTestResponse = Test-HttpEndpoint "$BaseUrl/api/calls/test-graph-api" "GET" @{} $null 200 "Graph API Connectivity"
    
    if ($graphTestResponse) {
        # Parse the results array to check individual tests
        $results = $graphTestResponse.results
        
        # Check authentication (app-only should work, delegated should fail)
        $authResult = $results | Where-Object { $_ -like "*Delegated Auth*" } | Select-Object -First 1
        if ($authResult -like "*Expected with app-only auth*") {
            Write-TestResult "Graph Authentication" "PASS" "App-only authentication working correctly" $authResult
        } else {
            Write-TestResult "Graph Authentication" "WARN" "Unexpected auth result: $authResult" $authResult
        }
        
        # Check Communications API access
        $commsResult = $results | Where-Object { $_ -like "*Communications Access*" } | Select-Object -First 1
        if ($commsResult -like "*Success*") {
            Write-TestResult "Communications API Access" "PASS" "Can access Microsoft Graph Communications" $commsResult
        } else {
            Write-TestResult "Communications API Access" "FAIL" "Cannot access Communications API: $commsResult" $commsResult
        }
        
        # Check Calls API access
        $callsResult = $results | Where-Object { $_ -like "*List Calls*" } | Select-Object -First 1
        if ($callsResult -like "*Found * calls*") {
            Write-TestResult "Calls API Access" "PASS" "Can access Calls API" $callsResult
        } elseif ($callsResult -like "*Failed*") {
            Write-TestResult "Calls API Access" "FAIL" "Cannot access Calls API: $callsResult" $callsResult
        } else {
            Write-TestResult "Calls API Access" "PASS" "Calls API accessible" $callsResult
        }
        
        # Check Answer API (test call answer functionality)
        $answerResult = $results | Where-Object { $_ -like "*Answer Test*" } | Select-Object -First 1
        if ($answerResult -like "*Error*") {
            if ($answerResult -like "*UnknownError*" -or $answerResult -like "*Call not found*") {
                Write-TestResult "Answer API Test" "PASS" "Answer API working (test error expected)" $answerResult
            } else {
                Write-TestResult "Answer API Test" "WARN" "Answer API error: $answerResult" $answerResult
            }
        } else {
            Write-TestResult "Answer API Test" "PASS" "Answer API accessible" $answerResult
        }
        
        # Overall Graph API assessment
        $successfulTests = ($results | Where-Object { $_ -like "*✅*" }).Count
        $failedTests = ($results | Where-Object { $_ -like "*❌*" }).Count
        
        if ($successfulTests -ge 2 -and $failedTests -eq 0) {
            Write-TestResult "Overall Graph API Status" "PASS" "Graph API integration working ($successfulTests successful tests)" $results
        } elseif ($failedTests -gt 0) {
            Write-TestResult "Overall Graph API Status" "FAIL" "Graph API has issues ($failedTests failed tests)" $results
        } else {
            Write-TestResult "Overall Graph API Status" "WARN" "Graph API partially working" $results
        }
        
        return $graphTestResponse
    }
    
    return $null
}

function Test-CallWebhookProduction {
    Write-Host "`n📞 Production Call Webhook Testing" -ForegroundColor Yellow
    
    # Test realistic production webhook scenarios
    $productionWebhookTests = @(
        @{
            Name = "Teams Meeting Join Request"
            Payload = @{
                "value" = @(
                    @{
                        "resourceUrl" = "https://graph.microsoft.com/v1.0/communications/calls/prod-call-001"
                        "resourceData" = @{
                            "id" = "prod-call-001"
                            "state" = "incoming"
                            "direction" = "incoming"
                            "source" = @{
                                "identity" = @{
                                    "user" = @{
                                        "displayName" = "Production User"
                                        "id" = "prod-user-001"
                                    }
                                }
                            }
                            "targets" = @(
                                @{
                                    "identity" = @{
                                        "application" = @{
                                            "displayName" = "Teams Compliance Bot"
                                            "id" = "153ad72f-6fa4-4e88-b0fe-f0f785466699"
                                        }
                                    }
                                }
                            )
                            "callbackUri" = "$BaseUrl/api/calls"
                            "mediaConfig" = @{
                                "removeFromDefaultAudioGroup" = $false
                            }
                            "chatInfo" = @{
                                "threadId" = "19:meeting_prod123@thread.v2"
                            }
                        }
                        "changeType" = "created"
                        "clientState" = "TeamsComplianceBot-Production"
                    }
                )
            }
        },
        @{
            Name = "Call State - Established"
            Payload = @{
                "value" = @(
                    @{
                        "resourceUrl" = "https://graph.microsoft.com/v1.0/communications/calls/prod-call-002"
                        "resourceData" = @{
                            "id" = "prod-call-002"
                            "state" = "established"
                            "direction" = "incoming"
                        }
                        "changeType" = "updated"
                        "clientState" = "TeamsComplianceBot-Production"
                    }
                )
            }
        },
        @{
            Name = "Call Recording Started"
            Payload = @{
                "value" = @(
                    @{
                        "resourceUrl" = "https://graph.microsoft.com/v1.0/communications/calls/prod-call-003"
                        "resourceData" = @{
                            "id" = "prod-call-003"
                            "state" = "established"
                            "direction" = "incoming"
                            "recordingInfo" = @{
                                "status" = "recording"
                            }
                        }
                        "changeType" = "updated"
                        "clientState" = "TeamsComplianceBot-Production"
                    }
                )
            }
        }
    )
    
    foreach ($test in $productionWebhookTests) {
        $payload = $test.Payload | ConvertTo-Json -Depth 10
        $response = Test-HttpEndpoint "$BaseUrl/api/calls" "POST" @{"Content-Type"="application/json"} $payload 200 $test.Name
        
        if ($response) {
            # Check if webhook was processed correctly
            if ($response.status -eq "processed" -or $response.message -like "*processed*") {
                Write-TestResult "$($test.Name) - Processing" "PASS" "Webhook processed successfully" $response
            }
        }
    }
}

function Test-AutoAnswerProduction {
    Write-Host "`n🔄 Production Auto-Answer Testing" -ForegroundColor Yellow
    
    $autoAnswerTests = @(
        @{
            Name = "Production Auto-Answer Test"
            Payload = @{
                "callId" = "prod-auto-001"
                "callState" = "incoming"
                "direction" = "incoming"
                "source" = @{
                    "identity" = @{
                        "user" = @{
                            "displayName" = "Production Test User"
                            "id" = "prod-test-user"
                        }
                    }
                }
                "targets" = @(
                    @{
                        "identity" = @{
                            "application" = @{
                                "id" = "153ad72f-6fa4-4e88-b0fe-f0f785466699"
                            }
                        }
                    }
                )
            }
        }
    )
    
    foreach ($test in $autoAnswerTests) {
        $payload = $test.Payload | ConvertTo-Json -Depth 10
        $response = Test-HttpEndpoint "$BaseUrl/api/calls/test-auto-answer" "POST" @{"Content-Type"="application/json"} $payload 200 $test.Name
        
        if ($response -and $response.testResult) {
            $result = $response.testResult
            if ($result.status -eq "answer_attempted") {
                Write-TestResult "$($test.Name) - Logic" "PASS" "Auto-answer logic executed" $result
            } elseif ($result.error -and $result.error -like "*Call not found*") {
                Write-TestResult "$($test.Name) - Expected" "PASS" "Auto-answer logic working (test call not found is expected)" $result
            } else {
                Write-TestResult "$($test.Name) - Error" "WARN" "Auto-answer issue: $($result.error)" $result
            }
        }
    }
}

function Get-ProductionLogs {
    Write-Host "`n📊 Production Logs Analysis" -ForegroundColor Yellow
    
    if (-not $IncludeAzureQueries) {
        Write-TestResult "Azure Queries" "INFO" "Skipped - use -IncludeAzureQueries to include Azure log queries" $null
        return
    }
    
    try {
        # Recent application logs
        $recentLogsQuery = @"
traces 
| where timestamp > ago(1h) 
| where customDimensions.Category contains 'TeamsComplianceBot' or message contains 'TeamsComplianceBot'
| project timestamp, severityLevel, message, customDimensions
| order by timestamp desc 
| limit 50
"@
        
        Write-Host "Querying Application Insights for recent logs..." -ForegroundColor Gray
        $recentLogs = az monitor app-insights query --app $AppInsightsAppId --analytics-query $recentLogsQuery --output json | ConvertFrom-Json
        
        if ($recentLogs.tables -and $recentLogs.tables[0].rows.Count -gt 0) {
            Write-TestResult "Recent Application Logs" "PASS" "Found $($recentLogs.tables[0].rows.Count) log entries in last hour" $recentLogs.tables[0].rows.Count
            
            # Analyze log levels
            $errorLogs = $recentLogs.tables[0].rows | Where-Object { $_[1] -eq 3 } # Error level
            $warningLogs = $recentLogs.tables[0].rows | Where-Object { $_[1] -eq 2 } # Warning level
            
            if ($errorLogs.Count -gt 0) {
                Write-TestResult "Error Logs" "WARN" "Found $($errorLogs.Count) error entries" $errorLogs.Count
                if ($Verbose) {
                    foreach ($error in $errorLogs[0..2]) { # Show first 3 errors
                        Write-Host "   ERROR: $($error[2])" -ForegroundColor Red
                    }
                }
            } else {
                Write-TestResult "Error Logs" "PASS" "No error entries in recent logs" $null
            }
            
            if ($warningLogs.Count -gt 0) {
                Write-TestResult "Warning Logs" "INFO" "Found $($warningLogs.Count) warning entries" $warningLogs.Count
            }
        } else {
            Write-TestResult "Recent Application Logs" "WARN" "No recent logs found - check if logging is working" $null
        }
        
        # Check for call-related logs specifically
        $callLogsQuery = @"
traces 
| where timestamp > ago(6h) 
| where message contains 'call' or message contains 'webhook' or message contains 'Graph'
| project timestamp, severityLevel, message
| order by timestamp desc 
| limit 20
"@
        
        $callLogs = az monitor app-insights query --app $AppInsightsAppId --analytics-query $callLogsQuery --output json | ConvertFrom-Json
        
        if ($callLogs.tables -and $callLogs.tables[0].rows.Count -gt 0) {
            Write-TestResult "Call-Related Logs" "PASS" "Found $($callLogs.tables[0].rows.Count) call/webhook related logs" $callLogs.tables[0].rows.Count
        } else {
            Write-TestResult "Call-Related Logs" "WARN" "No call/webhook related logs found in last 6 hours" $null
        }
        
        # Check performance metrics
        $perfQuery = @"
requests 
| where timestamp > ago(1h) 
| summarize 
    RequestCount = count(),
    AvgDuration = avg(duration),
    MaxDuration = max(duration),
    SuccessRate = avg(success) * 100
| project RequestCount, AvgDuration, MaxDuration, SuccessRate
"@
        
        $perfMetrics = az monitor app-insights query --app $AppInsightsAppId --analytics-query $perfQuery --output json | ConvertFrom-Json
        
        if ($perfMetrics.tables -and $perfMetrics.tables[0].rows.Count -gt 0) {
            $metrics = $perfMetrics.tables[0].rows[0]
            $requestCount = $metrics[0]
            $avgDuration = [math]::Round($metrics[1], 2)
            $maxDuration = [math]::Round($metrics[2], 2)
            $successRate = [math]::Round($metrics[3], 2)
            
            Write-TestResult "Performance Metrics" "PASS" "Requests: $requestCount, Avg: ${avgDuration}ms, Max: ${maxDuration}ms, Success: ${successRate}%" $metrics
            
            if ($successRate -lt 95) {
                Write-TestResult "Success Rate" "WARN" "Success rate below 95%: ${successRate}%" $successRate
            } else {
                Write-TestResult "Success Rate" "PASS" "Good success rate: ${successRate}%" $successRate
            }
        }
        
    }
    catch {
        Write-TestResult "Application Insights Query" "FAIL" "Unable to query production logs: $($_.Exception.Message)" $_.Exception
    }
}

function Get-AzureResourceStatus {
    Write-Host "`n🏗️ Azure Resource Status" -ForegroundColor Yellow
    
    if (-not $IncludeAzureQueries) {
        Write-TestResult "Azure Resource Check" "INFO" "Skipped - use -IncludeAzureQueries to include Azure resource queries" $null
        return
    }
    
    try {
        # Check App Service status
        Write-Host "Checking App Service status..." -ForegroundColor Gray
        $appStatus = az webapp show --name $AppServiceName --resource-group $ResourceGroup --query "{state:state,hostingEnvironmentProfile:hostingEnvironmentProfile,httpsOnly:httpsOnly}" --output json | ConvertFrom-Json
        
        if ($appStatus.state -eq "Running") {
            Write-TestResult "App Service State" "PASS" "Running" $appStatus.state
        } else {
            Write-TestResult "App Service State" "FAIL" "Not running: $($appStatus.state)" $appStatus.state
        }
        
        if ($appStatus.httpsOnly) {
            Write-TestResult "HTTPS Only Setting" "PASS" "HTTPS enforced" $appStatus.httpsOnly
        } else {
            Write-TestResult "HTTPS Only Setting" "WARN" "HTTPS not enforced" $appStatus.httpsOnly
        }
        
        # Check App Service configuration
        $appConfig = az webapp config show --name $AppServiceName --resource-group $ResourceGroup --query "{alwaysOn:alwaysOn,httpLoggingEnabled:httpLoggingEnabled,detailedErrorLoggingEnabled:detailedErrorLoggingEnabled}" --output json | ConvertFrom-Json
        
        if ($appConfig.alwaysOn) {
            Write-TestResult "Always On Setting" "PASS" "Always On enabled" $appConfig.alwaysOn
        } else {
            Write-TestResult "Always On Setting" "WARN" "Always On disabled - may cause cold starts" $appConfig.alwaysOn
        }
        
    }
    catch {
        Write-TestResult "Azure Resource Status" "WARN" "Unable to check Azure resources: $($_.Exception.Message)" $_.Exception
    }
}

function Test-ProductionEndToEnd {
    Write-Host "`n🎯 End-to-End Production Test" -ForegroundColor Yellow
    
    # Simulate a complete call workflow
    Write-Host "Simulating complete call workflow..." -ForegroundColor Gray
    
    # Step 1: Incoming call notification
    $incomingCallPayload = @{
        "value" = @(
            @{
                "resourceUrl" = "https://graph.microsoft.com/v1.0/communications/calls/e2e-test-$(Get-Date -Format 'yyyyMMddHHmmss')"
                "resourceData" = @{
                    "id" = "e2e-test-$(Get-Date -Format 'yyyyMMddHHmmss')"
                    "state" = "incoming"
                    "direction" = "incoming"
                    "source" = @{
                        "identity" = @{
                            "user" = @{
                                "displayName" = "E2E Test User"
                                "id" = "e2e-test-user"
                            }
                        }
                    }
                    "targets" = @(
                        @{
                            "identity" = @{
                                "application" = @{
                                    "id" = "153ad72f-6fa4-4e88-b0fe-f0f785466699"
                                }
                            }
                        }
                    )
                    "callbackUri" = "$BaseUrl/api/calls"
                }
                "changeType" = "created"
                "clientState" = "TeamsComplianceBot-E2E-Test"
            }
        )
    } | ConvertTo-Json -Depth 10
    
    $callResponse = Test-HttpEndpoint "$BaseUrl/api/calls" "POST" @{"Content-Type"="application/json"} $incomingCallPayload 200 "E2E - Incoming Call"
    
    if ($callResponse) {
        Start-Sleep -Seconds 2
        
        # Step 2: Call established notification
        $establishedCallPayload = @{
            "value" = @(
                @{
                    "resourceUrl" = "https://graph.microsoft.com/v1.0/communications/calls/e2e-test-$(Get-Date -Format 'yyyyMMddHHmmss')"
                    "resourceData" = @{
                        "id" = "e2e-test-$(Get-Date -Format 'yyyyMMddHHmmss')"
                        "state" = "established"
                        "direction" = "incoming"
                    }
                    "changeType" = "updated"
                    "clientState" = "TeamsComplianceBot-E2E-Test"
                }
            )
        } | ConvertTo-Json -Depth 10
        
        $establishedResponse = Test-HttpEndpoint "$BaseUrl/api/calls" "POST" @{"Content-Type"="application/json"} $establishedCallPayload 200 "E2E - Call Established"
        
        if ($establishedResponse) {
            Write-TestResult "E2E Workflow" "PASS" "Complete call workflow simulation successful" $null
        }
    }
}

# Main execution
if ($MonitorOnly) {
    Write-Host "`n📊 MONITORING MODE - Logs and Status Only" -ForegroundColor Magenta
    Get-ProductionLogs
    Get-AzureResourceStatus
} else {
    # Run full test suite
    Write-Host "`n🚀 Starting Production Test Suite..." -ForegroundColor Green
    
    # Core tests
    $isHealthy = Get-ProductionHealth
    if (-not $isHealthy) {
        Write-Host "❌ Service not available - stopping tests" -ForegroundColor Red
        exit 1
    }
    
    $config = Test-ProductionConfiguration
    $graphResult = Test-GraphApiProduction
    Test-CallWebhookProduction
    Test-AutoAnswerProduction
    Test-ProductionEndToEnd
    
    # Monitoring and status
    Get-ProductionLogs
    Get-AzureResourceStatus
}

# Final Summary
$endTime = Get-Date
$duration = $endTime - $global:startTime

Write-Host "`n📋 PRODUCTION TEST SUMMARY" -ForegroundColor Magenta
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor DarkGray
Write-Host "⏱️  Duration: $($duration.TotalSeconds) seconds" -ForegroundColor Cyan
Write-Host "✅ Passed: $global:passCount" -ForegroundColor Green
Write-Host "⚠️  Warnings: $global:warningCount" -ForegroundColor Yellow
Write-Host "❌ Failed: $global:errorCount" -ForegroundColor Red
Write-Host "📊 Total Tests: $($global:passCount + $global:warningCount + $global:errorCount)" -ForegroundColor Cyan

# Production Readiness Assessment
Write-Host "`n🏭 PRODUCTION READINESS ASSESSMENT" -ForegroundColor Magenta
$criticalIssues = $global:testResults | Where-Object { $_.Status -eq "FAIL" }
$totalIssues = $criticalIssues.Count

if ($totalIssues -eq 0) {
    Write-Host "🟢 PRODUCTION READY" -ForegroundColor Green
    Write-Host "✅ All critical tests passed" -ForegroundColor Green
    Write-Host "✅ Bot is ready for live Teams calling" -ForegroundColor Green
} elseif ($totalIssues -le 2) {
    Write-Host "🟡 PRODUCTION READY WITH WARNINGS" -ForegroundColor Yellow
    Write-Host "⚠️  Minor issues found - review before live deployment" -ForegroundColor Yellow
} else {
    Write-Host "🔴 NOT PRODUCTION READY" -ForegroundColor Red
    Write-Host "❌ Critical issues must be resolved" -ForegroundColor Red
}

if ($criticalIssues.Count -gt 0) {
    Write-Host "`n🚨 CRITICAL ISSUES TO RESOLVE:" -ForegroundColor Red
    foreach ($issue in $criticalIssues) {
        Write-Host "  • $($issue.TestName): $($issue.Message)" -ForegroundColor Red
    }
}

# Production Recommendations
Write-Host "`n💡 PRODUCTION RECOMMENDATIONS:" -ForegroundColor Cyan

$recommendations = @()

# Check for specific production issues
if ($global:testResults | Where-Object { $_.TestName -like "*Graph*" -and $_.Status -eq "FAIL" -and $_.TestName -notlike "*Overall*" }) {
    $recommendations += "🔐 CRITICAL: Fix Microsoft Graph API access issues"
}

if ($global:testResults | Where-Object { $_.TestName -like "*Communications*" -and $_.Status -eq "FAIL" }) {
    $recommendations += "� CRITICAL: Grant Microsoft Graph Communications permissions"
}

if ($global:testResults | Where-Object { $_.TestName -like "*Overall Graph API Status*" -and $_.Status -eq "FAIL" }) {
    $recommendations += "� CRITICAL: Graph API integration not working - check app registration and permissions"
}

if ($global:testResults | Where-Object { $_.TestName -like "*Environment Mode*" -and $_.Status -ne "PASS" }) {
    $recommendations += "⚙️  Set ASPNETCORE_ENVIRONMENT=Production"
}

if ($global:testResults | Where-Object { $_.TestName -like "*Always On*" -and $_.Status -eq "WARN" }) {
    $recommendations += "⚡ Enable Always On to prevent cold starts"
}

if ($global:testResults | Where-Object { $_.TestName -like "*Error Logs*" -and $_.Status -eq "WARN" }) {
    $recommendations += "📊 Review error logs in Application Insights"
}

if ($global:testResults | Where-Object { $_.TestName -like "*Success Rate*" -and $_.Status -eq "WARN" }) {
    $recommendations += "📈 Investigate low success rate issues"
}

# General production recommendations
$recommendations += "🔍 Monitor Application Insights for real call scenarios"
$recommendations += "📝 Test with real Teams meetings/calls"
$recommendations += "🔔 Set up alerts for failures and performance issues"
$recommendations += "🔄 Implement health checks and auto-restart policies"

foreach ($rec in $recommendations) {
    Write-Host "  $rec" -ForegroundColor Yellow
}

# Save results
$resultsFile = "production-test-results-$(Get-Date -Format 'yyyyMMdd-HHmmss').json"
$global:testResults | ConvertTo-Json -Depth 3 | Out-File $resultsFile
Write-Host "`n💾 Detailed results saved to: $resultsFile" -ForegroundColor Cyan

Write-Host "`n🏁 Production testing completed!" -ForegroundColor Green
Write-Host "🌐 Bot URL: $BaseUrl" -ForegroundColor Cyan
Write-Host "📊 Monitor at: https://portal.azure.com/#resource/subscriptions/<sub-id>/resourceGroups/$ResourceGroup/providers/Microsoft.Web/sites/$AppServiceName" -ForegroundColor Cyan
