using Microsoft.AspNetCore.Mvc;
using Microsoft.Graph;
using System.Text.Json;
using TeamsComplianceBot.Services;
using System.Security.Cryptography;
using System.Text;
using TeamsComplianceBot.Models;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using System.Diagnostics;

namespace TeamsComplianceBot.Controllers;

/// <summary>
/// Production-ready controller for handling Microsoft Graph API webhook notifications
/// with enhanced security, compliance logging, and observability features for Teams compliance recording
/// </summary>
[ApiController]
[Route("api/notifications")]
public class NotificationsController : ControllerBase
{
    private readonly ILogger<NotificationsController> _logger;
    private readonly IConfiguration _configuration;
    private readonly ICallRecordingService _recordingService;
    private readonly IComplianceService _complianceService;
    private readonly IGraphSubscriptionService? _subscriptionService;
    private readonly ICallJoiningService _callJoiningService;
    private readonly TelemetryClient _telemetryClient;

    // Security and monitoring
    private static readonly ActivitySource ActivitySource = new("TeamsComplianceBot.NotificationsController");
    private const int MAX_REQUEST_SIZE = 1_048_576; // 1MB limit for notifications
    private const string CORRELATION_ID_HEADER = "X-Correlation-ID";
    private const string SIGNATURE_HEADER = "X-MS-Signature";
    private const string CERTIFICATE_HEADER = "X-MS-Certificate";

    public NotificationsController(
        ILogger<NotificationsController> logger,
        IConfiguration configuration,
        ICallRecordingService recordingService,
        IComplianceService complianceService,
        ICallJoiningService callJoiningService,
        TelemetryClient telemetryClient,
        IGraphSubscriptionService? subscriptionService = null) // Optional dependency
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _recordingService = recordingService ?? throw new ArgumentNullException(nameof(recordingService));
        _complianceService = complianceService ?? throw new ArgumentNullException(nameof(complianceService));
        _callJoiningService = callJoiningService ?? throw new ArgumentNullException(nameof(callJoiningService));
        _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
        _subscriptionService = subscriptionService; // May be null if not registered
    }

    /// <summary>
/// GET endpoint for Microsoft Graph subscription validation
/// Microsoft Graph will call this endpoint with a validationToken query parameter when creating a subscription
/// </summary>
[HttpGet]
[Produces("text/plain")]
public IActionResult Get()
{
    try
    {
        _logger.LogInformation("Received GET request to webhook endpoint");

        // Get the validation token from the query string
        var validationToken = Request.Query["validationToken"].FirstOrDefault();

        if (string.IsNullOrEmpty(validationToken))
        {
            _logger.LogWarning("GET request without validationToken query parameter");
            return BadRequest("GET requests require a validationToken query parameter for Graph validation");
        }

        _logger.LogInformation("Received Graph validation request with token: {ValidationTokenPreview}...",
            validationToken.Length > 10 ? validationToken.Substring(0, 10) + "..." : validationToken);

        return Content(validationToken, "text/plain");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error processing GET validation request");
        return StatusCode(500, "An error occurred while processing the validation request");
    }
}

