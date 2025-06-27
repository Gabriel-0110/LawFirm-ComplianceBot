using System.Globalization;

namespace TeamsComplianceBot.Middleware;

/// <summary>
/// Critical middleware to prevent CultureNotFoundException by ensuring thread safety and culture validation
/// This middleware MUST run first in the pipeline to prevent corrupted culture data from crashing the app
/// </summary>
public class CultureSafetyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CultureSafetyMiddleware> _logger;

    public CultureSafetyMiddleware(RequestDelegate next, ILogger<CultureSafetyMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            // CRITICAL: Ensure thread culture safety before processing any request
            EnsureCultureSafety();

            // Validate and sanitize Accept-Language header to prevent corrupted culture data
            SanitizeAcceptLanguageHeader(context);

            await _next(context);
        }
        catch (CultureNotFoundException ex)
        {
            _logger.LogError(ex, "CultureNotFoundException intercepted in CultureSafetyMiddleware. " +
                           "Invalid culture: '{InvalidCulture}', Message: {Message}", 
                           ex.InvalidCultureName, ex.Message);

            // Force reset to safe culture
            EnsureCultureSafety();

            // Return a safe error response
            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"error\":\"Culture configuration error\",\"code\":\"CULTURE_ERROR\"}");
            return;
        }
        catch (Exception ex) when (ex.Message.Contains("culture") || ex.Message.Contains("Culture"))
        {
            _logger.LogError(ex, "Culture-related exception intercepted: {Message}", ex.Message);

            // Force reset to safe culture
            EnsureCultureSafety();

            // Return a safe error response
            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"error\":\"Culture processing error\",\"code\":\"CULTURE_PROCESSING_ERROR\"}");
            return;
        }
    }

    private void EnsureCultureSafety()
    {
        try
        {
            // Set thread culture to invariant to prevent any culture-related issues
            var invariantCulture = CultureInfo.InvariantCulture;
            
            Thread.CurrentThread.CurrentCulture = invariantCulture;
            Thread.CurrentThread.CurrentUICulture = invariantCulture;
            
            CultureInfo.DefaultThreadCurrentCulture = invariantCulture;
            CultureInfo.DefaultThreadCurrentUICulture = invariantCulture;
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "CRITICAL: Failed to set culture safety in middleware");
            // Don't throw - we need to keep the app running
        }
    }

    private void SanitizeAcceptLanguageHeader(HttpContext context)
    {
        try
        {
            if (context.Request.Headers.ContainsKey("Accept-Language"))
            {
                var acceptLanguage = context.Request.Headers["Accept-Language"].ToString();
                
                // Check for corrupted or suspicious culture data
                if (ContainsInvalidCultureData(acceptLanguage))
                {
                    _logger.LogWarning("Removing potentially corrupted Accept-Language header: {AcceptLanguage}", 
                        acceptLanguage);
                    
                    // Remove the corrupted header and set a safe default
                    context.Request.Headers.Remove("Accept-Language");
                    context.Request.Headers.Append("Accept-Language", "en-US");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sanitizing Accept-Language header");
            
            // Remove any potentially problematic headers
            try
            {
                context.Request.Headers.Remove("Accept-Language");
                context.Request.Headers.Append("Accept-Language", "en-US");
            }
            catch
            {
                // Ignore if we can't fix it
            }
        }
    }

    private static bool ContainsInvalidCultureData(string acceptLanguage)
    {
        if (string.IsNullOrEmpty(acceptLanguage))
            return false;

        // Check for signs of corruption - non-printable characters, excessive length, or strange patterns
        if (acceptLanguage.Length > 200) // Reasonable max length for Accept-Language
            return true;

        // Check for non-printable characters or corrupted data patterns
        foreach (char c in acceptLanguage)
        {
            if (char.IsControl(c) && c != '\t' && c != '\r' && c != '\n')
                return true;
                
            // Check for patterns that indicate corruption (like the ones in your error log)
            if (c > 127 && !char.IsLetter(c) && c != '-' && c != ',' && c != ';' && c != '=' && c != '.')
                return true;
        }

        // Check for suspicious patterns that match your error log
        var suspiciousPatterns = new[] { "wgwsww", "����", "�m�", "bf�??", "kr s~" };
        foreach (var pattern in suspiciousPatterns)
        {
            if (acceptLanguage.Contains(pattern))
                return true;
        }

        return false;
    }
}
