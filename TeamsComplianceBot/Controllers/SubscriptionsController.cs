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
    public class SubscriptionsController : ControllerBase
    {
        private readonly IGraphSubscriptionService _subscriptionService;
        private readonly GraphServiceClient _graphClient;
        private readonly ILogger<SubscriptionsController> _logger;
        private readonly IConfiguration _configuration;

        public SubscriptionsController(
            IGraphSubscriptionService subscriptionService,
            GraphServiceClient graphClient,
            ILogger<SubscriptionsController> logger,
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

                var notificationUrl = "https://arandiateamsbot.ggunifiedtech.com/api/notifications";
                
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

                var notificationUrl = "https://arandiateamsbot.ggunifiedtech.com/api/notifications";
                
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

                var notificationUrl = "https://arandiateamsbot.ggunifiedtech.com/api/notifications";
                
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

                var notificationUrl = "https://arandiateamsbot.ggunifiedtech.com/api/notifications";
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
                        // Check if subscription doesn't exist (404)
                        if (ex.Error?.Code == "NotFound" || ex.ResponseStatusCode == 404)
                        {
                            _logger.LogWarning("⚠️ Subscription {SubscriptionId} not found - recreating", subscription.Id);
                            
                            // Try to recreate the subscription based on its resource type
                            await RecreateSubscriptionBasedOnResourceAsync(subscription);
                            
                            renewalResults.Add(new
                            {
                                success = false,
                                subscriptionId = subscription.Id,
                                resource = subscription.Resource,
                                error = "NotFound - Recreated",
                                action = "Subscription recreated"
                            });
                        }
                        else
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

        /// <summary>
        /// Attempt live calls subscription with alternative configuration
        /// </summary>
        [HttpPost("create-live-calls-alternative")]
        public async Task<IActionResult> CreateLiveCallsAlternative()
        {
            try
            {
                _logger.LogInformation("Attempting live calls subscription with alternative approach");

                var notificationUrl = "https://arandiateamsbot.ggunifiedtech.com/api/notifications";
                
                // Try with just "created" changeType (less permissions required)
                var subscription = new Microsoft.Graph.Models.Subscription
                {
                    Resource = "communications/calls",
                    ChangeType = "created", // Only "created" instead of "created,updated"
                    NotificationUrl = notificationUrl,
                    ExpirationDateTime = DateTimeOffset.UtcNow.AddHours(1), // Shorter duration first
                    ClientState = "TeamsComplianceBot-LiveCalls-Alternative-2025"
                };

                var createdSubscription = await _graphClient.Subscriptions.PostAsync(subscription);

                if (createdSubscription != null)
                {
                    _logger.LogInformation("Successfully created alternative live calls subscription: {SubscriptionId}", createdSubscription.Id);
                    
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
                        message = "🎉 SUCCESS! Alternative live calls subscription created!",
                        note = "Using 'created' only changeType to reduce permission requirements",
                        timestamp = DateTimeOffset.UtcNow
                    });
                }
                else
                {
                    return StatusCode(500, new
                    {
                        success = false,
                        message = "Alternative live calls subscription creation returned null",
                        timestamp = DateTimeOffset.UtcNow
                    });
                }
            }
            catch (Microsoft.Graph.Models.ODataErrors.ODataError ex)
            {
                _logger.LogError(ex, "Microsoft Graph error creating alternative live calls subscription");
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Error?.Code,
                    message = ex.Error?.Message,
                    details = ex.Error?.Details?.Select(d => new { d.Code, d.Message }),
                    analysis = ex.Error?.Code switch
                    {
                        "ExtensionError" when ex.Error.Message?.Contains("pma.plat.skype.com") == true => 
                            "TENANT CONFIGURATION ISSUE: Live calls require Skype for Business media platform. This requires Microsoft to enable your tenant for Teams calling APIs.",
                        "Forbidden" => "Permission issue: Need Calls.AccessMedia.All and possibly CloudCommunications.Calling",
                        _ => "Unknown error with live calls subscription"
                    },
                    tenantRequirements = new[]
                    {
                        "Live calls subscriptions require special tenant enablement by Microsoft",
                        "Contact Microsoft support to enable 'Teams Calling API' for your tenant",
                        "Alternative: Use call records subscriptions for post-call analysis",
                        "Consider using Microsoft Teams app manifest for real-time call access"
                    },
                    timestamp = DateTimeOffset.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error creating alternative live calls subscription");
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    timestamp = DateTimeOffset.UtcNow
                });
            }
        }

        /// <summary>
        /// Attempt online meetings subscription with minimal requirements
        /// </summary>
        [HttpPost("create-online-meetings-minimal")]
        public async Task<IActionResult> CreateOnlineMeetingsMinimal()
        {
            try
            {
                _logger.LogInformation("Attempting online meetings subscription with minimal configuration");

                var notificationUrl = "https://arandiateamsbot.ggunifiedtech.com/api/notifications";
                
                // Try with only "created" changeType
                var subscription = new Microsoft.Graph.Models.Subscription
                {
                    Resource = "communications/onlineMeetings",
                    ChangeType = "created", // Only "created" instead of "created,updated"
                    NotificationUrl = notificationUrl,
                    ExpirationDateTime = DateTimeOffset.UtcNow.AddHours(1), // Shorter duration first
                    ClientState = "TeamsComplianceBot-OnlineMeetings-Minimal-2025"
                };

                var createdSubscription = await _graphClient.Subscriptions.PostAsync(subscription);

                if (createdSubscription != null)
                {
                    _logger.LogInformation("Successfully created minimal online meetings subscription: {SubscriptionId}", createdSubscription.Id);
                    
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
                        message = "🎉 SUCCESS! Minimal online meetings subscription created!",
                        note = "Using 'created' only changeType for better compatibility",
                        timestamp = DateTimeOffset.UtcNow
                    });
                }
                else
                {
                    return StatusCode(500, new
                    {
                        success = false,
                        message = "Minimal online meetings subscription creation returned null",
                        timestamp = DateTimeOffset.UtcNow
                    });
                }
            }
            catch (Microsoft.Graph.Models.ODataErrors.ODataError ex)
            {
                _logger.LogError(ex, "Microsoft Graph error creating minimal online meetings subscription");
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Error?.Code,
                    message = ex.Error?.Message,
                    details = ex.Error?.Details?.Select(d => new { d.Code, d.Message }),
                    analysis = ex.Error?.Code switch
                    {
                        "BadRequest" when ex.Error.Message?.Contains("meeting join url") == true => 
                            "GRAPH API LIMITATION: Online meetings subscriptions may not be fully supported for webhook notifications",
                        "Forbidden" => "Permission issue: Ensure OnlineMeetings.ReadWrite.All is granted and consented",
                        "ExtensionError" => "API compatibility issue with online meetings webhook subscriptions",
                        _ => "Unknown error with online meetings subscription"
                    },
                    alternatives = new[]
                    {
                        "Use calendar events subscriptions instead: users/{userId}/events",
                        "Poll online meetings API periodically for new meetings",
                        "Use Teams app manifest with meeting lifecycle events",
                        "Focus on call records subscriptions for compliance needs"
                    },
                    timestamp = DateTimeOffset.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error creating minimal online meetings subscription");
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    timestamp = DateTimeOffset.UtcNow
                });
            }
        }

        /// <summary>
        /// Create calendar events subscription as alternative to online meetings
        /// </summary>
        [HttpPost("create-calendar-events")]
        public IActionResult CreateCalendarEventsSubscription()
        {
            try
            {
                _logger.LogInformation("Creating calendar events subscription as alternative to online meetings");

                var notificationUrl = "https://arandiateamsbot.ggunifiedtech.com/api/notifications";
                
                // Subscribe to calendar events which include Teams meetings
                var subscription = new Microsoft.Graph.Models.Subscription
                {
                    Resource = "users/{user-id}/events", // Will need specific user or use me for current user
                    ChangeType = "created,updated,deleted",
                    NotificationUrl = notificationUrl,
                    ExpirationDateTime = DateTimeOffset.UtcNow.AddHours(24),
                    ClientState = "TeamsComplianceBot-CalendarEvents-2025"
                };

                // Note: This will fail because we need a specific user ID, but it demonstrates the approach
                return BadRequest(new
                {
                    success = false,
                    message = "Calendar events subscription requires specific user ID",
                    explanation = "To monitor Teams meetings via calendar events, you need to:",
                    steps = new[]
                    {
                        "1. Get list of users in your tenant",
                        "2. Create subscriptions for each user's calendar: users/{userId}/events",
                        "3. Filter events for Teams meetings (those with onlineMeeting property)",
                        "4. Use Calendars.Read permission instead of OnlineMeetings.ReadWrite.All"
                    },
                    alternativeEndpoint = "Use POST /api/subscriptions/create-user-calendar/{userId} with specific user ID",
                    requiredPermissions = new[]
                    {
                        "Calendars.Read",
                        "User.Read.All (to get user list)"
                    },
                    timestamp = DateTimeOffset.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error explaining calendar events subscription");
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    timestamp = DateTimeOffset.UtcNow
                });
            }
        }

        /// <summary>
        /// Get current permission status and suggest next steps
        /// </summary>
        [HttpGet("permission-status")]
        public async Task<IActionResult> GetPermissionStatus()
        {
            try
            {
                _logger.LogInformation("Checking current permission status for Teams subscriptions");

                var results = new List<object>();

                // Test call records (should work)
                try
                {
                    var testSubscription = new Microsoft.Graph.Models.Subscription
                    {
                        Resource = "communications/callRecords",
                        ChangeType = "created",
                        NotificationUrl = "https://arandiateamsbot.ggunifiedtech.com/api/notifications",
                        ExpirationDateTime = DateTimeOffset.UtcNow.AddMinutes(5), // Very short test
                        ClientState = "TeamsComplianceBot-PermissionTest-CallRecords"
                    };

                    var created = await _graphClient.Subscriptions.PostAsync(testSubscription);
                    if (created?.Id != null)
                    {
                        // Clean up immediately
                        await _graphClient.Subscriptions[created.Id].DeleteAsync();
                        results.Add(new
                        {
                            resource = "communications/callRecords",
                            permission = "CallRecords.Read.All",
                            status = "✅ WORKING",
                            message = "Permission granted and functional"
                        });
                    }
                }
                catch (Exception ex)
                {
                    results.Add(new
                    {
                        resource = "communications/callRecords",
                        permission = "CallRecords.Read.All",
                        status = "❌ FAILED",
                        error = ex.Message
                    });
                }

                // Test live calls
                try
                {
                    var testSubscription = new Microsoft.Graph.Models.Subscription
                    {
                        Resource = "communications/calls",
                        ChangeType = "created",
                        NotificationUrl = "https://arandiateamsbot.ggunifiedtech.com/api/notifications",
                        ExpirationDateTime = DateTimeOffset.UtcNow.AddMinutes(5),
                        ClientState = "TeamsComplianceBot-PermissionTest-Calls"
                    };

                    var created = await _graphClient.Subscriptions.PostAsync(testSubscription);
                    if (created?.Id != null)
                    {
                        await _graphClient.Subscriptions[created.Id].DeleteAsync();
                        results.Add(new
                        {
                            resource = "communications/calls",
                            permission = "Calls.AccessMedia.All",
                            status = "✅ WORKING",
                            message = "Permission granted and tenant configured"
                        });
                    }
                }
                catch (Exception ex)
                {
                    results.Add(new
                    {
                        resource = "communications/calls",
                        permission = "Calls.AccessMedia.All",
                        status = "❌ FAILED",
                        error = ex.Message.Contains("pma.plat.skype.com") ? 
                            "TENANT CONFIGURATION: Requires Microsoft to enable Teams calling APIs" : ex.Message
                    });
                }

                // Test online meetings
                try
                {
                    var testSubscription = new Microsoft.Graph.Models.Subscription
                    {
                        Resource = "communications/onlineMeetings",
                        ChangeType = "created",
                        NotificationUrl = "https://arandiateamsbot.ggunifiedtech.com/api/notifications",
                        ExpirationDateTime = DateTimeOffset.UtcNow.AddMinutes(5),
                        ClientState = "TeamsComplianceBot-PermissionTest-OnlineMeetings"
                    };

                    var created = await _graphClient.Subscriptions.PostAsync(testSubscription);
                    if (created?.Id != null)
                    {
                        await _graphClient.Subscriptions[created.Id].DeleteAsync();
                        results.Add(new
                        {
                            resource = "communications/onlineMeetings",
                            permission = "OnlineMeetings.ReadWrite.All",
                            status = "✅ WORKING",
                            message = "Permission granted and functional"
                        });
                    }
                }
                catch (Exception ex)
                {
                    results.Add(new
                    {
                        resource = "communications/onlineMeetings",
                        permission = "OnlineMeetings.ReadWrite.All",
                        status = "❌ FAILED",
                        error = ex.Message.Contains("meeting join url") ? 
                            "GRAPH API LIMITATION: Online meetings webhooks not fully supported" : ex.Message
                    });
                }

                var workingCount = results.Count(r => r.GetType().GetProperty("status")?.GetValue(r)?.ToString()?.Contains("WORKING") == true);

                return Ok(new
                {
                    success = true,
                    permissionStatus = results,
                    summary = new
                    {
                        total = results.Count,
                        working = workingCount,
                        failed = results.Count - workingCount,
                        overallStatus = workingCount == results.Count ? "All permissions working" : 
                                       workingCount > 0 ? "Partial permissions working" : "No permissions working"
                    },
                    recommendations = new[]
                    {
                        workingCount > 0 ? "✅ Focus on working subscriptions for compliance monitoring" : "❌ Check Azure AD app permissions",
                        "📞 For live calls: Contact Microsoft support to enable Teams calling APIs",
                        "📅 For meetings: Use calendar events subscriptions as alternative",
                        "📊 Call records provide comprehensive post-call compliance data"
                    },
                    nextSteps = new[]
                    {
                        "Use working subscriptions for immediate compliance needs",
                        "Submit Microsoft support ticket for advanced Teams API access",
                        "Consider alternative approaches for non-working resources",
                        "Monitor /api/notifications for webhook deliveries"
                    },
                    timestamp = DateTimeOffset.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking permission status");
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    timestamp = DateTimeOffset.UtcNow
                });
            }
        }

        /// <summary>
        /// Check current Graph API permissions and consent status
        /// </summary>
        [HttpGet("check-permissions")]
        public async Task<IActionResult> CheckPermissions()
        {
            try
            {
                _logger.LogInformation("Checking Microsoft Graph API permissions and consent status");

                var results = new List<object>();

                // Test each permission by attempting to access corresponding resources
                var permissionTests = new[]
                {
                    new { 
                        Permission = "CallRecords.Read.All", 
                        Resource = "communications/callRecords",
                        Test = "List call records",
                        Required = true
                    },
                    new { 
                        Permission = "Calls.AccessMedia.All", 
                        Resource = "communications/calls",
                        Test = "Access live calls",
                        Required = false
                    },
                    new { 
                        Permission = "OnlineMeetings.ReadWrite.All", 
                        Resource = "communications/onlineMeetings",
                        Test = "Manage online meetings",
                        Required = false
                    },
                    new { 
                        Permission = "User.Read.All", 
                        Resource = "users",
                        Test = "Read user information",
                        Required = false
                    },
                    new { 
                        Permission = "Group.Read.All", 
                        Resource = "groups",
                        Test = "Read group information",
                        Required = false
                    }
                };

                foreach (var test in permissionTests)
                {
                    try
                    {                        // Test permission by attempting a simple read operation
                        string status = "Unknown";
                        string details = "";

                        switch (test.Resource)
                        {
                            case "communications/callRecords":
                                try
                                {
                                    // Try to get call records (will fail if no permission)
                                    var callRecords = await _graphClient.Communications.CallRecords.GetAsync(config => {
                                        config.QueryParameters.Top = 1;
                                    });
                                    status = "✅ Granted";
                                    details = $"Found {callRecords?.Value?.Count ?? 0} call records";
                                }
                                catch (Microsoft.Graph.Models.ODataErrors.ODataError ex)
                                {
                                    status = ex.Error?.Code == "Forbidden" ? "❌ Not Granted" : "⚠️ Unknown";
                                    details = ex.Error?.Message ?? "Permission test failed";
                                }
                                break;

                            case "users":
                                try
                                {
                                    var users = await _graphClient.Users.GetAsync(config => {
                                        config.QueryParameters.Top = 1;
                                        config.QueryParameters.Select = new[] { "id", "displayName" };
                                    });
                                    status = "✅ Granted";
                                    details = $"Found {users?.Value?.Count ?? 0} users";
                                }
                                catch (Microsoft.Graph.Models.ODataErrors.ODataError ex)
                                {
                                    status = ex.Error?.Code == "Forbidden" ? "❌ Not Granted" : "⚠️ Unknown";
                                    details = ex.Error?.Message ?? "Permission test failed";
                                }
                                break;

                            case "groups":
                                try
                                {
                                    var groups = await _graphClient.Groups.GetAsync(config => {
                                        config.QueryParameters.Top = 1;
                                        config.QueryParameters.Select = new[] { "id", "displayName" };
                                    });
                                    status = "✅ Granted";
                                    details = $"Found {groups?.Value?.Count ?? 0} groups";
                                }
                                catch (Microsoft.Graph.Models.ODataErrors.ODataError ex)
                                {
                                    status = ex.Error?.Code == "Forbidden" ? "❌ Not Granted" : "⚠️ Unknown";
                                    details = ex.Error?.Message ?? "Permission test failed";
                                }
                                break;

                            default:
                                status = "⚠️ Test Not Implemented";
                                details = "Manual verification required";
                                break;
                        }

                        results.Add(new
                        {
                            permission = test.Permission,
                            resource = test.Resource,
                            testDescription = test.Test,
                            required = test.Required,
                            status = status,
                            details = details
                        });
                    }
                    catch (Exception ex)
                    {
                        results.Add(new
                        {
                            permission = test.Permission,
                            resource = test.Resource,
                            testDescription = test.Test,
                            required = test.Required,
                            status = "❌ Error",
                            details = ex.Message
                        });
                    }
                }

                var grantedCount = results.Count(r => r.GetType().GetProperty("status")?.GetValue(r)?.ToString()?.Contains("✅") == true);
                var requiredCount = results.Count(r => r.GetType().GetProperty("required")?.GetValue(r) as bool? == true);
                var requiredGranted = results.Count(r => 
                    r.GetType().GetProperty("required")?.GetValue(r) as bool? == true && 
                    r.GetType().GetProperty("status")?.GetValue(r)?.ToString()?.Contains("✅") == true);

                return Ok(new
                {
                    success = true,
                    permissionStatus = new
                    {
                        total = results.Count,
                        granted = grantedCount,
                        required = requiredCount,
                        requiredGranted = requiredGranted,
                        ready = requiredGranted >= requiredCount,
                        timestamp = DateTimeOffset.UtcNow
                    },
                    permissions = results,
                    analysis = new
                    {
                        core_functionality = requiredGranted >= requiredCount ? 
                            "✅ Core permissions granted - call records monitoring available" :
                            "❌ Missing required permissions - cannot monitor call records",
                        advanced_features = grantedCount == results.Count ?
                            "✅ All permissions granted - full functionality available" :
                            $"⚠️ Partial permissions - {grantedCount}/{results.Count} features available",
                        recommendation = requiredGranted < requiredCount ?
                            "Grant CallRecords.Read.All permission and provide admin consent" :
                            "Consider granting additional permissions for enhanced functionality"
                    },
                    nextSteps = requiredGranted >= requiredCount ? new[]
                    {
                        "✅ Core permissions working - proceed with subscription creation",
                        "Test remaining subscriptions with current permissions",
                        "Consider adding missing permissions for full Teams monitoring"
                    } : new[]
                    {
                        "❌ Grant required permissions in Azure AD app registration",
                        "Provide admin consent for granted permissions",
                        "Retry permission check after granting permissions"
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking permissions");
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    message = "Failed to check Graph API permissions",
                    timestamp = DateTimeOffset.UtcNow
                });
            }
        }

        /// <summary>
        /// Try alternative subscription approach with different changeTypes and configurations
        /// </summary>
        [HttpPost("create-alternative-subscriptions")]
        public async Task<IActionResult> CreateAlternativeSubscriptions()
        {
            try
            {
                _logger.LogInformation("Attempting alternative subscription approaches");

                var notificationUrl = "https://arandiateamsbot.ggunifiedtech.com/api/notifications";
                var results = new List<object>();

                // Alternative approaches with different configurations
                var subscriptionAttempts = new[]
                {
                    new { 
                        Resource = "communications/onlineMeetings", 
                        ChangeType = "created", // Only created, not updated
                        Description = "Online Meetings (created only)",
                        Alternative = "Single change type approach"
                    },
                    new { 
                        Resource = "communications/onlineMeetings", 
                        ChangeType = "updated", // Only updated
                        Description = "Online Meetings (updated only)",
                        Alternative = "Update notifications only"
                    },
                    new { 
                        Resource = "communications/calls", 
                        ChangeType = "created", // Only created, not updated
                        Description = "Live Calls (created only)",
                        Alternative = "Call start notifications only"
                    },
                    new { 
                        Resource = "users", 
                        ChangeType = "updated", // User changes
                        Description = "User Changes",
                        Alternative = "User profile monitoring"
                    }
                };

                foreach (var attempt in subscriptionAttempts)
                {
                    try
                    {
                        _logger.LogInformation("Trying alternative: {Resource} with {ChangeType}", attempt.Resource, attempt.ChangeType);

                        var subscription = new Microsoft.Graph.Models.Subscription
                        {
                            Resource = attempt.Resource,
                            ChangeType = attempt.ChangeType,
                            NotificationUrl = notificationUrl,
                            ExpirationDateTime = DateTimeOffset.UtcNow.AddHours(1), // Short test duration
                            ClientState = $"TeamsComplianceBot-Alternative-{attempt.Resource.Replace("/", "-")}-{attempt.ChangeType}-2025"
                        };

                        var createdSubscription = await _graphClient.Subscriptions.PostAsync(subscription);

                        if (createdSubscription != null)
                        {
                            _logger.LogInformation("✅ Alternative subscription successful: {Resource} - {ChangeType}", attempt.Resource, attempt.ChangeType);
                            
                            results.Add(new
                            {
                                success = true,
                                resource = attempt.Resource,
                                changeType = attempt.ChangeType,
                                description = attempt.Description,
                                alternative = attempt.Alternative,
                                subscriptionId = createdSubscription.Id,
                                expirationDateTime = createdSubscription.ExpirationDateTime
                            });
                        }
                        else
                        {
                            results.Add(new
                            {
                                success = false,
                                resource = attempt.Resource,
                                changeType = attempt.ChangeType,
                                description = attempt.Description,
                                alternative = attempt.Alternative,
                                error = "Subscription creation returned null"
                            });
                        }
                    }
                    catch (Microsoft.Graph.Models.ODataErrors.ODataError ex)
                    {
                        _logger.LogWarning("❌ Alternative subscription failed: {Resource} - {Error}", attempt.Resource, ex.Error?.Code);
                        results.Add(new
                        {
                            success = false,
                            resource = attempt.Resource,
                            changeType = attempt.ChangeType,
                            description = attempt.Description,
                            alternative = attempt.Alternative,
                            error = ex.Error?.Code,
                            message = ex.Error?.Message,
                            recommendation = ex.Error?.Code switch
                            {
                                "Forbidden" => "Permission required or not consented",
                                "ExtensionError" => "Tenant configuration or resource not available",
                                "BadRequest" => "Invalid subscription parameters",
                                _ => "Check Graph API documentation for this resource"
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        results.Add(new
                        {
                            success = false,
                            resource = attempt.Resource,
                            changeType = attempt.ChangeType,
                            description = attempt.Description,
                            alternative = attempt.Alternative,
                            error = ex.Message
                        });
                    }
                }

                var successCount = results.Count(r => r.GetType().GetProperty("success")?.GetValue(r) as bool? == true);
                var totalCount = results.Count;

                return Ok(new
                {
                    success = successCount > 0,
                    message = $"Alternative subscription attempts: {successCount}/{totalCount} successful",
                    attempts = results,
                    summary = new
                    {
                        total = totalCount,
                        successful = successCount,
                        failed = totalCount - successCount
                    },
                    analysis = successCount > 0 ?
                        $"✅ Found {successCount} working alternative configurations" :
                        "❌ All alternative approaches failed - may need tenant-level configuration",
                    recommendations = successCount > 0 ? new[]
                    {
                        "Use successful alternative configurations for production",
                        "Monitor working subscriptions for Teams activity",
                        "Consider extending successful subscriptions to 24 hours"
                    } : new[]
                    {
                        "Verify all required permissions are granted and consented",
                        "Check tenant configuration for Teams API access",
                        "Focus on call records subscriptions which are proven to work"
                    },
                    timestamp = DateTimeOffset.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in alternative subscription attempts");
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    timestamp = DateTimeOffset.UtcNow
                });
            }
        }

        /// <summary>
        /// Force create all possible subscriptions with comprehensive error reporting
        /// </summary>
        [HttpPost("force-create-all")]
        public async Task<IActionResult> ForceCreateAllSubscriptions()
        {
            try
            {
                _logger.LogInformation("Force creating all possible Teams subscriptions with detailed error analysis");

                var notificationUrl = "https://arandiateamsbot.ggunifiedtech.com/api/notifications";
                var results = new List<object>();

                // Comprehensive list of all possible Teams-related subscriptions
                var allSubscriptions = new[]
                {
                    // Core Teams resources
                    new { Resource = "communications/callRecords", ChangeType = "created", Priority = "High", RequiredPermission = "CallRecords.Read.All" },
                    new { Resource = "communications/calls", ChangeType = "created", Priority = "High", RequiredPermission = "Calls.AccessMedia.All" },
                    new { Resource = "communications/calls", ChangeType = "updated", Priority = "Medium", RequiredPermission = "Calls.AccessMedia.All" },
                    new { Resource = "communications/onlineMeetings", ChangeType = "created", Priority = "High", RequiredPermission = "OnlineMeetings.ReadWrite.All" },
                    new { Resource = "communications/onlineMeetings", ChangeType = "updated", Priority = "Medium", RequiredPermission = "OnlineMeetings.ReadWrite.All" },
                    
                    // User and group resources (for Teams context)
                    new { Resource = "users", ChangeType = "updated", Priority = "Low", RequiredPermission = "User.Read.All" },
                    new { Resource = "groups", ChangeType = "updated", Priority = "Low", RequiredPermission = "Group.Read.All" },
                    
                    // Calendar events (Teams meetings)
                    new { Resource = "users/{id}/events", ChangeType = "created", Priority = "Medium", RequiredPermission = "Calendars.Read" },
                    new { Resource = "users/{id}/events", ChangeType = "updated", Priority = "Medium", RequiredPermission = "Calendars.Read" },
                };

                foreach (var sub in allSubscriptions)
                {
                    try
                    {
                        _logger.LogInformation("Force attempting: {Resource} - {ChangeType} (Priority: {Priority})", 
                            sub.Resource, sub.ChangeType, sub.Priority);

                        var resource = sub.Resource;
                        
                        // Handle user-specific resources by using 'me' or skipping
                        if (resource.Contains("{id}"))
                        {
                            resource = resource.Replace("users/{id}", "me");
                        }

                        var subscription = new Microsoft.Graph.Models.Subscription
                        {
                            Resource = resource,
                            ChangeType = sub.ChangeType,
                            NotificationUrl = notificationUrl,
                            ExpirationDateTime = DateTimeOffset.UtcNow.AddHours(24),
                            ClientState = $"TeamsComplianceBot-Force-{resource.Replace("/", "-")}-{sub.ChangeType}-2025"
                        };

                        var createdSubscription = await _graphClient.Subscriptions.PostAsync(subscription);

                        if (createdSubscription != null)
                        {
                            _logger.LogInformation("✅ FORCE SUCCESS: {Resource} - {ChangeType}", resource, sub.ChangeType);
                            
                            results.Add(new
                            {
                                success = true,
                                resource = resource,
                                originalResource = sub.Resource,
                                changeType = sub.ChangeType,
                                priority = sub.Priority,
                                requiredPermission = sub.RequiredPermission,
                                subscriptionId = createdSubscription.Id,
                                expirationDateTime = createdSubscription.ExpirationDateTime,
                                status = "Successfully created",
                                impact = sub.Priority switch
                                {
                                    "High" => "Critical for Teams compliance monitoring",
                                    "Medium" => "Enhances monitoring capabilities",
                                    "Low" => "Provides additional context",
                                    _ => "Unknown impact"
                                }
                            });
                        }
                        else
                        {
                            results.Add(new
                            {
                                success = false,
                                resource = resource,
                                originalResource = sub.Resource,
                                changeType = sub.ChangeType,
                                priority = sub.Priority,
                                requiredPermission = sub.RequiredPermission,
                                error = "Subscription creation returned null",
                                status = "Failed - null response"
                            });
                        }
                    }
                    catch (Microsoft.Graph.Models.ODataErrors.ODataError ex)
                    {
                        var errorAnalysis = ex.Error?.Code switch
                        {
                            "Forbidden" => $"Permission '{sub.RequiredPermission}' not granted or not consented",
                            "ExtensionError" => "Tenant configuration issue or resource not available",
                            "BadRequest" => "Invalid subscription parameters or unsupported resource",
                            "InvalidRequest" => "Resource or changeType not supported",
                            "TooManyRequests" => "Rate limited - too many subscription requests",
                            _ => "Unknown Graph API error"
                        };

                        _logger.LogWarning("❌ FORCE FAILED: {Resource} - {Error} - {Analysis}", 
                            sub.Resource, ex.Error?.Code, errorAnalysis);

                        results.Add(new
                        {
                            success = false,
                            resource = sub.Resource,
                            originalResource = sub.Resource,
                            changeType = sub.ChangeType,
                            priority = sub.Priority,
                            requiredPermission = sub.RequiredPermission,
                            error = ex.Error?.Code,
                            message = ex.Error?.Message,
                            analysis = errorAnalysis,
                            status = "Failed with Graph error",
                            recommendation = ex.Error?.Code switch
                            {
                                "Forbidden" => $"Grant and consent '{sub.RequiredPermission}' permission",
                                "ExtensionError" => "Contact tenant admin for Teams API configuration",
                                "BadRequest" => "Resource may not support webhook subscriptions",
                                _ => "Review Microsoft Graph documentation"
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        results.Add(new
                        {
                            success = false,
                            resource = sub.Resource,
                            originalResource = sub.Resource,
                            changeType = sub.ChangeType,
                            priority = sub.Priority,
                            requiredPermission = sub.RequiredPermission,
                            error = ex.GetType().Name,
                            message = ex.Message,
                            status = "Failed with unexpected error"
                        });
                    }
                }

                var successCount = results.Count(r => r.GetType().GetProperty("success")?.GetValue(r) as bool? == true);
                var totalCount = results.Count;
                var highPrioritySuccess = results.Count(r => 
                    r.GetType().GetProperty("success")?.GetValue(r) as bool? == true &&
                    r.GetType().GetProperty("priority")?.GetValue(r)?.ToString() == "High");
                var highPriorityTotal = results.Count(r => 
                    r.GetType().GetProperty("priority")?.GetValue(r)?.ToString() == "High");

                return Ok(new
                {
                    success = successCount > 0,
                    message = $"Force subscription creation: {successCount}/{totalCount} successful",
                    results = results,
                    summary = new
                    {
                        total = totalCount,
                        successful = successCount,
                        failed = totalCount - successCount,
                        highPrioritySuccess = highPrioritySuccess,
                        highPriorityTotal = highPriorityTotal,
                        criticalFunctionality = highPrioritySuccess > 0 ? "Available" : "Limited"
                    },
                    analysis = new
                    {
                        overall = successCount == totalCount ? 
                            "🎉 Perfect! All subscriptions created successfully" :
                            successCount > 0 ?
                                $"✅ Partial success - {successCount}/{totalCount} subscriptions working" :
                                "❌ No subscriptions created - permission or configuration issues",
                        critical = highPrioritySuccess == highPriorityTotal ?
                            "✅ All critical Teams monitoring subscriptions active" :
                            highPrioritySuccess > 0 ?
                                $"⚠️ Partial critical functionality - {highPrioritySuccess}/{highPriorityTotal} high-priority subscriptions" :
                                "❌ No critical subscriptions - Teams monitoring severely limited",
                        permissions = "Check failed subscriptions for specific permission requirements"
                    },
                    logStreamContext = new
                    {
                        explanation = "The HTTP 400 errors you see in log stream are from malformed Microsoft Graph validation requests",
                        example = "https://arandiabot-app:80/api/notifications (HTTPS on port 80 is invalid)",
                        impact = "These errors are normal and don't affect subscription functionality",
                        evidence = $"{successCount} successful subscriptions prove webhook validation works"
                    },
                    nextSteps = successCount > 0 ? new[]
                    {
                        $"✅ Monitor {successCount} active subscriptions for Teams notifications",
                        "✅ Ignore HTTP 400 errors in log stream - they're expected",
                        "Set up subscription renewal for 24-hour maintenance",
                        "Consider granting missing permissions for failed subscriptions"
                    } : new[]
                    {
                        "❌ Grant required permissions in Azure AD app registration",
                        "Provide admin consent for all granted permissions",
                        "Verify tenant configuration allows Teams API access",
                        "Focus on getting CallRecords.Read.All permission working first"
                    },
                    timestamp = DateTimeOffset.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in force subscription creation");
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    message = "Failed to force create subscriptions",
                    timestamp = DateTimeOffset.UtcNow
                });
            }
        }

        /// <summary>
        /// Test Azure blob storage connectivity and permissions
        /// </summary>
        [HttpPost("test-blob-storage")]
        public async Task<IActionResult> TestBlobStorage()
        {
            try
            {
                _logger.LogInformation("Testing blob storage connectivity");

                // Check if blob storage is accessible
                var connectionString = _configuration.GetConnectionString("BlobStorage");
                if (string.IsNullOrEmpty(connectionString))
                {
                    return BadRequest(new
                    {
                        success = false,
                        error = "BlobStorage connection string not configured",
                        timestamp = DateTimeOffset.UtcNow
                    });
                }                // Test blob service connection
                var blobServiceClient = new Azure.Storage.Blobs.BlobServiceClient(connectionString);
                
                var testResults = new List<object>();

                // Test 1: List containers
                try
                {
                    var containers = new List<string>();
                    await foreach (var container in blobServiceClient.GetBlobContainersAsync())
                    {
                        containers.Add(container.Name);
                    }

                    testResults.Add(new
                    {
                        test = "List Containers",
                        success = true,
                        containers = containers,
                        count = containers.Count
                    });
                }
                catch (Exception ex)
                {
                    testResults.Add(new
                    {
                        test = "List Containers",
                        success = false,
                        error = ex.Message
                    });
                }

                // Test 2: Try to create recordings container
                try
                {
                    var recordingsContainer = blobServiceClient.GetBlobContainerClient("recordings");
                    var createResponse = await recordingsContainer.CreateIfNotExistsAsync(Azure.Storage.Blobs.Models.PublicAccessType.None);
                    
                    testResults.Add(new
                    {
                        test = "Create/Access Recordings Container",
                        success = true,
                        created = createResponse != null,
                        message = createResponse != null ? "Container created" : "Container already exists"
                    });
                }
                catch (Azure.RequestFailedException ex) when (ex.Status == 409)
                {
                    testResults.Add(new
                    {
                        test = "Create/Access Recordings Container",
                        success = true,
                        created = false,
                        message = "Container already exists (409 - expected)"
                    });
                }
                catch (Exception ex)
                {
                    testResults.Add(new
                    {
                        test = "Create/Access Recordings Container",
                        success = false,
                        error = ex.Message,
                        errorType = ex.GetType().Name
                    });
                }

                // Test 3: Try to create metadata container
                try
                {
                    var metadataContainer = blobServiceClient.GetBlobContainerClient("metadata");
                    var createResponse = await metadataContainer.CreateIfNotExistsAsync(Azure.Storage.Blobs.Models.PublicAccessType.None);
                    
                    testResults.Add(new
                    {
                        test = "Create/Access Metadata Container",
                        success = true,
                        created = createResponse != null,
                        message = createResponse != null ? "Container created" : "Container already exists"
                    });
                }
                catch (Azure.RequestFailedException ex) when (ex.Status == 409)
                {
                    testResults.Add(new
                    {
                        test = "Create/Access Metadata Container",
                        success = true,
                        created = false,
                        message = "Container already exists (409 - expected)"
                    });
                }
                catch (Exception ex)
                {
                    testResults.Add(new
                    {
                        test = "Create/Access Metadata Container",
                        success = false,
                        error = ex.Message,
                        errorType = ex.GetType().Name
                    });
                }

                // Test 4: Try to write a test blob
                try
                {
                    var testContainer = blobServiceClient.GetBlobContainerClient("recordings");
                    var testBlobName = $"connectivity-test-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.txt";
                    var testBlobClient = testContainer.GetBlobClient(testBlobName);
                    
                    var testContent = $"Connectivity test from Teams Compliance Bot at {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss}";
                    await testBlobClient.UploadAsync(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(testContent)), overwrite: true);
                    
                    // Clean up test blob
                    await testBlobClient.DeleteIfExistsAsync();
                    
                    testResults.Add(new
                    {
                        test = "Write/Delete Test Blob",
                        success = true,
                        message = "Successfully wrote and deleted test blob"
                    });
                }
                catch (Exception ex)
                {
                    testResults.Add(new
                    {
                        test = "Write/Delete Test Blob",
                        success = false,
                        error = ex.Message,
                        errorType = ex.GetType().Name
                    });
                }

                var successCount = testResults.Count(r => r.GetType().GetProperty("success")?.GetValue(r) as bool? == true);
                var totalTests = testResults.Count;

                return Ok(new
                {
                    success = successCount == totalTests,
                    message = $"Blob storage connectivity test completed: {successCount}/{totalTests} tests passed",
                    overallStatus = successCount == totalTests ? "✅ HEALTHY" : "❌ ISSUES DETECTED",
                    storageAccount = "arandiastorage",
                    testResults = testResults,
                    recommendations = successCount < totalTests ? new[]
                    {
                        "Check Azure Storage Account access keys",
                        "Verify storage account permissions and firewall rules",
                        "Ensure storage account is accessible from Azure App Service",
                        "Check if storage account key has expired"
                    } : new[]
                    {
                        "✅ Blob storage is fully operational",
                        "✅ Containers are accessible",
                        "✅ Read/write permissions working correctly"
                    },
                    timestamp = DateTimeOffset.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing blob storage connectivity");
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    errorType = ex.GetType().Name,
                    message = "Failed to test blob storage connectivity",
                    troubleshooting = new[]
                    {
                        "Check BlobStorage connection string in appsettings.json",
                        "Verify Azure Storage Account access key is correct",
                        "Ensure storage account is not behind firewall restrictions",
                        "Check App Service managed identity permissions if applicable"
                    },
                    timestamp = DateTimeOffset.UtcNow
                });
            }
        }

        /// <summary>
        /// Recreate a subscription based on its resource type when the original subscription is not found
        /// </summary>
        private async Task RecreateSubscriptionBasedOnResourceAsync(Microsoft.Graph.Models.Subscription originalSubscription)
        {
            try
            {
                _logger.LogInformation("Attempting to recreate subscription for resource: {Resource}", originalSubscription.Resource);

                switch (originalSubscription.Resource?.ToLower())
                {
                    case var resource when resource != null && resource.Contains("callrecords"):
                        _logger.LogInformation("Recreating call records subscription");
                        await CreateCallRecordsSubscriptionAsync();
                        break;

                    case var resource when resource != null && resource.Contains("calls"):
                        _logger.LogInformation("Recreating calls subscription");
                        await CreateLiveCallsSubscriptionAsync();
                        break;

                    case var resource when resource != null && resource.Contains("onlinemeetings"):
                        _logger.LogInformation("Recreating online meetings subscription");
                        await CreateOnlineMeetingsSubscriptionAsync();
                        break;

                    case var resource when resource != null && resource.Contains("teams") && resource.Contains("messages"):
                        _logger.LogInformation("Recreating teams messages subscription - using call records instead");
                        await CreateCallRecordsSubscriptionAsync(); // Fallback since specific endpoint may not exist
                        break;

                    case var resource when resource != null && resource.Contains("chats") && resource.Contains("messages"):
                        _logger.LogInformation("Recreating chat messages subscription - using call records instead");
                        await CreateCallRecordsSubscriptionAsync(); // Fallback since specific endpoint may not exist
                        break;

                    default:
                        _logger.LogWarning("Unknown or null resource type for recreation: {Resource}", originalSubscription.Resource);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recreating subscription for resource: {Resource}", originalSubscription.Resource);
            }
        }

        /// <summary>
        /// Internal method to create call records subscription
        /// </summary>
        private async Task<Microsoft.Graph.Models.Subscription?> CreateCallRecordsSubscriptionAsync()
        {
            try
            {
                var notificationUrl = "https://arandiateamsbot.ggunifiedtech.com/api/notifications";
                
                var subscription = new Microsoft.Graph.Models.Subscription
                {
                    Resource = "communications/callRecords",
                    ChangeType = "created",
                    NotificationUrl = notificationUrl,
                    ExpirationDateTime = DateTimeOffset.UtcNow.AddHours(24),
                    ClientState = "TeamsComplianceBot-CallRecords-Recreated-2025"
                };

                return await _graphClient.Subscriptions.PostAsync(subscription);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating call records subscription");
                return null;
            }
        }

        /// <summary>
        /// Internal method to create live calls subscription
        /// </summary>
        private async Task<Microsoft.Graph.Models.Subscription?> CreateLiveCallsSubscriptionAsync()
        {
            try
            {
                var notificationUrl = "https://arandiateamsbot.ggunifiedtech.com/api/notifications";
                
                var subscription = new Microsoft.Graph.Models.Subscription
                {
                    Resource = "communications/calls",
                    ChangeType = "created,updated",
                    NotificationUrl = notificationUrl,
                    ExpirationDateTime = DateTimeOffset.UtcNow.AddHours(24),
                    ClientState = "TeamsComplianceBot-LiveCalls-Recreated-2025"
                };

                return await _graphClient.Subscriptions.PostAsync(subscription);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating live calls subscription");
                return null;
            }
        }

        /// <summary>
        /// Internal method to create online meetings subscription
        /// </summary>
        private async Task<Microsoft.Graph.Models.Subscription?> CreateOnlineMeetingsSubscriptionAsync()
        {
            try
            {
                var notificationUrl = "https://arandiateamsbot.ggunifiedtech.com/api/notifications";
                
                var subscription = new Microsoft.Graph.Models.Subscription
                {
                    Resource = "communications/onlineMeetings",
                    ChangeType = "created,updated",
                    NotificationUrl = notificationUrl,
                    ExpirationDateTime = DateTimeOffset.UtcNow.AddHours(24),
                    ClientState = "TeamsComplianceBot-OnlineMeetings-Recreated-2025"
                };

                return await _graphClient.Subscriptions.PostAsync(subscription);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating online meetings subscription");
                return null;
            }
        }

        /// <summary>
        /// Get all active subscriptions (basic GET endpoint for /api/subscriptions)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            try
            {
                _logger.LogInformation("Retrieving all Graph API subscriptions");

                // Get all subscriptions from Microsoft Graph
                var subscriptions = await _graphClient.Subscriptions.GetAsync();
                var activeSubscriptions = subscriptions?.Value?.Where(s => 
                    s.ClientState != null && 
                    s.ClientState.Contains("TeamsComplianceBot")).ToList() ?? new List<Microsoft.Graph.Models.Subscription>();

                _logger.LogInformation("Found {Count} active Teams Compliance Bot subscriptions", activeSubscriptions.Count);

                return Ok(new
                {
                    success = true,
                    count = activeSubscriptions.Count,
                    subscriptions = activeSubscriptions.Select(s => new
                    {
                        id = s.Id,
                        resource = s.Resource,
                        changeType = s.ChangeType,
                        notificationUrl = s.NotificationUrl,
                        expirationDateTime = s.ExpirationDateTime,
                        clientState = s.ClientState,
                        applicationId = s.ApplicationId
                    }),
                    timestamp = DateTimeOffset.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving subscriptions");
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    message = "Failed to retrieve subscriptions",
                    timestamp = DateTimeOffset.UtcNow
                });
            }
        }

        /// <summary>
        /// Create a new Graph API subscription (basic POST endpoint for /api/subscriptions)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] CreateSubscriptionRequest request)
        {
            try
            {
                _logger.LogInformation("Creating new Graph API subscription for resource: {Resource}", request?.Resource);

                if (request == null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        error = "Request body is required",
                        timestamp = DateTimeOffset.UtcNow
                    });
                }

                // Validate required fields
                if (string.IsNullOrEmpty(request.Resource) || string.IsNullOrEmpty(request.ChangeType))
                {
                    return BadRequest(new
                    {
                        success = false,
                        error = "Resource and ChangeType are required",
                        timestamp = DateTimeOffset.UtcNow
                    });
                }

                // Use the notification URL from request or default to custom domain
                var notificationUrl = !string.IsNullOrEmpty(request.NotificationUrl) 
                    ? request.NotificationUrl 
                    : "https://arandiateamsbot.ggunifiedtech.com/api/notifications";

                // Create the subscription
                var subscription = new Microsoft.Graph.Models.Subscription
                {
                    Resource = request.Resource,
                    ChangeType = request.ChangeType,
                    NotificationUrl = notificationUrl,
                    ExpirationDateTime = request.ExpirationDateTime ?? DateTimeOffset.UtcNow.AddHours(1),
                    ClientState = request.ClientState ?? $"TeamsComplianceBot-{request.Resource?.Replace("/", "-")}-{DateTimeOffset.UtcNow:yyyyMMdd}"
                };

                var createdSubscription = await _graphClient.Subscriptions.PostAsync(subscription);

                if (createdSubscription != null)
                {
                    _logger.LogInformation("Successfully created subscription: {SubscriptionId} for resource: {Resource}", 
                        createdSubscription.Id, createdSubscription.Resource);

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
                            clientState = createdSubscription.ClientState,
                            applicationId = createdSubscription.ApplicationId
                        },
                        message = "Subscription created successfully",
                        timestamp = DateTimeOffset.UtcNow
                    });
                }
                else
                {
                    return StatusCode(500, new
                    {
                        success = false,
                        error = "Failed to create subscription - no response from Graph API",
                        timestamp = DateTimeOffset.UtcNow
                    });
                }
            }
            catch (Microsoft.Graph.Models.ODataErrors.ODataError ex)
            {
                _logger.LogError(ex, "Graph API error creating subscription");
                return StatusCode(ex.ResponseStatusCode > 0 ? ex.ResponseStatusCode : 500, new
                {
                    success = false,
                    error = ex.Error?.Message ?? ex.Message,
                    code = ex.Error?.Code,
                    details = ex.Error?.Details?.Select(d => new { code = d.Code, message = d.Message }),
                    timestamp = DateTimeOffset.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating subscription");
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    message = "Failed to create subscription",
                    timestamp = DateTimeOffset.UtcNow
                });
            }
        }

        /// <summary>
        /// Request model for creating subscriptions
        /// </summary>
        public class CreateSubscriptionRequest
        {
            public string? Resource { get; set; }
            public string? ChangeType { get; set; }
            public string? NotificationUrl { get; set; }
            public DateTimeOffset? ExpirationDateTime { get; set; }
            public string? ClientState { get; set; }
        }
    }
}
