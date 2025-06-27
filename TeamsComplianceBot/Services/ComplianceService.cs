using Azure.Storage.Blobs;
using TeamsComplianceBot.Models;
using System.Text.Json;

namespace TeamsComplianceBot.Services;

/// <summary>
/// Implementation of compliance service for audit logging and policy management
/// </summary>
public class ComplianceService : IComplianceService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<ComplianceService> _logger;
    private readonly IConfiguration _configuration;
    private readonly ICallRecordingService _callRecordingService;
    private readonly string _complianceContainerName = "compliance";

    public ComplianceService(
        BlobServiceClient blobServiceClient,
        ILogger<ComplianceService> logger,
        IConfiguration configuration,
        ICallRecordingService callRecordingService)
    {
        _blobServiceClient = blobServiceClient ?? throw new ArgumentNullException(nameof(blobServiceClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _callRecordingService = callRecordingService ?? throw new ArgumentNullException(nameof(callRecordingService));
    }

    public async Task LogComplianceEventAsync(ComplianceEventType eventType, MeetingInfo meetingInfo, CancellationToken cancellationToken = default)
    {
        try
        {
            var complianceEvent = new ComplianceEvent
            {
                EventType = eventType,
                MeetingId = meetingInfo.Id,
                Description = $"{eventType} for meeting: {meetingInfo.Title ?? meetingInfo.Id}",
                TenantId = meetingInfo.TenantId ?? string.Empty,
                AdditionalData = new Dictionary<string, object>
                {
                    ["MeetingTitle"] = meetingInfo.Title ?? string.Empty,
                    ["Organizer"] = meetingInfo.Organizer ?? string.Empty,
                    ["StartTime"] = meetingInfo.StartTime?.ToString("O") ?? string.Empty,
                    ["ParticipantCount"] = meetingInfo.Participants.Count
                }
            };

            await StoreComplianceEventAsync(complianceEvent, cancellationToken);
            _logger.LogInformation("Compliance event logged: {EventType} for meeting {MeetingId}", eventType, meetingInfo.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log compliance event {EventType} for meeting {MeetingId}", eventType, meetingInfo.Id);
        }
    }

    public async Task ProcessCompletedRecordingAsync(RecordingMetadata recordingMetadata, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Processing completed recording {RecordingId}", recordingMetadata.Id);

            // Apply retention policy
            var retentionDays = _configuration.GetValue<int>("Compliance:DefaultRetentionDays", 2555); // 7 years
            recordingMetadata.RetentionPolicy = new RetentionPolicy
            {
                RetentionDays = retentionDays,
                ExpirationDate = DateTime.UtcNow.AddDays(retentionDays),
                AutoDelete = _configuration.GetValue<bool>("Compliance:AutoDelete", true),
                PolicyVersion = _configuration.GetValue<string>("Compliance:PolicyVersion", "1.0") ?? "1.0"
            };

            // Log compliance event
            var complianceEvent = new ComplianceEvent
            {
                EventType = ComplianceEventType.RetentionPolicyApplied,
                RecordingId = recordingMetadata.Id,
                MeetingId = recordingMetadata.MeetingId,
                Description = $"Retention policy applied to recording {recordingMetadata.Id}",
                TenantId = recordingMetadata.TenantId,
                AdditionalData = new Dictionary<string, object>
                {
                    ["RetentionDays"] = retentionDays,
                    ["ExpirationDate"] = recordingMetadata.RetentionPolicy.ExpirationDate.ToString("O"),
                    ["FileSizeMB"] = recordingMetadata.FileSizeMB,
                    ["Duration"] = recordingMetadata.Duration.ToString()
                }
            };

            await StoreComplianceEventAsync(complianceEvent, cancellationToken);

            // Schedule future retention processing (in a real implementation, this would use a job scheduler)
            _logger.LogInformation("Recording {RecordingId} processed for compliance. Expires: {ExpirationDate}", 
                recordingMetadata.Id, recordingMetadata.RetentionPolicy.ExpirationDate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process completed recording {RecordingId} for compliance", recordingMetadata.Id);
        }
    }

    public async Task<SystemStatus> GetSystemStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // In a real implementation, these would be actual system metrics
            var status = new SystemStatus
            {                OverallStatus = "Healthy",
                ComplianceStatus = "Compliant",
                LastHealthCheck = DateTime.UtcNow,
                ServiceStatus = new Dictionary<string, ServiceHealthStatus>
                {
                    ["RecordingService"] = new ServiceHealthStatus { Status = "Healthy", LastChecked = DateTime.UtcNow },
                    ["BlobStorage"] = new ServiceHealthStatus { Status = "Healthy", LastChecked = DateTime.UtcNow },
                    ["GraphAPI"] = new ServiceHealthStatus { Status = "Healthy", LastChecked = DateTime.UtcNow },
                    ["ComplianceEngine"] = new ServiceHealthStatus { Status = "Healthy", LastChecked = DateTime.UtcNow }
                }
            };

            // Get today's recording count (simplified implementation)
            var today = DateTime.UtcNow.Date;
            var recentRecordings = await GetRecentRecordingsAsync(100, cancellationToken);
            
            status.TotalRecordingsToday = recentRecordings.Count(r => r.StartTime.Date == today);
            status.ActiveRecordings = recentRecordings.Count(r => r.Status == RecordingStatus.InProgress);

            // Calculate storage usage (simplified)
            var totalSizeBytes = recentRecordings.Sum(r => r.FileSizeBytes);
            var maxStorageBytes = _configuration.GetValue<long>("Storage:MaxSizeBytes", 1_000_000_000_000L); // 1TB default
            status.StorageUsagePercentage = (double)totalSizeBytes / maxStorageBytes * 100;

            return status;
        }        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get system status");
            return new SystemStatus
            {
                OverallStatus = "Error",
                ComplianceStatus = "Unknown",
                LastHealthCheck = DateTime.UtcNow
            };
        }
    }    public Task<bool> IsUserAdminAsync(string userId, CancellationToken cancellationToken = default)
    {
        try
        {
            // For now, use a simple configuration-based approach
            // In Gabriel's case, we'll add his user ID to the admin list
            var adminUsers = _configuration.GetSection("Compliance:AdminUsers").Get<string[]>() ?? Array.Empty<string>();
            
            // Also check known admin user IDs (Gabriel's AAD ID)
            var knownAdminIds = new[]
            {
                "b9fe3c36-4b59-4f9b-b180-0ab07e93d652", // Gabriel Chiappa
                "4b346215-3b6a-46ca-9811-4ca1efcd9298", // Alex Arandia  
                "92b1a687-c496-4b64-8eb5-69bcfceb5ad8"  // Lucy Arandia
            };
            
            var isAdmin = adminUsers.Contains(userId) || knownAdminIds.Contains(userId);
            
            _logger.LogInformation("User {UserId} admin check result: {IsAdmin}", userId, isAdmin);
            
            return Task.FromResult(isAdmin);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check admin status for user {UserId}", userId);
            return Task.FromResult(false);
        }
    }

    public async Task<List<RecordingMetadata>> GetRecentRecordingsAsync(int count, CancellationToken cancellationToken = default)
    {
        try
        {
            // This is a simplified implementation - in reality, you'd want proper indexing and querying
            var recordings = new List<RecordingMetadata>();
            var containerClient = _blobServiceClient.GetBlobContainerClient("recordings");

            await foreach (var blobItem in containerClient.GetBlobsAsync(prefix: "metadata/", cancellationToken: cancellationToken))
            {
                try
                {
                    var blobClient = containerClient.GetBlobClient(blobItem.Name);
                    var response = await blobClient.DownloadContentAsync(cancellationToken);
                    var json = response.Value.Content.ToString();
                    var metadata = JsonSerializer.Deserialize<RecordingMetadata>(json);

                    if (metadata != null)
                    {
                        recordings.Add(metadata);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse recording metadata from {BlobName}", blobItem.Name);
                }

                if (recordings.Count >= count * 2) // Get more than needed for proper sorting
                    break;
            }

            return recordings
                .Where(r => r.Status != RecordingStatus.Deleted)
                .OrderByDescending(r => r.StartTime)
                .Take(count)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get recent recordings");
            return new List<RecordingMetadata>();
        }
    }

    public async Task ApplyRetentionPoliciesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting retention policy application");

            var allRecordings = await GetRecentRecordingsAsync(10000, cancellationToken); // Get all recordings
            var expiredRecordings = allRecordings
                .Where(r => !r.RetentionPolicy.IsLegalHold 
                           && r.RetentionPolicy.AutoDelete 
                           && DateTime.UtcNow > r.RetentionPolicy.ExpirationDate)
                .ToList();

            foreach (var recording in expiredRecordings)
            {
                var deleted = await _callRecordingService.DeleteRecordingAsync(
                    recording.Id, 
                    "Automatic deletion - retention policy expired", 
                    cancellationToken);

                if (deleted)
                {
                    await LogComplianceEventAsync(
                        ComplianceEventType.RecordingDeleted,
                        new MeetingInfo { Id = recording.MeetingId, TenantId = recording.TenantId },
                        cancellationToken);
                }
            }

            _logger.LogInformation("Retention policy application completed. Processed {Count} expired recordings", expiredRecordings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply retention policies");
        }
    }

    public async Task<bool> ValidateComplianceAsync(string recordingId, CancellationToken cancellationToken = default)
    {
        try
        {
            var metadata = await _callRecordingService.GetRecordingMetadataAsync(recordingId, cancellationToken);
            if (metadata == null)
            {
                return false;
            }

            // Check various compliance requirements
            var isCompliant = true;
            var violations = new List<string>();

            // Check if recording has required metadata
            if (string.IsNullOrEmpty(metadata.MeetingTitle))
            {
                violations.Add("Missing meeting title");
                isCompliant = false;
            }

            // Check if retention policy is applied
            if (metadata.RetentionPolicy.ExpirationDate == default)
            {
                violations.Add("No retention policy applied");
                isCompliant = false;
            }

            // Check file integrity (simplified)
            if (metadata.FileSizeBytes <= 0)
            {
                violations.Add("Invalid file size");
                isCompliant = false;
            }

            if (!isCompliant)
            {
                await LogComplianceEventAsync(
                    ComplianceEventType.ComplianceViolation,
                    new MeetingInfo { Id = metadata.MeetingId, TenantId = metadata.TenantId },
                    cancellationToken);

                _logger.LogWarning("Compliance validation failed for recording {RecordingId}. Violations: {Violations}", 
                    recordingId, string.Join(", ", violations));
            }

            return isCompliant;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate compliance for recording {RecordingId}", recordingId);
            return false;
        }
    }    public Task<UserAccessLevel> GetUserAccessLevelAsync(string userId, CancellationToken cancellationToken = default)
    {
        try
        {
            // In a real implementation, this would check against Azure AD roles or a database
            var superAdmins = _configuration.GetSection("Compliance:SuperAdminUsers").Get<string[]>() ?? Array.Empty<string>();
            if (superAdmins.Contains(userId))
            {
                return Task.FromResult(UserAccessLevel.SuperAdmin);
            }

            var admins = _configuration.GetSection("Compliance:AdminUsers").Get<string[]>() ?? Array.Empty<string>();
            if (admins.Contains(userId))
            {
                return Task.FromResult(UserAccessLevel.Admin);
            }

            var viewers = _configuration.GetSection("Compliance:ViewerUsers").Get<string[]>() ?? Array.Empty<string>();
            if (viewers.Contains(userId))
            {
                return Task.FromResult(UserAccessLevel.Viewer);
            }

            return Task.FromResult(UserAccessLevel.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get user access level for {UserId}", userId);
            return Task.FromResult(UserAccessLevel.None);
        }
    }    private async Task StoreComplianceEventAsync(ComplianceEvent complianceEvent, CancellationToken cancellationToken)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_complianceContainerName);
            
            try
            {
                await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 409)
            {
                // Container already exists - this is expected and not an error
                _logger.LogDebug("Compliance container already exists (409 Conflict): {ContainerName}", _complianceContainerName);
            }

            var eventBlobName = $"events/{DateTime.UtcNow:yyyy/MM/dd}/{complianceEvent.Id}.json";
            var blobClient = containerClient.GetBlobClient(eventBlobName);

            var json = JsonSerializer.Serialize(complianceEvent, new JsonSerializerOptions { WriteIndented = true });
            var content = BinaryData.FromString(json);

            await blobClient.UploadAsync(content, overwrite: true, cancellationToken);

            // Set tags for easy searching
            var tags = new Dictionary<string, string>
            {
                ["EventType"] = complianceEvent.EventType.ToString(),
                ["TenantId"] = complianceEvent.TenantId,
                ["Date"] = complianceEvent.Timestamp.ToString("yyyy-MM-dd")
            };

            if (!string.IsNullOrEmpty(complianceEvent.MeetingId))
            {
                tags["MeetingId"] = complianceEvent.MeetingId;
            }

            await blobClient.SetTagsAsync(tags, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store compliance event {EventId}", complianceEvent.Id);
            throw;
        }
    }
}
