using TeamsComplianceBot.Models;

namespace TeamsComplianceBot.Services;

/// <summary>
/// Service for managing call recordings
/// </summary>
public interface ICallRecordingService
{
    /// <summary>
    /// Start recording a Teams meeting
    /// </summary>
    Task<RecordingResult> StartRecordingAsync(MeetingInfo meetingInfo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stop recording a Teams meeting
    /// </summary>
    Task<RecordingResult> StopRecordingAsync(string meetingId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get recording metadata
    /// </summary>
    Task<RecordingMetadata?> GetRecordingMetadataAsync(string recordingId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all recordings for a specific meeting
    /// </summary>
    Task<List<RecordingMetadata>> GetMeetingRecordingsAsync(string meetingId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Download recording content
    /// </summary>
    Task<Stream?> DownloadRecordingAsync(string recordingId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a recording (with compliance checks)
    /// </summary>
    Task<bool> DeleteRecordingAsync(string recordingId, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Process a call record for compliance requirements
    /// </summary>
    Task ProcessCallRecordForComplianceAsync(Microsoft.Graph.Models.CallRecords.CallRecord callRecord, CancellationToken cancellationToken = default);
}
