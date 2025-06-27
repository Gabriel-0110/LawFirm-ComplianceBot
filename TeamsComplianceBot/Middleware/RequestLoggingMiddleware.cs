using System.Text;
using System.Text.Json;

namespace TeamsComplianceBot.Middleware;

/// <summary>
/// Middleware for logging incoming requests to help diagnose BadRequest (400) errors
/// </summary>
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;
    
    // Only log detailed request information for bot endpoints
    private readonly string[] _monitoredPaths = { "/api/messages", "/api/calls" };

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var shouldMonitor = _monitoredPaths.Any(path => 
            context.Request.Path.StartsWithSegments(path, StringComparison.OrdinalIgnoreCase));

        if (!shouldMonitor)
        {
            await _next(context);
            return;
        }        // Bot Framework uses multiple correlation ID headers - check all of them
        var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault() 
                           ?? context.Request.Headers["x-ms-correlation-id"].FirstOrDefault()
                           ?? context.Request.Headers["MS-CV"].FirstOrDefault()
                           ?? context.Request.Headers["x-ms-client-request-id"].FirstOrDefault()
                           ?? context.Request.Headers["x-ms-request-id"].FirstOrDefault()
                           ?? Guid.NewGuid().ToString();
                           
        // Log which header was used
        var headerSource = "Generated";
        if (context.Request.Headers.ContainsKey("x-ms-correlation-id"))
            headerSource = "x-ms-correlation-id";
        else if (context.Request.Headers.ContainsKey("MS-CV"))
            headerSource = "MS-CV";
        else if (context.Request.Headers.ContainsKey("x-ms-client-request-id"))
            headerSource = "x-ms-client-request-id";
        else if (context.Request.Headers.ContainsKey("x-ms-request-id"))
            headerSource = "x-ms-request-id";
        else if (context.Request.Headers.ContainsKey("X-Correlation-ID"))
            headerSource = "X-Correlation-ID";
            
        _logger.LogDebug("Using correlation ID {CorrelationId} from {HeaderSource}", correlationId, headerSource);
        
        if (!context.Request.Headers.ContainsKey("X-Correlation-ID"))
            context.Request.Headers.Append("X-Correlation-ID", correlationId);

        // Enable buffering to allow multiple reads
        context.Request.EnableBuffering();

        // Log request details
        await LogRequestAsync(context, correlationId);

        // Capture response
        var originalBodyStream = context.Response.Body;
        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        var statusCode = 0;
        try
        {
            await _next(context);
            statusCode = context.Response.StatusCode;
        }
        catch (Exception ex)
        {
            statusCode = 500;
            _logger.LogError(ex, "Unhandled exception in request pipeline for {Path} with correlation ID {CorrelationId}",
                context.Request.Path, correlationId);
            throw;
        }
        finally
        {
            // Log response if it's a 400 BadRequest
            if (statusCode == 400)
            {
                await LogBadRequestResponseAsync(context, correlationId, responseBody);
            }

            // Copy response back to original stream
            context.Response.Body = originalBodyStream;
            responseBody.Seek(0, SeekOrigin.Begin);
            await responseBody.CopyToAsync(originalBodyStream);
        }
    }

    private async Task LogRequestAsync(HttpContext context, string correlationId)
    {
        try
        {
            var request = context.Request;
              _logger.LogInformation("Incoming {Method} request to {Path} with correlation ID {CorrelationId}. " +
                                 "ContentType: {ContentType}, ContentLength: {ContentLength}, UserAgent: {UserAgent}",
                request.Method, request.Path, correlationId, 
                request.ContentType, request.ContentLength, request.Headers.UserAgent.ToString());

            // Log headers (exclude sensitive ones)
            var safeHeaders = request.Headers
                .Where(h => !IsSensitiveHeader(h.Key))
                .ToDictionary(h => h.Key, h => string.Join(", ", h.Value.AsEnumerable()));
            
            _logger.LogDebug("Request headers for {CorrelationId}: {@Headers}", correlationId, safeHeaders);

            // Read and log request body for POST requests
            if (request.Method == "POST" && request.ContentLength > 0)
            {
                request.Body.Position = 0;
                using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
                var body = await reader.ReadToEndAsync();
                request.Body.Position = 0; // Reset for downstream processing

                if (!string.IsNullOrEmpty(body))
                {
                    // For security, only log a preview of the body and check if it's valid JSON
                    var bodyPreview = body.Length > 1000 ? body.Substring(0, 1000) + "..." : body;
                    
                    var isValidJson = IsValidJson(body);
                    _logger.LogDebug("Request body for {CorrelationId} (valid JSON: {IsValidJson}): {BodyPreview}", 
                        correlationId, isValidJson, bodyPreview);

                    if (!isValidJson)
                    {
                        _logger.LogWarning("Invalid JSON detected in request body for {CorrelationId}. Raw body: {RawBody}",
                            correlationId, body);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging request details for correlation ID {CorrelationId}", correlationId);
        }
    }

    private async Task LogBadRequestResponseAsync(HttpContext context, string correlationId, MemoryStream responseBody)
    {
        try
        {
            responseBody.Seek(0, SeekOrigin.Begin);
            var responseContent = await new StreamReader(responseBody).ReadToEndAsync();
            
            _logger.LogWarning("BadRequest (400) response for {Path} with correlation ID {CorrelationId}. " +
                             "Response: {ResponseContent}",
                context.Request.Path, correlationId, responseContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging BadRequest response for correlation ID {CorrelationId}", correlationId);
        }
    }

    private static bool IsSensitiveHeader(string headerName)
    {
        var sensitiveHeaders = new[] { "authorization", "cookie", "x-api-key", "x-auth-token" };
        return sensitiveHeaders.Contains(headerName.ToLowerInvariant());
    }

    private static bool IsValidJson(string json)
    {
        try
        {
            JsonDocument.Parse(json);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
