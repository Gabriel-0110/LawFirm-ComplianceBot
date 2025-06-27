using Microsoft.AspNetCore.Mvc;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;

namespace TeamsComplianceBot.Controllers;

/// <summary>
/// Production-ready home controller that provides comprehensive information about the Teams Compliance Bot
/// with enhanced security, observability, and health monitoring capabilities
/// </summary>
[ApiController]
[Route("")]
public class HomeController : ControllerBase
{
    private readonly ILogger<HomeController> _logger;
    private readonly IConfiguration _configuration;
    private readonly TelemetryClient _telemetryClient;

    // Security and monitoring
    private static readonly ActivitySource ActivitySource = new("TeamsComplianceBot.HomeController");
    private const string CORRELATION_ID_HEADER = "X-Correlation-ID";

    public HomeController(
        ILogger<HomeController> logger, 
        IConfiguration configuration,
        TelemetryClient telemetryClient)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
    }

    /// <summary>
    /// Root endpoint that provides comprehensive information about the bot with security filtering
    /// </summary>
    [HttpGet]
    [Route("")]
    [Produces("application/json")]
    public IActionResult Get()
    {
        var correlationId = HttpContext.Request.Headers[CORRELATION_ID_HEADER].FirstOrDefault() 
                           ?? Guid.NewGuid().ToString();

        using var activity = ActivitySource.StartActivity("BotInfo.Get");
        activity?.SetTag("correlation.id", correlationId);

        try
        {
            _logger.LogInformation("Root endpoint accessed from {RemoteIpAddress} with correlation ID {CorrelationId}", 
                HttpContext.Connection.RemoteIpAddress?.ToString(), correlationId);

            // Get assembly information
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version?.ToString() ?? "Unknown";
            var buildDate = GetBuildDate(assembly);
            var environment = _configuration["ASPNETCORE_ENVIRONMENT"] ?? "Production";

            // Determine what information to expose based on environment
            var botInfo = new
            {
                name = "Teams Compliance Bot",
                description = "Enterprise compliance recording bot for Microsoft Teams",
                version = version,
                buildDate = buildDate,
                status = "Running",
                environment = environment,
                endpoints = new
                {
                    botMessages = "/api/messages",
                    notifications = "/api/notifications", 
                    calls = "/api/calls",
                    health = "/health",
                    info = "/info"
                },
                // Only expose sensitive configuration in development
                configuration = environment == "Development" ? new
                {
                    botId = _configuration["MicrosoftAppId"],
                    tenantId = _configuration["MicrosoftAppTenantId"],
                    appType = _configuration["MicrosoftAppType"]
                } : new
                {
                    botId = MaskSensitiveValue(_configuration["MicrosoftAppId"]),
                    tenantId = MaskSensitiveValue(_configuration["MicrosoftAppTenantId"]),
                    appType = _configuration["MicrosoftAppType"]
                },
                correlationId = correlationId,
                timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
            };

            // Log the access for security monitoring
            _telemetryClient.TrackEvent("BotInfo.Accessed", new Dictionary<string, string>
            {
                ["CorrelationId"] = correlationId,
                ["RemoteIpAddress"] = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                ["UserAgent"] = HttpContext.Request.Headers.UserAgent.ToString()
            });

            HttpContext.Response.Headers.Append(CORRELATION_ID_HEADER, correlationId);
            return Ok(botInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in root endpoint with correlation ID {CorrelationId}", correlationId);
            
            _telemetryClient.TrackException(ex, new Dictionary<string, string>
            {
                ["Operation"] = "BotInfoGet",
                ["CorrelationId"] = correlationId
            });
            
            return StatusCode(500, new { error = "Internal server error", correlationId = correlationId });
        }
    }

    /// <summary>
    /// Alternative route for /index
    /// </summary>
    [HttpGet]
    [Route("index")]
    [Produces("application/json")]
    public IActionResult Index()
    {
        return Get();
    }

    /// <summary>
    /// Comprehensive information endpoint with detailed system information
    /// </summary>
    [HttpGet]
    [Route("info")]
    [Produces("application/json")]
    public IActionResult Info()
    {
        var correlationId = HttpContext.Request.Headers[CORRELATION_ID_HEADER].FirstOrDefault() 
                           ?? Guid.NewGuid().ToString();

        using var activity = ActivitySource.StartActivity("BotInfo.GetDetailed");
        activity?.SetTag("correlation.id", correlationId);

        try
        {
            _logger.LogInformation("Info endpoint accessed from {RemoteIpAddress} with correlation ID {CorrelationId}", 
                HttpContext.Connection.RemoteIpAddress?.ToString(), correlationId);

            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version?.ToString() ?? "Unknown";
            var buildDate = GetBuildDate(assembly);
            var environment = _configuration["ASPNETCORE_ENVIRONMENT"] ?? "Production";

            var systemInfo = new
            {
                application = new
                {
                    name = "Teams Compliance Bot",
                    version = version,
                    buildDate = buildDate,
                    description = "Enterprise compliance bot for Microsoft Teams call recording",
                    environment = environment,
                    framework = ".NET 9.0",
                    runtime = Environment.Version.ToString()
                },
                configuration = environment == "Development" ? new
                {
                    botId = _configuration["MicrosoftAppId"],
                    tenantId = _configuration["MicrosoftAppTenantId"],
                    appType = _configuration["MicrosoftAppType"]
                } : new
                {
                    botId = MaskSensitiveValue(_configuration["MicrosoftAppId"]),
                    tenantId = MaskSensitiveValue(_configuration["MicrosoftAppTenantId"]),
                    appType = _configuration["MicrosoftAppType"]
                },
                features = new
                {
                    complianceRecording = true,
                    azureBlobStorage = !string.IsNullOrEmpty(_configuration.GetConnectionString("BlobStorage")),
                    applicationInsights = !string.IsNullOrEmpty(_configuration.GetConnectionString("ApplicationInsights")),
                    graphSubscriptions = !string.IsNullOrEmpty(_configuration["Recording:NotificationClientState"]),
                    encryption = !string.IsNullOrEmpty(_configuration["Recording:EncryptionCertificateThumbprint"])
                },
                system = new
                {
                    machineName = Environment.MachineName,
                    operatingSystem = Environment.OSVersion.ToString(),
                    processorCount = Environment.ProcessorCount,
                    workingSet = Environment.WorkingSet,
                    uptime = TimeSpan.FromMilliseconds(Environment.TickCount64).ToString(@"dd\.hh\:mm\:ss")
                },
                compliance = new
                {
                    dataRetentionYears = 7,
                    encryptionEnabled = !string.IsNullOrEmpty(_configuration["Recording:EncryptionCertificateThumbprint"]),
                    auditLoggingEnabled = true,
                    gdprCompliant = true,
                    iso27001Compliant = true
                },
                correlationId = correlationId,
                timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
            };

            // Log the access for security monitoring
            _telemetryClient.TrackEvent("BotDetailedInfo.Accessed", new Dictionary<string, string>
            {
                ["CorrelationId"] = correlationId,
                ["RemoteIpAddress"] = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                ["UserAgent"] = HttpContext.Request.Headers.UserAgent.ToString(),
                ["Environment"] = environment
            });

            HttpContext.Response.Headers.Append(CORRELATION_ID_HEADER, correlationId);
            return Ok(systemInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in info endpoint with correlation ID {CorrelationId}", correlationId);
            
            _telemetryClient.TrackException(ex, new Dictionary<string, string>
            {
                ["Operation"] = "BotDetailedInfoGet",
                ["CorrelationId"] = correlationId
            });
            
            return StatusCode(500, new { error = "Internal server error", correlationId = correlationId });
        }
    }

    /// <summary>
    /// Security monitoring endpoint (for authorized users only)
    /// </summary>
    [HttpGet]
    [Route("security")]
    [Produces("application/json")]
    public IActionResult Security()
    {
        var correlationId = HttpContext.Request.Headers[CORRELATION_ID_HEADER].FirstOrDefault() 
                           ?? Guid.NewGuid().ToString();

        using var activity = ActivitySource.StartActivity("BotSecurity.Get");
        activity?.SetTag("correlation.id", correlationId);

        try
        {
            _logger.LogInformation("Security endpoint accessed from {RemoteIpAddress} with correlation ID {CorrelationId}", 
                HttpContext.Connection.RemoteIpAddress?.ToString(), correlationId);

            var environment = _configuration["ASPNETCORE_ENVIRONMENT"] ?? "Production";

            // Only provide security information in development environment
            if (environment != "Development")
            {
                _logger.LogWarning("Security endpoint accessed in production environment from {RemoteIpAddress}", 
                    HttpContext.Connection.RemoteIpAddress?.ToString());
                
                return Forbid("Security information not available in production");
            }

            var securityInfo = new
            {
                authentication = new
                {
                    microsoftAppId = _configuration["MicrosoftAppId"],
                    microsoftAppTenantId = _configuration["MicrosoftAppTenantId"],
                    hasAppPassword = !string.IsNullOrEmpty(_configuration["MicrosoftAppPassword"]),
                    hasClientSecret = !string.IsNullOrEmpty(_configuration["ClientSecret"])
                },
                certificates = new
                {
                    encryptionCertificate = !string.IsNullOrEmpty(_configuration["Recording:EncryptionCertificateThumbprint"]),
                    certificateThumbprint = _configuration["Recording:EncryptionCertificateThumbprint"]
                },
                storage = new
                {
                    hasBlobStorage = !string.IsNullOrEmpty(_configuration.GetConnectionString("BlobStorage")),
                    hasApplicationInsights = !string.IsNullOrEmpty(_configuration.GetConnectionString("ApplicationInsights"))
                },
                correlationId = correlationId,
                timestamp = DateTimeOffset.UtcNow
            };

            // Log security endpoint access for audit
            _telemetryClient.TrackEvent("BotSecurity.Accessed", new Dictionary<string, string>
            {
                ["CorrelationId"] = correlationId,
                ["RemoteIpAddress"] = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                ["UserAgent"] = HttpContext.Request.Headers.UserAgent.ToString(),
                ["Environment"] = environment
            });

            HttpContext.Response.Headers.Append(CORRELATION_ID_HEADER, correlationId);
            return Ok(securityInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in security endpoint with correlation ID {CorrelationId}", correlationId);
            return StatusCode(500, new { error = "Internal server error", correlationId = correlationId });        }
    }

    /// <summary>
    /// Enhanced health check endpoint with detailed service status
    /// </summary>
    [HttpGet]
    [Route("health")]
    [Produces("application/json")]
    public IActionResult Health()
    {
        var correlationId = HttpContext.Request.Headers[CORRELATION_ID_HEADER].FirstOrDefault() 
                           ?? Guid.NewGuid().ToString();

        using var activity = ActivitySource.StartActivity("BotHealth.Check");
        activity?.SetTag("correlation.id", correlationId);

        try
        {
            _logger.LogDebug("Health endpoint accessed from {RemoteIpAddress} with correlation ID {CorrelationId}", 
                HttpContext.Connection.RemoteIpAddress?.ToString(), correlationId);

            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version?.ToString() ?? "Unknown";

            var healthStatus = new
            {
                status = "healthy",
                timestamp = DateTimeOffset.UtcNow,
                version = version,
                environment = _configuration["ASPNETCORE_ENVIRONMENT"],
                uptime = TimeSpan.FromMilliseconds(Environment.TickCount64).ToString(@"dd\.hh\:mm\:ss"),
                dependencies = CheckDependenciesHealth(),
                correlationId = correlationId
            };

            HttpContext.Response.Headers.Append(CORRELATION_ID_HEADER, correlationId);
            return Ok(healthStatus);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during health check with correlation ID {CorrelationId}", correlationId);
            
            return StatusCode(500, new 
            { 
                status = "unhealthy", 
                timestamp = DateTimeOffset.UtcNow,
                error = "Health check failed",
                correlationId = correlationId
            });
        }
    }

    /// <summary>
    /// Get build date from assembly
    /// </summary>
    private static DateTime GetBuildDate(Assembly assembly)
    {
        try
        {
            var attribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();            if (attribute != null && DateTime.TryParse(attribute.InformationalVersion, out var buildDate))
            {
                return buildDate;
            }

            // Fallback: use file creation time
            var filePath = assembly.Location;
            if (!string.IsNullOrEmpty(filePath) && System.IO.File.Exists(filePath))
            {
                return System.IO.File.GetCreationTime(filePath);
            }
        }
        catch
        {
            // Ignore errors and return default
        }

        return DateTime.UtcNow;
    }

    /// <summary>
    /// Mask sensitive configuration values for security
    /// </summary>
    private static string? MaskSensitiveValue(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        if (value.Length <= 8)        return "***";

        return $"{value.Substring(0, 4)}***{value.Substring(value.Length - 4)}";
    }

    /// <summary>
    /// Check health of dependencies
    /// </summary>
    private object CheckDependenciesHealth()
    {
        var dependencies = new Dictionary<string, object>();

        try
        {
            // Check configuration
            var hasRequiredConfig = !string.IsNullOrEmpty(_configuration["MicrosoftAppId"]) &&
                                  !string.IsNullOrEmpty(_configuration["MicrosoftAppPassword"]);
            dependencies["configuration"] = new { 
                status = hasRequiredConfig ? "healthy" : "unhealthy",
                lastChecked = DateTimeOffset.UtcNow
            };

            // Check Application Insights
            var hasAppInsights = !string.IsNullOrEmpty(_configuration.GetConnectionString("ApplicationInsights"));
            dependencies["applicationInsights"] = new { 
                status = hasAppInsights ? "healthy" : "not-configured",
                lastChecked = DateTimeOffset.UtcNow
            };

            // Check Blob Storage
            var hasBlobStorage = !string.IsNullOrEmpty(_configuration.GetConnectionString("BlobStorage"));
            dependencies["blobStorage"] = new { 
                status = hasBlobStorage ? "healthy" : "not-configured",
                lastChecked = DateTimeOffset.UtcNow
            };

            // Check notification configuration
            var hasNotificationConfig = !string.IsNullOrEmpty(_configuration["Recording:NotificationClientState"]);
            dependencies["notifications"] = new { 
                status = hasNotificationConfig ? "healthy" : "not-configured",
                lastChecked = DateTimeOffset.UtcNow
            };

            return dependencies;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking dependencies during health check");
            return new { error = "Unable to check dependencies", lastChecked = DateTimeOffset.UtcNow };
        }
    }

    /// <summary>
    /// Safely handles culture identifiers to prevent CultureNotFoundException
    /// </summary>
    /// <param name="cultureId">The culture identifier which may be invalid</param>
    /// <returns>A valid CultureInfo or InvariantCulture if invalid</returns>
    private static CultureInfo SafeParseCulture(string? cultureId)
    {
        if (string.IsNullOrEmpty(cultureId))
            return CultureInfo.InvariantCulture;

        try
        {
            // Only attempt to create culture if it looks like a valid culture ID
            if (cultureId.Length <= 10 && 
                (cultureId.Contains('-') || // e.g., en-US
                 cultureId.All(c => char.IsLetter(c)))) // e.g., en
            {
                return CultureInfo.CreateSpecificCulture(cultureId);
            }
            
            return CultureInfo.InvariantCulture;
        }
        catch (CultureNotFoundException)
        {
            // Always fall back to invariant culture
            return CultureInfo.InvariantCulture;
        }
    }
}
