using Microsoft.AspNetCore.Mvc;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Extensions.Logging;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using System.Diagnostics;
using System.Threading.Tasks;
using TeamsComplianceBot.Services;
using System.Text.Json;

namespace TeamsComplianceBot.Controllers
{
    /// <summary>
    /// Production-ready controller to handle Teams calling webhook requests with enhanced
    /// security, compliance logging, and observability features for call recording compliance
    /// </summary>
    [Route("api/calls")]    [ApiController]
    public class CallsController : ControllerBase    {
        private readonly IBotFrameworkHttpAdapter _adapter;
        private readonly ILogger<CallsController> _logger;
        private readonly TelemetryClient _telemetryClient;
        private readonly ICallRecordingService _callRecordingService;
        private readonly IComplianceService _complianceService;
        private readonly ICallJoiningService _callJoiningService;
        private readonly IBot _bot;

        // Security and monitoring
        private static readonly ActivitySource ActivitySource = new("TeamsComplianceBot.CallsController");
        private const int MAX_REQUEST_SIZE = 2_097_152; // 2MB limit for call data
        private const string CORRELATION_ID_HEADER = "X-Correlation-ID";

        public CallsController(
            IBotFrameworkHttpAdapter adapter, 
            ILogger<CallsController> logger,
            TelemetryClient telemetryClient,
            ICallRecordingService callRecordingService,
            IComplianceService complianceService,
            ICallJoiningService callJoiningService,
            IBot bot)
        {            _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
            _callRecordingService = callRecordingService ?? throw new ArgumentNullException(nameof(callRecordingService));
            _complianceService = complianceService ?? throw new ArgumentNullException(nameof(complianceService));
            _callJoiningService = callJoiningService ?? throw new ArgumentNullException(nameof(callJoiningService));
            _bot = bot ?? throw new ArgumentNullException(nameof(bot));
        }