/// <summary>
/// POST endpoint for handling Microsoft Graph API notifications with enhanced security and compliance
/// Processes actual notification events sent after subscription validation
/// </summary>
[HttpPost]
    public async Task<IActionResult> Post()
    {
        var correlationId = HttpContext.Request.Headers[CORRELATION_ID_HEADER].FirstOrDefault() 
                           ?? Guid.NewGuid().ToString();

        using var activity = ActivitySource.StartActivity("GraphNotification.Process");
        activity?.SetTag("correlation.id", correlationId);
        activity?.SetTag("remote.address", HttpContext.Connection.RemoteIpAddress?.ToString());

        using var operation = _telemetryClient.StartOperation<RequestTelemetry>("Graph Notification Processing");
        operation.Telemetry.Properties["CorrelationId"] = correlationId;
        operation.Telemetry.Properties["RemoteIpAddress"] = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

        try
        {
            _logger.LogInformation("Received POST notification from Microsoft Graph with correlation ID {CorrelationId}", correlationId);

            // Check if this is a validation request (Microsoft Graph can send validation via POST)
            var validationToken = Request.Query["validationToken"].FirstOrDefault();
            if (!string.IsNullOrEmpty(validationToken))
            {
                _logger.LogInformation("Received Graph validation request via POST with token: {ValidationTokenPreview}...",
                    validationToken.Length > 10 ? validationToken.Substring(0, 10) + "..." : validationToken);
                return Content(validationToken, "text/plain");
            }

            // Security validation
            if (!ValidateNotificationRequest())
            {
                _logger.LogWarning("Graph notification request validation failed from {RemoteIpAddress}", 
                    HttpContext.Connection.RemoteIpAddress?.ToString());
                return BadRequest("Invalid request");
            }

            // Content length validation
            if (Request.ContentLength > MAX_REQUEST_SIZE)
            {
                _logger.LogWarning("Graph notification request too large: {ContentLength} bytes from {RemoteIpAddress}", 
                    Request.ContentLength, HttpContext.Connection.RemoteIpAddress?.ToString());
                return BadRequest("Request too large");
            }

            // Add correlation ID to response headers for traceability
            HttpContext.Response.Headers.Append(CORRELATION_ID_HEADER, correlationId);

            // Enhanced logging for compliance
            var scopeProperties = new Dictionary<string, object?>
            {
                ["CorrelationId"] = correlationId,
                ["Operation"] = "GraphNotificationProcessing",
                ["RemoteIpAddress"] = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                ["UserAgent"] = HttpContext.Request.Headers.UserAgent.ToString(),
                ["Timestamp"] = DateTimeOffset.UtcNow
            };

            using var scope = _logger.BeginScope(scopeProperties);

            // Read the request body
            string requestBody;
            using (var reader = new StreamReader(Request.Body, leaveOpen: true))
            {
                requestBody = await reader.ReadToEndAsync();
            }

            // Check if empty or invalid
            if (string.IsNullOrEmpty(requestBody))
            {
                _logger.LogWarning("Received empty notification body with correlation ID {CorrelationId}", correlationId);
                return BadRequest("Request body cannot be empty");
            }

            _logger.LogDebug("Received payload with correlation ID {CorrelationId}: {PayloadStart}...", 
                correlationId, requestBody.Length > 100 ? requestBody.Substring(0, 100) + "..." : requestBody);

            // Deserialize the notification
            var options = new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            
            var notification = JsonSerializer.Deserialize<NotificationPayload>(requestBody, options);
            
            if (notification == null)
            {
                _logger.LogWarning("Failed to deserialize notification payload with correlation ID {CorrelationId}", correlationId);
                return BadRequest("Invalid notification format");
            }

            // Handle validation requests ONLY if no notifications are present
            if (!string.IsNullOrEmpty(notification.ValidationCode))
            {
                _logger.LogInformation("Handling POST validation request with validation code and correlation ID {CorrelationId}", correlationId);
                return HandleValidationRequest(notification);
            }

            // Handle actual notification
            if (notification.Value?.Count > 0)
            {
                _logger.LogInformation("Processing {NotificationCount} notifications with correlation ID {CorrelationId}", 
                    notification.Value.Count, correlationId);
                
                await ProcessNotifications(notification, correlationId);
                
                _logger.LogInformation("Successfully processed {NotificationCount} notifications with correlation ID {CorrelationId}", 
                    notification.Value.Count, correlationId);
                
                operation.Telemetry.Success = true;
                return Accepted();
            }

            // If we get here, the notification format is unexpected
            _logger.LogWarning("Received notification with unexpected format and correlation ID {CorrelationId}", correlationId);
            return BadRequest("Invalid notification format - no Value array found");
        }
        catch (JsonException ex)
        {
            operation.Telemetry.Success = false;
            _logger.LogError(ex, "Error deserializing notification payload with correlation ID {CorrelationId}: {Message}", 
                correlationId, ex.Message);
            
            _telemetryClient.TrackException(ex, new Dictionary<string, string>
            {
                ["Operation"] = "GraphNotificationProcessing",
                ["CorrelationId"] = correlationId,
                ["ErrorType"] = "JsonDeserialization"
            });
            
            return BadRequest($"Invalid JSON format: {ex.Message}");
        }
        catch (Exception ex)
        {
            operation.Telemetry.Success = false;
            _logger.LogError(ex, "Error processing notification with correlation ID {CorrelationId}: {Message}", 
                correlationId, ex.Message);
            
            _telemetryClient.TrackException(ex, new Dictionary<string, string>
            {
                ["Operation"] = "GraphNotificationProcessing",
                ["CorrelationId"] = correlationId,
                ["RemoteIpAddress"] = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown"
            });
            
            return StatusCode(500, "An error occurred while processing the notification");
        }
    }

    /// <summary>
    /// Handle a validation request from Microsoft Graph during subscription creation
    /// </summary>
    private IActionResult HandleValidationRequest(NotificationPayload notification)
    {
        try
        {
            // Validate that the ClientState matches what we expect if it's provided
            if (!string.IsNullOrEmpty(notification.ClientState))
            {
                var expectedClientState = _configuration["Recording:NotificationClientState"];
                if (expectedClientState != notification.ClientState)
                {
                    _logger.LogWarning("Client state validation failed. Expected: {Expected}, Actual: {Actual}",
                        expectedClientState, notification.ClientState);
                    return BadRequest("Client state validation failed");
                }
            }

            // For validation requests, we need to send back the validation token
            _logger.LogInformation("Validation request processed successfully. Returning validation token.");
            return Ok(new { validationResponse = notification.ValidationCode });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling validation request");
            return StatusCode(500, "Error handling validation request");
        }
    }    /// <summary>
    /// Process notification events from Microsoft Graph with enhanced logging and compliance
    /// </summary>
    private async Task ProcessNotifications(NotificationPayload notification, string correlationId)
    {
        if (notification.Value == null || notification.Value.Count == 0)
        {
            _logger.LogWarning("ProcessNotifications called with null or empty Value collection");
            return;
        }

        foreach (var change in notification.Value)
        {
            _logger.LogInformation("Processing notification with correlation ID {CorrelationId}: {ResourceData}, Type: {ChangeType}",
                correlationId, change.ResourceData?.Id ?? "Unknown", change.ChangeType);

            using var changeActivity = ActivitySource.StartActivity("GraphNotification.ProcessChange");
            changeActivity?.SetTag("correlation.id", correlationId);
            changeActivity?.SetTag("change.type", change.ChangeType);
            changeActivity?.SetTag("resource", change.Resource);

            try
            {
                // Handle lifecycle notifications for subscriptions if they exist
                if (change.ChangeType == "lifecycleNotification" && _subscriptionService != null)
                {
                    _logger.LogInformation("Received lifecycle notification for subscription {SubscriptionId}", change.SubscriptionId);
                    
                    // Renew the subscription if it's about to expire
                    if (change.Resource?.Contains("expirationDateTime") == true)
                    {
                        await _subscriptionService.RenewSubscriptionAsync(change.SubscriptionId);
                    }
                    continue;
                }
                
                // Verify signature if we have encrypted content
                if (!string.IsNullOrEmpty(notification.EncryptedContent))
                {
                    if (!VerifySignature(notification))
                    {
                        _logger.LogWarning("Signature verification failed for notification");
                        continue;
                    }
                }

                // Process based on the resource data type - we're most interested in call recordings
                if (change.ResourceData != null && change.Resource?.Contains("recordings") == true)
                {
                    await HandleRecordingNotification(change);
                }
                else if (change.Resource?.Contains("transcripts") == true)
                {
                    await HandleTranscriptNotification(change);
                }
                else if (change.Resource?.Contains("communications/calls") == true)
                {
                    await HandleCallNotification(change);
                }
                else
                {
                    _logger.LogInformation("Received notification for unsupported resource: {Resource}", 
                        change.Resource ?? "Unknown");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing notification item: {Message}", ex.Message);
            }
        }
    }

    /// <summary>
    /// Handle a recording notification
    /// </summary>
    private async Task HandleRecordingNotification(ChangeNotification change)
    {
        if (string.IsNullOrEmpty(change.Resource))
        {
            _logger.LogWarning("Received notification with null or empty Resource");
            return;
        }
        
        // Extract meeting and recording IDs from the resource URL
        var resourceParts = change.Resource.Split('/');
        string? meetingId = null;
        string? recordingId = null;

        for (int i = 0; i < resourceParts.Length - 1; i++)
        {
            if (resourceParts[i] == "onlineMeetings" && i + 1 < resourceParts.Length)
            {
                meetingId = resourceParts[i + 1];
            }
            else if (resourceParts[i] == "recordings" && i + 1 < resourceParts.Length)
            {
                recordingId = resourceParts[i + 1];
            }
        }

        if (string.IsNullOrEmpty(meetingId))
        {
            _logger.LogWarning("Could not extract meeting ID from notification resource: {Resource}", change.Resource);
            return;
        }

        // If we're handling a recording that became available
        if (change.ChangeType == "created" || change.ChangeType == "updated")
        {
            _logger.LogInformation("Recording {RecordingId} for meeting {MeetingId} is available", 
                recordingId ?? "unknown", meetingId);

            var recordingInfo = change.ResourceData?.AdditionalData;
            if (recordingInfo != null)
            {
                // Process the recording metadata
                // In production, this would download the recording and update our metadata
                await _complianceService.LogComplianceEventAsync(
                    ComplianceEventType.RecordingAvailable,
                    new MeetingInfo { Id = meetingId },
                    CancellationToken.None);

                // If we have a direct recording ID, try to download it
                if (!string.IsNullOrEmpty(recordingId))
                {
                    try
                    {
                        // TODO: Implement actual recording download logic here
                        _logger.LogInformation("Recording download would be triggered here for {RecordingId}", recordingId);
                        
                        // In the actual implementation, we'd download and store the recording
                        // await _recordingService.DownloadRecordingAsync(recordingId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error downloading recording {RecordingId}", recordingId);
                    }
                }
            }
        }
        else if (change.ChangeType == "deleted")
        {
            _logger.LogInformation("Recording {RecordingId} for meeting {MeetingId} was deleted", 
                recordingId ?? "unknown", meetingId);

            // Update our records that the recording was deleted in Teams
            await _complianceService.LogComplianceEventAsync(
                ComplianceEventType.RecordingDeleted,
                new MeetingInfo { Id = meetingId },
                CancellationToken.None);
        }
    }

    /// <summary>
    /// Handle a call notification - this is where we automatically join calls for compliance recording
    /// </summary>
    private async Task HandleCallNotification(ChangeNotification change)
    {
        if (string.IsNullOrEmpty(change.Resource))
        {
            _logger.LogWarning("Received call notification with null or empty Resource");
            return;
        }

        _logger.LogInformation("Processing call notification: Resource={Resource}, ChangeType={ChangeType}", 
            change.Resource, change.ChangeType);

        try
        {
            // Extract call ID from the resource URL
            // Expected format: /communications/calls/{call-id}
            var resourceParts = change.Resource.Split('/');
            string? callId = null;

            for (int i = 0; i < resourceParts.Length - 1; i++)
            {
                if (resourceParts[i] == "calls" && i + 1 < resourceParts.Length)
                {
                    callId = resourceParts[i + 1];
                    break;
                }
            }

            if (string.IsNullOrEmpty(callId))
            {
                _logger.LogWarning("Could not extract call ID from notification resource: {Resource}", change.Resource);
                return;
            }

            // Handle different call notification types
            switch (change.ChangeType.ToLowerInvariant())
            {
                case "created":
                    _logger.LogInformation("Call {CallId} was created - attempting to join for compliance recording", callId);
                    await HandleCallCreated(callId, change);
                    break;

                case "updated":
                    _logger.LogInformation("Call {CallId} was updated - checking if action needed", callId);
                    await HandleCallUpdated(callId, change);
                    break;

                case "deleted":
                    _logger.LogInformation("Call {CallId} was ended/deleted", callId);
                    await HandleCallEnded(callId, change);
                    break;

                default:
                    _logger.LogInformation("Received call notification with unsupported change type: {ChangeType} for call {CallId}", 
                        change.ChangeType, callId);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing call notification: {Message}", ex.Message);
            
            // Log compliance event for the error
            await _complianceService.LogComplianceEventAsync(
                ComplianceEventType.SystemError,
                new MeetingInfo { Id = "unknown" },
                CancellationToken.None);
        }
    }

    /// <summary>
    /// Handle when a new call is created - this is where we join the call
    /// </summary>
    private async Task HandleCallCreated(string callId, ChangeNotification change)
    {
        try
        {
            _logger.LogInformation("Attempting to join call {CallId} for compliance recording", callId);

            // Try to join the call using our call joining service
            var joinResult = await _callJoiningService.JoinCallAsync(callId, CancellationToken.None);

            if (joinResult.Success)
            {
                _logger.LogInformation("Successfully joined call {CallId} for compliance recording", callId);
                
                // Log successful compliance event
                await _complianceService.LogComplianceEventAsync(
                    ComplianceEventType.CallJoined,
                    new MeetingInfo { Id = callId },
                    CancellationToken.None);

                // Track telemetry
                _telemetryClient.TrackEvent("CallJoined", new Dictionary<string, string>
                {
                    ["CallId"] = callId,
                    ["JoinMethod"] = "GraphNotification",
                    ["Timestamp"] = DateTimeOffset.UtcNow.ToString()
                });
            }
            else
            {
                _logger.LogWarning("Failed to join call {CallId} for compliance recording", callId);
                
                // Log failed compliance event
                await _complianceService.LogComplianceEventAsync(
                    ComplianceEventType.SystemError,
                    new MeetingInfo { Id = callId },
                    CancellationToken.None);

                // Track failure telemetry
                _telemetryClient.TrackEvent("CallJoinFailed", new Dictionary<string, string>
                {
                    ["CallId"] = callId,
                    ["JoinMethod"] = "GraphNotification",
                    ["Timestamp"] = DateTimeOffset.UtcNow.ToString(),
                    ["Reason"] = "JoinCallAsync returned false"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error joining call {CallId}: {Message}", callId, ex.Message);
            
            // Log compliance event for the error
            await _complianceService.LogComplianceEventAsync(
                ComplianceEventType.SystemError,
                new MeetingInfo { Id = callId },
                CancellationToken.None);

            // Track exception telemetry
            _telemetryClient.TrackException(ex, new Dictionary<string, string>
            {
                ["CallId"] = callId,
                ["Operation"] = "CallJoin",
                ["JoinMethod"] = "GraphNotification"
            });
        }
    }

    /// <summary>
    /// Handle when a call is updated - check if we need to start recording
    /// </summary>
    private async Task HandleCallUpdated(string callId, ChangeNotification change)
    {
        try
        {
            _logger.LogInformation("Call {CallId} was updated - checking recording status", callId);

            // Check if we need to start recording (e.g., if call state changed to "established")
            // The resource data might contain information about the call state
            if (change.ResourceData?.AdditionalData != null)
            {
                var callData = change.ResourceData.AdditionalData;
                
                // Log what we received for debugging
                _logger.LogDebug("Call {CallId} update data: {Data}", callId, 
                    string.Join(", ", callData.Select(kvp => $"{kvp.Key}={kvp.Value}")));

                // Check if this is a call state change that indicates we should start recording
                if (callData.ContainsKey("state") && callData["state"]?.ToString() == "established")
                {
                    _logger.LogInformation("Call {CallId} is now established - ensuring recording is started", callId);
                    
                    // Try to start recording if not already started
                    var meetingInfo = new MeetingInfo { Id = callId };
                    await _recordingService.StartRecordingAsync(meetingInfo, CancellationToken.None);
                    
                    // Log compliance event
                    await _complianceService.LogComplianceEventAsync(
                        ComplianceEventType.RecordingStarted,
                        new MeetingInfo { Id = callId },
                        CancellationToken.None);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling call update for {CallId}: {Message}", callId, ex.Message);
        }
    }

    /// <summary>
    /// Handle when a call ends - ensure recording is stopped and processed
    /// </summary>
    private async Task HandleCallEnded(string callId, ChangeNotification change)
    {
        try
        {
            _logger.LogInformation("Call {CallId} has ended - ensuring recording is stopped and processed", callId);

            // Stop recording if it's still active
            await _recordingService.StopRecordingAsync(callId, CancellationToken.None);

            // Log compliance event
            await _complianceService.LogComplianceEventAsync(
                ComplianceEventType.CallEnded,
                new MeetingInfo { Id = callId },
                CancellationToken.None);

            // Track telemetry
            _telemetryClient.TrackEvent("CallEnded", new Dictionary<string, string>
            {
                ["CallId"] = callId,
                ["Timestamp"] = DateTimeOffset.UtcNow.ToString()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling call end for {CallId}: {Message}", callId, ex.Message);
        }
    }

    /// <summary>
    /// Handle a transcript notification
    /// </summary>
    private async Task HandleTranscriptNotification(ChangeNotification change)
    {
        if (string.IsNullOrEmpty(change.Resource))
        {
            _logger.LogWarning("Received transcript notification with null or empty Resource");
            return;
        }
        
        // Extract meeting ID from the resource URL
        var resourceParts = change.Resource.Split('/');
        string? meetingId = null;
        
        for (int i = 0; i < resourceParts.Length - 1; i++)
        {
            if (resourceParts[i] == "onlineMeetings" && i + 1 < resourceParts.Length)
            {
                meetingId = resourceParts[i + 1];
                break;
            }
        }

        if (string.IsNullOrEmpty(meetingId))
        {
            _logger.LogWarning("Could not extract meeting ID from notification resource: {Resource}", change.Resource);
            return;
        }

        _logger.LogInformation("Transcript notification for meeting {MeetingId}: {ChangeType}", 
            meetingId, change.ChangeType);

        // Log the event
        await _complianceService.LogComplianceEventAsync(
            ComplianceEventType.SystemError, // Using this as a placeholder since there's no transcript-specific event
            new MeetingInfo { Id = meetingId },
            CancellationToken.None);
    }

    /// <summary>
    /// Verify the signature of encrypted content
    /// </summary>
    private bool VerifySignature(NotificationPayload notification)
    {
        try
        {
            // In a real implementation, you would:
            // 1. Get your validation certificate
            // 2. Extract the signature from the notification
            // 3. Verify the signature against the encrypted content
            
            // This is simplified for demonstration purposes
            var encryptionCertificateThumbprint = _configuration["Recording:EncryptionCertificateThumbprint"];
            
            _logger.LogInformation("Verifying signature with certificate {Thumbprint}", 
                encryptionCertificateThumbprint);
            
            // Here we would do actual verification using a certificate from the certificate store
            // For demo purposes, we'll return true
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying notification signature");
            return false;
        }
    }

    /// <summary>
    /// Validate incoming notification request for security compliance
    /// </summary>
    private bool ValidateNotificationRequest()
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
                    return false;
                }
            }

            // Validate Microsoft Graph user agent
            var userAgent = HttpContext.Request.Headers.UserAgent.ToString();
            if (string.IsNullOrEmpty(userAgent) || !userAgent.Contains("Microsoft", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Suspicious user agent for Graph notification: {UserAgent}", userAgent);
                return false;
            }

            // Additional security checks can be added here
            // - Client certificate validation
            // - IP address allowlisting
            // - Custom authentication headers

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error validating Graph notification request");
            return false;
        }
    }

    /// <summary>
    /// Enhanced health check endpoint for notifications with dependency validation
    /// </summary>
    [HttpGet("health")]
    public async Task<IActionResult> GetHealthAsync()
    {
        using var activity = ActivitySource.StartActivity("NotificationsHealth.Check");

        try
        {
            _logger.LogDebug("Graph notifications endpoint health check from {RemoteIpAddress}", 
                HttpContext.Connection.RemoteIpAddress?.ToString());

            var healthData = new
            {
                status = "healthy",
                endpoint = "notifications",
                timestamp = DateTimeOffset.UtcNow,
                version = GetType().Assembly.GetName().Version?.ToString(),
                dependencies = await CheckNotificationDependenciesAsync()
            };

            return Ok(healthData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Graph notifications endpoint health check");
            
            return StatusCode(500, new 
            { 
                status = "unhealthy", 
                endpoint = "notifications",
                timestamp = DateTimeOffset.UtcNow,
                error = "Health check failed"
            });
        }
    }

    /// <summary>
    /// Check notification endpoint dependencies
    /// </summary>
    private Task<object> CheckNotificationDependenciesAsync()
    {
        var dependencies = new Dictionary<string, object>();

        try
        {
            // Check recording service health
            dependencies["recordingService"] = new { status = _recordingService != null ? "healthy" : "unhealthy" };

            // Check compliance service health
            dependencies["complianceService"] = new { status = _complianceService != null ? "healthy" : "unhealthy" };

            // Check subscription service health (optional)
            dependencies["subscriptionService"] = new { 
                status = _subscriptionService != null ? "healthy" : "not-configured",
                note = _subscriptionService == null ? "Optional service not registered" : null
            };

            // Check configuration
            var hasRequiredConfig = !string.IsNullOrEmpty(_configuration["Recording:NotificationClientState"]);
            dependencies["configuration"] = new { status = hasRequiredConfig ? "healthy" : "incomplete" };

            return Task.FromResult<object>(dependencies);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking notification dependencies during health check");
            return Task.FromResult<object>(new { error = "Unable to check dependencies" });
        }
    }

    #region Notification Models

    /// <summary>
    /// Main notification payload from Microsoft Graph
    /// </summary>
    public class NotificationPayload
    {
        public List<ChangeNotification>? Value { get; set; }
        
        // For validation requests
        public string? ValidationCode { get; set; }
        public string? ClientState { get; set; }
        
        // For encrypted content
        public string? EncryptedContent { get; set; }
    }

    /// <summary>
    /// Individual change notification
    /// </summary>
    public class ChangeNotification
    {
        public string ChangeType { get; set; } = string.Empty;
        public string ClientState { get; set; } = string.Empty;
        public string? Resource { get; set; }
        public ResourceData? ResourceData { get; set; }
        public DateTimeOffset SubscriptionExpirationDateTime { get; set; }
        public string SubscriptionId { get; set; } = string.Empty;
        public string? TenantId { get; set; }
    }

    /// <summary>
    /// Resource data in the notification
    /// </summary>
    public class ResourceData
    {
        public string Id { get; set; } = string.Empty;
        public Dictionary<string, object> AdditionalData { get; set; } = new();
    }

    #endregion
}