using Azure.Storage.Blobs;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using System.Net;
using System.Text.Json;

namespace TeamsComplianceBot.Services;

/// <summary>
/// Service implementation for managing Microsoft Graph subscriptions
/// </summary>
public class GraphSubscriptionService : IGraphSubscriptionService
{
    private readonly GraphServiceClient _graphClient;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GraphSubscriptionService> _logger;
    private readonly string _subscriptionsContainerName = "graph-subscriptions";

    public GraphSubscriptionService(
        GraphServiceClient graphClient, 
        BlobServiceClient blobServiceClient,
        IConfiguration configuration,
        ILogger<GraphSubscriptionService> logger)
    {
        _graphClient = graphClient ?? throw new ArgumentNullException(nameof(graphClient));
        _blobServiceClient = blobServiceClient ?? throw new ArgumentNullException(nameof(blobServiceClient));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Create a new subscription to receive notifications for a resource
    /// </summary>
    public async Task<string> CreateSubscriptionAsync(string resource, string changeType, string? clientState = null, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Creating subscription for resource: {Resource}, changeType: {ChangeType}", resource, changeType);

            // Ensure container exists before proceeding
            await EnsureContainerExistsAsync(cancellationToken);

            // Get configuration values
            var notificationUrl = _configuration["Recording:NotificationUrl"];
            var subscriptionRenewalMinutes = _configuration.GetValue<int>("Recording:SubscriptionRenewalMinutes", 60);
            
            // Use provided clientState or get default from configuration
            var subscriptionClientState = clientState ?? _configuration["Recording:NotificationClientState"] ?? Guid.NewGuid().ToString();

            if (string.IsNullOrEmpty(notificationUrl))
            {
                throw new InvalidOperationException("Recording:NotificationUrl is not configured in appsettings.json");
            }

            // Create a new subscription
            var subscription = new Microsoft.Graph.Models.Subscription
            {
                Resource = resource,
                ChangeType = changeType,
                NotificationUrl = notificationUrl,
                ExpirationDateTime = DateTimeOffset.UtcNow.AddMinutes(subscriptionRenewalMinutes),
                ClientState = subscriptionClientState,
                LifecycleNotificationUrl = notificationUrl // Also receive lifecycle notifications at the same endpoint
            };            // Create the subscription via Graph API
            _logger.LogInformation("Attempting to create subscription via Graph API - Resource: {Resource}, ChangeType: {ChangeType}, NotificationUrl: {NotificationUrl}", 
                resource, changeType, notificationUrl);
            
            try
            {
                var createdSubscription = await _graphClient.Subscriptions
                    .PostAsync(subscription, cancellationToken: cancellationToken);

                if (createdSubscription == null || string.IsNullOrEmpty(createdSubscription.Id))
                {
                    throw new InvalidOperationException("Failed to create subscription - null or empty subscription ID returned");
                }

                // Store the subscription details in blob storage for tracking
                await StoreSubscriptionAsync(createdSubscription, cancellationToken);

                _logger.LogInformation("Created subscription: {SubscriptionId} for resource: {Resource}, expires: {ExpirationTime}", 
                    createdSubscription.Id, resource, createdSubscription.ExpirationDateTime);

                return createdSubscription.Id;
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, "HTTP error creating subscription for resource: {Resource}. Inner exception: {InnerException}", 
                    resource, httpEx.InnerException?.Message);
                throw;
            }
            catch (System.Net.WebException webEx)
            {
                _logger.LogError(webEx, "Web exception creating subscription for resource: {Resource}. Status: {Status}", 
                    resource, webEx.Status);
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating subscription for resource: {Resource}", resource);
            throw;
        }
    }

    /// <summary>
    /// Renew an existing subscription to extend its expiration time
    /// </summary>
    public async Task<bool> RenewSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Renewing subscription: {SubscriptionId}", subscriptionId);

            // Get the subscription renewal minutes from configuration
            var subscriptionRenewalMinutes = _configuration.GetValue<int>("Recording:SubscriptionRenewalMinutes", 60);

