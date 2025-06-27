using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Builder.TraceExtensions;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Extensions.Logging;

namespace TeamsComplianceBot.Bots;

/// <summary>
/// Bot Framework HTTP Adapter with comprehensive error handling for compliance scenarios
/// </summary>
public class AdapterWithErrorHandler : CloudAdapter
{
    private readonly ILogger<AdapterWithErrorHandler> _logger;

    public AdapterWithErrorHandler(
        BotFrameworkAuthentication auth,
        ILogger<AdapterWithErrorHandler> logger)
        : base(auth, logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        OnTurnError = async (turnContext, exception) =>
        {
            // Log the exception
            _logger.LogError(exception, "Error occurred during bot turn execution. Activity: {ActivityType}, Id: {ActivityId}",
                turnContext.Activity?.Type, turnContext.Activity?.Id);

            // Send a message to the user
            var errorMessage = "I encountered an error while processing your request. " +
                              "The error has been logged and our team will investigate. " +
                              "Please try again later or contact support if the issue persists.";

            try
            {
                // Send error message to user
                await turnContext.SendActivityAsync(MessageFactory.Text(errorMessage));

                // Send a trace activity for Bot Framework Emulator
                await turnContext.TraceActivityAsync("OnTurnError Trace", exception.Message, 
                    "https://www.botframework.com/schemas/error", "TurnError");
            }
            catch (Exception sendException)
            {
                _logger.LogError(sendException, "Failed to send error message to user");
            }

            // Log compliance-specific error information
            LogComplianceError(turnContext, exception);
        };
    }

    /// <summary>
    /// Log compliance-specific error information for audit purposes
    /// </summary>
    private void LogComplianceError(ITurnContext turnContext, Exception exception)
    {
        try
        {
            var complianceErrorInfo = new
            {
                Timestamp = DateTime.UtcNow,
                UserId = turnContext.Activity?.From?.Id,
                UserName = turnContext.Activity?.From?.Name,
                ConversationId = turnContext.Activity?.Conversation?.Id,
                TenantId = turnContext.Activity?.Conversation?.TenantId,
                ActivityType = turnContext.Activity?.Type,
                ActivityId = turnContext.Activity?.Id,
                ErrorType = exception.GetType().Name,
                ErrorMessage = exception.Message,
                StackTrace = exception.StackTrace,
                Source = "TeamsComplianceBot"
            };

            _logger.LogError("Compliance Bot Error: {@ComplianceErrorInfo}", complianceErrorInfo);
        }
        catch (Exception loggingException)
        {
            _logger.LogError(loggingException, "Failed to log compliance error information");
        }
    }
}
