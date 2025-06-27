using Microsoft.Graph;
using Microsoft.Graph.Models;
using TeamsComplianceBot.Services;

namespace TeamsComplianceBot.Services
{
    /// <summary>
    /// Service for polling Microsoft Graph for Teams calls when subscriptions are not available
    /// This is a fallback mechanism when Graph subscriptions cannot be created due to permission issues
    /// </summary>
    public interface ICallPollingService
    {
        /// <summary>
        /// Start polling for Teams calls
        /// </summary>
        Task StartPollingAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Stop polling for Teams calls
        /// </summary>
        Task StopPollingAsync();
        
        /// <summary>
        /// Get the current polling status
        /// </summary>
        bool IsPolling { get; }
        
        /// <summary>
        /// Get the last poll time
        /// </summary>
        DateTimeOffset? LastPollTime { get; }
    }

    /// <summary>
    /// Implementation of call polling service for Teams compliance bot
    /// </summary>
    public class CallPollingService : ICallPollingService
    {
        private readonly GraphServiceClient _graphClient;
        private readonly ICallRecordingService _callRecordingService;
        private readonly ILogger<CallPollingService> _logger;
        private readonly IConfiguration _configuration;
        
        private Timer? _pollingTimer;
        private readonly SemaphoreSlim _pollingSemaphore = new(1, 1);
        private bool _isPolling = false;
        private DateTimeOffset? _lastPollTime;
        private readonly HashSet<string> _seenCallIds = new();

