using Microsoft.AspNetCore.Mvc;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Schema;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using System.Diagnostics;
using System.Text.Json;

namespace TeamsComplianceBot.Controllers;

/// <summary>
/// Production-ready Bot Framework message endpoint controller with enhanced security,
/// compliance logging, and observability features for Microsoft Teams compliance recording
/// </summary>
[Route("api/messages")]
[ApiController]
public class BotController : ControllerBase
{
    private readonly IBotFrameworkHttpAdapter _adapter;
    private readonly IBot _bot;
    private readonly ILogger<BotController> _logger;
    private readonly TelemetryClient _telemetryClient;
    private readonly IConfiguration _configuration;

    // Security and rate limiting
    private static readonly ActivitySource ActivitySource = new("TeamsComplianceBot.BotController");
    private const int MAX_REQUEST_SIZE = 1_048_576; // 1MB limit for bot messages
    private const string CORRELATION_ID_HEADER = "X-Correlation-ID";

    public BotController(
        IBotFrameworkHttpAdapter adapter, 
        IBot bot,
        ILogger<BotController> logger,
        TelemetryClient telemetryClient,
        IConfiguration configuration)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        _bot = bot ?? throw new ArgumentNullException(nameof(bot));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>
    /// Health check and information endpoint for the bot
    /// </summary>
    [HttpGet]
    public IActionResult Get()
    {
        try
        {
            _logger.LogInformation("Bot endpoint health check requested");
            
            var botInfo = new
            {
                status = "healthy",
                botName = "Teams Compliance Bot",
                version = "1.0.0",
                timestamp = DateTimeOffset.UtcNow,
                endpoints = new[]
                {
                    "POST /api/messages - Bot Framework message handling",
                    "GET /api/messages - This health check endpoint"
                }
            };

            return Ok(botInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during bot health check");
            return StatusCode(500, new { status = "unhealthy", error = ex.Message });
        }
    }

    /// <summary>
    /// Main endpoint for processing bot messages from Teams with enhanced security and compliance logging
    /// </summary>
    /// <returns>HTTP response</returns>    [HttpPost]
    public async Task<IActionResult> PostAsync()
    {        // Bot Framework and Web Chat correlation IDs can come from multiple headers
        var correlationId = HttpContext.Request.Headers[CORRELATION_ID_HEADER].FirstOrDefault() 
                           ?? HttpContext.Request.Headers["x-ms-correlation-id"].FirstOrDefault()
                           ?? HttpContext.Request.Headers["MS-CV"].FirstOrDefault()
                           ?? HttpContext.Request.Headers["x-ms-client-request-id"].FirstOrDefault()
                           ?? HttpContext.Request.Headers["x-ms-request-id"].FirstOrDefault()
                           ?? Guid.NewGuid().ToString();

        // Check if this is one of the specific correlation IDs from recent errors
        var knownProblemIds = new[] { 
            "8cf2fa7e4e4363b8c3108a862cbc1b19", 
            "6ebcc2810580524611981c6d0f7445b1", 
            "69112a3a9acf5a9f9458aa7c506a0861",
            "4011d0659e0ea18ad2bf520a314676e3"
        };
        
        var isKnownProblem = knownProblemIds.Contains(correlationId);
        if (isKnownProblem)
        {
            _logger.LogWarning("Processing known problematic correlation ID: {CorrelationId}", correlationId);
        }

        using var activity = ActivitySource.StartActivity("BotMessage.Process");
        activity?.SetTag("correlation.id", correlationId);
        activity?.SetTag("remote.address", HttpContext.Connection.RemoteIpAddress?.ToString());

        using var operation = _telemetryClient.StartOperation<RequestTelemetry>("Bot Message Processing");
        operation.Telemetry.Properties["CorrelationId"] = correlationId;
        operation.Telemetry.Properties["RemoteIpAddress"] = HttpContext.Connection.RemoteIpAddress?.ToString();        // Capture request body for diagnostic purposes
        string? requestBody = null;
        try
        {
            // Enable buffering to allow multiple reads of the request body
            Request.EnableBuffering();
            
            using var reader = new StreamReader(Request.Body, leaveOpen: true);
            requestBody = await reader.ReadToEndAsync();
            Request.Body.Position = 0; // Reset stream position for downstream processing
            
            // Log request details for BadRequest debugging
            _logger.LogInformation("Bot request received. Method: {Method}, ContentType: {ContentType}, ContentLength: {ContentLength}, CorrelationId: {CorrelationId}",
                Request.Method, Request.ContentType, Request.ContentLength, correlationId);
                  if (!string.IsNullOrEmpty(requestBody))
            {
                // Log first 500 characters of request body for debugging (avoid logging full payload for security)
                var bodyPreview = requestBody.Length > 500 ? requestBody.Substring(0, 500) + "..." : requestBody;
                _logger.LogDebug("Request body preview for {CorrelationId}: {RequestBodyPreview}", correlationId, bodyPreview);
                
                // For known problem correlation IDs, log the full request body
                if (isKnownProblem)
                {
                    _logger.LogWarning("Full request body for known problem {CorrelationId}: {FullRequestBody}", correlationId, requestBody);
                }
                
                // Validate JSON structure
                try
                {
                    var jsonDocument = JsonDocument.Parse(requestBody);
                    _logger.LogDebug("Request JSON is valid for {CorrelationId}", correlationId);
                    
                    // Check for required Bot Framework Activity properties
                    if (jsonDocument.RootElement.TryGetProperty("type", out var typeElement))
                    {
                        var activityType = typeElement.GetString();
                        _logger.LogDebug("Activity type: {ActivityType} for {CorrelationId}", activityType, correlationId);
                        
                        // Check for required properties based on activity type
                        var hasId = jsonDocument.RootElement.TryGetProperty("id", out _);
                        var hasTimestamp = jsonDocument.RootElement.TryGetProperty("timestamp", out _);
                        var hasChannelId = jsonDocument.RootElement.TryGetProperty("channelId", out _);
                        var hasFrom = jsonDocument.RootElement.TryGetProperty("from", out _);
                        var hasConversation = jsonDocument.RootElement.TryGetProperty("conversation", out _);
                        var hasRecipient = jsonDocument.RootElement.TryGetProperty("recipient", out _);
                        var hasServiceUrl = jsonDocument.RootElement.TryGetProperty("serviceUrl", out _);
                        
                        _logger.LogDebug("Activity validation for {CorrelationId}: id={HasId}, timestamp={HasTimestamp}, channelId={HasChannelId}, from={HasFrom}, conversation={HasConversation}, recipient={HasRecipient}, serviceUrl={HasServiceUrl}",
                            correlationId, hasId, hasTimestamp, hasChannelId, hasFrom, hasConversation, hasRecipient, hasServiceUrl);
                            
                        if (!hasChannelId || !hasFrom || !hasConversation || !hasRecipient)
                        {
                            _logger.LogWarning("Activity missing required properties for {CorrelationId}", correlationId);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Activity missing 'type' property for {CorrelationId}", correlationId);
                    }
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogError(jsonEx, "Invalid JSON in request body for {CorrelationId}: {RequestBody}", correlationId, requestBody);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read request body for diagnostics");
        }        try
        {
            // Log authentication headers for debugging
            var authHeader = HttpContext.Request.Headers["Authorization"].FirstOrDefault();
            if (string.IsNullOrEmpty(authHeader))
            {
                _logger.LogWarning("Request missing Authorization header for {CorrelationId}", correlationId);
            }
            else
            {
                // Log header format for debugging (without exposing the token)
                var headerInfo = authHeader.StartsWith("Bearer ") ? "Bearer token present" : $"Auth header format: {authHeader.Split(' ')[0]}";
                _logger.LogDebug("Authorization header for {CorrelationId}: {HeaderInfo}", correlationId, headerInfo);
            }

            // Don't reject requests here - let Bot Framework adapter handle authentication
            // The adapter will properly validate Bot Framework tokens
            if (!ValidateRequest())
            {
                _logger.LogWarning("Bot message request validation failed from {RemoteIpAddress}. CorrelationId: {CorrelationId}", 
                    HttpContext.Connection.RemoteIpAddress?.ToString(), correlationId);
                return BadRequest("Invalid request");
            }

            // Content length validation
            if (Request.ContentLength > MAX_REQUEST_SIZE)
            {
                _logger.LogWarning("Bot message request too large: {ContentLength} bytes from {RemoteIpAddress}. CorrelationId: {CorrelationId}", 
                    Request.ContentLength, HttpContext.Connection.RemoteIpAddress?.ToString(), correlationId);
                return BadRequest("Request too large");
            }            _logger.LogInformation("Processing bot message request from {RemoteIpAddress} with correlation ID {CorrelationId}", 
                HttpContext.Connection.RemoteIpAddress?.ToString(), correlationId);

            // Add correlation ID to response headers for traceability
            HttpContext.Response.Headers.Append(CORRELATION_ID_HEADER, correlationId);

            // Enhanced logging for compliance
            var scopeProperties = new Dictionary<string, object?>
            {
                ["CorrelationId"] = correlationId,
                ["Operation"] = "BotMessageProcessing",
                ["RemoteIpAddress"] = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                ["UserAgent"] = HttpContext.Request.Headers.UserAgent.ToString(),
                ["Timestamp"] = DateTimeOffset.UtcNow
            };

            using var scope = _logger.BeginScope(scopeProperties);            // Delegate the processing of the HTTP POST to the adapter.
            // The adapter will invoke the bot.
            try
            {
                await _adapter.ProcessAsync(Request, Response, _bot);
                _logger.LogInformation("Successfully processed bot message with correlation ID {CorrelationId}", correlationId);            }
            catch (Microsoft.Bot.Schema.ErrorResponseException botEx)
            {
                _logger.LogError(botEx, "Bot Framework error for {CorrelationId}: {BotError}", correlationId, botEx.Message);
                
                // Check if it's an authentication error
                if (botEx.Message.Contains("Unauthorized") || botEx.Message.Contains("401"))
                {
                    _logger.LogError("Authentication failed for {CorrelationId}. Verify MicrosoftAppId and MicrosoftAppPassword configuration", correlationId);
                    return Unauthorized("Bot Framework authentication failed");
                }
                
                throw; // Re-throw to be caught by outer catch blocks
            }
            catch (Microsoft.IdentityModel.Tokens.SecurityTokenMalformedException tokenEx)
            {
                _logger.LogError(tokenEx, "JWT token malformed for {CorrelationId}: {TokenError}", correlationId, tokenEx.Message);
                return Unauthorized("Invalid authentication token format");
            }
            catch (Microsoft.IdentityModel.Tokens.SecurityTokenException securityEx)
            {
                _logger.LogError(securityEx, "Security token validation failed for {CorrelationId}: {SecurityError}", correlationId, securityEx.Message);
                return Unauthorized("Token validation failed");
            }
            catch (InvalidOperationException invalidOpEx) when (invalidOpEx.Message.Contains("Activity"))
            {
                _logger.LogError(invalidOpEx, "Invalid Activity object for {CorrelationId}: {Error}. RequestBody: {RequestBody}", 
                    correlationId, invalidOpEx.Message, requestBody);
                throw; // Re-throw to be caught by outer catch blocks
            }
            operation.Telemetry.Success = true;

            return new EmptyResult();
        }
        catch (UnauthorizedAccessException ex)
        {
            operation.Telemetry.Success = false;
            _logger.LogWarning(ex, "Unauthorized bot message request from {RemoteIpAddress} with correlation ID {CorrelationId}", 
                HttpContext.Connection.RemoteIpAddress?.ToString(), correlationId);

            _telemetryClient.TrackException(ex, new Dictionary<string, string>
            {
                ["Operation"] = "BotMessageProcessing",
                ["CorrelationId"] = correlationId,
                ["ErrorType"] = "Unauthorized"
            });

            return Unauthorized();
        }
        catch (ArgumentException ex)
        {
            operation.Telemetry.Success = false;
            _logger.LogWarning(ex, "Invalid bot message request from {RemoteIpAddress} with correlation ID {CorrelationId}. RequestBody: {RequestBody}", 
                HttpContext.Connection.RemoteIpAddress?.ToString(), correlationId, requestBody);

            _telemetryClient.TrackException(ex, new Dictionary<string, string>
            {
                ["Operation"] = "BotMessageProcessing",
                ["CorrelationId"] = correlationId,
                ["ErrorType"] = "ArgumentException",
                ["RequestBody"] = requestBody ?? "null"
            });

            return BadRequest("Invalid message format");
        }
        catch (JsonException ex)
        {
            operation.Telemetry.Success = false;
            _logger.LogError(ex, "JSON deserialization error for bot message from {RemoteIpAddress} with correlation ID {CorrelationId}. RequestBody: {RequestBody}", 
                HttpContext.Connection.RemoteIpAddress?.ToString(), correlationId, requestBody);

            _telemetryClient.TrackException(ex, new Dictionary<string, string>
            {
                ["Operation"] = "BotMessageProcessing",
                ["CorrelationId"] = correlationId,
                ["ErrorType"] = "JsonException",
                ["RequestBody"] = requestBody ?? "null"
            });

            return BadRequest("Invalid JSON format");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("model") || ex.Message.Contains("validation"))
        {
            operation.Telemetry.Success = false;
            _logger.LogError(ex, "Model validation error for bot message from {RemoteIpAddress} with correlation ID {CorrelationId}. RequestBody: {RequestBody}", 
                HttpContext.Connection.RemoteIpAddress?.ToString(), correlationId, requestBody);

            _telemetryClient.TrackException(ex, new Dictionary<string, string>
            {
                ["Operation"] = "BotMessageProcessing",
                ["CorrelationId"] = correlationId,
                ["ErrorType"] = "ModelValidation",
                ["RequestBody"] = requestBody ?? "null"
            });

            return BadRequest($"Model validation failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            operation.Telemetry.Success = false;
            _logger.LogError(ex, "Error processing bot message from {RemoteIpAddress} with correlation ID {CorrelationId}. RequestBody: {RequestBody}", 
                HttpContext.Connection.RemoteIpAddress?.ToString(), correlationId, requestBody);            _telemetryClient.TrackException(ex, new Dictionary<string, string>
            {
                ["Operation"] = "BotMessageProcessing",
                ["CorrelationId"] = correlationId,
                ["RemoteIpAddress"] = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                ["RequestBody"] = requestBody ?? "null"
            });            // Return BadRequest for client errors, InternalServerError for server errors
            if (ex is ArgumentException || ex.Message.Contains("400") || ex.Message.Contains("BadRequest"))
            {
                // Enhanced logging for BadRequest scenarios with correlation IDs from your error report
                _logger.LogError("BadRequest error for correlation ID {CorrelationId}: {ErrorMessage}. " +
                               "RequestBody: {RequestBody}. Exception: {Exception}", 
                               correlationId, ex.Message, requestBody, ex.ToString());
                               
                return BadRequest($"Invalid request: {ex.Message}");
            }

            // Don't expose internal errors in production
            return StatusCode(500, "An error occurred while processing the message");
        }
    }

    /// <summary>
    /// Enhanced health check endpoint for the bot with dependency validation
    /// </summary>
    /// <returns>Detailed bot health status</returns>
    [HttpGet("health")]
    public async Task<IActionResult> GetHealthAsync()
    {
        using var activity = ActivitySource.StartActivity("BotHealth.Check");
        
        try
        {
            _logger.LogDebug("Bot health check requested from {RemoteIpAddress}", 
                HttpContext.Connection.RemoteIpAddress?.ToString());

            var healthData = new
            {
                status = "healthy",
                timestamp = DateTimeOffset.UtcNow,
                version = GetType().Assembly.GetName().Version?.ToString(),
                environment = _configuration["ASPNETCORE_ENVIRONMENT"],
                botId = _configuration["MicrosoftAppId"],
                dependencies = await CheckDependenciesAsync()
            };

            return Ok(healthData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during bot health check");
            
            return StatusCode(500, new 
            { 
                status = "unhealthy", 
                timestamp = DateTimeOffset.UtcNow,
                error = "Health check failed"
            });
        }
    }

    /// <summary>
    /// Readiness probe endpoint for Kubernetes/container orchestration
    /// </summary>
    [HttpGet("ready")]
    public IActionResult GetReadiness()
    {
        try
        {
            // Basic readiness checks
            var isReady = _adapter != null && _bot != null;
            
            if (isReady)
            {
                return Ok(new { status = "ready", timestamp = DateTimeOffset.UtcNow });
            }
            else
            {
                return ServiceUnavailable(new { status = "not-ready", timestamp = DateTimeOffset.UtcNow });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during readiness check");
            return ServiceUnavailable(new { status = "not-ready", error = "Readiness check failed" });
        }
    }

    /// <summary>
    /// Liveness probe endpoint for Kubernetes/container orchestration
    /// </summary>
    [HttpGet("live")]
    public IActionResult GetLiveness()
    {
        return Ok(new { status = "alive", timestamp = DateTimeOffset.UtcNow });
    }    private bool ValidateRequest()
    {
        try
        {
            // Validate Content-Type for POST requests
            if (HttpContext.Request.Method == "POST")
            {
                var contentType = HttpContext.Request.ContentType;
                if (string.IsNullOrEmpty(contentType) || 
                    !contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Invalid content type: {ContentType}", contentType);
                    // Don't reject - the Bot Framework uses other content types in some scenarios
                    // return false;
                }
            }

            // Log rather than reject on missing Authorization header
            // Microsoft Bot Framework handles auth internally
            if (!HttpContext.Request.Headers.ContainsKey("Authorization"))
            {
                _logger.LogInformation("Request missing Authorization header - may be normal for some Bot Framework messages");
            }

            // Accept all requests and let Bot Framework handle validation
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error validating bot request");
            return false;
        }
    }private Task<object> CheckDependenciesAsync()
    {
        var dependencies = new Dictionary<string, object>();

        try
        {
            // Check adapter health
            dependencies["adapter"] = new { status = _adapter != null ? "healthy" : "unhealthy" };

            // Check bot health
            dependencies["bot"] = new { status = _bot != null ? "healthy" : "unhealthy" };

            // Check configuration
            var hasRequiredConfig = !string.IsNullOrEmpty(_configuration["MicrosoftAppId"]) &&
                                  !string.IsNullOrEmpty(_configuration["MicrosoftAppPassword"]);
            dependencies["configuration"] = new { status = hasRequiredConfig ? "healthy" : "unhealthy" };

            return Task.FromResult<object>(dependencies);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking dependencies during health check");
            return Task.FromResult<object>(new { error = "Unable to check dependencies" });
        }
    }

    private IActionResult ServiceUnavailable(object value)
    {
        return StatusCode(503, value);
    }
}
