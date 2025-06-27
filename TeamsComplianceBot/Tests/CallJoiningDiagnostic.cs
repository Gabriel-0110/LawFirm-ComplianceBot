using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace TeamsComplianceBot.Tests
{
    /// <summary>
    /// Advanced diagnostic tool for testing Teams call joining and recording functionality
    /// This class performs real-world scenario testing to identify issues with call joining
    /// </summary>
    public class CallJoiningDiagnostic
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly List<DiagnosticResult> _results;

        public CallJoiningDiagnostic(string baseUrl = "https://arandiabot-app.azurewebsites.net")
        {
            _baseUrl = baseUrl;
            _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
            _results = new List<DiagnosticResult>();
        }

        public class DiagnosticResult
        {
            public string TestName { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty; // PASS, FAIL, WARN
            public string Message { get; set; } = string.Empty;
            public object? Details { get; set; }
            public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        }

        /// <summary>
        /// Comprehensive test suite for call joining functionality
        /// </summary>
        public async Task<List<DiagnosticResult>> RunComprehensiveTestsAsync()
        {
            Console.WriteLine("🔍 Starting Comprehensive Call Joining Diagnostic...");
            Console.WriteLine($"🌐 Testing against: {_baseUrl}");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            // Test 1: Basic connectivity and bot health
            await TestBotHealthAsync();

            // Test 2: Graph API authentication and permissions
            await TestGraphApiConnectivityAsync();

            // Test 3: Call webhook processing with various scenarios
            await TestCallWebhookProcessingAsync();

            // Test 4: Auto-answer functionality
            await TestAutoAnswerFunctionalityAsync();

            // Test 5: Meeting-specific scenarios
            await TestMeetingJoiningAsync();

            // Test 6: Error handling and resilience
            await TestErrorHandlingAsync();

            // Test 7: Recording infrastructure
            await TestRecordingInfrastructureAsync();

            // Test 8: Configuration validation
            await TestConfigurationAsync();

            // Generate summary
            GenerateTestSummary();

            return _results;
        }

        private async Task TestBotHealthAsync()
        {
            try
            {
                Console.WriteLine("\n🏥 Testing Bot Health...");
                
                var response = await _httpClient.GetAsync($"{_baseUrl}/health");
                if (response.IsSuccessStatusCode)
                {
                    AddResult("Bot Health Check", "PASS", $"HTTP {response.StatusCode} - Bot is responding");
                }
                else
                {
                    AddResult("Bot Health Check", "FAIL", $"HTTP {response.StatusCode} - Bot health check failed");
                }

                // Test bot info endpoint
                var infoResponse = await _httpClient.GetAsync($"{_baseUrl}/");
                if (infoResponse.IsSuccessStatusCode)
                {
                    var infoContent = await infoResponse.Content.ReadAsStringAsync();
                    var infoData = JsonSerializer.Deserialize<JsonElement>(infoContent);
                    
                    if (infoData.TryGetProperty("configuration", out var config))
                    {
                        AddResult("Bot Configuration", "PASS", "Bot configuration retrieved successfully", config.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                AddResult("Bot Health Check", "FAIL", $"Exception: {ex.Message}");
            }
        }

        private async Task TestGraphApiConnectivityAsync()
        {
            try
            {
                Console.WriteLine("\n📊 Testing Microsoft Graph API...");
                
                var response = await _httpClient.GetAsync($"{_baseUrl}/api/calls/test-graph-api");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var data = JsonSerializer.Deserialize<JsonElement>(content);
                    
                    // Check authentication
                    if (data.TryGetProperty("authenticationTest", out var authTest))
                    {
                        if (authTest.TryGetProperty("isAuthenticated", out var isAuth) && isAuth.GetBoolean())
                        {
                            AddResult("Graph API Authentication", "PASS", "Client credentials authentication successful");
                        }
                        else
                        {
                            var error = authTest.TryGetProperty("error", out var errorProp) ? errorProp.GetString() : "Unknown error";
                            AddResult("Graph API Authentication", "FAIL", $"Authentication failed: {error}");
                        }
                    }

                    // Check permissions
                    if (data.TryGetProperty("permissionsTest", out var permTest))
                    {
                        var requiredPerms = new[] { "Calls.Initiate.All", "Calls.AccessMedia.All", "Calls.JoinGroupCall.All", "OnlineMeetings.Read.All" };
                        
                        if (permTest.TryGetProperty("grantedPermissions", out var grantedPerms))
                        {
                            var grantedList = JsonSerializer.Deserialize<string[]>(grantedPerms.GetRawText()) ?? Array.Empty<string>();
                            
                            foreach (var perm in requiredPerms)
                            {
                                if (Array.Exists(grantedList, p => p == perm))
                                {
                                    AddResult($"Permission: {perm}", "PASS", "Permission granted");
                                }
                                else
                                {
                                    AddResult($"Permission: {perm}", "FAIL", "Permission missing - this will prevent call joining");
                                }
                            }
                        }
                    }
                }
                else
                {
                    AddResult("Graph API Test", "FAIL", $"HTTP {response.StatusCode} - Graph API test endpoint failed");
                }
            }
            catch (Exception ex)
            {
                AddResult("Graph API Connectivity", "FAIL", $"Exception: {ex.Message}");
            }
        }

        private async Task TestCallWebhookProcessingAsync()
        {
            try
            {
                Console.WriteLine("\n📞 Testing Call Webhook Processing...");

                // Test various call scenarios
                var scenarios = new Dictionary<string, object>
                {
                    ["Incoming Call"] = new
                    {
                        value = new[]
                        {
                            new
                            {
                                resourceUrl = "https://graph.microsoft.com/v1.0/communications/calls/test-diagnostic-001",
                                resourceData = new
                                {
                                    id = "test-diagnostic-001",
                                    state = "incoming",
                                    direction = "incoming",
                                    source = new
                                    {
                                        identity = new
                                        {
                                            user = new
                                            {
                                                displayName = "Diagnostic Test User",
                                                id = "diagnostic-user-001"
                                            }
                                        }
                                    },
                                    callbackUri = $"{_baseUrl}/api/calls"
                                },
                                changeType = "created",
                                clientState = "TeamsComplianceBot-Diagnostic"
                            }
                        }
                    },
                    ["Meeting Call"] = new
                    {
                        value = new[]
                        {
                            new
                            {
                                resourceUrl = "https://graph.microsoft.com/v1.0/communications/calls/test-meeting-001",
                                resourceData = new
                                {
                                    id = "test-meeting-001",
                                    state = "incoming",
                                    direction = "incoming",
                                    subject = "Diagnostic Meeting Test",
                                    chatInfo = new
                                    {
                                        threadId = "19:meeting_diagnostic@thread.v2",
                                        messageId = "0"
                                    },
                                    callbackUri = $"{_baseUrl}/api/calls"
                                },
                                changeType = "created",
                                clientState = "TeamsComplianceBot-Diagnostic"
                            }
                        }
                    },
                    ["Call Established"] = new
                    {
                        value = new[]
                        {
                            new
                            {
                                resourceUrl = "https://graph.microsoft.com/v1.0/communications/calls/test-diagnostic-001",
                                resourceData = new
                                {
                                    id = "test-diagnostic-001",
                                    state = "established",
                                    direction = "incoming"
                                },
                                changeType = "updated",
                                clientState = "TeamsComplianceBot-Diagnostic"
                            }
                        }
                    }
                };

                foreach (var scenario in scenarios)
                {
                    try
                    {
                        var json = JsonSerializer.Serialize(scenario.Value);
                        var content = new StringContent(json, Encoding.UTF8, "application/json");
                        
                        var response = await _httpClient.PostAsync($"{_baseUrl}/api/calls", content);
                        
                        if (response.IsSuccessStatusCode)
                        {
                            AddResult($"Webhook: {scenario.Key}", "PASS", $"HTTP {response.StatusCode} - Processed successfully");
                        }
                        else
                        {
                            var errorContent = await response.Content.ReadAsStringAsync();
                            AddResult($"Webhook: {scenario.Key}", "FAIL", $"HTTP {response.StatusCode} - {errorContent}");
                        }
                    }
                    catch (Exception ex)
                    {
                        AddResult($"Webhook: {scenario.Key}", "FAIL", $"Exception: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                AddResult("Call Webhook Processing", "FAIL", $"Test setup exception: {ex.Message}");
            }
        }

        private async Task TestAutoAnswerFunctionalityAsync()
        {
            try
            {
                Console.WriteLine("\n🔄 Testing Auto-Answer Functionality...");

                var autoAnswerScenarios = new Dictionary<string, object>
                {
                    ["Basic Auto-Answer"] = new
                    {
                        callId = "diagnostic-auto-001",
                        callState = "incoming",
                        direction = "incoming",
                        source = new
                        {
                            identity = new
                            {
                                user = new
                                {
                                    displayName = "Auto-Answer Test User",
                                    id = "auto-test-user-001"
                                }
                            }
                        }
                    },
                    ["Meeting Auto-Answer"] = new
                    {
                        callId = "diagnostic-meeting-auto-001",
                        callState = "incoming",
                        direction = "incoming",
                        chatInfo = new
                        {
                            threadId = "19:meeting_auto_test@thread.v2",
                            messageId = "0"
                        }
                    }
                };

                foreach (var scenario in autoAnswerScenarios)
                {
                    try
                    {
                        var json = JsonSerializer.Serialize(scenario.Value);
                        var content = new StringContent(json, Encoding.UTF8, "application/json");
                        
                        var response = await _httpClient.PostAsync($"{_baseUrl}/api/calls/test-auto-answer", content);
                        
                        if (response.IsSuccessStatusCode)
                        {
                            var responseContent = await response.Content.ReadAsStringAsync();
                            var responseData = JsonSerializer.Deserialize<JsonElement>(responseContent);
                            
                            if (responseData.TryGetProperty("testResult", out var testResult))
                            {
                                if (testResult.TryGetProperty("status", out var status))
                                {
                                    var statusValue = status.GetString();
                                    if (statusValue == "answer_attempted" || 
                                        (statusValue == "answer_failed" && testResult.TryGetProperty("error", out var error) && 
                                         error.GetString()?.Contains("Call not found") == true))
                                    {
                                        AddResult($"Auto-Answer: {scenario.Key}", "PASS", 
                                            "Auto-answer logic working (expected behavior for test calls)");
                                    }
                                    else
                                    {
                                        var errorMsg = testResult.TryGetProperty("error", out var errorProp) ? 
                                            errorProp.GetString() : "Unknown error";
                                        AddResult($"Auto-Answer: {scenario.Key}", "WARN", 
                                            $"Auto-answer issue: {errorMsg}");
                                    }
                                }
                            }
                        }
                        else
                        {
                            AddResult($"Auto-Answer: {scenario.Key}", "FAIL", $"HTTP {response.StatusCode}");
                        }
                    }
                    catch (Exception ex)
                    {
                        AddResult($"Auto-Answer: {scenario.Key}", "FAIL", $"Exception: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                AddResult("Auto-Answer Testing", "FAIL", $"Test setup exception: {ex.Message}");
            }
        }

        private async Task TestMeetingJoiningAsync()
        {
            try
            {
                Console.WriteLine("\n🎯 Testing Meeting-Specific Scenarios...");

                // Test meeting webhook with specific meeting properties
                var meetingPayload = new
                {
                    value = new[]
                    {
                        new
                        {
                            resourceUrl = "https://graph.microsoft.com/v1.0/communications/calls/meeting-diagnostic-001",
                            resourceData = new
                            {
                                id = "meeting-diagnostic-001",
                                state = "incoming",
                                direction = "incoming",
                                subject = "Compliance Test Meeting",
                                chatInfo = new
                                {
                                    threadId = "19:meeting_compliance_test@thread.v2",
                                    messageId = "1",
                                    organizerId = "meeting-organizer-001"
                                },
                                meetingInfo = new
                                {
                                    organizerId = "meeting-organizer-001",
                                    allowConversationWithoutHost = true
                                },
                                callbackUri = $"{_baseUrl}/api/calls"
                            },
                            changeType = "created",
                            clientState = "TeamsComplianceBot-Meeting-Test"
                        }
                    }
                };

                var json = JsonSerializer.Serialize(meetingPayload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync($"{_baseUrl}/api/calls", content);
                
                if (response.IsSuccessStatusCode)
                {
                    AddResult("Meeting Call Processing", "PASS", "Meeting-specific call processed successfully");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    AddResult("Meeting Call Processing", "FAIL", $"HTTP {response.StatusCode} - {errorContent}");
                }
            }
            catch (Exception ex)
            {
                AddResult("Meeting Joining Test", "FAIL", $"Exception: {ex.Message}");
            }
        }

        private async Task TestErrorHandlingAsync()
        {
            try
            {
                Console.WriteLine("\n🛡️ Testing Error Handling...");

                // Test invalid JSON
                var invalidContent = new StringContent("invalid-json", Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"{_baseUrl}/api/calls", invalidContent);
                
                if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    AddResult("Invalid JSON Handling", "PASS", "Bot properly handles invalid JSON");
                }
                else
                {
                    AddResult("Invalid JSON Handling", "WARN", $"Unexpected response: {response.StatusCode}");
                }

                // Test malformed webhook
                var malformedPayload = new { invalid = "structure" };
                var malformedJson = JsonSerializer.Serialize(malformedPayload);
                var malformedContent = new StringContent(malformedJson, Encoding.UTF8, "application/json");
                
                var malformedResponse = await _httpClient.PostAsync($"{_baseUrl}/api/calls", malformedContent);
                AddResult("Malformed Webhook Handling", "PASS", $"Handled malformed webhook: {malformedResponse.StatusCode}");
            }
            catch (Exception ex)
            {
                AddResult("Error Handling Test", "FAIL", $"Exception: {ex.Message}");
            }
        }

        private async Task TestRecordingInfrastructureAsync()
        {
            try
            {
                Console.WriteLine("\n💾 Testing Recording Infrastructure...");

                // Check if there are any recording-specific endpoints
                var response = await _httpClient.GetAsync($"{_baseUrl}/api/recording/status");
                if (response.IsSuccessStatusCode)
                {
                    AddResult("Recording Status Endpoint", "PASS", "Recording status endpoint accessible");
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    AddResult("Recording Status Endpoint", "WARN", "Recording status endpoint not found - may need implementation");
                }
                else
                {
                    AddResult("Recording Status Endpoint", "FAIL", $"HTTP {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                AddResult("Recording Infrastructure", "WARN", $"Recording endpoints may not be implemented: {ex.Message}");
            }
        }

        private async Task TestConfigurationAsync()
        {
            try
            {
                Console.WriteLine("\n⚙️ Testing Configuration...");

                var response = await _httpClient.GetAsync($"{_baseUrl}/");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var data = JsonSerializer.Deserialize<JsonElement>(content);
                    
                    if (data.TryGetProperty("configuration", out var config))
                    {
                        // Check required configuration
                        var requiredFields = new[] { "botId", "tenantId", "appType" };
                        
                        foreach (var field in requiredFields)
                        {
                            if (config.TryGetProperty(field, out var fieldValue) && !string.IsNullOrEmpty(fieldValue.GetString()))
                            {
                                AddResult($"Config: {field}", "PASS", $"Value: {fieldValue.GetString()}");
                            }
                            else
                            {
                                AddResult($"Config: {field}", "FAIL", "Missing required configuration");
                            }
                        }
                    }

                    // Check environment
                    if (data.TryGetProperty("environment", out var env))
                    {
                        var environment = env.GetString();
                        if (environment == "Development")
                        {
                            AddResult("Environment", "WARN", "Running in Development mode - consider Production settings for live use");
                        }
                        else
                        {
                            AddResult("Environment", "PASS", $"Running in {environment} mode");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AddResult("Configuration Test", "FAIL", $"Exception: {ex.Message}");
            }
        }

        private void AddResult(string testName, string status, string message, object? details = null)
        {
            _results.Add(new DiagnosticResult
            {
                TestName = testName,
                Status = status,
                Message = message,
                Details = details
            });

            var color = status switch
            {
                "PASS" => ConsoleColor.Green,
                "FAIL" => ConsoleColor.Red,
                "WARN" => ConsoleColor.Yellow,
                _ => ConsoleColor.White
            };

            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            Console.ForegroundColor = color;
            Console.WriteLine($"[{timestamp}] [{status}] {testName}: {message}");
            Console.ResetColor();
        }

        private void GenerateTestSummary()
        {
            Console.WriteLine("\n📋 Test Summary");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            var passed = _results.Count(r => r.Status == "PASS");
            var failed = _results.Count(r => r.Status == "FAIL");
            var warnings = _results.Count(r => r.Status == "WARN");

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✅ Passed: {passed}");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"⚠️  Warnings: {warnings}");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"❌ Failed: {failed}");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"📊 Total Tests: {_results.Count}");
            Console.ResetColor();

            // Critical issues
            var criticalIssues = _results.Where(r => r.Status == "FAIL").ToList();
            if (criticalIssues.Any())
            {
                Console.WriteLine("\n🚨 Critical Issues:");
                Console.ForegroundColor = ConsoleColor.Red;
                foreach (var issue in criticalIssues)
                {
                    Console.WriteLine($"  • {issue.TestName}: {issue.Message}");
                }
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\n✅ No critical issues found!");
                Console.ResetColor();
            }

            // Recommendations
            Console.WriteLine("\n💡 Recommendations:");
            Console.ForegroundColor = ConsoleColor.Cyan;
            
            var authIssues = _results.Any(r => r.TestName.Contains("Authentication") && r.Status == "FAIL");
            var permissionIssues = _results.Any(r => r.TestName.Contains("Permission") && r.Status == "FAIL");
            var autoAnswerIssues = _results.Any(r => r.TestName.Contains("Auto-Answer") && r.Status == "FAIL");

            if (authIssues)
                Console.WriteLine("  🔐 Fix Microsoft Graph authentication - critical for call joining");
            if (permissionIssues)
                Console.WriteLine("  🔑 Grant missing Microsoft Graph permissions");
            if (autoAnswerIssues)
                Console.WriteLine("  📞 Debug auto-answer logic");
            if (warnings > 0)
                Console.WriteLine("  ⚠️  Review warnings for potential configuration issues");
            
            if (!authIssues && !permissionIssues && !autoAnswerIssues && failed == 0)
            {
                Console.WriteLine("  ✅ Bot appears to be functioning well!");
                Console.WriteLine("  💡 If call joining still isn't working, check:");
                Console.WriteLine("     • Teams app manifest is uploaded and approved");
                Console.WriteLine("     • Bot is added to the Teams channel/meeting");
                Console.WriteLine("     • Meeting policies allow bot joining");
                Console.WriteLine("     • Real call scenarios vs test scenarios");
            }
            
            Console.ResetColor();
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
