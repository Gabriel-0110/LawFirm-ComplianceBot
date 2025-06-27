using Microsoft.AspNetCore.Mvc;
using TeamsComplianceBot.Services;

namespace TeamsComplianceBot.Controllers
{
    /// <summary>
    /// Controller for managing call polling as a fallback when Graph subscriptions are not available
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class PollingController : ControllerBase
    {
        private readonly ICallPollingService _pollingService;
        private readonly ILogger<PollingController> _logger;

        public PollingController(
            ICallPollingService pollingService,
            ILogger<PollingController> logger)
        {
            _pollingService = pollingService;
            _logger = logger;
        }

        /// <summary>
        /// Start call polling service
        /// </summary>
        [HttpPost("start")]
        public async Task<IActionResult> StartPolling()
        {
            try
            {
                _logger.LogInformation("Starting call polling service via API");
                await _pollingService.StartPollingAsync();
                
                return Ok(new
                {
                    success = true,
                    message = "Call polling started successfully",
                    isPolling = _pollingService.IsPolling,
                    timestamp = DateTimeOffset.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting call polling service");
                return Ok(new
                {
                    success = false,
                    message = $"Error starting polling: {ex.Message}",
                    timestamp = DateTimeOffset.UtcNow
                });
            }
        }

        /// <summary>
        /// Stop call polling service
        /// </summary>
        [HttpPost("stop")]
        public async Task<IActionResult> StopPolling()
        {
            try
            {
                _logger.LogInformation("Stopping call polling service via API");
                await _pollingService.StopPollingAsync();
                
                return Ok(new
                {
                    success = true,
                    message = "Call polling stopped successfully",
                    isPolling = _pollingService.IsPolling,
                    timestamp = DateTimeOffset.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping call polling service");
                return Ok(new
                {
                    success = false,
                    message = $"Error stopping polling: {ex.Message}",
                    timestamp = DateTimeOffset.UtcNow
                });
            }
        }

        /// <summary>
        /// Get call polling service status
        /// </summary>
        [HttpGet("status")]
        public IActionResult GetPollingStatus()
        {
            try
            {
                return Ok(new
                {
                    isPolling = _pollingService.IsPolling,
                    lastPollTime = _pollingService.LastPollTime,
                    timestamp = DateTimeOffset.UtcNow,
                    message = _pollingService.IsPolling ? "Polling is active" : "Polling is inactive"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting call polling status");
                return Ok(new
                {
                    error = true,
                    message = $"Error getting polling status: {ex.Message}",
                    timestamp = DateTimeOffset.UtcNow
                });
            }
        }

        /// <summary>
        /// Get comprehensive status of call monitoring (subscriptions + polling)
        /// </summary>
        [HttpGet("comprehensive-status")]
        public async Task<IActionResult> GetComprehensiveStatus()
        {
            try
            {
                // Note: This could be enhanced to check subscription status as well
                return Ok(new
                {
                    polling = new
                    {
                        isActive = _pollingService.IsPolling,
                        lastPollTime = _pollingService.LastPollTime
                    },
                    subscriptions = new
                    {
                        isActive = false, // TODO: Check subscription service status
                        count = 0 // TODO: Get active subscription count
                    },
                    recommendation = _pollingService.IsPolling 
                        ? "Call monitoring is active via polling (fallback mode)"
                        : "Call monitoring is not active - consider starting polling or fixing subscription permissions",
                    timestamp = DateTimeOffset.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting comprehensive status");
                return Ok(new
                {
                    error = true,
                    message = $"Error getting comprehensive status: {ex.Message}",
                    timestamp = DateTimeOffset.UtcNow
                });
            }
        }
    }
}
