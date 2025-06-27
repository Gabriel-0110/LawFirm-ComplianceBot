using System.Net;
using System.Text.Json;

namespace TeamsComplianceBot.Middleware
{
    /// <summary>
    /// Global exception handling middleware to catch and log unhandled exceptions
    /// </summary>
    public class GlobalExceptionHandlerMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;
        private readonly IWebHostEnvironment _environment;

        public GlobalExceptionHandlerMiddleware(
            RequestDelegate next, 
            ILogger<GlobalExceptionHandlerMiddleware> logger,
            IWebHostEnvironment environment)
        {
            _next = next;
            _logger = logger;
            _environment = environment;
        }        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                // Handle OPTIONS requests for CORS preflight
                if (context.Request.Method == "OPTIONS")
                {
                    context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
                    context.Response.Headers.Append("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
                    context.Response.Headers.Append("Access-Control-Allow-Headers", "Content-Type, Authorization, X-Correlation-ID");
                    context.Response.Headers.Append("Access-Control-Expose-Headers", "X-Correlation-ID");
                    context.Response.Headers.Append("Access-Control-Max-Age", "86400"); // 24 hours
                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                    return;
                }

                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred while processing the request. Request: {Method} {Path}", 
                    context.Request.Method, context.Request.Path);

                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";

            var response = new
            {
                error = new
                {
                    message = _environment.IsDevelopment() ? exception.Message : "An error occurred while processing your request.",
                    details = _environment.IsDevelopment() ? exception.ToString() : null,
                    timestamp = DateTime.UtcNow,
                    requestId = context.TraceIdentifier
                }
            };

            // Determine appropriate status code based on exception type
            context.Response.StatusCode = exception switch
            {
                ArgumentException => (int)HttpStatusCode.BadRequest,
                UnauthorizedAccessException => (int)HttpStatusCode.Unauthorized,
                NotSupportedException => (int)HttpStatusCode.NotImplemented,
                TimeoutException => (int)HttpStatusCode.RequestTimeout,
                _ => (int)HttpStatusCode.InternalServerError
            };

            var jsonResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await context.Response.WriteAsync(jsonResponse);
        }
    }
}