            // Create the request body for renewal
            var requestBody = new Microsoft.Graph.Models.Subscription
            {
                ExpirationDateTime = DateTimeOffset.UtcNow.AddMinutes(subscriptionRenewalMinutes)
            };

            // Renew the subscription via Graph API
            var renewedSubscription = await _graphClient.Subscriptions[subscriptionId]
                .PatchAsync(requestBody, cancellationToken: cancellationToken);

            if (renewedSubscription == null)
            {
                _logger.LogWarning("Failed to renew subscription: {SubscriptionId} - null response", subscriptionId);
                return false;
            }

            // Update the stored subscription details
            await StoreSubscriptionAsync(renewedSubscription, cancellationToken);

            _logger.LogInformation("Successfully renewed subscription: {SubscriptionId}, new expiration: {ExpirationTime}",
                subscriptionId, renewedSubscription.ExpirationDateTime);
            return true;
        }
        catch (ServiceException ex)
        {
            // Check if this is a 404 Not Found error
            var statusCode = GetStatusCodeFromServiceException(ex);
            if (statusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning("⚠️ Subscription {SubscriptionId} not found during renewal attempt - it may have expired and been deleted by Microsoft Graph", subscriptionId);
                
                // Delete from our local storage since it doesn't exist anymore
                await DeleteSubscriptionStorageAsync(subscriptionId, cancellationToken);
                
                // Return false so the caller knows the renewal failed and can decide to recreate
                return false;
            }
            
            _logger.LogError(ex, "❌ Error renewing subscription: {SubscriptionId}, Status code: {StatusCode}", 
                subscriptionId, statusCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error renewing subscription: {SubscriptionId}", subscriptionId);
            return false;
        }
    }

    /// <summary>
    /// Delete an existing subscription
    /// </summary>
    public async Task<bool> DeleteSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Deleting subscription: {SubscriptionId}", subscriptionId);

            // Delete the subscription via Graph API
            await _graphClient.Subscriptions[subscriptionId]
                .DeleteAsync(cancellationToken: cancellationToken);

            // Delete from our local storage
            await DeleteSubscriptionStorageAsync(subscriptionId, cancellationToken);

            _logger.LogInformation("Successfully deleted subscription: {SubscriptionId}", subscriptionId);
            return true;
        }
        catch (ServiceException ex)
        {
            // Check if this is a 404 Not Found error
            var statusCode = GetStatusCodeFromServiceException(ex);
            if (statusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Subscription {SubscriptionId} not found during deletion attempt - it may have already been deleted", 
                    subscriptionId);
                
                // Delete from our local storage since it doesn't exist anymore
                await DeleteSubscriptionStorageAsync(subscriptionId, cancellationToken);
                return true;
            }
            
            _logger.LogError(ex, "Error deleting subscription: {SubscriptionId}, Status code: {StatusCode}", 
                subscriptionId, statusCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting subscription: {SubscriptionId}", subscriptionId);
            return false;
        }
    }

    /// <summary>
    /// Get all active subscriptions for the application
    /// </summary>
    public async Task<IEnumerable<Microsoft.Graph.Models.Subscription>> GetActiveSubscriptionsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting all active subscriptions");

            // First, try to get subscriptions from Graph API
            var graphSubscriptions = new List<Microsoft.Graph.Models.Subscription>();
            
            try
            {
                var subscriptionsResponse = await _graphClient.Subscriptions
                    .GetAsync(cancellationToken: cancellationToken);

                if (subscriptionsResponse?.Value != null)
                {
                    graphSubscriptions = subscriptionsResponse.Value.ToList();
                    _logger.LogInformation("Retrieved {Count} subscriptions from Graph API", graphSubscriptions.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to retrieve subscriptions from Graph API, falling back to local storage");
            }

            // As a fallback or supplement, get subscriptions from our local storage
            await EnsureContainerExistsAsync(cancellationToken);
            var containerClient = _blobServiceClient.GetBlobContainerClient(_subscriptionsContainerName);
            var localSubscriptions = new List<Microsoft.Graph.Models.Subscription>();

            await foreach (var blobItem in containerClient.GetBlobsAsync(cancellationToken: cancellationToken))
            {
                try
                {
                    var blobClient = containerClient.GetBlobClient(blobItem.Name);
                    var blobContent = await blobClient.DownloadContentAsync(cancellationToken);
                    var json = blobContent.Value.Content.ToString();
                    var subscription = JsonSerializer.Deserialize<Microsoft.Graph.Models.Subscription>(json, 
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    
                    if (subscription != null)
                    {
                        localSubscriptions.Add(subscription);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize subscription from blob: {BlobName}", blobItem.Name);
                }
            }

            _logger.LogInformation("Retrieved {Count} subscriptions from local storage", localSubscriptions.Count);

            // Merge the results, giving preference to Graph API data
            var allSubscriptionIds = new HashSet<string>();
            var mergedSubscriptions = new List<Microsoft.Graph.Models.Subscription>();

            // Add Graph API subscriptions first
            foreach (var subscription in graphSubscriptions)
            {
                if (subscription.Id != null)
                {
                    allSubscriptionIds.Add(subscription.Id);
                    mergedSubscriptions.Add(subscription);
                }
            }

            // Add local storage subscriptions if they're not already included
            foreach (var subscription in localSubscriptions)
            {
                if (subscription.Id != null && !allSubscriptionIds.Contains(subscription.Id))
                {
                    mergedSubscriptions.Add(subscription);
                }
            }

            _logger.LogInformation("Returning {Count} total active subscriptions", mergedSubscriptions.Count);
            return mergedSubscriptions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active subscriptions");
            return new List<Microsoft.Graph.Models.Subscription>();
        }
    }

    /// <summary>
    /// Check if a subscription exists for a specific resource type
    /// </summary>
    public async Task<bool> HasActiveSubscriptionAsync(string resourceType, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Checking for active subscriptions for resource type: {ResourceType}", resourceType);

            // Get all active subscriptions
            var subscriptions = await GetActiveSubscriptionsAsync(cancellationToken);

            // Check if any subscription matches the resource type
            var hasSubscription = subscriptions.Any(s => 
                s.Resource != null && 
                s.Resource.Contains(resourceType, StringComparison.OrdinalIgnoreCase));

            _logger.LogInformation("Active subscription for resource type '{ResourceType}': {HasSubscription}", 
                resourceType, hasSubscription);
            
            return hasSubscription;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for active subscriptions for resource type: {ResourceType}", resourceType);
            return false;
        }
    }

    #region Private helper methods

    /// <summary>
    /// Extract HTTP status code from ServiceException
    /// </summary>
    private HttpStatusCode GetStatusCodeFromServiceException(ServiceException ex)
    {
        // In Microsoft Graph SDK, the status code is available in different ways depending on the version
        // This handles both old and new ways to get the status code
        if (ex.ResponseStatusCode != 0)
        {
            return (HttpStatusCode)ex.ResponseStatusCode;
        }
        
        // Try to parse from the error message
        if (ex.Message.Contains("404") || ex.Message.ToLower().Contains("not found"))
        {
            return HttpStatusCode.NotFound;
        }
        
        if (ex.Message.Contains("401") || ex.Message.ToLower().Contains("unauthorized"))
        {
            return HttpStatusCode.Unauthorized;
        }
        
        if (ex.Message.Contains("403") || ex.Message.ToLower().Contains("forbidden"))
        {
            return HttpStatusCode.Forbidden;
        }
        
        // Default to InternalServerError if we can't determine a more specific status
        return HttpStatusCode.InternalServerError;
    }    /// <summary>
    /// Ensure the blob container for storing subscriptions exists
    /// </summary>
    private async Task EnsureContainerExistsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_subscriptionsContainerName);
            
            try
            {
                // Use CreateIfNotExists instead of manually handling the 409 - this is idempotent
                var response = await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
                if (response != null)
                {
                    _logger.LogInformation("Container '{ContainerName}' created successfully", _subscriptionsContainerName);
                }
                else
                {
                    _logger.LogInformation("Container '{ContainerName}' already exists", _subscriptionsContainerName);
                }
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 409) // Conflict error code
            {
                // Container already exists, which is fine - just log and continue
                // This shouldn't happen with CreateIfNotExistsAsync but we'll handle it just in case
                _logger.LogInformation("Container '{ContainerName}' already exists (409 Conflict)", _subscriptionsContainerName);
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 403) // Forbidden
            {
                _logger.LogWarning("Permission denied accessing container '{ContainerName}'. Status: {Status}. " +
                                  "Will continue operation but storage may not be available.", 
                                  _subscriptionsContainerName, ex.Status);
                // Continue without failing - we'll attempt to use the container anyway
            }
            catch (Azure.RequestFailedException ex)
            {
                // Handle other Azure storage errors without failing
                _logger.LogWarning("Azure Storage error when accessing container '{ContainerName}'. Status: {Status}, ErrorCode: {ErrorCode}. " + 
                                  "Will continue operation but storage may not be available.",
                                  _subscriptionsContainerName, ex.Status, ex.ErrorCode);
                // Log the full error details at debug level for troubleshooting
                _logger.LogDebug(ex, "Detailed error when accessing container '{ContainerName}'", _subscriptionsContainerName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ensuring blob container exists: {ContainerName}", _subscriptionsContainerName);
            // Don't throw - try to continue operation even if storage is unavailable
            // This allows the application to function even if persistence is temporarily unavailable
        }
    }    /// <summary>
    /// Store subscription details in blob storage
    /// </summary>
    private async Task StoreSubscriptionAsync(Microsoft.Graph.Models.Subscription subscription, CancellationToken cancellationToken)
    {
        if (subscription?.Id == null)
        {
            _logger.LogWarning("Cannot store subscription with null ID");
            return;
        }

        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_subscriptionsContainerName);
            var blobName = $"{subscription.Id}.json";
            var blobClient = containerClient.GetBlobClient(blobName);
            
            var json = JsonSerializer.Serialize(subscription, new JsonSerializerOptions { WriteIndented = true });
            var content = BinaryData.FromString(json);
              // Use the overwrite flag to handle the case where the blob already exists
            // This eliminates the need for a separate catch/retry for 409 Conflict
            
            try
            {                // We're explicitly setting overwrite=true which should prevent 409 conflicts
                await blobClient.UploadAsync(content, overwrite: true, cancellationToken: cancellationToken);
                _logger.LogInformation("Stored subscription {SubscriptionId} in blob storage", subscription.Id);
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 403)
            {
                // Permission issue - log but continue
                _logger.LogWarning("Permission denied storing subscription {SubscriptionId} in blob storage. Status: {Status}. " +
                                  "Subscription will continue to work but won't be stored for tracking.", 
                                  subscription.Id, ex.Status);
                // Don't throw - allow the subscription to work even if storage fails
            }
            catch (Azure.RequestFailedException ex)
            {
                // Handle other specific Azure storage errors
                _logger.LogWarning("Azure Storage error when storing subscription {SubscriptionId}. Status: {Status}, ErrorCode: {ErrorCode}. " + 
                                  "Subscription will continue to work but may not be stored correctly.",
                                  subscription.Id, ex.Status, ex.ErrorCode);
                
                // Log the full error at debug level for troubleshooting
                _logger.LogDebug(ex, "Detailed storage error for subscription {SubscriptionId}", subscription.Id);
                
                // Don't throw - allow operations to continue even if storage fails
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing subscription {SubscriptionId} in blob storage", subscription.Id);
            // Don't throw - allow the subscription to work even if storage fails
        }
    }

    /// <summary>
    /// Delete subscription details from blob storage
    /// </summary>
    private async Task DeleteSubscriptionStorageAsync(string subscriptionId, CancellationToken cancellationToken)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_subscriptionsContainerName);
            var blobName = $"{subscriptionId}.json";
            var blobClient = containerClient.GetBlobClient(blobName);
            
            await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
            
            _logger.LogInformation("Deleted subscription {SubscriptionId} from blob storage", subscriptionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting subscription {SubscriptionId} from blob storage", subscriptionId);
            // Don't rethrow, as this is a cleanup operation that shouldn't fail the main request
        }
    }

    #endregion
}