        public CallPollingService(
            GraphServiceClient graphClient,
            ICallRecordingService callRecordingService,
            ILogger<CallPollingService> logger,
            IConfiguration configuration)
        {
            _graphClient = graphClient ?? throw new ArgumentNullException(nameof(graphClient));
            _callRecordingService = callRecordingService ?? throw new ArgumentNullException(nameof(callRecordingService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public bool IsPolling => _isPolling;
        public DateTimeOffset? LastPollTime => _lastPollTime;

        public async Task StartPollingAsync(CancellationToken cancellationToken = default)
        {
            await _pollingSemaphore.WaitAsync(cancellationToken);
            try
            {
                if (_isPolling)
                {
                    _logger.LogWarning("Call polling is already running");
                    return;
                }

                var pollingIntervalSeconds = _configuration.GetValue<int>("Recording:PollingIntervalSeconds", 30);
                _logger.LogInformation("Starting call polling with interval of {IntervalSeconds} seconds", pollingIntervalSeconds);

                _pollingTimer = new Timer(async _ => await PollForCallsAsync(), null, 
                    TimeSpan.Zero, TimeSpan.FromSeconds(pollingIntervalSeconds));
                    
                _isPolling = true;
                _logger.LogInformation("Call polling started successfully");
            }
            finally
            {
                _pollingSemaphore.Release();
            }
        }

        public async Task StopPollingAsync()
        {
            await _pollingSemaphore.WaitAsync();
            try
            {
                if (!_isPolling)
                {
                    _logger.LogWarning("Call polling is not currently running");
                    return;
                }

                _pollingTimer?.Dispose();
                _pollingTimer = null;
                _isPolling = false;
                _logger.LogInformation("Call polling stopped");
            }
            finally
            {
                _pollingSemaphore.Release();
            }
        }

        private async Task PollForCallsAsync()
        {
            if (!await _pollingSemaphore.WaitAsync(100)) // Short timeout to avoid blocking
            {
                _logger.LogDebug("Skipping poll cycle - previous poll still in progress");
                return;
            }

            try
            {
                _lastPollTime = DateTimeOffset.UtcNow;
                _logger.LogDebug("Starting call polling cycle at {PollTime}", _lastPollTime);

                // Poll for active calls
                await PollForActiveCallsAsync();
                
                // Poll for call records (completed calls)
                await PollForCallRecordsAsync();

                _logger.LogDebug("Completed call polling cycle");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during call polling cycle");
            }
            finally
            {
                _pollingSemaphore.Release();
            }
        }

        private async Task PollForActiveCallsAsync()
        {
            try
            {
                _logger.LogDebug("Polling for active calls");
                
                // Note: This requires Calls.AccessMedia.All permission
                // For now, we'll catch and log the permission error
                try
                {
                    var calls = await _graphClient.Communications.Calls.GetAsync();
                    
                    if (calls?.Value != null)
                    {
                        _logger.LogInformation("Found {CallCount} active calls", calls.Value.Count);
                        
                        foreach (var call in calls.Value)
                        {
                            if (!string.IsNullOrEmpty(call.Id) && !_seenCallIds.Contains(call.Id))
                            {
                                _seenCallIds.Add(call.Id);
                                _logger.LogInformation("Processing new call: {CallId}, State: {CallState}", call.Id, call.State);
                                
                                // Process the call for recording
                                await ProcessCallForRecordingAsync(call);
                            }
                        }
                    }
                }
                catch (Exception ex) when (ex.Message.Contains("Forbidden") || ex.Message.Contains("Unauthorized"))
                {
                    _logger.LogWarning("Insufficient permissions to access active calls: {Message}", ex.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error polling for active calls");
            }
        }

        private async Task PollForCallRecordsAsync()
        {
            try
            {
                _logger.LogDebug("Polling for call records");
                
                // Poll for call records from the last few minutes
                var startTime = DateTimeOffset.UtcNow.AddMinutes(-5);
                
                try
                {
                    var callRecords = await _graphClient.Communications.CallRecords
                        .GetAsync(requestConfiguration =>
                        {
                            requestConfiguration.QueryParameters.Filter = $"startDateTime ge {startTime:yyyy-MM-ddTHH:mm:ssZ}";
                            requestConfiguration.QueryParameters.Top = 50;
                        });
                    
                    if (callRecords?.Value != null)
                    {
                        _logger.LogInformation("Found {RecordCount} call records", callRecords.Value.Count);
                        
                        foreach (var callRecord in callRecords.Value)
                        {
                            if (!string.IsNullOrEmpty(callRecord.Id) && !_seenCallIds.Contains(callRecord.Id))
                            {
                                _seenCallIds.Add(callRecord.Id);
                                _logger.LogInformation("Processing new call record: {CallRecordId}", callRecord.Id);
                                
                                // Process the call record
                                await ProcessCallRecordAsync(callRecord);
                            }
                        }
                    }
                }
                catch (Exception ex) when (ex.Message.Contains("Forbidden") || ex.Message.Contains("Unauthorized"))
                {
                    _logger.LogWarning("Insufficient permissions to access call records: {Message}", ex.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error polling for call records");
            }
        }

        private async Task ProcessCallForRecordingAsync(Call call)
        {
            try
            {
                if (call.State == CallState.Established)
                {
                    _logger.LogInformation("Call {CallId} is established, attempting to start recording", call.Id);
                    // Here we would start recording - this requires the bot to be added to the call
                    // For now, we'll just log the opportunity
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing call {CallId} for recording", call.Id);
            }
        }

        private async Task ProcessCallRecordAsync(Microsoft.Graph.Models.CallRecords.CallRecord callRecord)
        {
            try
            {
                _logger.LogInformation("Processing call record {CallRecordId}, Type: {Type}", 
                    callRecord.Id, callRecord.Type);
                
                // Check if this call involves Teams and might need compliance recording
                if (callRecord.Organizer?.User != null)
                {
                    _logger.LogInformation("Call organized by: {OrganizerName} ({OrganizerEmail})", 
                        callRecord.Organizer.User.DisplayName, 
                        callRecord.Organizer.User.Id);
                    
                    // Here we would check compliance requirements and process accordingly
                    // For now, we'll log the call for compliance tracking
                    await _callRecordingService.ProcessCallRecordForComplianceAsync(callRecord);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing call record {CallRecordId}", callRecord.Id);
            }
        }

        public void Dispose()
        {
            _pollingTimer?.Dispose();
            _pollingSemaphore?.Dispose();
        }
    }
}
