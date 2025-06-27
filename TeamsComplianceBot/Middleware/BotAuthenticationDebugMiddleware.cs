using Microsoft.Bot.Connector.Authentication;
using System.Text.Json;

namespace TeamsComplianceBot.Middleware;

/// <summary>
/// Middleware to log and debug Bot Framework authentication issues
/// </summary>
public class BotAuthenticationDebugMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<BotAuthenticationDebugMiddleware> _logger;

    public BotAuthenticationDebugMiddleware(RequestDelegate next, ILogger<BotAuthenticationDebugMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {        // Only log for bot message endpoints
        if (context.Request.Path.StartsWithSegments("/api/messages"))
        {
            LogBotRequestDetails(context);
        }

        await _next(context);
    }    private void LogBotRequestDetails(HttpContext context)
    {
        try
        {
            var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? Guid.NewGuid().ToString();
            
            // Log request headers (excluding sensitive auth tokens)
            var headers = context.Request.Headers
                .Where(h => !h.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(h => h.Key, h => h.Value.ToString());

            // Log authorization header presence and format
            var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
            var authInfo = "None";
            if (!string.IsNullOrEmpty(authHeader))
            {
                if (authHeader.StartsWith("Bearer "))
                {
                    var token = authHeader.Substring(7);
                    authInfo = $"Bearer token (length: {token.Length})";
                    
                    // Try to decode JWT header for debugging (without validating)
                    try
                    {
                        var parts = token.Split('.');
                        if (parts.Length >= 2)
                        {
                            var headerBytes = Convert.FromBase64String(AddPadding(parts[0]));
                            var headerJson = System.Text.Encoding.UTF8.GetString(headerBytes);
                            var header = JsonSerializer.Deserialize<JsonElement>(headerJson);
                            
                            var alg = header.TryGetProperty("alg", out var algProp) ? algProp.GetString() : "unknown";
                            var typ = header.TryGetProperty("typ", out var typProp) ? typProp.GetString() : "unknown";
                            
                            authInfo += $", alg: {alg}, typ: {typ}";
                        }
                    }
                    catch (Exception ex)
                    {
                        authInfo += $", JWT decode error: {ex.Message}";
                    }
                }
                else
                {
                    authInfo = $"Non-Bearer: {authHeader.Split(' ')[0]}";
                }
            }

            _logger.LogDebug("Bot request debug for {CorrelationId}:", correlationId);
            _logger.LogDebug("  - Method: {Method}", context.Request.Method);
            _logger.LogDebug("  - Content-Type: {ContentType}", context.Request.ContentType);
            _logger.LogDebug("  - Content-Length: {ContentLength}", context.Request.ContentLength);
            _logger.LogDebug("  - Authorization: {AuthInfo}", authInfo);
            _logger.LogDebug("  - User-Agent: {UserAgent}", context.Request.Headers.UserAgent.ToString());
            _logger.LogDebug("  - Remote IP: {RemoteIP}", context.Connection.RemoteIpAddress);
            _logger.LogDebug("  - Headers: {@Headers}", headers);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to log bot request debug information");
        }
    }

    private static string AddPadding(string base64)
    {
        // Add padding to base64 string if needed
        var padding = 4 - (base64.Length % 4);
        if (padding != 4)
        {
            base64 += new string('=', padding);
        }
        return base64;
    }
}
