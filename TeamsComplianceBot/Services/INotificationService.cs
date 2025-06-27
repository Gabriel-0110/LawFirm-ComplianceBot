using Microsoft.Bot.Builder;
using TeamsComplianceBot.Models;

namespace TeamsComplianceBot.Services;

/// <summary>
/// Service for managing notifications in the compliance bot
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Notify participants when a recording starts
    /// </summary>
    Task NotifyRecordingStartedAsync(ITurnContext turnContext, MeetingInfo meetingInfo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Notify participants when a recording fails
    /// </summary>
    Task NotifyRecordingFailedAsync(ITurnContext turnContext, MeetingInfo meetingInfo, string errorMessage, CancellationToken cancellationToken = default);

    /// <summary>
    /// Notify participants when a recording completes
    /// </summary>
    Task NotifyRecordingCompletedAsync(ITurnContext turnContext, MeetingInfo meetingInfo, RecordingMetadata recordingMetadata, CancellationToken cancellationToken = default);

    /// <summary>
    /// Send compliance alerts to administrators
    /// </summary>
    Task SendComplianceAlertAsync(string message, ComplianceEventType eventType, CancellationToken cancellationToken = default);
}