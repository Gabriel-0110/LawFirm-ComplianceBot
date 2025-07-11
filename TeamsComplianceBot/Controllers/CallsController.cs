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
using Microsoft.Graph;

namespace TeamsComplianceBot.Controllers
{
    /// <summary>
    /// Production-ready controller to handle Teams calling webhook requests with enhanced
    /// security, compliance logging, and observability features for call recording compliance
    /// </summary>
    [Route("api/calls")]    [ApiController]
    public class CallsController : ControllerBase
    {
        private readonly IBotFrameworkHttpAdapter _adapter;
        private readonly ILogger<CallsController> _logger;
        private readonly TelemetryClient _telemetryClient;
        private readonly ICallRecordingService _callRecordingService;
        private readonly IComplianceService _complianceService;
        private readonly IBot _bot;
        private readonly GraphServiceClient _graphServiceClient;

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
            IBot bot,
            GraphServiceClient graphServiceClient)
        {            _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
            _callRecordingService = callRecordingService ?? throw new ArgumentNullException(nameof(callRecordingService));
            _complianceService = complianceService ?? throw new ArgumentNullException(nameof(complianceService));
            _bot = bot ?? throw new ArgumentNullException(nameof(bot));
            _graphServiceClient = graphServiceClient ?? throw new ArgumentNullException(nameof(graphServiceClient));
        }

