using Microsoft.Graph.Models;

namespace TeamsComplianceBot.Services;

/// <summary>
/// Background service that periodically renews Microsoft Graph subscriptions before they expire
/// </summary>
public class SubscriptionRenewalService : BackgroundService
{
    private readonly IGraphSubscriptionService _subscriptionService;
    private readonly ILogger<SubscriptionRenewalService> _logger;
    private readonly IConfiguration _configuration;
    private readonly TimeSpan _checkInterval;
    private readonly TimeSpan _renewalThreshold;

    public SubscriptionRenewalService(
        IGraphSubscriptionService subscriptionService,
        ILogger<SubscriptionRenewalService> logger,
        IConfiguration configuration)
    {
        _subscriptionService = subscriptionService ?? throw new ArgumentNullException(nameof(subscriptionService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        
        // Default check interval is 15 minutes
        _checkInterval = TimeSpan.FromMinutes(
            _configuration.GetValue<double>("Subscription:RenewalCheckIntervalMinutes", 15));
        
        // Default renewal threshold is 60 minutes before expiration
        _renewalThreshold = TimeSpan.FromMinutes(
            _configuration.GetValue<double>("Subscription:RenewalThresholdMinutes", 60));
    }

    /// <summary>
    /// Execute the background service logic
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Subscription renewal service started");
        
        // Don't start immediately - wait a brief delay for the app to fully initialize
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Checking for subscriptions to renew...");
                await RenewExpiringSubscriptionsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in subscription renewal service");
            }

            // Wait for the next check interval
            _logger.LogInformation("Next subscription check in {CheckInterval} minutes", _checkInterval.TotalMinutes);
            await Task.Delay(_checkInterval, stoppingToken);
        }
    }

    /// <summary>
    /// Find and renew subscriptions that are approaching expiration
    /// </summary>
    private async Task RenewExpiringSubscriptionsAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Get all active subscriptions
            var subscriptions = await _subscriptionService.GetActiveSubscriptionsAsync(cancellationToken);
            
            // Calculate the threshold time - we'll renew any subscription expiring before this
            var renewalTime = DateTimeOffset.UtcNow.Add(_renewalThreshold);
            
            _logger.LogInformation("Found {Count} active subscriptions. Renewal threshold: {RenewalTime}", 
                subscriptions.Count(), renewalTime);

            // Find subscriptions that need renewal
            var expiringSubscriptions = subscriptions.Where(s => 
                s.ExpirationDateTime.HasValue && 
                s.ExpirationDateTime.Value <= renewalTime);

            int renewedCount = 0;
            int failedCount = 0;
            
            // Renew each expiring subscription
            foreach (var subscription in expiringSubscriptions)
            {
                if (string.IsNullOrEmpty(subscription.Id))
                {
                    _logger.LogWarning("Skipping subscription with null ID");
                    continue;
                }

                _logger.LogInformation("Renewing subscription {SubscriptionId} expiring at {ExpirationTime}", 
                    subscription.Id, subscription.ExpirationDateTime);

                try
                {
                    var renewalResult = await _subscriptionService.RenewSubscriptionAsync(subscription.Id, cancellationToken);
                    
                    if (renewalResult)
                    {
                        renewedCount++;
                        _logger.LogInformation("Successfully renewed subscription {SubscriptionId}", subscription.Id);
                    }
                    else
                    {
                        failedCount++;
                        _logger.LogWarning("Failed to renew subscription {SubscriptionId}", subscription.Id);
                    }
                }
                catch (Exception ex)
                {
                    failedCount++;
                    _logger.LogError(ex, "Error renewing subscription {SubscriptionId}", subscription.Id);
                }
            }

            _logger.LogInformation("Subscription renewal complete. Successfully renewed: {RenewedCount}, Failed: {FailedCount}", 
                renewedCount, failedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in RenewExpiringSubscriptionsAsync");
        }
    }
}
