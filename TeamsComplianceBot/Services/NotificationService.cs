using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using TeamsComplianceBot.Models;

namespace TeamsComplianceBot.Services;

/// <summary>
/// Implementation of notification service for Teams compliance bot
/// </summary>
public class NotificationService : INotificationService
{
    private readonly ILogger<NotificationService> _logger;
    private readonly IConfiguration _configuration;

    public NotificationService(
        ILogger<NotificationService> logger,
        IConfiguration configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public async Task NotifyRecordingStartedAsync(ITurnContext turnContext, MeetingInfo meetingInfo, CancellationToken cancellationToken = default)
    {
        try
        {
            var notificationEnabled = _configuration.GetValue<bool>("Notifications:RecordingStarted", true);
            if (!notificationEnabled)
            {
                return;
            }

            var message = CreateRecordingStartedMessage(meetingInfo);
            await turnContext.SendActivityAsync(message, cancellationToken);

            _logger.LogInformation("Recording started notification sent for meeting {MeetingId}", meetingInfo.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send recording started notification for meeting {MeetingId}", meetingInfo.Id);
        }
    }

    public async Task NotifyRecordingFailedAsync(ITurnContext turnContext, MeetingInfo meetingInfo, string errorMessage, CancellationToken cancellationToken = default)
    {
        try
        {
            var message = CreateRecordingFailedMessage(meetingInfo, errorMessage);
            await turnContext.SendActivityAsync(message, cancellationToken);

            _logger.LogInformation("Recording failed notification sent for meeting {MeetingId}", meetingInfo.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send recording failed notification for meeting {MeetingId}", meetingInfo.Id);
        }
    }

    public async Task NotifyRecordingCompletedAsync(ITurnContext turnContext, MeetingInfo meetingInfo, RecordingMetadata recordingMetadata, CancellationToken cancellationToken = default)
    {
        try
        {
            var notificationEnabled = _configuration.GetValue<bool>("Notifications:RecordingCompleted", true);
            if (!notificationEnabled)
            {
                return;
            }

            var message = CreateRecordingCompletedMessage(meetingInfo, recordingMetadata);
            await turnContext.SendActivityAsync(message, cancellationToken);

            _logger.LogInformation("Recording completed notification sent for meeting {MeetingId}", meetingInfo.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send recording completed notification for meeting {MeetingId}", meetingInfo.Id);
        }
    }

    public async Task SendComplianceAlertAsync(string message, ComplianceEventType eventType, CancellationToken cancellationToken = default)
    {
        try
        {
            var alertsEnabled = _configuration.GetValue<bool>("Notifications:ComplianceAlerts", true);
            if (!alertsEnabled)
            {
                return;
            }

            _logger.LogInformation("Compliance alert: {Message}, Event type: {EventType}", message, eventType);

            await Task.Delay(100, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send compliance alert: {Message}", message);
        }
    }

    private IActivity CreateRecordingStartedMessage(MeetingInfo meetingInfo)
    {
        return MessageFactory.Text($"🔴 **Recording Started** | This call is now being recorded for compliance purposes. Recording is required by your organization's policies.");
    }

    private IActivity CreateRecordingFailedMessage(MeetingInfo meetingInfo, string errorMessage)
    {
        return MessageFactory.Text($"⚠️ **Recording Failed** | Unable to record this call: {errorMessage}");
    }

    private IActivity CreateRecordingCompletedMessage(MeetingInfo meetingInfo, RecordingMetadata recordingMetadata)
    {
        var fileSizeMB = recordingMetadata.FileSizeMB.ToString("F1");
        var duration = (recordingMetadata.EndTime - recordingMetadata.StartTime).ToString(@"hh\:mm\:ss");

        return MessageFactory.Text($"✅ **Recording Completed** | " +
                                 $"Duration: {duration}, Size: {fileSizeMB} MB. " +
                                 $"The recording will be stored securely according to compliance policies.");
    }
}
