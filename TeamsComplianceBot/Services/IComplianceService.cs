using Microsoft.Bot.Builder;
using TeamsComplianceBot.Models;

namespace TeamsComplianceBot.Services;

/// <summary>
/// Service for managing compliance operations and audit logging
/// </summary>
public interface IComplianceService
{
    /// <summary>
    /// Log a compliance event for audit purposes
    /// </summary>
    Task LogComplianceEventAsync(ComplianceEventType eventType, MeetingInfo meetingInfo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Process a completed recording for compliance requirements
    /// </summary>
    Task ProcessCompletedRecordingAsync(RecordingMetadata recordingMetadata, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get system status for compliance monitoring
    /// </summary>
    Task<SystemStatus> GetSystemStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a user has admin privileges
    /// </summary>
    Task<bool> IsUserAdminAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get recent recordings (admin only)
    /// </summary>
    Task<List<RecordingMetadata>> GetRecentRecordingsAsync(int count, CancellationToken cancellationToken = default);

    /// <summary>
    /// Apply retention policies to recordings
    /// </summary>
    Task ApplyRetentionPoliciesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Check compliance status of recordings
    /// </summary>
    Task<bool> ValidateComplianceAsync(string recordingId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get user access level for compliance operations
    /// </summary>
    Task<UserAccessLevel> GetUserAccessLevelAsync(string userId, CancellationToken cancellationToken = default);
}


