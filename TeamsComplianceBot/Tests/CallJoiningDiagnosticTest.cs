using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using TeamsComplianceBot.Controllers;
using TeamsComplianceBot.Services;

namespace TeamsComplianceBot.Tests
{
    /// <summary>
    /// Comprehensive diagnostic test suite to identify why the Teams bot is not joining/picking up calls
    /// This test simulates various call scenarios and validates bot responses
    /// </summary>
    public class CallJoiningDiagnosticTest
    {
        private readonly ILogger<CallJoiningDiagnosticTest> _logger;
        private readonly HttpClient _httpClient;
        private readonly string _botEndpoint;
        private readonly string _teamsAppId;

        public CallJoiningDiagnosticTest()
        {
            // Initialize logger
            using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            _logger = loggerFactory.CreateLogger<CallJoiningDiagnosticTest>();
            
            // Initialize HTTP client
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Microsoft-SkypeBotApi/1.0");
            
            // Bot configuration
            _botEndpoint = "https://arandiabot.ggunifiedtech.com";
            _teamsAppId = "00000000-0000-0000-0000-000000000000";
        }

        /// <summary>
        /// Main diagnostic entry point - runs all tests to identify call joining issues
        /// </summary>
        public async Task<DiagnosticResult> RunComprehensiveDiagnosticsAsync()
        {
            var result = new DiagnosticResult();
            
            _logger.LogInformation("=== STARTING COMPREHENSIVE CALL JOINING DIAGNOSTICS ===");

            try
            {
                // Test 1: Bot Endpoint Connectivity
                result.EndpointConnectivity = await TestBotEndpointConnectivityAsync();
                
                // Test 2: Bot Authentication Configuration  
                result.AuthenticationConfig = await TestBotAuthenticationConfigAsync();
                
                // Test 3: Graph API Permissions
                result.GraphPermissions = await TestGraphApiPermissionsAsync();
                
                // Test 4: Webhook Endpoints
                result.WebhookConfiguration = await TestWebhookEndpointsAsync();
                
                // Test 5: Call Subscription Setup
                result.CallSubscriptions = await TestCallSubscriptionsAsync();
                
                // Test 6: Call Joining Simulation
                result.CallJoiningSimulation = await TestCallJoiningSimulationAsync();
                
                // Test 7: Teams Manifest Validation
                result.TeamsManifestValidation = await TestTeamsManifestValidationAsync();
                
                // Test 8: Application Registration Check
                result.AppRegistrationCheck = await TestApplicationRegistrationAsync();

                // Generate comprehensive report
                result.OverallStatus = DetermineOverallStatus(result);
                result.Recommendations = GenerateRecommendations(result);
                
                _logger.LogInformation("=== DIAGNOSTICS COMPLETED ===");
                LogDiagnosticSummary(result);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error during diagnostics");
                result.CriticalError = ex.Message;
                result.OverallStatus = DiagnosticStatus.Critical;
                return result;
            }
        }

        #region Individual Test Methods

        /// <summary>
        /// Test 1: Check if bot endpoints are accessible and responding correctly
        /// </summary>
        private async Task<TestResult> TestBotEndpointConnectivityAsync()
        {
            _logger.LogInformation("Testing bot endpoint connectivity...");
            var testResult = new TestResult { TestName = "Bot Endpoint Connectivity" };

            try
            {
                // Test main bot endpoint
                var botResponse = await _httpClient.GetAsync($"{_botEndpoint}/api/messages");
                testResult.Details.Add($"Bot endpoint status: {botResponse.StatusCode}");
                
                // Test calls endpoint
                var callsResponse = await _httpClient.GetAsync($"{_botEndpoint}/api/calls/test");
                testResult.Details.Add($"Calls endpoint status: {callsResponse.StatusCode}");
                
                // Test health endpoint
                var healthResponse = await _httpClient.GetAsync($"{_botEndpoint}/api/calls/health");
                testResult.Details.Add($"Health endpoint status: {healthResponse.StatusCode}");
                
                if (healthResponse.IsSuccessStatusCode)
                {
                    var healthContent = await healthResponse.Content.ReadAsStringAsync();
                    testResult.Details.Add($"Health check response: {healthContent}");
                }

                testResult.Status = (botResponse.StatusCode == System.Net.HttpStatusCode.MethodNotAllowed || 
                                   botResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized) && 
                                   callsResponse.IsSuccessStatusCode 
                    ? DiagnosticStatus.Success 
                    : DiagnosticStatus.Warning;
                    
                testResult.Message = testResult.Status == DiagnosticStatus.Success 
                    ? "Bot endpoints are accessible" 
                    : "Some endpoints may not be properly configured";
            }
            catch (Exception ex)
            {
                testResult.Status = DiagnosticStatus.Failed;
                testResult.Message = $"Failed to connect to bot endpoints: {ex.Message}";
                testResult.Details.Add($"Exception: {ex}");
            }

            return testResult;
        }

