using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using TeamsComplianceBot.Services;

namespace TeamsComplianceBot.Controllers
{
    /// <summary>
    /// Controller to implement fixes for call joining issues
    /// This controller provides automated fixes for common problems
    /// </summary>
    [Route("api/fix")]
    [ApiController]
    public class CallJoiningFixController : ControllerBase
    {
        private readonly ILogger<CallJoiningFixController> _logger;
        private readonly IConfiguration _configuration;
        private readonly GraphServiceClient _graphClient;
        private readonly IGraphSubscriptionService _subscriptionService;

        public CallJoiningFixController(
            ILogger<CallJoiningFixController> logger,
            IConfiguration configuration,
            GraphServiceClient graphClient,
            IGraphSubscriptionService subscriptionService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _graphClient = graphClient ?? throw new ArgumentNullException(nameof(graphClient));
            _subscriptionService = subscriptionService ?? throw new ArgumentNullException(nameof(subscriptionService));
        }

        /// <summary>
        /// Create necessary Graph subscriptions for call events
        /// </summary>
        [HttpPost("create-subscriptions")]
        public async Task<IActionResult> CreateCallSubscriptionsAsync()
        {
            try
            {
                _logger.LogInformation("Creating Graph subscriptions for call events...");

                var results = new List<object>();
                var subscriptionTypes = new[]
                {
                    new { Resource = "/communications/calls", ChangeType = "created,updated,deleted", Description = "Call events" },
                    new { Resource = "/communications/onlineMeetings", ChangeType = "created,updated,deleted", Description = "Meeting events" }
                };

                foreach (var subType in subscriptionTypes)
                {
                    try
                    {
                        var subscriptionId = await _subscriptionService.CreateSubscriptionAsync(
                            subType.Resource,
                            subType.ChangeType,
                            _configuration["Recording:NotificationClientState"]
                        );

                        results.Add(new
                        {
                            resource = subType.Resource,
                            changeType = subType.ChangeType, 
                            description = subType.Description,
                            subscriptionId = subscriptionId,
                            success = true,
                            message = "Subscription created successfully"
                        });

                        _logger.LogInformation("Created subscription {SubscriptionId} for {Resource}", subscriptionId, subType.Resource);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to create subscription for {Resource}", subType.Resource);
                        results.Add(new
                        {
                            resource = subType.Resource,
                            changeType = subType.ChangeType,
                            description = subType.Description,
                            subscriptionId = (string?)null,
                            success = false,
                            message = ex.Message
                        });
                    }
                }

                var overallSuccess = results.All(r => ((dynamic)r).success);

                return Ok(new
                {
                    fixName = "Create Graph Subscriptions",
                    success = overallSuccess,
                    message = overallSuccess ? "All subscriptions created successfully" : "Some subscriptions failed",
                    details = results,
                    timestamp = DateTimeOffset.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating call subscriptions");
                return StatusCode(500, new
                {
                    fixName = "Create Graph Subscriptions",
                    success = false,
                    message = "Failed to create subscriptions",
                    error = ex.Message,
                    timestamp = DateTimeOffset.UtcNow
                });
            }
        }

        /// <summary>
        /// Force create a test subscription to verify Graph API functionality
        /// </summary>
        [HttpPost("test-subscription")]
        public async Task<IActionResult> CreateTestSubscriptionAsync()
        {
            try
            {
                _logger.LogInformation("Creating test subscription...");

                // Create a short-lived test subscription
                var notificationUrl = _configuration["Recording:NotificationUrl"];
                if (string.IsNullOrEmpty(notificationUrl))
                {
                    return BadRequest(new { message = "NotificationUrl not configured" });
                }

                var subscription = new Subscription
                {
                    Resource = "/communications/calls",
                    ChangeType = "created,updated,deleted",
                    NotificationUrl = notificationUrl,
                    ExpirationDateTime = DateTimeOffset.UtcNow.AddMinutes(10), // Short test duration
                    ClientState = $"test-{Guid.NewGuid()}"
                };

                var createdSubscription = await _graphClient.Subscriptions.PostAsync(subscription);

                return Ok(new
                {
                    message = "Test subscription created successfully",
                    subscriptionId = createdSubscription?.Id,
                    expirationDateTime = createdSubscription?.ExpirationDateTime,
                    resource = createdSubscription?.Resource,
                    notificationUrl = createdSubscription?.NotificationUrl,
                    timestamp = DateTimeOffset.UtcNow,
                    note = "This is a test subscription with short expiration"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating test subscription");
                return StatusCode(500, new
                {
                    message = "Failed to create test subscription",
                    error = ex.Message,
                    possibleCauses = new[]
                    {
                        "Missing Graph API permissions",
                        "Notification URL not accessible",
                        "Authentication issues",
                        "Insufficient privileges"
                    },
                    timestamp = DateTimeOffset.UtcNow
                });
            }
        }

        /// <summary>
        /// Get comprehensive troubleshooting guide
        /// </summary>
        [HttpGet("troubleshooting-guide")]
        public IActionResult GetTroubleshootingGuide()
        {
            return Ok(new
            {
                title = "Teams Bot Call Joining Troubleshooting Guide",
                commonIssues = new[]
                {
                    new
                    {
                        issue = "Bot not receiving call events",
                        symptoms = new[] { "Bot doesn't respond to incoming calls", "No webhook notifications received" },
                        causes = new[] { "Missing Graph subscriptions", "Webhook URL not accessible", "Authentication issues" },
                        solutions = new[] { "Create Graph subscriptions", "Verify webhook endpoint", "Check bot credentials" },
                        apiCall = "POST /api/fix/create-subscriptions"
                    },
                    new
                    {
                        issue = "Permission denied errors",
                        symptoms = new[] { "403 Forbidden responses", "Authorization failures" },
                        causes = new[] { "Missing Graph API permissions", "Admin consent not granted" },
                        solutions = new[] { "Add required permissions in Azure AD", "Grant admin consent" },
                        apiCall = "Check Azure AD app registration"
                    },
                    new
                    {
                        issue = "Configuration problems",
                        symptoms = new[] { "Bot fails to start", "Missing configuration errors" },
                        causes = new[] { "Invalid app credentials", "Missing connection strings" },
                        solutions = new[] { "Verify app registration", "Check configuration values" },
                        apiCall = "GET /api/diagnostics/quick-check"
                    }
                },
                requiredPermissions = new[]
                {
                    "Calls.AccessMedia.All",
                    "Calls.Initiate.All",
                    "Calls.JoinGroupCall.All",
                    "Calls.JoinGroupCallAsGuest.All",
                    "OnlineMeetings.ReadWrite.All",
                    "Subscription.ReadWrite.All"
                },
                diagnosticEndpoints = new[]
                {
                    "GET /api/diagnostics/call-joining - Full diagnostic scan",
                    "GET /api/diagnostics/quick-check - Quick health check", 
                    "POST /api/fix/test-subscription - Test Graph subscription creation",
                    "POST /api/fix/create-subscriptions - Create required subscriptions"
                },
                manualSteps = new[]
                {
                    "1. Verify Azure AD app registration has calling permissions",
                    "2. Grant admin consent for all application permissions",
                    "3. Configure calling webhook URL in app registration",
                    "4. Test bot endpoints are publicly accessible",
                    "5. Create Graph subscriptions for call events",
                    "6. Test with actual Teams calls",
                    "7. Monitor application logs during testing"
                }
            });
        }
    }
}
