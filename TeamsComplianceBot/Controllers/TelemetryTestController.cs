using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.AspNetCore.Mvc;

namespace TeamsComplianceBot.Controllers
{    [ApiController]
    [Route("api/[controller]")]
    public class TelemetryTestController : ControllerBase
    {
        private readonly TelemetryClient _telemetryClient;
        private readonly ILogger<TelemetryTestController> _logger;
        private readonly IConfiguration _configuration;

        public TelemetryTestController(TelemetryClient telemetryClient, ILogger<TelemetryTestController> logger, IConfiguration configuration)
        {
            _telemetryClient = telemetryClient;
            _logger = logger;
            _configuration = configuration;
        }

        [HttpGet("test")]
        public IActionResult TestTelemetry()
        {
            try
            {
                // Log different types of telemetry
                _logger.LogInformation("Test telemetry endpoint called at {Timestamp}", DateTime.UtcNow);
                
                // Track custom event
                _telemetryClient.TrackEvent("TelemetryTest", new Dictionary<string, string>
                {
                    {"TestType", "Manual"},
                    {"Timestamp", DateTime.UtcNow.ToString("O")},
                    {"CorrelationId", HttpContext.TraceIdentifier}
                });

                // Track custom metrics
                _telemetryClient.TrackMetric("TelemetryTestCounter", 1);
                
                // Track dependency (simulate)
                _telemetryClient.TrackDependency("HTTP", "TestEndpoint", "GET /api/telemetrytest/test", DateTime.UtcNow.AddSeconds(-1), TimeSpan.FromMilliseconds(100), true);

                // Track trace
                _telemetryClient.TrackTrace("Telemetry test executed successfully", SeverityLevel.Information);

                // Ensure telemetry is flushed
                _telemetryClient.Flush();

                return Ok(new
                {
                    status = "success",
                    message = "Telemetry test completed",
                    timestamp = DateTime.UtcNow,
                    correlationId = HttpContext.TraceIdentifier,
                    telemetryClient = new
                    {
                        instrumentationKey = _telemetryClient.InstrumentationKey,
                        isEnabled = _telemetryClient.IsEnabled()
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in telemetry test");
                _telemetryClient.TrackException(ex);
                _telemetryClient.Flush();
                
                return StatusCode(500, new
                {
                    status = "error",
                    message = ex.Message,
                    timestamp = DateTime.UtcNow
                });
            }
        }        [HttpGet("config")]
        public IActionResult GetTelemetryConfig()
        {
            var connectionStringFromConfig = _configuration.GetConnectionString("ApplicationInsights");
            var connectionStringFromSection = _configuration["ApplicationInsights:ConnectionString"];
            var connectionStringFromEnv = Environment.GetEnvironmentVariable("ApplicationInsights__ConnectionString");            // Parse instrumentation key from connection string
            string? expectedInstrumentationKey = null;
            if (!string.IsNullOrEmpty(connectionStringFromSection))
            {
                var parts = connectionStringFromSection.Split(';');
                var ikPart = parts.FirstOrDefault(p => p.StartsWith("InstrumentationKey=", StringComparison.OrdinalIgnoreCase));
                if (ikPart != null)
                {
                    expectedInstrumentationKey = ikPart.Substring("InstrumentationKey=".Length);
                }
            }

            return Ok(new
            {
                instrumentationKey = _telemetryClient.InstrumentationKey,
                expectedInstrumentationKey = expectedInstrumentationKey,
                instrumentationKeyMatch = _telemetryClient.InstrumentationKey == expectedInstrumentationKey,
                isEnabled = _telemetryClient.IsEnabled(),
                configuration = new
                {
                    connectionStringFromConfig = connectionStringFromConfig,
                    connectionStringFromSection = connectionStringFromSection,
                    connectionStringFromEnv = connectionStringFromEnv,
                    hasAnyConnectionString = !string.IsNullOrEmpty(connectionStringFromConfig) || 
                                           !string.IsNullOrEmpty(connectionStringFromSection) ||
                                           !string.IsNullOrEmpty(connectionStringFromEnv)
                },
                context = new
                {
                    applicationVersion = _telemetryClient.Context.Component.Version,
                    roleName = _telemetryClient.Context.Cloud.RoleName,
                    operationId = _telemetryClient.Context.Operation.Id,
                    sessionId = _telemetryClient.Context.Session.Id
                },
                timestamp = DateTime.UtcNow
            });
        }
    }
}