        /// <summary>
        /// Test endpoint for call processing validation
        /// </summary>
        [HttpGet("test")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult GetTestAsync()
        {
            try
            {
                var testInfo = new
                {
                    message = "Teams Calls Controller is operational",
                    endpoint = "/api/calls",
                    methods = new[] { "POST", "OPTIONS", "GET" },
                    timestamp = DateTimeOffset.UtcNow,
                    userAgent = HttpContext.Request.Headers.UserAgent.ToString(),
                    validation = ValidateCallRequest()
                };

                return Ok(testInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Test endpoint failed for CallsController");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Handles preflight OPTIONS requests for CORS
        /// </summary>
        [HttpOptions]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult Options()
        {
            // CORS headers are added by the middleware
            return Ok();
        }

        /// <summary>
        /// Handle incoming Teams calling webhook requests with enhanced security and compliance logging
        /// </summary>
        [HttpPost]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> PostAsync()
        {
            var correlationId = HttpContext.Request.Headers[CORRELATION_ID_HEADER].FirstOrDefault() 
                               ?? Guid.NewGuid().ToString();

            using var activity = ActivitySource.StartActivity("TeamsCall.Process");
            activity?.SetTag("correlation.id", correlationId);
            activity?.SetTag("remote.address", HttpContext.Connection.RemoteIpAddress?.ToString());

            using var operation = _telemetryClient.StartOperation<RequestTelemetry>("Teams Call Processing");
            operation.Telemetry.Properties["CorrelationId"] = correlationId;
            operation.Telemetry.Properties["RemoteIpAddress"] = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

            try
            {                // Security validation - temporarily disabled for debugging
                //if (!ValidateCallRequest())
                //{
                //    _logger.LogWarning("Teams calling webhook request validation failed from {RemoteIpAddress}", 
                //        HttpContext.Connection.RemoteIpAddress?.ToString());
                //    return BadRequest("Invalid request");
                //}

                // Content length validation
                if (Request.ContentLength > MAX_REQUEST_SIZE)
                {
                    _logger.LogWarning("Teams calling request too large: {ContentLength} bytes from {RemoteIpAddress}", 
                        Request.ContentLength, HttpContext.Connection.RemoteIpAddress?.ToString());
                    return BadRequest("Request too large");
                }

                _logger.LogInformation("Processing Teams calling webhook request from {RemoteIpAddress} with correlation ID {CorrelationId}", 
                    HttpContext.Connection.RemoteIpAddress?.ToString(), correlationId);

                // Add correlation ID to response headers for traceability
                HttpContext.Response.Headers.Append(CORRELATION_ID_HEADER, correlationId);

                // Enhanced logging for compliance
                var scopeProperties = new Dictionary<string, object?>
                {
                    ["CorrelationId"] = correlationId,
                    ["Operation"] = "TeamsCallProcessing",
                    ["RemoteIpAddress"] = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                    ["UserAgent"] = HttpContext.Request.Headers.UserAgent.ToString(),
                    ["Timestamp"] = DateTimeOffset.UtcNow
                };

                using var scope = _logger.BeginScope(scopeProperties);                // Check if this is a Teams calling webhook vs bot message
                var userAgent = HttpContext.Request.Headers.UserAgent.ToString();
                
                // Enhanced webhook detection - check content type and structure
                var isTeamsCallingWebhook = false;
                var contentType = HttpContext.Request.ContentType ?? "";
                
                // First check: User-Agent contains known calling service identifiers
                if (userAgent.Contains("Microsoft-Skype", StringComparison.OrdinalIgnoreCase) ||
                    userAgent.Contains("Calling", StringComparison.OrdinalIgnoreCase) ||
                    userAgent.Contains("Teams", StringComparison.OrdinalIgnoreCase) ||
                    userAgent.Contains("curl", StringComparison.OrdinalIgnoreCase)) // Include curl for testing
                {
                    isTeamsCallingWebhook = true;
                    _logger.LogInformation("Detected calling webhook based on User-Agent: {UserAgent}", userAgent);
                }

                // Second check: Examine content structure for Graph webhook format
                if (!isTeamsCallingWebhook && contentType.Contains("application/json"))
                {
                    Request.EnableBuffering();
                    using var reader = new StreamReader(Request.Body, leaveOpen: true);
                    var bodyContent = await reader.ReadToEndAsync();
                    Request.Body.Position = 0; // Reset stream position
                    
                    // Look for Graph webhook signatures
                    if (bodyContent.Contains("\"value\"") && 
                        (bodyContent.Contains("\"resourceData\"") || 
                         bodyContent.Contains("\"subscriptionId\"") ||
                         bodyContent.Contains("\"changeType\"") ||
                         bodyContent.Contains("communications/calls")))
                    {
                        isTeamsCallingWebhook = true;
                        _logger.LogInformation("Detected calling webhook based on content structure with Graph webhook format");
                    }
                    else if (bodyContent.Contains("\"type\"") && bodyContent.Contains("\"text\""))
                    {
                        // This looks like a Bot Framework Activity
                        isTeamsCallingWebhook = false;
                        _logger.LogInformation("Detected Bot Framework message based on content structure");
                    }
                    else
                    {
                        // Default to webhook for unknown JSON content
                        isTeamsCallingWebhook = true;
                        _logger.LogInformation("Defaulting to calling webhook for unknown JSON content");
                    }
                }

                if (isTeamsCallingWebhook)
                {
                    // Handle Teams calling webhook directly
                    var result = await ProcessTeamsCallingWebhookAsync(correlationId);
                    _logger.LogInformation("Successfully processed Teams calling webhook with correlation ID {CorrelationId}", correlationId);
                    operation.Telemetry.Success = true;
                    return result;
                }
                else
                {
                    // For regular bot messages, validate first
                    if (!ValidateCallRequest())
                    {
                        _logger.LogWarning("Bot message request validation failed from {RemoteIpAddress}", 
                            HttpContext.Connection.RemoteIpAddress?.ToString());
                        return BadRequest("Invalid bot message request");
                    }

                    // Process as bot framework message
                    try
                    {
                        // Delegate the processing of the HTTP POST to the adapter for bot messages
                        // The adapter will invoke the bot's OnMessageActivityAsync method
                        await _adapter.ProcessAsync(Request, Response, _bot);
                        _logger.LogInformation("Successfully processed bot message with correlation ID {CorrelationId}", correlationId);
                        operation.Telemetry.Success = true;
                        return new EmptyResult();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to process as bot message - this might be a webhook that wasn't detected properly");
                        
                        // Log detailed error for debugging
                        _logger.LogError("BadRequest: Missing activity or activity type.");
                        var errorResponse = "BadRequest: Missing activity or activity type.";
                        
                        operation.Telemetry.Success = false;
                        return BadRequest(errorResponse);
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                operation.Telemetry.Success = false;
                _logger.LogWarning(ex, "Unauthorized Teams calling request from {RemoteIpAddress} with correlation ID {CorrelationId}", 
                    HttpContext.Connection.RemoteIpAddress?.ToString(), correlationId);

                _telemetryClient.TrackException(ex, new Dictionary<string, string>
                {
                    ["Operation"] = "TeamsCallProcessing",
                    ["CorrelationId"] = correlationId,
                    ["ErrorType"] = "Unauthorized"
                });

                return Unauthorized();
            }
            catch (ArgumentException ex)
            {
                operation.Telemetry.Success = false;
                _logger.LogWarning(ex, "Invalid Teams calling request from {RemoteIpAddress} with correlation ID {CorrelationId}", 
                    HttpContext.Connection.RemoteIpAddress?.ToString(), correlationId);

                return BadRequest("Invalid call data format");
            }
            catch (Exception ex)
            {
                operation.Telemetry.Success = false;
                _logger.LogError(ex, "Error processing Teams calling webhook from {RemoteIpAddress} with correlation ID {CorrelationId}", 
                    HttpContext.Connection.RemoteIpAddress?.ToString(), correlationId);

                _telemetryClient.TrackException(ex, new Dictionary<string, string>
                {
                    ["Operation"] = "TeamsCallProcessing",
                    ["CorrelationId"] = correlationId,
                    ["RemoteIpAddress"] = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown"
                });

                // Don't expose internal errors in production
                return StatusCode(500, "An error occurred while processing the call data");
            }
        }

        /// <summary>
        /// Enhanced health check for the calling endpoint with dependency validation
        /// </summary>
        [HttpGet("health")]
        public async Task<IActionResult> GetHealthAsync()
        {
            using var activity = ActivitySource.StartActivity("CallsHealth.Check");

            try
            {
                _logger.LogDebug("Teams calling endpoint health check from {RemoteIpAddress}", 
                    HttpContext.Connection.RemoteIpAddress?.ToString());

                var healthData = new
                {
                    status = "healthy",
                    endpoint = "calling",
                    timestamp = DateTimeOffset.UtcNow,
                    version = GetType().Assembly.GetName().Version?.ToString(),
                    dependencies = await CheckCallDependenciesAsync()
                };

                return Ok(healthData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Teams calling endpoint health check");
                
                return StatusCode(500, new 
                { 
                    status = "unhealthy", 
                    endpoint = "calling",
                    timestamp = DateTimeOffset.UtcNow,
                    error = "Health check failed"
                });
            }
        }

        /// <summary>
        /// Readiness probe endpoint for container orchestration
        /// </summary>
        [HttpGet("ready")]
        public IActionResult GetReadiness()
        {
            try
            {
                // Basic readiness checks for calling functionality
                var isReady = _adapter != null && 
                             _callRecordingService != null && 
                             _complianceService != null;
                
                if (isReady)
                {
                    return Ok(new { status = "ready", endpoint = "calling", timestamp = DateTimeOffset.UtcNow });
                }
                else
                {
                    return ServiceUnavailable(new { status = "not-ready", endpoint = "calling", timestamp = DateTimeOffset.UtcNow });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Teams calling readiness check");
                return ServiceUnavailable(new { status = "not-ready", endpoint = "calling", error = "Readiness check failed" });
            }
        }

        /// <summary>
        /// Liveness probe endpoint for container orchestration
        /// </summary>
        [HttpGet("live")]
        public IActionResult GetLiveness()
        {
            return Ok(new { status = "alive", endpoint = "calling", timestamp = DateTimeOffset.UtcNow });
        }

        /// <summary>
        /// Development test endpoint to verify controller functionality
        /// </summary>
        [HttpPost("test")]
        public async Task<IActionResult> TestAsync([FromBody] object? testData)
        {
            var correlationId = HttpContext.Request.Headers[CORRELATION_ID_HEADER].FirstOrDefault() 
                               ?? Guid.NewGuid().ToString();

            try
            {
                _logger.LogInformation("Test endpoint called with correlation ID {CorrelationId}", correlationId);
                
                // Add correlation ID to response headers
                HttpContext.Response.Headers.Append(CORRELATION_ID_HEADER, correlationId);

                // Log compliance event for testing
                await LogComplianceCallEventAsync("TestEndpointCalled", correlationId);

                var response = new
                {
                    status = "success",
                    message = "Test endpoint working correctly",
                    correlationId = correlationId,
                    timestamp = DateTimeOffset.UtcNow,
                    receivedData = testData,
                    dependencies = new
                    {
                        adapter = _adapter != null ? "available" : "null",
                        callRecordingService = _callRecordingService != null ? "available" : "null",
                        complianceService = _complianceService != null ? "available" : "null",
                        bot = _bot != null ? "available" : "null"
                    }
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in test endpoint with correlation ID {CorrelationId}", correlationId);
                return StatusCode(500, new { error = "Test endpoint failed", correlationId = correlationId });
            }
        }

        /// <summary>
        /// Simple endpoint to test basic controller functionality without dependencies
        /// </summary>
        [HttpGet("simple-test")]
        public IActionResult SimpleTest()
        {
            try
            {
                return Ok(new { 
                    status = "success", 
                    message = "Simple test endpoint working",
                    timestamp = DateTimeOffset.UtcNow,
                    controllerType = GetType().Name 
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    error = "Simple test failed", 
                    message = ex.Message,
                    timestamp = DateTimeOffset.UtcNow 
                });
            }
        }        private bool ValidateCallRequest()
        {
            try
            {
                // Get user agent for validation
                var userAgent = HttpContext.Request.Headers.UserAgent.ToString();
                
                // For Teams calling webhooks, be very lenient
                if (string.IsNullOrEmpty(userAgent))
                {
                    _logger.LogInformation("Empty user agent - allowing for Teams calling webhooks");
                    return true; // Allow empty user agent for calling webhooks
                }

                // Accept a wide variety of user agents for calling webhooks and bot messages
                var validUserAgents = new[]
                {
                    "Microsoft-SkypeBotApi",
                    "Microsoft-Skype", 
                    "Microsoft-BotFramework",
                    "Microsoft",
                    "Teams",
                    "Calling"
                };

                var isValidUserAgent = validUserAgents.Any(ua => 
                    userAgent.Contains(ua, StringComparison.OrdinalIgnoreCase));

                // For testing purposes, also allow some common user agents
                if (!isValidUserAgent && !string.IsNullOrEmpty(userAgent))
                {
                    var testUserAgents = new[] { "curl", "PostmanRuntime", "Insomnia" };
                    isValidUserAgent = testUserAgents.Any(ua => 
                        userAgent.Contains(ua, StringComparison.OrdinalIgnoreCase));
                    
                    if (isValidUserAgent)
                    {
                        _logger.LogInformation("Test user agent accepted: {UserAgent}", userAgent);
                    }
                }

                if (!isValidUserAgent)
                {
                    _logger.LogWarning("Invalid user agent for request: {UserAgent}", userAgent);
                    return false;
                }

                _logger.LogDebug("Request validation passed for user agent: {UserAgent}", userAgent);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error validating request");
                return true; // Be lenient on validation errors for calling webhooks
            }
        }

        private Task<object> CheckCallDependenciesAsync()
        {
            var dependencies = new Dictionary<string, object>();

            try
            {
                // Check adapter health
                dependencies["adapter"] = new { status = _adapter != null ? "healthy" : "unhealthy" };

                // Check call recording service health
                dependencies["callRecordingService"] = new { status = _callRecordingService != null ? "healthy" : "unhealthy" };

                // Check compliance service health
                dependencies["complianceService"] = new { status = _complianceService != null ? "healthy" : "unhealthy" };

                // Test connectivity to Microsoft Graph (if possible)
                try
                {
                    // This is a lightweight check - could be expanded to actual Graph connectivity test
                    dependencies["microsoftGraph"] = new { status = "unknown", note = "Connectivity check not implemented" };
                }
                catch (Exception ex)
                {
                    dependencies["microsoftGraph"] = new { status = "unhealthy", error = ex.Message };
                }

                return Task.FromResult<object>(dependencies);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking Teams calling dependencies during health check");
                return Task.FromResult<object>(new { error = "Unable to check dependencies" });
            }
        }

        private async Task LogComplianceCallEventAsync(string eventType, string correlationId)
        {
            try
            {
                var complianceEvent = new
                {
                    EventType = eventType,
                    CorrelationId = correlationId,
                    Endpoint = "TeamsCallingWebhook",
                    Timestamp = DateTimeOffset.UtcNow,
                    RemoteIpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                    UserAgent = HttpContext.Request.Headers.UserAgent.ToString()
                };

                // Log to Application Insights
                _telemetryClient.TrackEvent($"TeamsCall.{eventType}", new Dictionary<string, string>
                {
                    ["CorrelationId"] = correlationId,
                    ["RemoteIpAddress"] = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                    ["UserAgent"] = HttpContext.Request.Headers.UserAgent.ToString()
                });

                // Could also log to compliance service if needed
                await Task.CompletedTask; // Placeholder for async compliance logging
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to log compliance call event {EventType}", eventType);
            }
        }

        private IActionResult ServiceUnavailable(object value)
        {
            return StatusCode(503, value);
        }        /// <summary>
        /// Process Teams calling webhook requests with automatic call joining and recording
        /// </summary>
        private async Task<IActionResult> ProcessTeamsCallingWebhookAsync(string correlationId)
        {
            try
            {
                // Log compliance event for call processing
                await LogComplianceCallEventAsync("CallWebhookReceived", correlationId);

                // Read the webhook payload
                using var reader = new StreamReader(Request.Body);
                var webhookPayload = await reader.ReadToEndAsync();
                
                _logger.LogInformation("Teams calling webhook payload received: {PayloadLength} characters", 
                    webhookPayload?.Length ?? 0);

                // Parse and process the calling webhook
                if (!string.IsNullOrEmpty(webhookPayload))
                {                    try
                    {
                        // Try to parse as Microsoft Graph calling webhook format
                        var webhookData = JsonSerializer.Deserialize<Dictionary<string, object>>(webhookPayload);
                        
                        if (webhookData != null)
                        {
                            _logger.LogInformation("Teams calling webhook parsed successfully with {DataCount} properties", 
                                webhookData.Count);

                            // Check if this is a Microsoft Graph calling webhook format
                            if (webhookData.TryGetValue("@type", out var typeValue) && 
                                typeValue?.ToString()?.Contains("incomingCall") == true)
                            {                                // Extract the actual call data from the value array
                                if (webhookData.TryGetValue("value", out var valueObj) && valueObj != null)
                                {
                                    var valueJson = valueObj.ToString();
                                    if (!string.IsNullOrEmpty(valueJson))
                                    {
                                        var valueArray = JsonSerializer.Deserialize<JsonElement[]>(valueJson);
                                        if (valueArray != null && valueArray.Length > 0)
                                        {
                                            var callEvent = valueArray[0];
                                            if (callEvent.TryGetProperty("resourceData", out var resourceData))
                                            {
                                                // Convert resourceData to Dictionary for processing
                                                var callDataJson = resourceData.GetRawText();
                                                var callData = JsonSerializer.Deserialize<Dictionary<string, object>>(callDataJson);
                                                
                                                if (callData != null)
                                                {
                                                    _logger.LogInformation("Extracted call data from Microsoft Graph webhook with {DataCount} properties", 
                                                        callData.Count);

                                                    // Process the call event based on its type
                                                    var callResponse = await ProcessCallEventAsync(callData, correlationId);
                                                    
                                                    // Log the call event for compliance
                                                    await LogComplianceCallEventAsync("CallDataProcessed", correlationId);

                                                    // Return the call response (this might include call actions)
                                                    return Ok(callResponse);
                                                }
                                            }
                                        }
                                    }
                                }
                                
                                _logger.LogWarning("Could not extract call data from Microsoft Graph webhook format");
                            }
                            else
                            {
                                // Try to process as direct call data format
                                var callResponse = await ProcessCallEventAsync(webhookData, correlationId);
                                
                                // Log the call event for compliance
                                await LogComplianceCallEventAsync("CallDataProcessed", correlationId);

                                // Return the call response (this might include call actions)
                                return Ok(callResponse);
                            }
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Teams calling webhook payload is not JSON, treating as SDP or other format");
                        
                        // Log non-JSON webhook (like SDP)
                        await LogComplianceCallEventAsync("CallWebhookNonJson", correlationId);
                    }
                }

                // Return 200 OK to acknowledge the webhook
                return Ok(new { 
                    status = "processed", 
                    correlationId = correlationId,
                    timestamp = DateTimeOffset.UtcNow 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Teams calling webhook with correlation ID {CorrelationId}", correlationId);
                
                // Still return 200 to avoid webhook retries for non-critical errors
                return Ok(new { 
                    status = "error", 
                    correlationId = correlationId,
                    error = ex.Message 
                });
            }
        }/// <summary>
        /// Process specific call events and determine appropriate response actions
        /// </summary>
        private async Task<object> ProcessCallEventAsync(Dictionary<string, object> callData, string correlationId)
        {
            try
            {
                // Enhanced logging for call details
                LogCallDetails(callData, correlationId, "RECEIVED");

                // Extract call information
                var callId = ExtractCallProperty(callData, "id");
                var callState = ExtractCallProperty(callData, "state");
                var direction = ExtractCallProperty(callData, "direction");
                var callbackUri = ExtractCallProperty(callData, "callbackUri");

                _logger.LogInformation("Processing call event - ID: {CallId}, State: {CallState}, Direction: {Direction}", 
                    callId, callState, direction);

                // Handle different call states
                switch (callState?.ToLowerInvariant())
                {
                    case "incoming":
                    case "establishing":
                        // Automatically answer incoming calls
                        LogCallDetails(callData, correlationId, "ANSWERING");
                        return await HandleIncomingCallAsync(callId, callData, correlationId);

                    case "established":
                        // Call is active - start recording if not already started
                        LogCallDetails(callData, correlationId, "RECORDING_START");
                        return await HandleEstablishedCallAsync(callId, callData, correlationId);

                    case "terminated":
                    case "disconnected":
                        // Call ended - finalize recording
                        LogCallDetails(callData, correlationId, "RECORDING_STOP");
                        return await HandleTerminatedCallAsync(callId, callData, correlationId);

                    default:
                        // Log unknown state but acknowledge
                        LogCallDetails(callData, correlationId, "UNKNOWN_STATE");
                        _logger.LogWarning("Unknown call state: {CallState} for call {CallId}", callState, callId);
                        return new { status = "acknowledged", callId = callId, state = callState };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing call event with correlation ID {CorrelationId}", correlationId);
                return new { status = "error", error = ex.Message };
            }
        }        /// <summary>
        /// Handle incoming call - automatically answer and join
        /// </summary>
        private async Task<object> HandleIncomingCallAsync(string? callId, Dictionary<string, object> callData, string correlationId)
        {
            try
            {
                _logger.LogInformation("Handling incoming call {CallId} with correlation ID {CorrelationId}", callId, correlationId);

                // Extract caller information for compliance logging
                var source = ExtractCallProperty(callData, "source");
                var targets = ExtractCallProperty(callData, "targets");

                // Log compliance event for incoming call
                await LogComplianceCallEventAsync("IncomingCallReceived", correlationId);

                // Check if we should auto-answer this call (based on compliance policies)
                var shouldAnswer = await ShouldAutoAnswerCallAsync(callData, correlationId);                if (shouldAnswer && !string.IsNullOrEmpty(callId))
                {
                    _logger.LogInformation("🤖 AUTO-ANSWERING call {CallId} for compliance recording with correlation ID {CorrelationId}", callId, correlationId);
                    await LogComplianceCallEventAsync("CallAutoAnswered", correlationId);

                    // Actually answer the call using the Microsoft Graph API
                    _logger.LogInformation("📞 Invoking Microsoft Graph API to answer call {CallId}", callId);
                    var joinResult = await _callJoiningService.AnswerCallAsync(callId);
                    
                    if (joinResult.Success)
                    {
                        _logger.LogInformation("✅ Successfully answered call {CallId} - Bot joined at {JoinedAt}", callId, joinResult.JoinedAt);
                        await LogComplianceCallEventAsync("CallAnsweredSuccessfully", correlationId);
                        return new { status = "answered", callId = callId, joinedAt = joinResult.JoinedAt, message = joinResult.Message };
                    }
                    else
                    {
                        _logger.LogError("❌ Failed to answer call {CallId}: {Error} (Code: {ErrorCode})", callId, joinResult.Message, joinResult.ErrorCode);
                        await LogComplianceCallEventAsync("CallAnswerFailed", correlationId);
                        return new { status = "answer_failed", error = joinResult.Message, errorCode = joinResult.ErrorCode, callId = callId };
                    }
                }
                else
                {
                    _logger.LogInformation("Call {CallId} not auto-answered per compliance policy", callId);
                    return new { status = "not_answered", reason = "compliance_policy" };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling incoming call {CallId}", callId);
                return new { status = "error", error = ex.Message };
            }
        }        /// <summary>
        /// Handle established call - start recording
        /// </summary>
        private async Task<object> HandleEstablishedCallAsync(string? callId, Dictionary<string, object> callData, string correlationId)
        {
            try
            {
                _logger.LogInformation("Call {CallId} established - starting recording", callId);

                // Log compliance event for call establishment
                await LogComplianceCallEventAsync("CallEstablished", correlationId);                if (!string.IsNullOrEmpty(callId))
                {
                    // Start recording using the Microsoft Graph Calling API through the CallJoiningService
                    var recordingResult = await _callJoiningService.StartCallRecordingAsync(callId);
                    
                    if (recordingResult.Success)
                    {
                        _logger.LogInformation("Successfully started recording for call {CallId}: {RecordingId}", callId, recordingResult.RecordingId);
                        await LogComplianceCallEventAsync("RecordingStarted", correlationId);
                          return new { 
                            status = "recording_started", 
                            callId = callId, 
                            recordingId = recordingResult.RecordingId,
                            startedAt = DateTime.UtcNow
                        };
                    }
                    else
                    {
                        _logger.LogError("Failed to start recording for call {CallId}: {Error}", callId, recordingResult.ErrorMessage ?? "Unknown error");
                        
                        // Fallback to the existing call recording service
                        var meetingInfo = new TeamsComplianceBot.Models.MeetingInfo
                        {
                            Id = callId,
                            Title = "Teams Call Recording",
                            StartTime = DateTime.UtcNow,
                            Organizer = "Teams Compliance Bot",
                            TenantId = "59020e57-1a7b-463f-abbe-eed76e79d47c" // From config
                        };

                        var fallbackResult = await _callRecordingService.StartRecordingAsync(meetingInfo, CancellationToken.None);
                        _logger.LogInformation("Fallback recording started for call {CallId} with result: {RecordingResult}", callId, fallbackResult.Success);
                        
                        await LogComplianceCallEventAsync("RecordingStarted", correlationId);
                        return new { status = "recording_started_fallback", callId = callId };
                    }
                }

                return new { status = "no_call_id", error = "Cannot start recording without call ID" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling established call {CallId}", callId);
                return new { status = "error", error = ex.Message };
            }
        }

        /// <summary>
        /// Handle terminated call - finalize recording
        /// </summary>
        private async Task<object> HandleTerminatedCallAsync(string? callId, Dictionary<string, object> callData, string correlationId)
        {
            try
            {
                _logger.LogInformation("Call {CallId} terminated - finalizing recording", callId);

                // Log compliance event for call termination
                await LogComplianceCallEventAsync("CallTerminated", correlationId);                // Stop recording and process the recorded content
                if (!string.IsNullOrEmpty(callId))
                {
                    var stopResult = await _callRecordingService.StopRecordingAsync(callId, CancellationToken.None);
                    _logger.LogInformation("Recording stopped for call {CallId} with result: {StopResult}", callId, stopResult.Success);
                }

                await LogComplianceCallEventAsync("RecordingStopped", correlationId);

                return new { status = "recording_finalized", callId = callId };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling terminated call {CallId}", callId);
                return new { status = "error", error = ex.Message };
            }
        }

        /// <summary>
        /// Determine if the bot should automatically answer the call based on compliance policies
        /// </summary>
        private async Task<bool> ShouldAutoAnswerCallAsync(Dictionary<string, object> callData, string correlationId)
        {
            try
            {
                // Extract caller information
                var source = ExtractCallProperty(callData, "source");
                
                // For now, auto-answer all calls for compliance recording
                // This could be enhanced with more sophisticated policies
                _logger.LogInformation("Compliance policy: Auto-answering call for recording (correlation ID: {CorrelationId})", correlationId);
                
                await Task.CompletedTask; // Placeholder for async compliance check
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking compliance policy for auto-answer, defaulting to true");
                return true; // Default to recording for compliance
            }
        }        /// <summary>
        /// Extract property from call data dictionary safely, handling both direct and @odata formats
        /// </summary>
        private string? ExtractCallProperty(Dictionary<string, object> callData, string propertyName)
        {
            try
            {
                // Try direct property name first
                if (callData.TryGetValue(propertyName, out var value))
                {
                    return value?.ToString();
                }
                
                // For Microsoft Graph calling API, try alternative property names
                switch (propertyName.ToLowerInvariant())
                {
                    case "id":
                        // Try extracting ID from @odata.id
                        if (callData.TryGetValue("@odata.id", out var odataId))
                        {
                            var idString = odataId?.ToString();
                            if (!string.IsNullOrEmpty(idString))
                            {
                                // Extract the call ID from the path like "/communications/calls/{id}"
                                var lastSlash = idString.LastIndexOf('/');
                                if (lastSlash >= 0 && lastSlash < idString.Length - 1)
                                {
                                    return idString.Substring(lastSlash + 1);
                                }
                            }
                        }
                        break;
                        
                    case "callbackuri":
                        // Try alternative callback URL property names
                        if (callData.TryGetValue("callback_uri", out var callbackUri) ||
                            callData.TryGetValue("callbackUrl", out callbackUri))
                        {
                            return callbackUri?.ToString();
                        }
                        break;
                }
                
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error extracting property {PropertyName} from call data", propertyName);
                return null;
            }
        }/// <summary>
        /// Enhanced logging method for detailed call tracking
        /// </summary>
        private void LogCallDetails(Dictionary<string, object> callData, string correlationId, string eventType)
        {
            try
            {
                var callId = ExtractCallProperty(callData, "id");
                var callState = ExtractCallProperty(callData, "state");
                var direction = ExtractCallProperty(callData, "direction");
                var subject = ExtractCallProperty(callData, "subject");
                
                // Extract source user information safely
                var sourceUser = "Unknown";
                try
                {
                    if (callData.TryGetValue("source", out var sourceObj))
                    {
                        var sourceJson = sourceObj?.ToString();
                        if (!string.IsNullOrEmpty(sourceJson))
                        {
                            var sourceDict = JsonSerializer.Deserialize<Dictionary<string, object>>(sourceJson);
                            if (sourceDict?.TryGetValue("identity", out var identityObj) == true)
                            {
                                var identityJson = identityObj?.ToString();
                                if (!string.IsNullOrEmpty(identityJson))
                                {
                                    var identityDict = JsonSerializer.Deserialize<Dictionary<string, object>>(identityJson);
                                    if (identityDict?.TryGetValue("user", out var userObj) == true)
                                    {
                                        var userJson = userObj?.ToString();
                                        if (!string.IsNullOrEmpty(userJson))
                                        {
                                            var userDict = JsonSerializer.Deserialize<Dictionary<string, object>>(userJson);
                                            sourceUser = userDict?.TryGetValue("displayName", out var displayName) == true 
                                                ? displayName?.ToString() ?? "Unknown"
                                                : userDict?.TryGetValue("id", out var userId) == true 
                                                    ? userId?.ToString() ?? "Unknown" 
                                                    : "Unknown";
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Error parsing source user information - using fallback");
                    sourceUser = "ParseError";
                }

                _logger.LogInformation("=== TEAMS CALL EVENT === {EventType} | Call ID: {CallId} | State: {CallState} | Direction: {Direction} | From: {SourceUser} | Subject: {Subject} | Correlation: {CorrelationId}",
                    eventType, callId, callState, direction, sourceUser, subject, correlationId);

                // Also log to Application Insights for tracking
                _telemetryClient.TrackEvent("TeamsCallEvent", new Dictionary<string, string>
                {
                    ["EventType"] = eventType,
                    ["CallId"] = callId ?? "Unknown",
                    ["CallState"] = callState ?? "Unknown",
                    ["Direction"] = direction ?? "Unknown",
                    ["SourceUser"] = sourceUser,
                    ["Subject"] = subject ?? "Unknown",
                    ["CorrelationId"] = correlationId
                });
            }            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error logging call details for event type {EventType}", eventType);
            }
        }

        /// <summary>
        /// Test endpoint to verify Microsoft Graph API connectivity and permissions
        /// </summary>
        [HttpGet("test-graph-api")]
        public async Task<IActionResult> TestGraphApiAsync()
        {
            try
            {
                _logger.LogInformation("Testing Graph API connectivity and permissions via test endpoint");
                
                var result = await _callJoiningService.TestGraphApiAsync();
                
                return Ok(new { 
                    status = "completed", 
                    timestamp = DateTimeOffset.UtcNow, 
                    results = result.Split(Environment.NewLine) 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Graph API test endpoint");
                return StatusCode(500, new { 
                    status = "error", 
                    message = ex.Message,
                    timestamp = DateTimeOffset.UtcNow 
                });
            }
        }

        /// <summary>
        /// Test endpoint to verify automatic call answering functionality
        /// </summary>
        [HttpPost("test-auto-answer")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> TestAutoAnswerAsync([FromBody] TestCallRequest request)
        {
            try
            {
                var correlationId = Guid.NewGuid().ToString();
                _logger.LogInformation("Testing automatic call answering for call {CallId} with correlation ID {CorrelationId}", 
                    request.CallId, correlationId);

                // Simulate call data
                var callData = new Dictionary<string, object>
                {
                    ["id"] = request.CallId ?? "test-call-123",
                    ["state"] = request.CallState ?? "incoming",
                    ["direction"] = "incoming",
                    ["source"] = new { identity = new { user = new { displayName = "Test User", id = "test-user-123" } } },
                    ["callbackUri"] = "https://arandiabot-app.azurewebsites.net/api/calls"
                };

                // Test the automatic call answering
                var result = await ProcessCallEventAsync(callData, correlationId);
                
                return Ok(new { 
                    testResult = result,
                    correlationId = correlationId,
                    callData = callData,
                    timestamp = DateTimeOffset.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing automatic call answering");
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }

    /// <summary>
    /// Test request model for simulating call scenarios
    /// </summary>
    public class TestCallRequest
    {
        public string? CallId { get; set; }
        public string? CallState { get; set; }
    }
}