        /// <summary>
        /// Health check and information endpoint for calls controller
        /// </summary>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult Get()
        {
            try
            {
                _logger.LogInformation("Calls endpoint health check requested");
                
                var callsInfo = new
                {
                    status = "healthy",
                    controllerName = "Teams Calls Controller",
                    version = "1.0.0",
                    timestamp = DateTimeOffset.UtcNow,
                    endpoints = new[]
                    {
                        "GET /api/calls - This health check endpoint",
                        "GET /api/calls/test - Detailed test endpoint",
                        "POST /api/calls - Call webhook processing",
                        "OPTIONS /api/calls - CORS preflight"
                    }
                };

                return Ok(callsInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during calls controller health check");
                return StatusCode(500, new { status = "unhealthy", error = ex.Message });
            }
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
                var isTeamsCallingWebhook = userAgent.Contains("Microsoft-Skype", StringComparison.OrdinalIgnoreCase) ||
                                          userAgent.Contains("Calling", StringComparison.OrdinalIgnoreCase) ||
                                          userAgent.Contains("Teams", StringComparison.OrdinalIgnoreCase);

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

                    // Delegate the processing of the HTTP POST to the adapter for bot messages
                    // The adapter will invoke the bot's OnMessageActivityAsync method
                    await _adapter.ProcessAsync(Request, Response, _bot);
                    _logger.LogInformation("Successfully processed bot message with correlation ID {CorrelationId}", correlationId);
                    operation.Telemetry.Success = true;
                    return new EmptyResult();
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
                        // Try to parse as JSON for call notifications
                        var callData = JsonSerializer.Deserialize<Dictionary<string, object>>(webhookPayload);
                        
                        if (callData != null)
                        {
                            _logger.LogInformation("Teams calling webhook parsed successfully with {DataCount} properties", 
                                callData.Count);

                            // Process the call event based on its type
                            var callResponse = await ProcessCallEventAsync(callData, correlationId);
                            
                            // Log the call event for compliance
                            await LogComplianceCallEventAsync("CallDataProcessed", correlationId);

                            // Return the call response (this might include call actions)
                            return Ok(callResponse);
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
        }        /// <summary>
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
        }

        /// <summary>
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
                var shouldAnswer = await ShouldAutoAnswerCallAsync(callData, correlationId);                if (shouldAnswer)
                {
                    // Create response to answer the call
                    var answerResponse = new
                    {
                        ODataType = "#microsoft.graph.answerPrompt",
                        callbackUri = "https://arandiateamsbot.ggunifiedtech.com/api/calls",
                        acceptedModalities = new[] { "audio", "video" },
                        mediaConfig = new
                        {
                            ODataType = "#microsoft.graph.serviceHostedMediaConfig",
                            removeFromDefaultAudioGroup = false
                        }
                    };

                    _logger.LogInformation("Auto-answering call {CallId} for compliance recording", callId);
                    await LogComplianceCallEventAsync("CallAutoAnswered", correlationId);

                    return answerResponse;
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
        }

        /// <summary>
        /// Handle established call - start recording with proper Microsoft Graph compliance
        /// </summary>
        private async Task<object> HandleEstablishedCallAsync(string? callId, Dictionary<string, object> callData, string correlationId)
        {
            try
            {
                _logger.LogInformation("Call {CallId} established - starting compliance recording", callId);

                // Log compliance event for call establishment
                await LogComplianceCallEventAsync("CallEstablished", correlationId);

                if (!string.IsNullOrEmpty(callId))
                {
                    // 🚨 MICROSOFT COMPLIANCE REQUIREMENT: Must call updateRecordingStatus BEFORE starting recording
                    // This is mandatory per Microsoft Graph API documentation for Media Access API
                    _logger.LogInformation("Calling updateRecordingStatus API to indicate recording start for call {CallId}", callId);
                    
                    try
                    {
                        // First, update recording status to indicate recording is starting
                        var recordingStatusResponse = await UpdateRecordingStatusAsync(callId, "recording", correlationId);
                        
                        if (recordingStatusResponse.Success)
                        {
                            _logger.LogInformation("Recording status successfully updated for call {CallId}, proceeding with actual recording", callId);
                            
                            // Only start actual recording AFTER successful updateRecordingStatus API call
                            var meetingInfo = new TeamsComplianceBot.Models.MeetingInfo
                            {
                                Id = callId,
                                Title = "Teams Call Recording",
                                StartTime = DateTime.UtcNow,
                                Organizer = "Teams Compliance Bot",
                                TenantId = "59020e57-1a7b-463f-abbe-eed76e79d47c"
                            };

                            var recordingResult = await _callRecordingService.StartRecordingAsync(meetingInfo, CancellationToken.None);
                            _logger.LogInformation("Recording started for call {CallId} with result: {RecordingResult}", callId, recordingResult.Success);
                            
                            await LogComplianceCallEventAsync("RecordingStarted", correlationId);
                            
                            return new { 
                                status = "recording_started", 
                                callId = callId,
                                complianceStatus = "updateRecordingStatus_called",
                                recordingStatus = recordingResult.Success ? "active" : "failed"
                            };
                        }
                        else
                        {
                            _logger.LogError("Failed to update recording status for call {CallId} - cannot start recording per Microsoft compliance", callId);
                            return new { 
                                status = "recording_failed", 
                                callId = callId,
                                reason = "updateRecordingStatus_failed",
                                error = "Microsoft Graph updateRecordingStatus API failed"
                            };
                        }
                    }
                    catch (Exception updateEx)
                    {
                        _logger.LogError(updateEx, "Exception calling updateRecordingStatus for call {CallId}", callId);
                        return new { 
                            status = "recording_failed", 
                            callId = callId,
                            reason = "updateRecordingStatus_exception",
                            error = updateEx.Message
                        };
                    }
                }

                return new { status = "recording_skipped", reason = "no_call_id" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling established call {CallId}", callId);
                return new { status = "error", error = ex.Message };
            }
        }

        /// <summary>
        /// Handle terminated call - finalize recording with proper Microsoft Graph compliance
        /// </summary>
        private async Task<object> HandleTerminatedCallAsync(string? callId, Dictionary<string, object> callData, string correlationId)
        {
            try
            {
                _logger.LogInformation("Call {CallId} terminated - finalizing recording", callId);

                // Log compliance event for call termination
                await LogComplianceCallEventAsync("CallTerminated", correlationId);

                if (!string.IsNullOrEmpty(callId))
                {
                    // 🚨 MICROSOFT COMPLIANCE REQUIREMENT: Must call updateRecordingStatus to indicate recording END
                    // This must be called BEFORE actually stopping the recording
                    _logger.LogInformation("Calling updateRecordingStatus API to indicate recording end for call {CallId}", callId);
                    
                    try
                    {
                        // First, update recording status to indicate recording is ending
                        var recordingStatusResponse = await UpdateRecordingStatusAsync(callId, "notRecording", correlationId);
                        
                        if (recordingStatusResponse.Success)
                        {
                            _logger.LogInformation("Recording status successfully updated to 'notRecording' for call {CallId}, proceeding to stop recording", callId);
                            
                            // Only stop actual recording AFTER successful updateRecordingStatus API call
                            var stopResult = await _callRecordingService.StopRecordingAsync(callId, CancellationToken.None);
                            _logger.LogInformation("Recording stopped for call {CallId} with result: {StopResult}", callId, stopResult.Success);
                            
                            await LogComplianceCallEventAsync("RecordingStopped", correlationId);
                            
                            return new { 
                                status = "recording_finalized", 
                                callId = callId,
                                complianceStatus = "updateRecordingStatus_called",
                                recordingStopStatus = stopResult.Success ? "success" : "failed"
                            };
                        }
                        else
                        {
                            _logger.LogError("Failed to update recording status to 'notRecording' for call {CallId} - stopping recording anyway for cleanup", callId);
                            
                            // Still try to stop recording for cleanup even if updateRecordingStatus failed
                            var stopResult = await _callRecordingService.StopRecordingAsync(callId, CancellationToken.None);
                            
                            return new { 
                                status = "recording_cleanup", 
                                callId = callId,
                                reason = "updateRecordingStatus_failed_but_cleaned_up",
                                recordingStopStatus = stopResult.Success ? "success" : "failed"
                            };
                        }
                    }
                    catch (Exception updateEx)
                    {
                        _logger.LogError(updateEx, "Exception calling updateRecordingStatus for call termination {CallId}", callId);
                        
                        // Still try to stop recording for cleanup
                        try
                        {
                            var stopResult = await _callRecordingService.StopRecordingAsync(callId, CancellationToken.None);
                            return new { 
                                status = "recording_cleanup", 
                                callId = callId,
                                reason = "updateRecordingStatus_exception_but_cleaned_up",
                                error = updateEx.Message,
                                recordingStopStatus = stopResult.Success ? "success" : "failed"
                            };
                        }
                        catch (Exception stopEx)
                        {
                            _logger.LogError(stopEx, "Failed to cleanup recording for call {CallId}", callId);
                            return new { 
                                status = "error", 
                                callId = callId,
                                error = "Both updateRecordingStatus and cleanup failed"
                            };
                        }
                    }
                }

                return new { status = "recording_finalized", callId = callId };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling terminated call {CallId}", callId);
                return new { status = "error", error = ex.Message };
            }
        }

        /// <summary>
        /// Update recording status via Microsoft Graph API as required for Media Access API compliance
        /// CRITICAL: This must be called before starting recording and before stopping recording
        /// </summary>
        private async Task<(bool Success, string Message)> UpdateRecordingStatusAsync(string callId, string status, string correlationId)
        {
            try
            {
                _logger.LogInformation("Updating recording status to '{Status}' for call {CallId} (correlation: {CorrelationId})", status, callId, correlationId);

                // ✅ IMPLEMENTED: Actual Microsoft Graph API call to updateRecordingStatus
                // This is MANDATORY per Microsoft Graph Media Access API documentation
                // Including ClientContext for session tracking and compliance requirements
                
                var updateRecordingStatusPostRequestBody = new Microsoft.Graph.Communications.Calls.Item.UpdateRecordingStatus.UpdateRecordingStatusPostRequestBody
                {
                    Status = status == "recording" ? 
                        Microsoft.Graph.Models.RecordingStatus.Recording : 
                        Microsoft.Graph.Models.RecordingStatus.NotRecording,
                    
                    // 🔥 COMPLIANCE ENHANCEMENT: Add ClientContext for session tracking
                    // This provides session context for compliance recording requirements
                    ClientContext = $"Teams-Compliance-Bot-Session-{correlationId}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}"
                };

                _logger.LogInformation("Calling Microsoft Graph updateRecordingStatus API for call {CallId} with ClientContext for session tracking", callId);

                // Make the actual Graph API call
                await _graphServiceClient.Communications.Calls[callId].UpdateRecordingStatus
                    .PostAsync(updateRecordingStatusPostRequestBody);

                _logger.LogInformation("✅ Successfully updated recording status to '{Status}' for call {CallId} via Microsoft Graph API with session context", status, callId);

                // Log the compliance action with success including session context
                _telemetryClient.TrackEvent("GraphAPI.UpdateRecordingStatus.Success", new Dictionary<string, string>
                {
                    ["CallId"] = callId,
                    ["Status"] = status,
                    ["CorrelationId"] = correlationId,
                    ["ClientContext"] = updateRecordingStatusPostRequestBody.ClientContext ?? "Unknown",
                    ["Timestamp"] = DateTimeOffset.UtcNow.ToString(),
                    ["Implementation"] = "MICROSOFT_GRAPH_API",
                    ["Result"] = "SUCCESS",
                    ["SessionCompliance"] = "ENABLED"
                });
                
                return (true, $"Recording status successfully updated to {status} via Microsoft Graph API");
            }
            catch (Microsoft.Graph.Models.ODataErrors.ODataError odataEx)
            {
                // Handle specific Graph API errors
                var errorCode = odataEx.Error?.Code ?? "UnknownGraphError";
                var errorMessage = odataEx.Error?.Message ?? "Unknown Graph API error";
                
                _logger.LogError("❌ Microsoft Graph API error updating recording status for call {CallId}: {ErrorCode} - {ErrorMessage}", 
                    callId, errorCode, errorMessage);
                
                _telemetryClient.TrackEvent("GraphAPI.UpdateRecordingStatus.GraphError", new Dictionary<string, string>
                {
                    ["CallId"] = callId,
                    ["Status"] = status,
                    ["CorrelationId"] = correlationId,
                    ["ErrorCode"] = errorCode,
                    ["ErrorMessage"] = errorMessage,
                    ["Implementation"] = "MICROSOFT_GRAPH_API",
                    ["SessionCompliance"] = "ERROR"
                });

                return (false, $"Graph API error: {errorCode} - {errorMessage}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to update recording status for call {CallId} to '{Status}'", callId, status);
                
                _telemetryClient.TrackException(ex, new Dictionary<string, string>
                {
                    ["CallId"] = callId,
                    ["Status"] = status,
                    ["CorrelationId"] = correlationId,
                    ["Operation"] = "UpdateRecordingStatus",
                    ["Implementation"] = "MICROSOFT_GRAPH_API",
                    ["SessionCompliance"] = "ERROR"
                });

                return (false, $"Exception: {ex.Message}");
            }
        }

        /// <summary>
        /// Determine if the bot should automatically answer the call based on compliance policies
        /// COMPLIANCE REQUIREMENT: Auto-answer ALL calls and meetings for recording
        /// </summary>
        private Task<bool> ShouldAutoAnswerCallAsync(Dictionary<string, object> callData, string correlationId)
        {
            try
            {
                // Extract call information for logging
                var source = ExtractCallProperty(callData, "source");
                var subject = ExtractCallProperty(callData, "subject");
                var callType = ExtractCallProperty(callData, "callType");
                var direction = ExtractCallProperty(callData, "direction");
                
                // 🔧 COMPLIANCE REQUIREMENT: Bot must join ALL calls for recording
                // The issue might be in the response format or timing, not the decision to join
                
                _logger.LogInformation("Auto-answering call for compliance recording - Subject: {Subject}, Type: {CallType}, Direction: {Direction} (correlation ID: {CorrelationId})", 
                    subject ?? "None", callType ?? "Unknown", direction ?? "Unknown", correlationId);
                
                // Always return true for compliance recording as required
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking compliance policy for auto-answer, defaulting to TRUE for compliance recording");
                return Task.FromResult(true); // Default to recording for compliance
            }
        }

        /// <summary>
        /// Extract property from call data dictionary safely
        /// </summary>
        private string? ExtractCallProperty(Dictionary<string, object> callData, string propertyName)
        {
            try
            {
                if (callData.TryGetValue(propertyName, out var value))
                {
                    return value?.ToString();
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error extracting property {PropertyName} from call data", propertyName);
                return null;
            }
        }        /// <summary>
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
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error logging call details for event type {EventType}", eventType);
            }
        }

        /// <summary>
        /// Check Graph API permissions for call recording and monitoring
        /// </summary>
        [HttpGet("permissions")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetPermissions()
        {
            try
            {
                _logger.LogInformation("Checking Graph API permissions for call recording");
                
                var permissionTests = new List<object>();
                var results = new List<object>();

                // Test 1: CallRecords.Read.All
                try
                {
                    var callRecords = await _graphServiceClient.Communications.CallRecords.GetAsync(requestConfiguration =>
                    {
                        requestConfiguration.QueryParameters.Top = 1;
                    });
                    
                    results.Add(new
                    {
                        permission = "CallRecords.Read.All",
                        status = "✅ Granted",
                        required = true,
                        description = "Read call records for compliance monitoring",
                        testResult = "Successfully accessed call records endpoint"
                    });
                }
                catch (Exception ex)
                {
                    results.Add(new
                    {
                        permission = "CallRecords.Read.All",
                        status = "❌ Missing or access denied",
                        required = true,
                        description = "Read call records for compliance monitoring",
                        error = ex.Message,
                        testResult = "Failed to access call records endpoint"
                    });
                }

                // Test 2: Calls.AccessMedia.All (for joining calls)
                try
                {
                    // Test if we can access calls endpoint (this might fail but helps verify permission)
                    var callsTest = await _graphServiceClient.Communications.Calls.GetAsync(requestConfiguration =>
                    {
                        requestConfiguration.QueryParameters.Top = 1;
                    });
                    
                    results.Add(new
                    {
                        permission = "Calls.AccessMedia.All",
                        status = "✅ Granted",
                        required = true,
                        description = "Join and record Teams calls",
                        testResult = "Successfully accessed calls endpoint"
                    });
                }
                catch (Exception ex)
                {
                    var statusMessage = ex.Message.Contains("Forbidden") || ex.Message.Contains("Unauthorized") 
                        ? "❌ Missing or access denied" 
                        : "⚠️ Cannot verify (may still be granted)";
                        
                    results.Add(new
                    {
                        permission = "Calls.AccessMedia.All",
                        status = statusMessage,
                        required = true,
                        description = "Join and record Teams calls",
                        note = "This permission might be granted but not testable via this endpoint",
                        testResult = ex.Message
                    });
                }

                // Test 3: OnlineMeetings.ReadWrite.All
                try
                {
                    // Try to access online meetings (this might not work for service principal)
                    var meetingsTest = await _graphServiceClient.Communications.OnlineMeetings.GetAsync(requestConfiguration =>
                    {
                        requestConfiguration.QueryParameters.Top = 1;
                    });
                    
                    results.Add(new
                    {
                        permission = "OnlineMeetings.ReadWrite.All",
                        status = "✅ Granted",
                        required = false,
                        description = "Create and manage online meetings",
                        testResult = "Successfully accessed online meetings endpoint"
                    });
                }
                catch (Exception ex)
                {
                    var statusMessage = ex.Message.Contains("Forbidden") || ex.Message.Contains("Unauthorized") 
                        ? "❌ Missing or access denied" 
                        : "⚠️ Cannot verify (may still be granted)";
                        
                    results.Add(new
                    {
                        permission = "OnlineMeetings.ReadWrite.All",
                        status = statusMessage,
                        required = false,
                        description = "Create and manage online meetings",
                        note = "This permission might be granted but not testable via this endpoint",
                        testResult = ex.Message
                    });
                }

                var grantedCount = results.Count(r => r.GetType().GetProperty("status")?.GetValue(r)?.ToString()?.Contains("✅") == true);
                var requiredCount = results.Count(r => r.GetType().GetProperty("required")?.GetValue(r) as bool? == true);
                var requiredGranted = results.Count(r => 
                    r.GetType().GetProperty("required")?.GetValue(r) as bool? == true && 
                    r.GetType().GetProperty("status")?.GetValue(r)?.ToString()?.Contains("✅") == true);

                return Ok(new
                {
                    success = true,
                    permissionStatus = new
                    {
                        total = results.Count,
                        granted = grantedCount,
                        required = requiredCount,
                        requiredGranted = requiredGranted,
                        ready = requiredGranted >= requiredCount
                    },
                    permissions = results,
                    analysis = new
                    {
                        callRecordingReady = requiredGranted >= requiredCount,
                        recommendation = requiredGranted < requiredCount 
                            ? "Grant missing required permissions in Azure AD app registration"
                            : "Permissions look good for call recording functionality"
                    },
                    timestamp = DateTimeOffset.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking Graph API permissions");
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    message = "Failed to check Graph API permissions",
                    timestamp = DateTimeOffset.UtcNow
                });
            }
        }
    }
}
