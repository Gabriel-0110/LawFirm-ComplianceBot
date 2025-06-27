using Microsoft.Graph.Models;
using TeamsComplianceBot.Models;

namespace TeamsComplianceBot.Services
{
    /// <summary>
    /// Service for managing Microsoft Graph calling operations to join and manage Teams calls
    /// This service handles the actual API calls to join calls, answer calls, and control call flow
    /// </summary>
    public interface ICallJoiningService
    {
        /// <summary>
        /// Answer an incoming call and join it for compliance recording
        /// </summary>
        Task<CallJoinResult> AnswerCallAsync(string callId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Join an ongoing call using the call ID
        /// </summary>
        Task<CallJoinResult> JoinCallAsync(string callId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Leave a call that the bot has joined
        /// </summary>
        Task<bool> LeaveCallAsync(string callId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get the current status of a call
        /// </summary>
        Task<CallStatus?> GetCallStatusAsync(string callId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Start recording on an active call
        /// </summary>
        Task<RecordingResult> StartCallRecordingAsync(string callId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stop recording on an active call
        /// </summary>
        Task<bool> StopCallRecordingAsync(string callId, string recordingId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Test Microsoft Graph API connectivity and permissions for debugging
        /// </summary>
        Task<string> TestGraphApiAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Result of a call join operation
    /// </summary>
    public class CallJoinResult
    {
        public bool Success { get; set; }
        public required string CallId { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? ErrorCode { get; set; }
        public DateTimeOffset? JoinedAt { get; set; }
    }

    /// <summary>
    /// Current status of a call
    /// </summary>
    public class CallStatus
    {
        public required string CallId { get; set; }
        public string State { get; set; } = string.Empty;
        public string Direction { get; set; } = string.Empty;
        public DateTimeOffset? CreatedDateTime { get; set; }
        public string? Source { get; set; }
        public string? Subject { get; set; }
    }
}