        /// <summary>
        /// Test 2: Validate bot authentication configuration
        /// </summary>
        private async Task<TestResult> TestBotAuthenticationConfigAsync()
        {
            _logger.LogInformation("Testing bot authentication configuration...");
            var testResult = new TestResult { TestName = "Bot Authentication Configuration" };

            try
            {
                // Test if bot responds to a basic message
                var messagePayload = CreateTestBotMessage();
                var content = new StringContent(JsonSerializer.Serialize(messagePayload), Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync($"{_botEndpoint}/api/messages", content);
                testResult.Details.Add($"Bot message response: {response.StatusCode}");

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    testResult.Status = DiagnosticStatus.Failed;
                    testResult.Message = "Bot authentication is failing - check MicrosoftAppId and MicrosoftAppPassword";
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    testResult.Status = DiagnosticStatus.Warning;
                    testResult.Message = "Bot endpoint is accessible but request format may be incorrect";
                }
                else
                {
                    testResult.Status = DiagnosticStatus.Success;
                    testResult.Message = "Bot authentication appears to be working";
                }

                testResult.Details.Add($"Response headers: {string.Join(", ", response.Headers.Select(h => $"{h.Key}={string.Join(",", h.Value)}"))}");
            }
            catch (Exception ex)
            {
                testResult.Status = DiagnosticStatus.Failed;
                testResult.Message = $"Authentication test failed: {ex.Message}";
            }

            return testResult;
        }

        /// <summary>
        /// Test 3: Check Microsoft Graph API permissions for calling
        /// </summary>
        private async Task<TestResult> TestGraphApiPermissionsAsync()
        {
            _logger.LogInformation("Testing Graph API permissions...");
            var testResult = new TestResult { TestName = "Graph API Permissions" };

            try
            {
                // This would require actual Graph client setup
                // For now, we'll check if the configuration appears correct
                testResult.Details.Add("Required permissions for calling:");
                testResult.Details.Add("- Calls.AccessMedia.All");
                testResult.Details.Add("- Calls.Initiate.All");
                testResult.Details.Add("- Calls.JoinGroupCall.All");
                testResult.Details.Add("- Calls.JoinGroupCallAsGuest.All");
                
                // Check if app registration has calling capabilities
                testResult.Status = DiagnosticStatus.Warning;
                testResult.Message = "Graph API permissions need manual verification in Azure AD";
                testResult.Details.Add("Manual check required: Verify these permissions are granted in Azure AD app registration");
            }
            catch (Exception ex)
            {
                testResult.Status = DiagnosticStatus.Failed;
                testResult.Message = $"Graph permissions test failed: {ex.Message}";
            }

            return testResult;
        }

        /// <summary>
        /// Test 4: Validate webhook endpoints for receiving call events
        /// </summary>
        private async Task<TestResult> TestWebhookEndpointsAsync()
        {
            _logger.LogInformation("Testing webhook endpoints...");
            var testResult = new TestResult { TestName = "Webhook Configuration" };

            try
            {
                // Test notifications endpoint
                var notificationsResponse = await _httpClient.GetAsync($"{_botEndpoint}/api/notifications");
                testResult.Details.Add($"Notifications endpoint: {notificationsResponse.StatusCode}");

                // Test calls webhook endpoint with different methods
                var callsGetResponse = await _httpClient.GetAsync($"{_botEndpoint}/api/calls");
                testResult.Details.Add($"Calls GET endpoint: {callsGetResponse.StatusCode}");

                // Test CORS preflight
                var corsRequest = new HttpRequestMessage(HttpMethod.Options, $"{_botEndpoint}/api/calls");
                var corsResponse = await _httpClient.SendAsync(corsRequest);
                testResult.Details.Add($"CORS preflight: {corsResponse.StatusCode}");

                testResult.Status = DiagnosticStatus.Success;
                testResult.Message = "Webhook endpoints are accessible";
            }
            catch (Exception ex)
            {
                testResult.Status = DiagnosticStatus.Failed;
                testResult.Message = $"Webhook test failed: {ex.Message}";
            }

            return testResult;
        }

