using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace TeamsComplianceBot.Controllers;

/// <summary>
/// Diagnostic controller for troubleshooting Bot Framework and Web Chat issues
/// </summary>
[Route("api/diagnostic")]
[ApiController]
public class DiagnosticController : ControllerBase
{
    private readonly ILogger<DiagnosticController> _logger;
    private readonly IConfiguration _configuration;

    public DiagnosticController(ILogger<DiagnosticController> logger, IConfiguration configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>
    /// Endpoint to check for specific problematic correlation IDs from Web Chat errors
    /// </summary>
    [HttpGet("correlation/{correlationId}")]
    public IActionResult GetCorrelationInfo(string correlationId)
    {
        try
        {
            var knownProblemIds = new Dictionary<string, string>
            {
                { "8cf2fa7e4e4363b8c3108a862cbc1b19", "2025-06-15 17:24:26 - BadRequest from Web Chat" },
                { "6ebcc2810580524611981c6d0f7445b1", "2025-06-15 17:23:31 - BadRequest from Web Chat" },
                { "69112a3a9acf5a9f9458aa7c506a0861", "2025-06-15 17:02:50 - BadRequest from Web Chat" },
                { "4011d0659e0ea18ad2bf520a314676e3", "2025-06-15 17:02:42 - BadRequest from Web Chat" }
            };

            var isKnownProblem = knownProblemIds.ContainsKey(correlationId);
            
            return Ok(new
            {
                correlationId,
                isKnownProblem,
                description = isKnownProblem ? knownProblemIds[correlationId] : "Not in known problem list",
                timestamp = DateTimeOffset.UtcNow,
                suggestions = isKnownProblem ? new[]
                {
                    "Check application logs for this correlation ID",
                    "Look for Bot Framework Activity validation errors",
                    "Verify JSON structure of incoming Web Chat messages",
                    "Check for missing required properties (channelId, from, conversation, recipient)"
                } : new[] { "This correlation ID is not in the known problem list" }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking correlation ID {CorrelationId}", correlationId);
            return StatusCode(500, "Error checking correlation ID");
        }
    }

    /// <summary>
    /// Get diagnostic information about the bot
    /// </summary>
    [HttpGet("info")]
    public IActionResult GetDiagnosticInfo()
    {
        try
        {
            var diagnosticInfo = new
            {
                timestamp = DateTimeOffset.UtcNow,
                environment = _configuration["ASPNETCORE_ENVIRONMENT"],
                botId = _configuration["MicrosoftAppId"],
                version = GetType().Assembly.GetName().Version?.ToString(),
                machineName = Environment.MachineName,
                processId = Environment.ProcessId,
                workingSet = Environment.WorkingSet,
                gcMemory = GC.GetTotalMemory(false),
                threadCount = Process.GetCurrentProcess().Threads.Count,
                uptime = DateTimeOffset.UtcNow - Process.GetCurrentProcess().StartTime,
                botFrameworkSettings = new
                {
                    hasAppId = !string.IsNullOrEmpty(_configuration["MicrosoftAppId"]),
                    hasAppPassword = !string.IsNullOrEmpty(_configuration["MicrosoftAppPassword"]),
                    appType = _configuration["MicrosoftAppType"] ?? "MultiTenant"
                },
                knownIssues = new
                {
                    webChatBadRequestErrors = new[]
                    {
                        "8cf2fa7e4e4363b8c3108a862cbc1b19",
                        "6ebcc2810580524611981c6d0f7445b1", 
                        "69112a3a9acf5a9f9458aa7c506a0861",
                        "4011d0659e0ea18ad2bf520a314676e3"
                    }
                }
            };

            return Ok(diagnosticInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting diagnostic info");
            return StatusCode(500, "Error getting diagnostic info");
        }
    }

    /// <summary>
    /// Test endpoint to validate Bot Framework Activity structure
    /// </summary>
    [HttpPost("validate-activity")]
    public IActionResult ValidateActivity([FromBody] object activity)
    {
        try
        {
            if (activity == null)
            {
                return BadRequest("Activity cannot be null");
            }

            var json = System.Text.Json.JsonSerializer.Serialize(activity);
            _logger.LogInformation("Validating activity: {Activity}", json);

            using var document = System.Text.Json.JsonDocument.Parse(json);
            var root = document.RootElement;

            var validation = new
            {
                isValid = true,
                properties = new
                {
                    hasType = root.TryGetProperty("type", out var typeElement),
                    type = typeElement.ValueKind != System.Text.Json.JsonValueKind.Undefined ? typeElement.GetString() : null,
                    hasId = root.TryGetProperty("id", out _),
                    hasTimestamp = root.TryGetProperty("timestamp", out _),
                    hasChannelId = root.TryGetProperty("channelId", out var channelElement),
                    channelId = channelElement.ValueKind != System.Text.Json.JsonValueKind.Undefined ? channelElement.GetString() : null,
                    hasFrom = root.TryGetProperty("from", out _),
                    hasConversation = root.TryGetProperty("conversation", out _),
                    hasRecipient = root.TryGetProperty("recipient", out _),
                    hasServiceUrl = root.TryGetProperty("serviceUrl", out _)
                },
                recommendations = new List<string>()
            };

            // Add recommendations based on missing properties
            if (!validation.properties.hasType)
                ((List<string>)validation.recommendations).Add("Activity is missing required 'type' property");
            if (!validation.properties.hasChannelId)
                ((List<string>)validation.recommendations).Add("Activity is missing required 'channelId' property");
            if (!validation.properties.hasFrom)
                ((List<string>)validation.recommendations).Add("Activity is missing required 'from' property");
            if (!validation.properties.hasConversation)
                ((List<string>)validation.recommendations).Add("Activity is missing required 'conversation' property");
            if (!validation.properties.hasRecipient)
                ((List<string>)validation.recommendations).Add("Activity is missing required 'recipient' property");

            return Ok(validation);
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogError(ex, "JSON validation error");
            return BadRequest(new { error = "Invalid JSON", details = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating activity");
            return StatusCode(500, "Error validating activity");
        }
    }

    /// <summary>
    /// Test endpoint to simulate different error scenarios for testing error handling
    /// </summary>
    [HttpGet("test-error/{errorType}")]
    public IActionResult TestError(string errorType)
    {
        try
        {
            _logger.LogInformation("Testing error type: {ErrorType}", errorType);

            return errorType.ToLowerInvariant() switch
            {
                "400" or "badrequest" => BadRequest("Simulated BadRequest error"),
                "401" or "unauthorized" => Unauthorized("Simulated Unauthorized error"),
                "403" or "forbidden" => StatusCode(403, "Simulated Forbidden error"),
                "404" or "notfound" => NotFound("Simulated NotFound error"),
                "500" or "internal" => throw new InvalidOperationException("Simulated internal server error"),
                "timeout" => throw new TimeoutException("Simulated timeout error"),
                "null" => throw new ArgumentNullException("testParameter", "Simulated null reference"),
                _ => Ok(new { message = "No error simulated", availableTypes = new[] { "400", "401", "403", "404", "500", "timeout", "null" } })
            };
        }
        catch (Exception ex) when (errorType.ToLowerInvariant() is "500" or "internal" or "timeout" or "null")
        {
            _logger.LogError(ex, "Simulated error for testing: {ErrorType}", errorType);
            return StatusCode(500, $"Simulated error: {ex.Message}");
        }
    }
}
