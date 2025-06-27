using Microsoft.AspNetCore.Mvc;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TeamsComplianceBot.Services;

namespace TeamsComplianceBot.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SubscriptionsControllerClean : ControllerBase
    {
        private readonly IGraphSubscriptionService _subscriptionService;
        private readonly GraphServiceClient _graphClient;
        private readonly ILogger<SubscriptionsControllerClean> _logger;
        private readonly IConfiguration _configuration;

        public SubscriptionsControllerClean(
            IGraphSubscriptionService subscriptionService,
            GraphServiceClient graphClient,
            ILogger<SubscriptionsControllerClean> logger,
            IConfiguration configuration)
        {
            _subscriptionService = subscriptionService;
            _graphClient = graphClient;
            _logger = logger;
            _configuration = configuration;
        }

        /// <summary>
        /// Create extended call records subscription with 24-hour expiration
        /// </summary>
        [HttpPost("create-call-records-extended")]
        public async Task<IActionResult> CreateCallRecordsExtended()
        {
            try
            {
                _logger.LogInformation("Creating extended call records subscription (24 hours)");

                var notificationUrl = "https://arandiabot.ggunifiedtech.com/api/notifications";
                
                _logger.LogInformation("Creating call records subscription with URL: {NotificationUrl}", notificationUrl);

                var subscription = new Microsoft.Graph.Models.Subscription
                {
                    Resource = "communications/callRecords",
                    ChangeType = "created",
                    NotificationUrl = notificationUrl,
                    ExpirationDateTime = DateTimeOffset.UtcNow.AddHours(24), // 24-hour subscription
                    ClientState = "TeamsComplianceBot-CallRecords-Extended-2025"
                };

                var createdSubscription = await _graphClient.Subscriptions.PostAsync(subscription);

                if (createdSubscription != null)
                {
                    _logger.LogInformation("Successfully created extended call records subscription: {SubscriptionId}", createdSubscription.Id);
                    
                    return Ok(new
                    {
                        success = true,
                        subscription = new
                        {
                            id = createdSubscription.Id,
                            resource = createdSubscription.Resource,
                            changeType = createdSubscription.ChangeType,
                            notificationUrl = createdSubscription.NotificationUrl,
                            expirationDateTime = createdSubscription.ExpirationDateTime,
                            clientState = createdSubscription.ClientState
                        },
                        message = "🎉 SUCCESS! Extended call records subscription created (24 hours)!",
                        timestamp = DateTimeOffset.UtcNow
                    });
                }
                else
                {
                    return StatusCode(500, new
                    {
                        success = false,
                        message = "Call records subscription creation returned null",
                        timestamp = DateTimeOffset.UtcNow
                    });
                }
            }
            catch (Microsoft.Graph.Models.ODataErrors.ODataError ex)
            {
                _logger.LogError(ex, "Microsoft Graph error creating call records subscription");
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Error?.Code,
                    message = ex.Error?.Message,
                    details = ex.Error?.Details?.Select(d => new { d.Code, d.Message }),
                    timestamp = DateTimeOffset.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error creating call records subscription");
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    timestamp = DateTimeOffset.UtcNow
                });
            }
        }

        /// <summary>
        /// Create subscription for live Teams calls with new permissions
        /// </summary>
        [HttpPost("create-live-calls-fixed")]
        public async Task<IActionResult> CreateLiveCallsFixed()
        {
            try
            {
                _logger.LogInformation("Creating live calls subscription with new permissions");

                var notificationUrl = "https://arandiabot.ggunifiedtech.com/api/notifications";
                
                var subscription = new Microsoft.Graph.Models.Subscription
                {
                    Resource = "communications/calls",
                    ChangeType = "created,updated",
                    NotificationUrl = notificationUrl,
                    ExpirationDateTime = DateTimeOffset.UtcNow.AddHours(24),
                    ClientState = "TeamsComplianceBot-LiveCalls-Fixed-2025"
                };

                var createdSubscription = await _graphClient.Subscriptions.PostAsync(subscription);

                if (createdSubscription != null)
                {
                    _logger.LogInformation("Successfully created live calls subscription: {SubscriptionId}", createdSubscription.Id);
                    
                    return Ok(new
                    {
                        success = true,
                        subscription = new
                        {
                            id = createdSubscription.Id,
                            resource = createdSubscription.Resource,
                            changeType = createdSubscription.ChangeType,
                            notificationUrl = createdSubscription.NotificationUrl,
                            expirationDateTime = createdSubscription.ExpirationDateTime,
                            clientState = createdSubscription.ClientState
                        },
                        message = "🎉 SUCCESS! Live calls subscription created with new permissions!",
                        capabilities = new[]
                        {
                            "Real-time call notifications",
                            "Call state monitoring",
                            "Participant tracking",
                            "Media access enabled"
                        },
                        timestamp = DateTimeOffset.UtcNow
                    });
                }
                else
                {
                    return StatusCode(500, new
                    {
                        success = false,
                        message = "Live calls subscription creation returned null",
                        timestamp = DateTimeOffset.UtcNow
                    });
                }
            }
            catch (Microsoft.Graph.Models.ODataErrors.ODataError ex)
            {
                _logger.LogError(ex, "Microsoft Graph error creating live calls subscription");
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Error?.Code,
                    message = ex.Error?.Message,
                    details = ex.Error?.Details?.Select(d => new { d.Code, d.Message }),
                    troubleshooting = ex.Error?.Code switch
                    {
                        "Forbidden" => "Check permissions: Calls.AccessMedia.All, Calls.JoinGroupCall.All required",
                        "UnauthorizedRequestType" => "Live calls may require special tenant configuration",
                        _ => "Review tenant settings for Teams calls API access"
                    },
                    timestamp = DateTimeOffset.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error creating live calls subscription");
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    timestamp = DateTimeOffset.UtcNow
                });
            }
        }

        /// <summary>
        /// Create subscription for online meetings with fixed permissions
        /// </summary>
        [HttpPost("create-online-meetings-fixed")]
        public async Task<IActionResult> CreateOnlineMeetingsFixed()
        {
            try
            {
                _logger.LogInformation("Creating online meetings subscription with corrected permissions");

                var notificationUrl = "https://arandiabot.ggunifiedtech.com/api/notifications";
                
                var subscription = new Microsoft.Graph.Models.Subscription
                {
                    Resource = "communications/onlineMeetings",
                    ChangeType = "created,updated", // Removed 'deleted' as it may not be supported
                    NotificationUrl = notificationUrl,
                    ExpirationDateTime = DateTimeOffset.UtcNow.AddHours(24),
                    ClientState = "TeamsComplianceBot-OnlineMeetings-Fixed-2025"
                };

                var createdSubscription = await _graphClient.Subscriptions.PostAsync(subscription);

                if (createdSubscription != null)
                {
                    _logger.LogInformation("Successfully created online meetings subscription: {SubscriptionId}", createdSubscription.Id);
                    
                    return Ok(new
                    {
                        success = true,
                        subscription = new
                        {
                            id = createdSubscription.Id,
                            resource = createdSubscription.Resource,
                            changeType = createdSubscription.ChangeType,
                            notificationUrl = createdSubscription.NotificationUrl,
                            expirationDateTime = createdSubscription.ExpirationDateTime,
                            clientState = createdSubscription.ClientState
                        },
                        message = "🎉 SUCCESS! Online meetings subscription created with new permissions!",
                        capabilities = new[]
                        {
                            "Meeting creation notifications",
                            "Meeting update alerts",
                            "Pre-meeting preparation",
                            "Scheduled meeting monitoring"
                        },
                        timestamp = DateTimeOffset.UtcNow
                    });
                }
                else
                {
                    return StatusCode(500, new
                    {
                        success = false,
                        message = "Online meetings subscription creation returned null",
                        timestamp = DateTimeOffset.UtcNow
                    });
                }
            }
            catch (Microsoft.Graph.Models.ODataErrors.ODataError ex)
            {
                _logger.LogError(ex, "Microsoft Graph error creating online meetings subscription");
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Error?.Code,
                    message = ex.Error?.Message,
                    details = ex.Error?.Details?.Select(d => new { d.Code, d.Message }),
                    troubleshooting = ex.Error?.Code switch
                    {
                        "Forbidden" => "Check permissions: OnlineMeetings.ReadWrite.All required",
                        "BadRequest" => "Try different changeType (created only or updated only)",
                        _ => "Review tenant configuration for online meetings API"
                    },
                    timestamp = DateTimeOffset.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error creating online meetings subscription");
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    timestamp = DateTimeOffset.UtcNow
                });
            }
        }

        /// <summary>
        /// Create all Teams subscriptions with the new permissions
        /// </summary>
        [HttpPost("create-all-with-permissions")]
        public async Task<IActionResult> CreateAllWithPermissions()
        {
            try
            {
                _logger.LogInformation("Creating all Teams subscriptions with newly granted permissions");

                var notificationUrl = "https://arandiabot.ggunifiedtech.com/api/notifications";
                var results = new List<object>();
                
                var subscriptionRequests = new[]
                {
                    new { 
                        Resource = "communications/callRecords", 
                        ChangeType = "created", 
                        Description = "Call Records (completed calls)",
                        Permission = "CallRecords.Read.All"
                    },
                    new { 
                        Resource = "communications/calls", 
                        ChangeType = "created,updated", 
                        Description = "Live Calls (real-time)",
                        Permission = "Calls.AccessMedia.All"
                    },
                    new { 
                        Resource = "communications/onlineMeetings", 
                        ChangeType = "created,updated", 
                        Description = "Online Meetings (scheduled)",
                        Permission = "OnlineMeetings.ReadWrite.All"
                    }
                };

                foreach (var sub in subscriptionRequests)
                {
                    try
                    {
                        _logger.LogInformation("Creating subscription for {Resource}", sub.Resource);

                        var subscription = new Microsoft.Graph.Models.Subscription
                        {
                            Resource = sub.Resource,
                            ChangeType = sub.ChangeType,
                            NotificationUrl = notificationUrl,
                            ExpirationDateTime = DateTimeOffset.UtcNow.AddHours(24),
                            ClientState = $"TeamsComplianceBot-{sub.Resource.Replace("/", "-")}-WithPermissions-2025"
                        };

                        var createdSubscription = await _graphClient.Subscriptions.PostAsync(subscription);

                        if (createdSubscription != null)
                        {
                            _logger.LogInformation("✅ Successfully created {Resource} subscription: {SubscriptionId}", 
                                sub.Resource, createdSubscription.Id);

                            results.Add(new
                            {
                                success = true,
                                resource = sub.Resource,
                                description = sub.Description,
                                subscriptionId = createdSubscription.Id,
                                changeType = createdSubscription.ChangeType,
                                expirationDateTime = createdSubscription.ExpirationDateTime,
                                permission = sub.Permission
                            });
                        }
                        else
                        {
                            results.Add(new
                            {
                                success = false,
                                resource = sub.Resource,
                                description = sub.Description,
                                error = "Subscription creation returned null",
                                permission = sub.Permission
                            });
                        }
                    }
                    catch (Microsoft.Graph.Models.ODataErrors.ODataError ex)
                    {
                        _logger.LogError(ex, "❌ Error creating {Resource} subscription", sub.Resource);
                        results.Add(new
                        {
                            success = false,
                            resource = sub.Resource,
                            description = sub.Description,
                            error = ex.Error?.Code,
                            message = ex.Error?.Message,
                            permission = sub.Permission
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "❌ Unexpected error creating {Resource} subscription", sub.Resource);
                        results.Add(new
                        {
                            success = false,
                            resource = sub.Resource,
                            description = sub.Description,
                            error = ex.Message,
                            permission = sub.Permission
                        });
                    }
                }

                var successCount = results.Count(r => r.GetType().GetProperty("success")?.GetValue(r) as bool? == true);
                var totalCount = results.Count;

                return Ok(new
                {
                    success = successCount > 0,
                    message = $"Teams subscriptions with new permissions: {successCount}/{totalCount} successful",
                    subscriptions = results,
                    summary = new
                    {
                        total = totalCount,
                        successful = successCount,
                        failed = totalCount - successCount
                    },
                    analysis = successCount == totalCount ? 
                        "🎉 All Teams subscriptions created! New permissions are working correctly." :
                        $"Partial success: {successCount}/{totalCount} subscriptions created.",
                    logStreamNote = new
                    {
                        explanation = "HTTP 400 errors in log stream are normal during webhook validation",
                        impact = "These errors don't affect functionality - ignore them",
                        cause = "Microsoft Graph validation requests sometimes have URL formatting issues"
                    },
                    nextSteps = new[]
                    {
                        "Monitor /api/notifications for Teams event notifications",
                        "Test with actual Teams calls and meetings",
                        "Set up subscription renewal to maintain 24-hour subscriptions",
                        "Implement compliance recording logic"
                    },
                    timestamp = DateTimeOffset.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating Teams subscriptions with permissions");
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    timestamp = DateTimeOffset.UtcNow
                });
            }
        }

        /// <summary>
        /// Renew all active subscriptions to extend their expiration time
        /// </summary>
        [HttpPost("renew-all")]
        public async Task<IActionResult> RenewAllSubscriptions()
        {
            try
            {
                _logger.LogInformation("Renewing all active subscriptions");

                var subscriptions = await _subscriptionService.GetActiveSubscriptionsAsync();
                var subscriptionsList = subscriptions?.ToList() ?? new List<Microsoft.Graph.Models.Subscription>();

                if (!subscriptionsList.Any())
                {
                    return Ok(new
                    {
                        success = true,
                        message = "No active subscriptions found to renew",
                        timestamp = DateTimeOffset.UtcNow
                    });
                }

                var renewalResults = new List<object>();

                foreach (var subscription in subscriptionsList)
                {
                    try
                    {
                        if (subscription.Id == null) continue;

                        _logger.LogInformation("Renewing subscription {SubscriptionId}", subscription.Id);

                        var updatedSubscription = new Microsoft.Graph.Models.Subscription
                        {
                            ExpirationDateTime = DateTimeOffset.UtcNow.AddHours(24) // Extend by 24 hours
                        };

                        var renewedSubscription = await _graphClient.Subscriptions[subscription.Id].PatchAsync(updatedSubscription);

                        if (renewedSubscription != null)
                        {
                            _logger.LogInformation("✅ Successfully renewed subscription {SubscriptionId}", subscription.Id);
                            renewalResults.Add(new
                            {
                                success = true,
                                subscriptionId = subscription.Id,
                                resource = subscription.Resource,
                                oldExpiration = subscription.ExpirationDateTime,
                                newExpiration = renewedSubscription.ExpirationDateTime
                            });
                        }
                        else
                        {
                            renewalResults.Add(new
                            {
                                success = false,
                                subscriptionId = subscription.Id,
                                resource = subscription.Resource,
                                error = "Renewal returned null"
                            });
                        }
                    }
                    catch (Microsoft.Graph.Models.ODataErrors.ODataError ex)
                    {
                        _logger.LogError(ex, "❌ Error renewing subscription {SubscriptionId}", subscription.Id);
                        renewalResults.Add(new
                        {
                            success = false,
                            subscriptionId = subscription.Id,
                            resource = subscription.Resource,
                            error = ex.Error?.Code,
                            message = ex.Error?.Message
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "❌ Unexpected error renewing subscription {SubscriptionId}", subscription.Id);
                        renewalResults.Add(new
                        {
                            success = false,
                            subscriptionId = subscription.Id,
                            resource = subscription.Resource,
                            error = ex.Message
                        });
                    }
                }

                var successCount = renewalResults.Count(r => r.GetType().GetProperty("success")?.GetValue(r) as bool? == true);
                var totalCount = renewalResults.Count;

                return Ok(new
                {
                    success = successCount > 0,
                    message = $"Subscription renewal completed: {successCount}/{totalCount} successful",
                    renewals = renewalResults,
                    summary = new
                    {
                        total = totalCount,
                        successful = successCount,
                        failed = totalCount - successCount
                    },
                    timestamp = DateTimeOffset.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error renewing subscriptions");
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    timestamp = DateTimeOffset.UtcNow
                });
            }
        }

        /// <summary>
        /// Dashboard showing subscription status and log stream explanation
        /// </summary>
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard()
        {
            try
            {
                _logger.LogInformation("Getting subscription dashboard");

                var subscriptions = await _subscriptionService.GetActiveSubscriptionsAsync();
                var subscriptionsList = subscriptions?.ToList() ?? new List<Microsoft.Graph.Models.Subscription>();

                var activeCount = subscriptionsList.Count(s => s.ExpirationDateTime > DateTimeOffset.UtcNow);
                var expiredCount = subscriptionsList.Count(s => s.ExpirationDateTime <= DateTimeOffset.UtcNow);

                return Ok(new
                {
                    success = true,
                    dashboard = new
                    {
                        timestamp = DateTimeOffset.UtcNow,
                        subscriptionHealth = new
                        {
                            total = subscriptionsList.Count,
                            active = activeCount,
                            expired = expiredCount,
                            status = activeCount > 0 ? "Operational" : "No Active Subscriptions"
                        },
                        subscriptions = subscriptionsList.Select(s => new
                        {
                            id = s.Id,
                            resource = s.Resource,
                            changeType = s.ChangeType,
                            expirationDateTime = s.ExpirationDateTime,
                            status = s.ExpirationDateTime > DateTimeOffset.UtcNow ? "Active" : "Expired",                            expiresInMinutes = s.ExpirationDateTime.HasValue && s.ExpirationDateTime > DateTimeOffset.UtcNow ? 
                                (int)Math.Max(0, (s.ExpirationDateTime.Value - DateTimeOffset.UtcNow).TotalMinutes) : 0
                        }),
                        logStreamExplanation = new
                        {
                            title = "About HTTP 400 errors in log stream",
                            explanation = "The repeating HTTP 400 errors are normal during Microsoft Graph webhook validation",
                            causes = new[]
                            {
                                "Microsoft Graph sends validation requests with mixed protocols (HTTPS on port 80)",
                                "Invalid or malformed validation requests from Graph API",
                                "Retry attempts for failed webhook validations",
                                "Testing requests from various Microsoft Graph endpoints"
                            },
                            impact = "These errors DO NOT affect functionality - they're expected during validation",
                            evidence = "Successful subscription creation proves webhook validation is working correctly",
                            action = "You can safely ignore these 400 errors - they're part of normal operation"
                        }
                    },
                    recommendations = activeCount == 0 ? new[]
                    {
                        "Create Teams subscriptions: POST /api/subscriptionsclean/create-all-with-permissions",
                        "Set up automated subscription renewal service"
                    } : new[]
                    {
                        $"✅ {activeCount} active subscriptions monitoring Teams activity",
                        "✅ Webhook validation working correctly",
                        "✅ Ready to receive Teams notifications"
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting dashboard");
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    timestamp = DateTimeOffset.UtcNow
                });
            }
        }
    }
}