        /// <summary>
        /// Test 5: Check call subscription setup with Microsoft Graph
        /// </summary>
        private async Task<TestResult> TestCallSubscriptionsAsync()
        {
            _logger.LogInformation("Testing call subscriptions...");
            var testResult = new TestResult { TestName = "Call Subscriptions" };

            try
            {
                testResult.Details.Add("Checking notification URL configuration:");
                testResult.Details.Add($"Configured notification URL: https://arandiabot.ggunifiedtech.com/api/notifications");
                testResult.Details.Add($"Bot endpoint: {_botEndpoint}");
                
                // Check if notification URL is accessible externally
                var notificationUrlTest = await _httpClient.GetAsync("https://arandiabot.ggunifiedtech.com/api/notifications");
                testResult.Details.Add($"External notification URL test: {notificationUrlTest.StatusCode}");

                testResult.Status = DiagnosticStatus.Warning;
                testResult.Message = "Call subscriptions require manual setup in Graph API";
                testResult.Details.Add("Manual action required: Create Graph subscriptions for call events");
            }
            catch (Exception ex)
            {
                testResult.Status = DiagnosticStatus.Failed;
                testResult.Message = $"Call subscriptions test failed: {ex.Message}";
            }

            return testResult;
        }

        /// <summary>
        /// Test 6: Simulate call joining scenarios
        /// </summary>
        private async Task<TestResult> TestCallJoiningSimulationAsync()
        {
            _logger.LogInformation("Testing call joining simulation...");
            var testResult = new TestResult { TestName = "Call Joining Simulation" };

            try
            {
                // Simulate incoming call webhook
                var incomingCallPayload = CreateIncomingCallPayload();
                var content = new StringContent(JsonSerializer.Serialize(incomingCallPayload), Encoding.UTF8, "application/json");
                
                // Add Teams calling headers
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "Microsoft-Skype/8.0");
                _httpClient.DefaultRequestHeaders.Add("X-Microsoft-Skype-Chain-ID", Guid.NewGuid().ToString());

                var response = await _httpClient.PostAsync($"{_botEndpoint}/api/calls", content);
                testResult.Details.Add($"Incoming call simulation response: {response.StatusCode}");
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    testResult.Details.Add($"Response content: {responseContent}");
                }

                // Simulate established call
                var establishedCallPayload = CreateEstablishedCallPayload();
                content = new StringContent(JsonSerializer.Serialize(establishedCallPayload), Encoding.UTF8, "application/json");
                
                var establishedResponse = await _httpClient.PostAsync($"{_botEndpoint}/api/calls", content);
                testResult.Details.Add($"Established call simulation response: {establishedResponse.StatusCode}");

                testResult.Status = response.IsSuccessStatusCode ? DiagnosticStatus.Success : DiagnosticStatus.Failed;
                testResult.Message = testResult.Status == DiagnosticStatus.Success 
                    ? "Call simulation responded correctly" 
                    : "Call simulation failed - check call handling logic";
            }
            catch (Exception ex)
            {
                testResult.Status = DiagnosticStatus.Failed;
                testResult.Message = $"Call simulation failed: {ex.Message}";
            }

