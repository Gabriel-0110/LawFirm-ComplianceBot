using Azure.Storage.Blobs;
using Microsoft.Extensions.Caching.Memory;

namespace TeamsComplianceBot.Services;

/// <summary>
/// Optimized storage helper that caches container existence to minimize 409 errors
/// </summary>
public class OptimizedStorageHelper
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<OptimizedStorageHelper> _logger;
    private readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(30);

    public OptimizedStorageHelper(
        BlobServiceClient blobServiceClient,
        IMemoryCache cache,
        ILogger<OptimizedStorageHelper> logger)
    {
        _blobServiceClient = blobServiceClient;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Ensure container exists with caching to minimize redundant calls
    /// </summary>
    public async Task<BlobContainerClient> EnsureContainerExistsAsync(
        string containerName, 
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"container_exists_{containerName}";
        
        // Check if we already know the container exists
        if (_cache.TryGetValue(cacheKey, out bool containerExists) && containerExists)
        {
            _logger.LogDebug("Container {ContainerName} existence confirmed from cache", containerName);
            return _blobServiceClient.GetBlobContainerClient(containerName);
        }

        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);

        try
        {
            // Try to create the container (idempotent operation)
            var response = await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            
            if (response != null)
            {
                _logger.LogInformation("Container '{ContainerName}' created successfully", containerName);
            }
            else
            {
                _logger.LogDebug("Container '{ContainerName}' already exists", containerName);
            }

            // Cache the fact that the container exists for 30 minutes
            _cache.Set(cacheKey, true, _cacheExpiry);
            
            return containerClient;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 409)
        {
            // Container already exists - cache this information
            _logger.LogDebug("Container '{ContainerName}' already exists (409 - cached for future)", containerName);
            _cache.Set(cacheKey, true, _cacheExpiry);
            return containerClient;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 403)
        {
            _logger.LogWarning("Permission denied accessing container '{ContainerName}'. Status: {Status}", 
                              containerName, ex.Status);
            // Don't cache permission failures
            return containerClient;
        }
        catch (Azure.RequestFailedException ex)
        {
            _logger.LogError(ex, "Failed to ensure container '{ContainerName}' exists. Status: {Status}", 
                            containerName, ex.Status);
            throw;
        }
    }

    /// <summary>
    /// Check if container exists without trying to create it
    /// </summary>
    public async Task<bool> ContainerExistsAsync(string containerName, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"container_exists_{containerName}";
        
        // Check cache first
        if (_cache.TryGetValue(cacheKey, out bool cachedExists))
        {
            return cachedExists;
        }

        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            var response = await containerClient.ExistsAsync(cancellationToken);
            var exists = response.Value;

            // Cache the result
            _cache.Set(cacheKey, exists, _cacheExpiry);
            
            return exists;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking if container '{ContainerName}' exists", containerName);
            return false;
        }
    }

    /// <summary>
    /// Clear the container existence cache (useful for testing or after container operations)
    /// </summary>
    public void ClearContainerCache(string containerName)
    {
        var cacheKey = $"container_exists_{containerName}";
        _cache.Remove(cacheKey);
        _logger.LogDebug("Cleared cache for container {ContainerName}", containerName);
    }

    /// <summary>
    /// Clear all container existence cache entries
    /// </summary>
    public void ClearAllContainerCache()
    {
        // Note: IMemoryCache doesn't have a clear all method, so we'd need to track keys
        // For now, individual cache entries will expire naturally
        _logger.LogDebug("Container cache will expire naturally in {CacheExpiry}", _cacheExpiry);
    }
}
