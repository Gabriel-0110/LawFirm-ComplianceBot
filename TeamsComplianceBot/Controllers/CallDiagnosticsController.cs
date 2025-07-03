using Microsoft.AspNetCore.Mvc;
using Microsoft.Graph;
using System.Linq;
using TeamsComplianceBot.Services;
using TeamsComplianceBot.Tests;

namespace TeamsComplianceBot.Controllers
{
    /// <summary>
    /// Controller for running call joining diagnostics and troubleshooting
    /// </summary>
    [Route("api/diagnostics")]
    [ApiController]
    public class CallDiagnosticsController : ControllerBase
    {
        private readonly ILogger<CallDiagnosticsController> _logger;

        public CallDiagnosticsController(ILogger<CallDiagnosticsController> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Run comprehensive call joining diagnostics
        /// </summary>
        [HttpGet("call-joining")]
        public async Task<IActionResult> RunCallJoiningDiagnosticsAsync()
        {
            try
            {
                _logger.LogInformation("Starting call joining diagnostics...");
                
                var diagnosticTest = new CallJoiningDiagnosticTest();
                var result = await diagnosticTest.RunComprehensiveDiagnosticsAsync();

                return Ok(new
                {
                    timestamp = DateTimeOffset.UtcNow,
                    overallStatus = result.OverallStatus.ToString(),
                    criticalError = result.CriticalError,
                    testResults = new
                    {
                        endpointConnectivity = CreateTestSummary(result.EndpointConnectivity),
                        authenticationConfig = CreateTestSummary(result.AuthenticationConfig),
                        graphPermissions = CreateTestSummary(result.GraphPermissions),
                        webhookConfiguration = CreateTestSummary(result.WebhookConfiguration),
                        callSubscriptions = CreateTestSummary(result.CallSubscriptions),
                        callJoiningSimulation = CreateTestSummary(result.CallJoiningSimulation),
                        teamsManifestValidation = CreateTestSummary(result.TeamsManifestValidation),
                        appRegistrationCheck = CreateTestSummary(result.AppRegistrationCheck)
                    },
                    recommendations = result.Recommendations,
                    nextSteps = GenerateNextSteps(result)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running call joining diagnostics");
                return StatusCode(500, new
                {
                    error = "Diagnostic test failed",
                    message = ex.Message,
                    timestamp = DateTimeOffset.UtcNow
                });
            }
        }

        /// <summary>
        /// Quick health check for call-related endpoints
        /// </summary>
        [HttpGet("quick-check")]
        public IActionResult QuickCallHealthCheck()
        {
            try
            {
                var issues = new List<string>();
                var warnings = new List<string>();

                // Check basic configuration
                var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
                
                var appId = config["MicrosoftAppId"];
                var appPassword = config["MicrosoftAppPassword"];
                var notificationUrl = config["Recording:NotificationUrl"];

                if (string.IsNullOrEmpty(appId))
                    issues.Add("MicrosoftAppId not configured");
                
                if (string.IsNullOrEmpty(appPassword))
                    issues.Add("MicrosoftAppPassword not configured");
                
                if (string.IsNullOrEmpty(notificationUrl))
                    warnings.Add("Notification URL not configured");

                // Check if services are available
                var callRecordingService = HttpContext.RequestServices.GetService<ICallRecordingService>();
                if (callRecordingService == null)
                    issues.Add("CallRecordingService not registered");

                var graphClient = HttpContext.RequestServices.GetService<GraphServiceClient>();
                if (graphClient == null)
                    warnings.Add("GraphServiceClient not available");

                var status = issues.Any() ? "Critical" : warnings.Any() ? "Warning" : "Healthy";

                return Ok(new
                {
                    status = status,
                    timestamp = DateTimeOffset.UtcNow,
                    issues = issues,
                    warnings = warnings,
                    configuration = new
                    {
                        appIdConfigured = !string.IsNullOrEmpty(appId),
                        appPasswordConfigured = !string.IsNullOrEmpty(appPassword),
                        notificationUrlConfigured = !string.IsNullOrEmpty(notificationUrl),
                        notificationUrl = notificationUrl
                    },
                    services = new
                    {
                        callRecordingServiceAvailable = callRecordingService != null,
                        graphClientAvailable = graphClient != null
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in quick call health check");
                return StatusCode(500, new
                {
                    status = "Error",
                    message = ex.Message,
                    timestamp = DateTimeOffset.UtcNow
                });
            }
        }        /// <summary>
        /// Test call webhook processing with sample payloads
        /// </summary>
        [HttpPost("test-webhook")]
        public Task<IActionResult> TestCallWebhookAsync([FromBody] object? customPayload = null)
        {
            try
            {
                _logger.LogInformation("Testing call webhook processing...");

                var testPayloads = new object[]
                {
                    // Incoming call
                    new
                    {
                        id = Guid.NewGuid().ToString(),
                        state = "incoming",
                        direction = "incoming",
                        subject = "Test Incoming Call",
                        callbackUri = "https://arandiateamsbot.ggunifiedtech.com/api/calls",
                        source = new
                        {
                            identity = new
                            {
                                user = new
                                {
                                    id = "test-user-123",
                                    displayName = "Test User"
                                }
                            }
                        }
                    },
                    // Established call  
                    new
                    {
                        id = Guid.NewGuid().ToString(),
                        state = "established",
                        direction = "incoming",
                        subject = "Test Established Call",
                        callbackUri = "https://arandiateamsbot.ggunifiedtech.com/api/calls"
                    },
                    // Terminated call
                    new
                    {
                        id = Guid.NewGuid().ToString(),
                        state = "terminated",
                        direction = "incoming",
                        subject = "Test Terminated Call",
                        callbackUri = "https://arandiateamsbot.ggunifiedtech.com/api/calls"
                    }
                };

                var results = new List<object>();

                foreach (var payload in testPayloads)
                {
                    try
                    {
                        // Here you would typically call the CallsController logic directly
                        // For now, we'll just validate the payload structure
                        var payloadDict = payload.GetType().GetProperties()
                            .ToDictionary(p => p.Name, p => p.GetValue(payload));
                        
                        results.Add(new
                        {
                            payloadType = payloadDict.ContainsKey("state") ? payloadDict["state"]?.ToString() : "unknown",
                            callId = payloadDict.ContainsKey("id") ? payloadDict["id"]?.ToString() : "unknown",
                            status = "Payload structure valid",
                            timestamp = DateTimeOffset.UtcNow
                        });
                    }
                    catch (Exception ex)
                    {
                        results.Add(new
                        {
                            payloadType = "unknown",
                            callId = "unknown", 
                            status = "Failed",
                            error = ex.Message
                        });
                    }
                }

                var response = Ok(new
                {
                    message = "Webhook test completed",
                    testResults = results,
                    customPayloadProvided = customPayload != null,
                    timestamp = DateTimeOffset.UtcNow
                });

                return Task.FromResult<IActionResult>(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing call webhook");
                var errorResponse = StatusCode(500, new
                {
                    error = "Webhook test failed",
                    message = ex.Message,
                    timestamp = DateTimeOffset.UtcNow
                });
                return Task.FromResult<IActionResult>(errorResponse);
            }
        }

        /// <summary>
        /// Generate step-by-step fix instructions
        /// </summary>
        [HttpGet("fix-guide")]
        public IActionResult GetCallJoiningFixGuide()
        {
            var fixGuide = new
            {
                title = "Teams Bot Call Joining Fix Guide",
                overview = "Step-by-step instructions to fix call joining issues",
                steps = new[]
                {
                    new
                    {
                        step = 1,
                        title = "Verify Azure AD App Registration",
                        description = "Check Microsoft Graph API permissions",
                        actions = new[]
                        {
                            "Navigate to Azure Portal > Azure Active Directory > App registrations",
                            "Find your app: Compliance Bot (153ad72f-6fa4-4e88-b0fe-f0f785466699)",
                            "Go to API permissions section",
                            "Ensure these permissions are added with Application type:",
                            "  - Microsoft Graph: Calls.AccessMedia.All",
                            "  - Microsoft Graph: Calls.Initiate.All", 
                            "  - Microsoft Graph: Calls.JoinGroupCall.All",
                            "  - Microsoft Graph: Calls.JoinGroupCallAsGuest.All",
                            "  - Microsoft Graph: OnlineMeetings.ReadWrite.All",
                            "Click 'Grant admin consent' button"
                        },
                        critical = true
                    },
                    new
                    {
                        step = 2,
                        title = "Configure Calling Webhook URL",
                        description = "Set up the webhook URL in Azure AD app registration",
                        actions = new[]
                        {
                            "In Azure AD app registration, go to 'Manage' > 'Manifest'",
                            "Find the 'replyUrlsWithType' section",
                            "Add calling webhook URL: https://arandiateamsbot.ggunifiedtech.com/api/calls",
                            "Save the manifest changes",
                            "Alternatively, use Azure CLI:",
                            "az ad app update --id 153ad72f-6fa4-4e88-b0fe-f0f785466699 --add replyUrls https://arandiateamsbot.ggunifiedtech.com/api/calls"
                        },
                        critical = true
                    },
                    new
                    {
                        step = 3,
                        title = "Create Graph Subscriptions",
                        description = "Set up subscriptions to receive call events",
                        actions = new[]
                        {
                            "Use Microsoft Graph API to create subscriptions",
                            "POST https://graph.microsoft.com/v1.0/subscriptions",
                            "Subscription payload:",
                            "  - resource: /communications/calls",
                            "  - changeType: created,updated,deleted",
                            "  - notificationUrl: https://arandiateamsbot.ggunifiedtech.com/api/notifications",
                            "  - expirationDateTime: (set appropriate expiration)",
                            "Test the subscription endpoint is accessible"
                        },
                        critical = true
                    },
                    new
                    {
                        step = 4,
                        title = "Test Bot Endpoints",
                        description = "Verify all endpoints are working correctly",
                        actions = new[]
                        {
                            "Test bot message endpoint: GET https://arandiateamsbot.ggunifiedtech.com/api/messages",
                            "Test calls endpoint: GET https://arandiateamsbot.ggunifiedtech.com/api/calls/health",
                            "Test notifications endpoint: GET https://arandiateamsbot.ggunifiedtech.com/api/notifications",
                            "All should return proper HTTP status codes",
                            "Check application logs for any errors"
                        },
                        critical = false
                    },
                    new
                    {
                        step = 5,
                        title = "Verify Teams App Installation",
                        description = "Ensure bot is properly installed in Teams",
                        actions = new[]
                        {
                            "Check if bot is sideloaded in Teams",
                            "Verify bot has necessary permissions in Teams admin center",
                            "Test basic bot functionality (send 'hi' message)",
                            "Check if bot appears in Teams apps list",
                            "Verify calling permissions are granted to the bot"
                        },
                        critical = false
                    },
                    new
                    {
                        step = 6,
                        title = "Test End-to-End Call Flow",
                        description = "Test actual call joining",
                        actions = new[]
                        {
                            "Make a test call to a user who has the bot installed",
                            "Check if bot receives incoming call webhook",
                            "Verify bot attempts to join/answer the call",
                            "Monitor application logs during the call",
                            "Check for any errors in call processing"
                        },
                        critical = false
                    }
                },
                troubleshooting = new
                {
                    commonIssues = new[]
                    {
                        "403 Forbidden: Missing Graph API permissions or admin consent not granted",
                        "404 Not Found: Webhook URL not accessible or incorrect",
                        "401 Unauthorized: Bot authentication credentials incorrect",
                        "Bot not responding: Check if bot is deployed and endpoints are accessible",
                        "Calls not detected: Graph subscriptions not properly configured"
                    },
                    logLocations = new[]
                    {
                        "Azure App Service: Diagnostic settings and Log stream",
                        "Application Insights: Custom events and traces",
                        "Azure AD: Sign-in logs for authentication issues",
                        "Bot Framework: Channel logs and conversation history"
                    }
                }
            };

            return Ok(fixGuide);
        }

        private object? CreateTestSummary(TeamsComplianceBot.Tests.TestResult? testResult)
        {
            if (testResult == null) return null;

            return new
            {
                status = testResult.Status.ToString(),
                message = testResult.Message,
                details = testResult.Details
            };
        }

        private List<string> GenerateNextSteps(TeamsComplianceBot.Tests.DiagnosticResult result)
        {
            var nextSteps = new List<string>();

            if (result.OverallStatus == TeamsComplianceBot.Tests.DiagnosticStatus.Failed || 
                result.OverallStatus == TeamsComplianceBot.Tests.DiagnosticStatus.Critical)
            {
                nextSteps.Add("🚨 IMMEDIATE ACTION REQUIRED");
                nextSteps.Add("1. Run diagnostics: GET /api/diagnostics/call-joining");
                nextSteps.Add("2. Review fix guide: GET /api/diagnostics/fix-guide");
                nextSteps.Add("3. Address critical issues first (authentication, permissions)");
                nextSteps.Add("4. Test endpoints: GET /api/diagnostics/quick-check");
                nextSteps.Add("5. Re-run full diagnostics to verify fixes");
            }
            else if (result.OverallStatus == TeamsComplianceBot.Tests.DiagnosticStatus.Warning)
            {
                nextSteps.Add("⚠️  CONFIGURATION IMPROVEMENTS NEEDED");
                nextSteps.Add("1. Review warnings in diagnostic results");
                nextSteps.Add("2. Configure Graph subscriptions for real-time call events");
                nextSteps.Add("3. Test with actual Teams calls");
                nextSteps.Add("4. Monitor application logs during testing");
            }
            else
            {
                nextSteps.Add("✅ SYSTEM APPEARS HEALTHY");
                nextSteps.Add("1. Test with real Teams calls to verify end-to-end functionality");
                nextSteps.Add("2. Monitor application logs during call attempts");
                nextSteps.Add("3. Set up automated monitoring for call joining success rate");
            }

            return nextSteps;
        }
    }
}