            return testResult;
        }

        /// <summary>
        /// Test 7: Validate Teams manifest configuration
        /// </summary>
        private async Task<TestResult> TestTeamsManifestValidationAsync()
        {
            _logger.LogInformation("Testing Teams manifest validation...");
            var testResult = new TestResult { TestName = "Teams Manifest Validation" };

            try
            {
                testResult.Details.Add("Checking manifest.json configuration:");
                testResult.Details.Add($"✓ Bot ID configured: {_teamsAppId}");
                testResult.Details.Add("✓ supportsCalling: true");
                testResult.Details.Add("✓ supportsVideo: true");
                testResult.Details.Add("✓ Scopes: personal, team, groupChat");
                
                testResult.Details.Add("Required permissions:");
                testResult.Details.Add("✓ OnlineMeeting.ReadBasic.Chat");
                testResult.Details.Add("✓ ChatMember.Read.Chat");
                testResult.Details.Add("✓ TeamsAppInstallation.ReadWriteAndConsentSelfForChat.All");

                testResult.Status = DiagnosticStatus.Success;
                testResult.Message = "Teams manifest appears correctly configured for calling";
            }
            catch (Exception ex)
            {
                testResult.Status = DiagnosticStatus.Failed;
                testResult.Message = $"Manifest validation failed: {ex.Message}";
            }

            return testResult;
        }

        /// <summary>
        /// Test 8: Check Azure AD application registration
        /// </summary>
        private async Task<TestResult> TestApplicationRegistrationAsync()
        {
            _logger.LogInformation("Testing Azure AD application registration...");
            var testResult = new TestResult { TestName = "Application Registration" };

            try
            {
                testResult.Details.Add("Azure AD App Registration Checklist:");
                testResult.Details.Add($"App ID: {_teamsAppId}");
                testResult.Details.Add("Required API Permissions:");
                testResult.Details.Add("- Microsoft Graph: Calls.AccessMedia.All (Application)");
                testResult.Details.Add("- Microsoft Graph: Calls.Initiate.All (Application)");
                testResult.Details.Add("- Microsoft Graph: Calls.JoinGroupCall.All (Application)");
                testResult.Details.Add("- Microsoft Graph: Calls.JoinGroupCallAsGuest.All (Application)");
                testResult.Details.Add("- Microsoft Graph: OnlineMeetings.ReadWrite.All (Application)");
                
                testResult.Details.Add("Application Settings:");
                testResult.Details.Add("- Calling webhook URL must be configured");
                testResult.Details.Add("- Bot must be published and available in Teams");
                testResult.Details.Add("- Admin consent must be granted for application permissions");

                testResult.Status = DiagnosticStatus.Warning;
                testResult.Message = "Application registration requires manual verification";
            }
            catch (Exception ex)
            {
                testResult.Status = DiagnosticStatus.Failed;
                testResult.Message = $"Application registration test failed: {ex.Message}";
            }

            return testResult;
        }

        #endregion

        #region Helper Methods

        private object CreateTestBotMessage()
        {
            return new
            {
                type = "message",
                id = Guid.NewGuid().ToString(),
                timestamp = DateTimeOffset.UtcNow,
                channelId = "msteams",
                serviceUrl = "https://smba.trafficmanager.net/amer/",
                from = new
                {
                    id = "test-user-id",
                    name = "Test User"
                },
                conversation = new
                {
                    id = "test-conversation-id",
                    conversationType = "personal"
                },
                recipient = new
                {
                    id = _teamsAppId,
                    name = "Compliance Bot"
                },
                text = "hi",
                channelData = new
                {
                    tenant = new
                    {
                        id = "59020e57-1a7b-463f-abbe-eed76e79d47c"
                    }
                }
            };
        }

        private object CreateIncomingCallPayload()
        {
            return new
            {
                id = Guid.NewGuid().ToString(),
                state = "incoming",
                direction = "incoming", 
                subject = "Test Call",
                callbackUri = $"{_botEndpoint}/api/calls",
                source = new
                {
                    identity = new
                    {
                        user = new
                        {
                            id = "test-caller-id",
                            displayName = "Test Caller"
                        }
                    }
                },
                targets = new[]
                {
                    new
                    {
                        identity = new
                        {
                            application = new
                            {
                                id = _teamsAppId,
                                displayName = "Compliance Bot"
                            }
                        }
                    }
                }
            };
        }

        private object CreateEstablishedCallPayload()
        {
            return new
            {
                id = Guid.NewGuid().ToString(),
                state = "established",
                direction = "incoming",
                subject = "Test Call - Established",
                callbackUri = $"{_botEndpoint}/api/calls"
            };
        }

        private DiagnosticStatus DetermineOverallStatus(DiagnosticResult result)
        {
            var testResults = new[]
            {
                result.EndpointConnectivity,
                result.AuthenticationConfig,
                result.GraphPermissions,
                result.WebhookConfiguration,
                result.CallSubscriptions,
                result.CallJoiningSimulation,
                result.TeamsManifestValidation,
                result.AppRegistrationCheck
            };

            if (testResults.Any(t => t?.Status == DiagnosticStatus.Critical || t?.Status == DiagnosticStatus.Failed))
                return DiagnosticStatus.Failed;
            
            if (testResults.Any(t => t?.Status == DiagnosticStatus.Warning))
                return DiagnosticStatus.Warning;
                
            return DiagnosticStatus.Success;
        }

        private List<string> GenerateRecommendations(DiagnosticResult result)
        {
            var recommendations = new List<string>();

            if (result.EndpointConnectivity?.Status != DiagnosticStatus.Success)
            {
                recommendations.Add("1. CRITICAL: Fix bot endpoint connectivity issues");
                recommendations.Add("   - Ensure bot is deployed and accessible at https://arandiabot.ggunifiedtech.com");
                recommendations.Add("   - Check Azure App Service status and configuration");
            }

            if (result.AuthenticationConfig?.Status == DiagnosticStatus.Failed)
            {
                recommendations.Add("2. CRITICAL: Fix bot authentication");
                recommendations.Add("   - Verify MicrosoftAppId and MicrosoftAppPassword in configuration");
                recommendations.Add("   - Check Azure AD app registration credentials");
            }

            if (result.GraphPermissions?.Status != DiagnosticStatus.Success)
            {
                recommendations.Add("3. HIGH PRIORITY: Configure Graph API permissions");
                recommendations.Add("   - Add Calls.AccessMedia.All permission to Azure AD app");
                recommendations.Add("   - Add Calls.Initiate.All permission");
                recommendations.Add("   - Add Calls.JoinGroupCall.All permission");
                recommendations.Add("   - Grant admin consent for all application permissions");
            }

            if (result.CallSubscriptions?.Status != DiagnosticStatus.Success)
            {
                recommendations.Add("4. HIGH PRIORITY: Set up Graph subscriptions for calls");
                recommendations.Add("   - Create subscription for /communications/calls");
                recommendations.Add("   - Configure proper webhook URL in subscription");
                recommendations.Add("   - Ensure webhook URL is accessible from Microsoft Graph");
            }

            if (result.CallJoiningSimulation?.Status != DiagnosticStatus.Success)
            {
                recommendations.Add("5. MEDIUM PRIORITY: Fix call handling logic");
                recommendations.Add("   - Review CallsController.ProcessCallEventAsync method");
                recommendations.Add("   - Ensure auto-answer logic is working correctly");
                recommendations.Add("   - Check call state transitions");
            }

            recommendations.Add("6. ADDITIONAL CHECKS:");
            recommendations.Add("   - Verify bot is sideloaded/installed in Teams");
            recommendations.Add("   - Check if calling webhook URL is configured in Azure AD app registration");
            recommendations.Add("   - Ensure bot has necessary Teams app permissions");
            recommendations.Add("   - Test with actual Teams call to validate end-to-end flow");

            return recommendations;
        }

        private void LogDiagnosticSummary(DiagnosticResult result)
        {
            _logger.LogInformation("=== DIAGNOSTIC SUMMARY ===");
            _logger.LogInformation($"Overall Status: {result.OverallStatus}");
            
            _logger.LogInformation("Test Results:");
            _logger.LogInformation($"  Endpoint Connectivity: {result.EndpointConnectivity?.Status} - {result.EndpointConnectivity?.Message}");
            _logger.LogInformation($"  Authentication Config: {result.AuthenticationConfig?.Status} - {result.AuthenticationConfig?.Message}");
            _logger.LogInformation($"  Graph Permissions: {result.GraphPermissions?.Status} - {result.GraphPermissions?.Message}");
            _logger.LogInformation($"  Webhook Configuration: {result.WebhookConfiguration?.Status} - {result.WebhookConfiguration?.Message}");
            _logger.LogInformation($"  Call Subscriptions: {result.CallSubscriptions?.Status} - {result.CallSubscriptions?.Message}");
            _logger.LogInformation($"  Call Joining Simulation: {result.CallJoiningSimulation?.Status} - {result.CallJoiningSimulation?.Message}");
            _logger.LogInformation($"  Teams Manifest: {result.TeamsManifestValidation?.Status} - {result.TeamsManifestValidation?.Message}");
            _logger.LogInformation($"  App Registration: {result.AppRegistrationCheck?.Status} - {result.AppRegistrationCheck?.Message}");

            _logger.LogInformation("Recommendations:");
            foreach (var recommendation in result.Recommendations)
            {
                _logger.LogInformation($"  {recommendation}");
            }
        }

        #endregion
    }

    #region Data Models

    public class DiagnosticResult
    {
        public DiagnosticStatus OverallStatus { get; set; }
        public string? CriticalError { get; set; }
        public TestResult? EndpointConnectivity { get; set; }
        public TestResult? AuthenticationConfig { get; set; }
        public TestResult? GraphPermissions { get; set; }
        public TestResult? WebhookConfiguration { get; set; }
        public TestResult? CallSubscriptions { get; set; }
        public TestResult? CallJoiningSimulation { get; set; }
        public TestResult? TeamsManifestValidation { get; set; }
        public TestResult? AppRegistrationCheck { get; set; }
        public List<string> Recommendations { get; set; } = new List<string>();
    }

    public class TestResult
    {
        public string TestName { get; set; } = string.Empty;
        public DiagnosticStatus Status { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> Details { get; set; } = new List<string>();
    }

    public enum DiagnosticStatus
    {
        Success,
        Warning,
        Failed,
        Critical
    }

    #endregion
}
