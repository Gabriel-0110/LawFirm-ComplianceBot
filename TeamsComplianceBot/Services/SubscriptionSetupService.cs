using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TeamsComplianceBot.Services;

namespace TeamsComplianceBot.Services;

/// <summary>
/// Background service that sets up Microsoft Graph subscriptions when the bot starts
/// This ensures the bot receives notifications when calls start
/// </summary>
public class SubscriptionSetupService : BackgroundService
{
    private readonly ILogger<SubscriptionSetupService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;

    public SubscriptionSetupService(
        ILogger<SubscriptionSetupService> logger,
        IServiceProvider serviceProvider,
        IConfiguration configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Starting subscription setup service...");

            // Wait a bit for the application to fully start
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            using var scope = _serviceProvider.CreateScope();
            var subscriptionService = scope.ServiceProvider.GetRequiredService<IGraphSubscriptionService>();

            await SetupCallSubscriptionsAsync(subscriptionService, stoppingToken);

            _logger.LogInformation("Subscription setup completed successfully");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Subscription setup service was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in subscription setup service");
        }
    }

    private async Task SetupCallSubscriptionsAsync(IGraphSubscriptionService subscriptionService, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Setting up Graph subscriptions for call notifications...");

            // Create subscription for all calls in the tenant
            // This will notify us when new calls are created
            var callsResource = "/communications/calls";
            var changeTypes = "created,updated,deleted";

            var subscriptionId = await subscriptionService.CreateSubscriptionAsync(
                callsResource, 
                changeTypes,
                clientState: _configuration["Recording:NotificationClientState"],
                cancellationToken);

            _logger.LogInformation("Created call subscription: {SubscriptionId} for resource: {Resource}", 
                subscriptionId, callsResource);

            // Also create subscription for online meetings if configured
            if (_configuration.GetValue<bool>("Recording:MonitorOnlineMeetings", true))
            {
                await SetupOnlineMeetingSubscriptions(subscriptionService, cancellationToken);
            }

            // Create subscription for call records (for compliance tracking)
            var callRecordsResource = "/communications/callRecords";
            var callRecordsSubscriptionId = await subscriptionService.CreateSubscriptionAsync(
                callRecordsResource,
                "created",
                clientState: _configuration["Recording:NotificationClientState"],
                cancellationToken);

            _logger.LogInformation("Created call records subscription: {SubscriptionId} for resource: {Resource}",
                callRecordsSubscriptionId, callRecordsResource);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting up call subscriptions");
            throw;
        }
    }

    private async Task SetupOnlineMeetingSubscriptions(IGraphSubscriptionService subscriptionService, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Setting up online meeting subscriptions...");

            // Note: Online meeting subscriptions require specific permissions
            // and might need to be set up per user or per application
            var onlineMeetingsResource = "/communications/onlineMeetings";
            
            var meetingSubscriptionId = await subscriptionService.CreateSubscriptionAsync(
                onlineMeetingsResource,
                "created,updated,deleted",
                clientState: _configuration["Recording:NotificationClientState"],
                cancellationToken);

            _logger.LogInformation("Created online meetings subscription: {SubscriptionId}", meetingSubscriptionId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not set up online meeting subscriptions (this may be expected if permissions are not available)");
        }
    }
}
