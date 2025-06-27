using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Teams;
using Microsoft.Bot.Schema;
using Microsoft.Bot.Schema.Teams;
using Microsoft.Extensions.Logging;
using TeamsComplianceBot.Services;
using TeamsComplianceBot.Models;

namespace TeamsComplianceBot.Bots;

/// <summary>
/// Main Teams Compliance Bot that handles Teams events and manages call recording
/// </summary>
public class TeamsComplianceBot : TeamsActivityHandler
{
    private readonly ILogger<TeamsComplianceBot> _logger;
    private readonly ICallRecordingService _callRecordingService;
    private readonly IComplianceService _complianceService;
    private readonly INotificationService _notificationService;

    public TeamsComplianceBot(
        ILogger<TeamsComplianceBot> logger,
        ICallRecordingService callRecordingService,
        IComplianceService complianceService,
        INotificationService notificationService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _callRecordingService = callRecordingService ?? throw new ArgumentNullException(nameof(callRecordingService));
        _complianceService = complianceService ?? throw new ArgumentNullException(nameof(complianceService));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
    }

    /// <summary>
    /// Handle when bot is added to a Teams team
    /// </summary>
    protected override async Task OnTeamsChannelCreatedAsync(ChannelInfo channelInfo, TeamInfo teamInfo, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Channel created in team: {teamName}, {teamId}, channel: {channelName}, {channelId}", 
            teamInfo.Name, teamInfo.Id, channelInfo.Name, channelInfo.Id);
        await base.OnTeamsChannelCreatedAsync(channelInfo, teamInfo, turnContext, cancellationToken);
    }

    /// <summary>
    /// Handle when members are added to a team where the bot is installed
    /// </summary>
    protected override async Task OnTeamsMembersAddedAsync(IList<TeamsChannelAccount> teamsMembersAdded, TeamInfo teamInfo, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
    {
        // Check if the bot itself is being added to the team
        foreach (var member in teamsMembersAdded)
        {
            // If the bot is being added to the team
            if (member.Id == turnContext.Activity.Recipient.Id)
            {
                _logger.LogInformation("Bot has been added to team: {teamName}, {teamId}", teamInfo.Name, teamInfo.Id);
                
                var welcomeMessage = MessageFactory.Text(
                    "👋 **Hello team!** I'm the Compliance Bot.\n\n" +
                    "I automatically record Teams calls for compliance purposes. Here are key things to know:\n\n" +
                    "• All calls and meetings will be recorded for compliance\n" +
                    "• Recordings are stored securely according to organizational policies\n" +
                    "• Type **help** to see available commands\n\n" +
                    "Thank you for adding me to your team. If you have any questions, please contact your compliance administrator."
                );
                
                await turnContext.SendActivityAsync(welcomeMessage, cancellationToken);
                _logger.LogInformation("Welcome message sent to team: {teamName}", teamInfo.Name);
                
                break; // Once we've handled the bot being added, we can exit the loop
            }
        }

        // Continue with base implementation
        await base.OnTeamsMembersAddedAsync(teamsMembersAdded, teamInfo, turnContext, cancellationToken);
    }

    /// <summary>
    /// Handle when members are added to the conversation
    /// </summary>
    protected override async Task OnMembersAddedAsync(
        IList<ChannelAccount> membersAdded,
        ITurnContext<IConversationUpdateActivity> turnContext,
        CancellationToken cancellationToken)
    {
        var welcomeText = "Hello! I'm the Compliance Bot. I will automatically record calls for compliance purposes. " +
                         "All recordings are stored securely and in accordance with your organization's policies.";

        foreach (var member in membersAdded)
        {
            if (member.Id != turnContext.Activity.Recipient.Id)
            {
                await turnContext.SendActivityAsync(MessageFactory.Text(welcomeText), cancellationToken);
                _logger.LogInformation("Welcome message sent to {MemberId}", member.Id);
            }
        }
    }    /// <summary>
    /// Handle regular message activities
    /// </summary>
    protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
    {
        var messageText = turnContext.Activity.Text?.Trim();
        
        _logger.LogInformation("Received message: {Message} from {UserId}", messageText, turnContext.Activity.From.Id);

        // Normalize message to lowercase for comparison
        var normalizedMessage = messageText?.ToLowerInvariant();        // Simple if-else logic as requested for validation
        if (normalizedMessage == "hi")
        {
            await turnContext.SendActivityAsync(MessageFactory.Text("Welcome to Arandia Compliance Bot!"), cancellationToken);
            _logger.LogInformation("Responded to hi command from user {UserId}", turnContext.Activity.From.Id);
        }
        else if (normalizedMessage == "help")
        {
            await turnContext.SendActivityAsync(MessageFactory.Text("Try: hi, help, status, compliance."), cancellationToken);
            _logger.LogInformation("Responded to help command from user {UserId}", turnContext.Activity.From.Id);
        }
        else if (normalizedMessage == "status")
        {
            await HandleStatusRequest(turnContext, cancellationToken);
        }
        else if (normalizedMessage == "compliance")
        {
            await HandleComplianceRequest(turnContext, cancellationToken);
        }
        else if (normalizedMessage == "recordings")
        {
            await HandleRecordingsRequest(turnContext, cancellationToken);
        }
        else
        {
            await HandleUnknownCommand(turnContext, cancellationToken);
        }
    }

    /// <summary>
    /// Handle Teams meeting events (call start/end) - simplified for demo
    /// Note: In production, you would use Graph API webhooks or other mechanisms
    /// </summary>
    protected override async Task OnEventActivityAsync(ITurnContext<IEventActivity> turnContext, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Event activity received: {EventType}", turnContext.Activity.Name);

        // Handle different event types
        switch (turnContext.Activity.Name)
        {
            case "application/vnd.microsoft.meetingStart":
                await HandleMeetingStartEvent(turnContext, cancellationToken);
                break;
            case "application/vnd.microsoft.meetingEnd":
                await HandleMeetingEndEvent(turnContext, cancellationToken);
                break;
            default:
                await base.OnEventActivityAsync(turnContext, cancellationToken);
                break;
        }
    }

    private async Task HandleMeetingStartEvent(ITurnContext<IEventActivity> turnContext, CancellationToken cancellationToken)
    {
        try
        {
            var meetingId = turnContext.Activity.Value?.ToString() ?? Guid.NewGuid().ToString();
            _logger.LogInformation("Meeting started: {MeetingId}", meetingId);
            
            var meetingInfo = new Models.MeetingInfo
            {
                Id = meetingId,
                StartTime = DateTime.UtcNow,
                Title = "Teams Meeting", // In production, get from Graph API
                Organizer = turnContext.Activity.From?.Name ?? "Unknown",
                TenantId = turnContext.Activity.Conversation.TenantId ?? string.Empty
            };

            // Start recording
            var recordingResult = await _callRecordingService.StartRecordingAsync(meetingInfo, cancellationToken);
            
            if (recordingResult.Success)
            {
                _logger.LogInformation("Recording started successfully for meeting {MeetingId}", meetingId);
                
                // Send notification to participants
                await _notificationService.NotifyRecordingStartedAsync(turnContext, meetingInfo, cancellationToken);
                
                // Log compliance event
                await _complianceService.LogComplianceEventAsync(
                    ComplianceEventType.RecordingStarted,
                    meetingInfo,
                    cancellationToken);
            }
            else
            {
                _logger.LogError("Failed to start recording for meeting {MeetingId}: {Error}", 
                    meetingId, recordingResult.ErrorMessage);
                
                await _notificationService.NotifyRecordingFailedAsync(turnContext, meetingInfo, recordingResult.ErrorMessage ?? "Unknown error", cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling meeting start event");
        }
    }

    private async Task HandleMeetingEndEvent(ITurnContext<IEventActivity> turnContext, CancellationToken cancellationToken)
    {
        try
        {
            var meetingId = turnContext.Activity.Value?.ToString() ?? Guid.NewGuid().ToString();
            _logger.LogInformation("Meeting ended: {MeetingId}", meetingId);
            
            var meetingInfo = new Models.MeetingInfo
            {
                Id = meetingId,
                EndTime = DateTime.UtcNow,
                TenantId = turnContext.Activity.Conversation.TenantId ?? string.Empty
            };

            // Stop recording
            var recordingResult = await _callRecordingService.StopRecordingAsync(meetingId, cancellationToken);
            
            if (recordingResult.Success && recordingResult.RecordingMetadata != null)
            {
                _logger.LogInformation("Recording stopped successfully for meeting {MeetingId}", meetingId);
                
                // Process the recording for compliance
                await _complianceService.ProcessCompletedRecordingAsync(recordingResult.RecordingMetadata, cancellationToken);
                
                // Send final notification
                await _notificationService.NotifyRecordingCompletedAsync(turnContext, meetingInfo, recordingResult.RecordingMetadata, cancellationToken);
                
                // Log compliance event
                await _complianceService.LogComplianceEventAsync(
                    ComplianceEventType.RecordingCompleted,
                    meetingInfo,
                    cancellationToken);
            }
            else
            {
                _logger.LogError("Failed to stop recording for meeting {MeetingId}: {Error}", 
                    meetingId, recordingResult.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling meeting end event");
        }
    }    private async Task HandleStatusRequest(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
    {
        // Send loading indicator for better UX
        await turnContext.SendActivityAsync(MessageFactory.Text("🔄 Checking system status..."), cancellationToken);
        
        try 
        {
            var status = await _complianceService.GetSystemStatusAsync(cancellationToken);
            
            var statusMessage = $"**✅ Compliance Bot Status**\n\n" +
                               $"🟢 System Status: {status.OverallStatus}\n" +
                               $"📊 Active Recordings: {status.ActiveRecordings}\n" +
                               $"💾 Total Recordings Today: {status.TotalRecordingsToday}\n" +
                               $"🔒 Compliance Status: {status.ComplianceStatus}\n" +
                               $"📈 Storage Usage: {status.StorageUsagePercentage:F1}%\n\n" +
                               $"*Last updated: {DateTime.Now:HH:mm:ss}*";

            await turnContext.SendActivityAsync(MessageFactory.Text(statusMessage), cancellationToken);
            _logger.LogInformation("Status request handled for user {UserId}", turnContext.Activity.From.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving status for user {UserId}", turnContext.Activity.From.Id);
            await turnContext.SendActivityAsync(
                MessageFactory.Text("❌ Unable to retrieve status at this time. Please try again later."), 
                cancellationToken);
        }
    }    private async Task HandleHelpRequest(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
    {
        var helpMessage = "**🤖 Teams Compliance Bot Help**\n\n" +
                         "I automatically record all Teams calls for compliance purposes. Here are the available commands:\n\n" +
                         "**📋 Commands:**\n" +
                         "• **hi** - Say hello and get a welcome message\n" +
                         "• **status** - Get current system and recording status\n" +
                         "• **recordings** - View recent recordings (administrators only)\n" +
                         "• **compliance** - View compliance policies and information\n" +
                         "• **help** - Show this help message\n\n" +
                         "**⚠️ Important Notes:**\n" +
                         "• All calls are recorded automatically when the bot is present\n" +
                         "• Recordings are stored securely for compliance purposes\n" +
                         "• Only authorized personnel can access recordings\n" +
                         "• Retention policies apply as per company policy\n\n" +
                         "**💡 Tips:**\n" +
                         "• Commands are case-insensitive (try **STATUS** or **status**)\n" +
                         "• Type any command to get started\n" +
                         "• Contact your administrator for access issues\n\n" +
                         "Need help? Contact your compliance administrator.";

        await turnContext.SendActivityAsync(MessageFactory.Text(helpMessage), cancellationToken);
        _logger.LogInformation("Help request handled for user {UserId}", turnContext.Activity.From.Id);
    }private async Task HandleRecordingsRequest(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
    {
        // Send loading indicator
        await turnContext.SendActivityAsync(MessageFactory.Text("🔄 Checking permissions and retrieving recordings..."), cancellationToken);
        
        try
        {
            // Check if user has admin privileges
            var isAdmin = await _complianceService.IsUserAdminAsync(turnContext.Activity.From.Id, cancellationToken);
            
            if (!isAdmin)
            {
                await turnContext.SendActivityAsync(
                    MessageFactory.Text("❌ **Access Denied**\n\nOnly administrators can view recording information.\n\nContact your compliance administrator for access."), 
                    cancellationToken);
                return;
            }

            var recentRecordings = await _complianceService.GetRecentRecordingsAsync(10, cancellationToken);
            
            var recordingsMessage = "**📹 Recent Recordings (Last 10)**\n\n";
            
            if (recentRecordings.Any())
            {
                foreach (var recording in recentRecordings)
                {
                    recordingsMessage += $"▶️ **{recording.MeetingTitle}**\n" +
                                       $"   📅 Date: {recording.StartTime:yyyy-MM-dd HH:mm}\n" +
                                       $"   ⏱️ Duration: {recording.Duration}\n" +
                                       $"   👤 Organizer: {recording.Organizer}\n" +
                                       $"   📂 Size: {recording.FileSizeMB:F1} MB\n\n";
                }
            }
            else
            {
                recordingsMessage += "📭 No recordings found.\n\nRecordings will appear here once meetings are held with the bot present.";
            }

            await turnContext.SendActivityAsync(MessageFactory.Text(recordingsMessage), cancellationToken);
            _logger.LogInformation("Recordings request handled for admin user {UserId}", turnContext.Activity.From.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving recordings for user {UserId}", turnContext.Activity.From.Id);
            await turnContext.SendActivityAsync(
                MessageFactory.Text("❌ Unable to retrieve recordings at this time. Please try again later."), 
                cancellationToken);
        }
    }private async Task HandleHiCommand(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
    {
        var message = "Hi there! 👋 I'm your Teams Compliance Bot. I help manage call recordings and compliance policies.\n\n" +
                     "Type **help** to see all available commands.";
        await turnContext.SendActivityAsync(MessageFactory.Text(message), cancellationToken);
        
        _logger.LogInformation("Responded to hi command from user {UserId}", turnContext.Activity.From.Id);
    }    private async Task HandleUnknownCommand(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
    {
        var userInput = turnContext.Activity.Text?.Trim() ?? "unknown command";
        
        var message = $"❓ I didn't understand **\"{userInput}\"**\n\n" +
                     "**Available commands:**\n" +
                     "• **hi** - Say hello\n" +
                     "• **help** - Show detailed help\n" +
                     "• **status** - Check system status\n" +
                     "• **recordings** - View recordings (admin only)\n\n" +
                     "💡 Commands are case-insensitive. Try typing **help** for more information.";
                     
        await turnContext.SendActivityAsync(MessageFactory.Text(message), cancellationToken);
        _logger.LogInformation("Unknown command '{Command}' from user {UserId}", userInput, turnContext.Activity.From.Id);
    }

    private async Task HandleComplianceRequest(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
    {
        var complianceMessage = "**🔒 Compliance Information**\n\n" +
                               "**Recording Policy:**\n" +
                               "• All Teams calls and meetings are automatically recorded\n" +
                               "• Recordings begin when the bot joins the call\n" +
                               "• Participants are notified when recording starts\n\n" +
                               "**Data Security:**\n" +
                               "• Recordings are encrypted and stored securely\n" +
                               "• Access is restricted to authorized personnel only\n" +
                               "• All access is logged and audited\n\n" +
                               "**Retention Policy:**\n" +
                               "• Recordings are retained according to organizational policy\n" +
                               "• Automatic deletion occurs after retention period expires\n" +
                               "• Legal holds may extend retention as required\n\n" +
                               "**Your Rights:**\n" +
                               "• Contact your compliance administrator for questions\n" +
                               "• Request access to recordings involving you (subject to policy)\n" +
                               "• Report compliance concerns to your administrator\n\n" +
                               "For more information, contact your compliance administrator.";

        await turnContext.SendActivityAsync(MessageFactory.Text(complianceMessage), cancellationToken);
        _logger.LogInformation("Compliance information provided to user {UserId}", turnContext.Activity.From.Id);
    }
}
