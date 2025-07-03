using Microsoft.Graph.Models;

namespace TeamsComplianceBot.Services;

/// <summary>
/// Service for managing Microsoft Graph subscriptions
/// </summary>
public interface IGraphSubscriptionService
{
    /// <summary>
    /// Create a new subscription to receive notifications for a resource
    /// </summary>
    /// <param name="resource">The resource to monitor (e.g., "communications/onlineMeetings/{id}/recordings")</param>
    /// <param name="changeType">The type of changes to subscribe to (e.g., "created,updated")</param>
    /// <param name="clientState">Optional client state for validating callbacks</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The subscription ID if successful</returns>
    Task<string> CreateSubscriptionAsync(string resource, string changeType, string? clientState = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renew an existing subscription to extend its expiration time
    /// </summary>
    /// <param name="subscriptionId">The ID of the subscription to renew</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if renewal successful, otherwise false</returns>
    Task<bool> RenewSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete an existing subscription
    /// </summary>
    /// <param name="subscriptionId">The ID of the subscription to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if deletion successful, otherwise false</returns>
    Task<bool> DeleteSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all active subscriptions for the application
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of active subscriptions</returns>
    Task<IEnumerable<Microsoft.Graph.Models.Subscription>> GetActiveSubscriptionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a subscription exists for a specific resource type
    /// </summary>
    /// <param name="resourceType">Partial resource path to check (e.g., "recordings")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if at least one active subscription exists for this resource type</returns>
    Task<bool> HasActiveSubscriptionAsync(string resourceType, CancellationToken cancellationToken = default);
}
