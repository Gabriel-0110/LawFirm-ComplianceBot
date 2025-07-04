using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace TeamsComplianceBot.Models;

/// <summary>
/// Information about a Teams meeting/call with enhanced validation and security
/// </summary>
public class MeetingInfo
{
    [Required]
    [StringLength(256, MinimumLength = 1)]
    public string Id { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Title { get; set; }

    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }

    [StringLength(256)]
    public string? Organizer { get; set; }

    [Required]
    [StringLength(36, MinimumLength = 36)] // GUID format
    public string? TenantId { get; set; }

    public List<ParticipantInfo> Participants { get; set; } = new();

    [Url]
    [StringLength(2048)]
    public string? MeetingUrl { get; set; }

    public bool IsRecurring { get; set; }

    [StringLength(1000)]
    public string? RecurrencePattern { get; set; }

    [JsonIgnore]
    public TimeSpan? Duration => EndTime.HasValue && StartTime.HasValue 
        ? EndTime.Value - StartTime.Value 
        : null;

    [JsonIgnore]
    public bool IsCompliant => !string.IsNullOrEmpty(Id) && 
                              !string.IsNullOrEmpty(TenantId) && 
                              Participants.Count > 0;
}

/// <summary>
/// Enhanced participant information with privacy controls
/// </summary>
public class ParticipantInfo
{
    [Required]
    [StringLength(256)]
    public string Id { get; set; } = string.Empty;

    [StringLength(256)]
    public string DisplayName { get; set; } = string.Empty;

    [EmailAddress]
    [StringLength(320)]
    public string? Email { get; set; }

    [StringLength(50)]
    public string Role { get; set; } = "Attendee"; // Organizer, Presenter, Attendee

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LeftAt { get; set; }

    [JsonIgnore]
    public TimeSpan? ParticipationDuration => LeftAt.HasValue 
        ? LeftAt.Value - JoinedAt 
        : DateTime.UtcNow - JoinedAt;

    public bool IsRecordingConsented { get; set; } = false;
    public DateTime? ConsentTimestamp { get; set; }
}

/// <summary>
/// Result of a recording operation with enhanced error handling
/// </summary>
public class RecordingResult
{
    public bool Success { get; set; }
    
    [StringLength(2000)]
    public string? ErrorMessage { get; set; }
    
    public RecordingMetadata? RecordingMetadata { get; set; }
    
    [StringLength(256)]
    public string? RecordingId { get; set; }
    
    public string? ErrorCode { get; set; }
    public Dictionary<string, object> ErrorDetails { get; set; } = new();
    
    public int RetryAttempts { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Create a successful result
    /// </summary>
    public static RecordingResult CreateSuccess(RecordingMetadata metadata, string recordingId)
    {
        return new RecordingResult
        {
            Success = true,
            RecordingMetadata = metadata,
            RecordingId = recordingId
        };
    }
    
    /// <summary>
    /// Create a failed result
    /// </summary>
    public static RecordingResult CreateFailure(string errorMessage, string? errorCode = null, Exception? exception = null)
    {
        var result = new RecordingResult
        {
            Success = false,
            ErrorMessage = errorMessage,
            ErrorCode = errorCode
        };
        
        if (exception != null)
        {
            result.ErrorDetails["ExceptionType"] = exception.GetType().Name;
            result.ErrorDetails["StackTrace"] = exception.StackTrace ?? string.Empty;
        }
        
        return result;
    }
}

/// <summary>
/// Enhanced metadata about a recording with compliance tracking
/// </summary>
public class RecordingMetadata
{
    [Required]
    [StringLength(256)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [StringLength(256)]
    public string MeetingId { get; set; } = string.Empty;

    [Required]
    [StringLength(500)]
    public string MeetingTitle { get; set; } = string.Empty;

    [Required]
    [StringLength(256)]
    public string Organizer { get; set; } = string.Empty;

    [Required]
    public DateTime StartTime { get; set; }

    [Required]
    public DateTime EndTime { get; set; }

    [JsonIgnore]
    public TimeSpan Duration => EndTime - StartTime;

    [Required]
    [StringLength(2048)]
    public string BlobPath { get; set; } = string.Empty;

    [Range(0, long.MaxValue)]
    public long FileSizeBytes { get; set; }

    [JsonIgnore]
    public double FileSizeMB => FileSizeBytes / (1024.0 * 1024.0);

    [StringLength(100)]
    public string ContentType { get; set; } = "video/mp4";

    public RecordingStatus Status { get; set; } = RecordingStatus.InProgress;

    public List<ParticipantInfo> Participants { get; set; } = new();

    [Required]
    [StringLength(36)]
    public string TenantId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
    public DateTime? LastAccessedAt { get; set; }

    [StringLength(2048)]
    public string? TranscriptionPath { get; set; }

    public bool HasTranscription { get; set; }

    /// <summary>
    /// Custom metadata for extensibility
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    [Required]
    public RetentionPolicy RetentionPolicy { get; set; } = new();

    /// <summary>
    /// Compliance validation results
    /// </summary>
    public ComplianceValidation ComplianceValidation { get; set; } = new();

    /// <summary>
    /// Hash for integrity verification
    /// </summary>
    [StringLength(128)]
    public string? FileHash { get; set; }

    /// <summary>
    /// Encryption information
    /// </summary>
    public EncryptionInfo? EncryptionInfo { get; set; }

    [JsonIgnore]
    public bool IsExpired => DateTime.UtcNow > RetentionPolicy.ExpirationDate;

    [JsonIgnore]
    public bool IsCompliant => ComplianceValidation.IsValid && 
                              !string.IsNullOrEmpty(FileHash) && 
                              FileSizeBytes > 0;
}

/// <summary>
/// Status of a recording with more granular states
/// </summary>
public enum RecordingStatus
{
    /// <summary>
    /// Recording is currently in progress
    /// </summary>
    InProgress,
    
    /// <summary>
    /// Recording completed successfully
    /// </summary>
    Completed,
    
    /// <summary>
    /// Recording failed due to an error
    /// </summary>
    Failed,
    
    /// <summary>
    /// Recording is being processed (e.g., transcription, compression)
    /// </summary>
    Processing,
    
    /// <summary>
    /// Recording has been archived for long-term storage
    /// </summary>
    Archived,
    
    /// <summary>
    /// Recording has been deleted
    /// </summary>
    Deleted,
    
    /// <summary>
    /// Recording is pending (scheduled but not started)
    /// </summary>
    Pending,
    
    /// <summary>
    /// Recording is paused
    /// </summary>
    Paused,
    
    /// <summary>
    /// Recording is under legal hold
    /// </summary>
    LegalHold
}

/// <summary>
/// Enhanced retention policy with audit trail
/// </summary>
public class RetentionPolicy
{
    [Range(1, 36500)] // 1 day to 100 years
    public int RetentionDays { get; set; } = 2555; // 7 years default

    public DateTime ExpirationDate { get; set; }
    
    public bool IsLegalHold { get; set; }
    
    [StringLength(1000)]
    public string? LegalHoldReason { get; set; }
    
    public bool AutoDelete { get; set; } = true;
    
    [Required]
    [StringLength(20)]
    public string PolicyVersion { get; set; } = "1.0";
    
    [StringLength(256)]
    public string? PolicySetBy { get; set; }
    
    public DateTime PolicySetAt { get; set; } = DateTime.UtcNow;
    
    [StringLength(500)]
    public string? PolicyReason { get; set; }
    
    /// <summary>
    /// Regulatory requirements this policy satisfies
    /// </summary>
    public List<string> RegulatoryRequirements { get; set; } = new();
    
    [JsonIgnore]
    public bool IsActive => !IsLegalHold && DateTime.UtcNow <= ExpirationDate;
    
    [JsonIgnore]
    public TimeSpan RemainingTime => ExpirationDate > DateTime.UtcNow 
        ? ExpirationDate - DateTime.UtcNow 
        : TimeSpan.Zero;
}

/// <summary>
/// Compliance validation results
/// </summary>
public class ComplianceValidation
{
    public bool IsValid { get; set; } = true;
    
    public List<ComplianceViolation> Violations { get; set; } = new();
    
    public DateTime ValidatedAt { get; set; } = DateTime.UtcNow;
    
    [StringLength(256)]
    public string? ValidatedBy { get; set; }
    
    [StringLength(20)]
    public string ValidationVersion { get; set; } = "1.0";
    
    public Dictionary<string, object> ValidationMetadata { get; set; } = new();
    
    [JsonIgnore]
    public bool HasCriticalViolations => Violations.Any(v => v.Severity == ViolationSeverity.Critical);
}

/// <summary>
/// Individual compliance violation
/// </summary>
public class ComplianceViolation
{
    [Required]
    [StringLength(100)]
    public string ViolationType { get; set; } = string.Empty;
    
    [Required]
    [StringLength(1000)]
    public string Description { get; set; } = string.Empty;
    
    public ViolationSeverity Severity { get; set; } = ViolationSeverity.Medium;
    
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    
    [StringLength(2000)]
    public string? RecommendedAction { get; set; }
    
    public bool IsResolved { get; set; }
    
    public DateTime? ResolvedAt { get; set; }
    
    [StringLength(256)]
    public string? ResolvedBy { get; set; }
}

/// <summary>
/// Severity levels for compliance violations
/// </summary>
public enum ViolationSeverity
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Encryption information for recordings
/// </summary>
public class EncryptionInfo
{
    [Required]
    [StringLength(50)]
    public string Algorithm { get; set; } = "AES-256-GCM";
    
    [StringLength(500)]
    public string? KeyVaultKeyId { get; set; }
    
    public DateTime EncryptedAt { get; set; } = DateTime.UtcNow;
    
    [StringLength(256)]
    public string? EncryptedBy { get; set; }
    
    public bool IsClientSideEncrypted { get; set; }
    
    public bool IsTransitEncrypted { get; set; } = true;
    
    public bool IsAtRestEncrypted { get; set; } = true;
}

/// <summary>
/// Enhanced system status information with detailed health metrics
/// </summary>
public class SystemStatus
{
    [StringLength(50)]
    public string OverallStatus { get; set; } = "Unknown";
    
    public int ActiveRecordings { get; set; }
    public int TotalRecordingsToday { get; set; }
    public int TotalRecordingsThisWeek { get; set; }
    public int TotalRecordingsThisMonth { get; set; }
    
    [StringLength(50)]
    public string ComplianceStatus { get; set; } = "Unknown";
    
    [Range(0, 100)]
    public double StorageUsagePercentage { get; set; }
    
    public long TotalStorageBytes { get; set; }
    public long AvailableStorageBytes { get; set; }
    
    public DateTime LastHealthCheck { get; set; } = DateTime.UtcNow;
    
    public Dictionary<string, ServiceHealthStatus> ServiceStatus { get; set; } = new();
    
    public List<SystemAlert> ActiveAlerts { get; set; } = new();
    
    public PerformanceMetrics Performance { get; set; } = new();
    
    [StringLength(20)]
    public string Version { get; set; } = "1.0.0";
    
    public DateTime Uptime { get; set; } = DateTime.UtcNow;
    
    [JsonIgnore]
    public TimeSpan UptimeDuration => DateTime.UtcNow - Uptime;
    
    [JsonIgnore]
    public bool IsHealthy => OverallStatus == "Healthy" && 
                           !ActiveAlerts.Any(a => a.Severity == AlertSeverity.Critical);
}

/// <summary>
/// Service health status details
/// </summary>
public class ServiceHealthStatus
{
    [StringLength(50)]
    public string Status { get; set; } = "Unknown";
    
    public DateTime LastChecked { get; set; } = DateTime.UtcNow;
    
    public TimeSpan ResponseTime { get; set; }
    
    [StringLength(500)]
    public string? Message { get; set; }
    
    public Dictionary<string, object> Metrics { get; set; } = new();
}

/// <summary>
/// System performance metrics
/// </summary>
public class PerformanceMetrics
{
    public double CpuUsagePercentage { get; set; }
    public double MemoryUsagePercentage { get; set; }
    public int ConcurrentRecordings { get; set; }
    public double AverageRecordingStartTimeSeconds { get; set; }
    public double AverageRecordingStopTimeSeconds { get; set; }
    public int TotalRequestsLastHour { get; set; }
    public int FailedRequestsLastHour { get; set; }
    
    [JsonIgnore]
    public double SuccessRatePercentage => TotalRequestsLastHour > 0 
        ? ((double)(TotalRequestsLastHour - FailedRequestsLastHour) / TotalRequestsLastHour) * 100 
        : 100;
}

/// <summary>
/// System alerts with severity tracking
/// </summary>
public class SystemAlert
{
    [Required]
    [StringLength(256)]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    [Required]
    [StringLength(100)]
    public string AlertType { get; set; } = string.Empty;
    
    [Required]
    [StringLength(1000)]
    public string Message { get; set; } = string.Empty;
    
    public AlertSeverity Severity { get; set; } = AlertSeverity.Medium;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public bool IsAcknowledged { get; set; }
    
    public DateTime? AcknowledgedAt { get; set; }
    
    [StringLength(256)]
    public string? AcknowledgedBy { get; set; }
    
    public bool IsResolved { get; set; }
    
    public DateTime? ResolvedAt { get; set; }
    
    [StringLength(256)]
    public string? ResolvedBy { get; set; }
    
    public Dictionary<string, object> AlertData { get; set; } = new();
}

/// <summary>
/// Alert severity levels
/// </summary>
public enum AlertSeverity
{
    Info,
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Enhanced compliance event types with more granular tracking
/// </summary>
public enum ComplianceEventType
{
    // Recording lifecycle events
    RecordingStarted,
    RecordingCompleted,
    RecordingFailed,
    RecordingPaused,
    RecordingResumed,
    RecordingCancelled,
    
    // Access and audit events
    RecordingAccessed,
    RecordingDownloaded,
    RecordingShared,
    RecordingDeleted,
    RecordingAvailable,
    
    // Policy and compliance events
    RetentionPolicyApplied,
    RetentionPolicyUpdated,
    LegalHoldApplied,
    LegalHoldRemoved,
    ComplianceViolation,
    ComplianceValidationPassed,
    ComplianceValidationFailed,
    
    // System and security events
    SystemError,
    SecurityBreach,
    UnauthorizedAccess,
    DataIntegrityCheck,
    BackupCreated,
    BackupRestored,
    
    // User and consent events
    ConsentGranted,
    ConsentRevoked,
    ParticipantJoined,
    ParticipantLeft,
    
    // Call lifecycle events
    CallJoined,
    CallEnded,
    
    // Administrative events
    PolicyUpdated,
    UserPermissionChanged,
    ConfigurationChanged,
    SystemMaintenance
}

/// <summary>
/// Enhanced compliance event log entry with security and audit features
/// </summary>
public class ComplianceEvent
{
    [Required]
    [StringLength(256)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public ComplianceEventType EventType { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [StringLength(256)]
    public string? UserId { get; set; }

    [StringLength(256)]
    public string? UserName { get; set; }

    [StringLength(256)]
    public string? MeetingId { get; set; }

    [StringLength(256)]
    public string? RecordingId { get; set; }

    [Required]
    [StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [StringLength(36)]
    public string TenantId { get; set; } = string.Empty;

    public Dictionary<string, object> AdditionalData { get; set; } = new();

    [StringLength(100)]
    public string Source { get; set; } = "TeamsComplianceBot";

    [StringLength(45)] // IPv6 max length
    public string? IpAddress { get; set; }

    [StringLength(500)]
    public string? UserAgent { get; set; }

    /// <summary>
    /// Correlation ID for tracking related events
    /// </summary>
    [StringLength(256)]
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Session ID for grouping events in the same session
    /// </summary>
    [StringLength(256)]
    public string? SessionId { get; set; }

    /// <summary>
    /// Risk level of the event
    /// </summary>
    public RiskLevel RiskLevel { get; set; } = RiskLevel.Low;

    /// <summary>
    /// Geographic location of the event
    /// </summary>
    [StringLength(100)]
    public string? Location { get; set; }

    /// <summary>
    /// Device information
    /// </summary>
    [StringLength(500)]
    public string? DeviceInfo { get; set; }

    /// <summary>
    /// Whether this event requires immediate attention
    /// </summary>
    public bool RequiresAlert { get; set; }

    /// <summary>
    /// Whether this event has been processed
    /// </summary>
    public bool IsProcessed { get; set; }

    public DateTime? ProcessedAt { get; set; }

    [StringLength(256)]
    public string? ProcessedBy { get; set; }

    /// <summary>
    /// Tags for categorizing and filtering events
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Hash for event integrity verification
    /// </summary>
    [StringLength(128)]
    public string? EventHash { get; set; }
}

/// <summary>
/// Risk levels for compliance events
/// </summary>
public enum RiskLevel
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Enhanced recording configuration settings with validation
/// </summary>
public class RecordingConfiguration
{
    public bool AutoStartRecording { get; set; } = true;
    
    public bool NotifyParticipants { get; set; } = true;
    
    public bool RequireConsent { get; set; } = true;
    
    public bool IncludeVideo { get; set; } = true;
    
    public bool IncludeAudio { get; set; } = true;
    
    public bool GenerateTranscription { get; set; } = true;
    
    [StringLength(20)]
    public string VideoQuality { get; set; } = "HD"; // SD, HD, FHD, 4K
    
    [StringLength(20)]
    public string AudioQuality { get; set; } = "High"; // Low, Medium, High, Lossless
    
    public bool EnableScreenSharing { get; set; } = true;
    
    [Range(1, 1440)] // 1 minute to 24 hours
    public int MaxRecordingLengthMinutes { get; set; } = 480; // 8 hours
    
    /// <summary>
    /// User types that should be excluded from recording
    /// </summary>
    public List<string> ExcludedUserTypes { get; set; } = new();
    
    /// <summary>
    /// Required tags that must be present on meetings to be recorded
    /// </summary>
    public List<string> RequiredRecordingTags { get; set; } = new();
    
    /// <summary>
    /// Recording quality settings
    /// </summary>
    public QualitySettings QualitySettings { get; set; } = new();
    
    /// <summary>
    /// Storage settings for recordings
    /// </summary>
    public StorageSettings StorageSettings { get; set; } = new();
    
    /// <summary>
    /// Privacy and consent settings
    /// </summary>
    public PrivacySettings PrivacySettings { get; set; } = new();
    
    /// <summary>
    /// Compliance and regulatory settings
    /// </summary>
    public ComplianceSettings ComplianceSettings { get; set; } = new();
}

/// <summary>
/// Quality settings for recordings
/// </summary>
public class QualitySettings
{
    [Range(1, 60)]
    public int VideoFrameRate { get; set; } = 30; // FPS
    
    [Range(128, 320)]
    public int AudioBitRate { get; set; } = 256; // kbps
    
    [Range(500, 50000)]
    public int VideoBitRate { get; set; } = 5000; // kbps
    
    [StringLength(20)]
    public string VideoCodec { get; set; } = "H.264";
    
    [StringLength(20)]
    public string AudioCodec { get; set; } = "AAC";
    
    public bool EnableHardwareAcceleration { get; set; } = true;
}

/// <summary>
/// Storage settings for recordings
/// </summary>
public class StorageSettings
{
    [StringLength(50)]
    public string StorageTier { get; set; } = "Hot"; // Hot, Cool, Archive
    
    [StringLength(100)]
    public string CompressionLevel { get; set; } = "Medium"; // None, Low, Medium, High
    
    public bool EnableDeduplication { get; set; } = true;
    
    public bool EnableChunking { get; set; } = true;
    
    [Range(1, 100)]
    public int ChunkSizeMB { get; set; } = 50;
    
    [StringLength(256)]
    public string? CustomStoragePath { get; set; }
}

/// <summary>
/// Privacy and consent settings
/// </summary>
public class PrivacySettings
{
    public bool RequireExplicitConsent { get; set; } = true;
    
    public bool AllowConsentWithdrawal { get; set; } = true;
    
    public bool AnonymizeParticipants { get; set; } = false;
    
    public bool RedactSensitiveContent { get; set; } = true;
    
    [Range(1, 60)]
    public int ConsentTimeoutMinutes { get; set; } = 5;
    
    public List<string> SensitiveDataPatterns { get; set; } = new();
}

/// <summary>
/// Compliance and regulatory settings
/// </summary>
public class ComplianceSettings
{
    public List<string> ApplicableRegulations { get; set; } = new(); // GDPR, HIPAA, SOX, etc.
    
    public bool EnableDataClassification { get; set; } = true;
    
    public bool EnableAutomaticRetention { get; set; } = true;
    
    public bool EnableImmutableStorage { get; set; } = true;
    
    public bool RequireDigitalSignature { get; set; } = false;
    
    [Range(1, 36500)]
    public int DefaultRetentionDays { get; set; } = 2555; // 7 years
    
    [StringLength(256)]
    public string? ComplianceOfficer { get; set; }
    
    public Dictionary<string, object> CustomComplianceSettings { get; set; } = new();
}

/// <summary>
/// Enhanced user access level for compliance operations with role-based permissions
/// </summary>
public enum UserAccessLevel
{
    /// <summary>
    /// No access to compliance features
    /// </summary>
    None,
    
    /// <summary>
    /// Can view recordings they are authorized to see
    /// </summary>
    Viewer,
    
    /// <summary>
    /// Can view and manage recordings within their scope
    /// </summary>
    Operator,
    
    /// <summary>
    /// Can manage recordings and compliance policies
    /// </summary>
    Admin,
    
    /// <summary>
    /// Full access to all compliance features and system administration
    /// </summary>
    SuperAdmin,
    
    /// <summary>
    /// Compliance officer with specialized permissions
    /// </summary>
    ComplianceOfficer,
    
    /// <summary>
    /// Legal counsel with access to legal hold and litigation features
    /// </summary>
    LegalCounsel,
    
    /// <summary>
    /// Auditor with read-only access to all compliance data
    /// </summary>
    Auditor
}

/// <summary>
/// Detailed user permissions for fine-grained access control
/// </summary>
public class UserPermissions
{
    [Required]
    [StringLength(256)]
    public string UserId { get; set; } = string.Empty;
    
    public UserAccessLevel AccessLevel { get; set; } = UserAccessLevel.None;
    
    public List<string> AllowedActions { get; set; } = new();
    
    public List<string> DeniedActions { get; set; } = new();
    
    public List<string> ScopedTenants { get; set; } = new(); // Tenants user can access
    
    public DateTime PermissionsGrantedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? PermissionsExpiresAt { get; set; }
    
    [StringLength(256)]
    public string? GrantedBy { get; set; }
    
    [StringLength(1000)]
    public string? Justification { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    public DateTime? LastAccessAt { get; set; }
    
    [JsonIgnore]
    public bool IsExpired => PermissionsExpiresAt.HasValue && DateTime.UtcNow > PermissionsExpiresAt.Value;
}

/// <summary>
/// Audit trail entry for tracking user actions
/// </summary>
public class AuditTrailEntry
{
    [Required]
    [StringLength(256)]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    [Required]
    [StringLength(256)]
    public string UserId { get; set; } = string.Empty;
    
    [StringLength(256)]
    public string? UserName { get; set; }
    
    [Required]
    [StringLength(100)]
    public string Action { get; set; } = string.Empty;
    
    [StringLength(256)]
    public string? ResourceId { get; set; }
    
    [StringLength(100)]
    public string? ResourceType { get; set; }
    
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    [StringLength(45)]
    public string? IpAddress { get; set; }
    
    [StringLength(500)]
    public string? UserAgent { get; set; }
    
    [StringLength(2000)]
    public string? Details { get; set; }
    
    public bool Success { get; set; } = true;
    
    [StringLength(1000)]
    public string? ErrorMessage { get; set; }
    
    [StringLength(256)]
    public string? SessionId { get; set; }
    
    [StringLength(256)]
    public string? CorrelationId { get; set; }
    
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Configuration for compliance bot behavior
/// </summary>
public class ComplianceBotConfiguration
{
    [Required]
    [StringLength(256)]
    public string BotId { get; set; } = string.Empty;
    
    [Required]
    [StringLength(256)]
    public string BotName { get; set; } = "Teams Compliance Bot";
    
    [StringLength(1000)]
    public string? BotDescription { get; set; }
    
    public bool IsEnabled { get; set; } = true;
    
    public RecordingConfiguration DefaultRecordingConfiguration { get; set; } = new();
    
    public NotificationConfiguration NotificationConfiguration { get; set; } = new();
    
    public SecurityConfiguration SecurityConfiguration { get; set; } = new();
    
    public Dictionary<string, object> CustomSettings { get; set; } = new();
    
    public DateTime ConfigurationVersion { get; set; } = DateTime.UtcNow;
    
    [StringLength(256)]
    public string? ConfigurationSetBy { get; set; }
}

/// <summary>
/// Notification configuration settings
/// </summary>
public class NotificationConfiguration
{
    public bool EnableRecordingStartNotifications { get; set; } = true;
    
    public bool EnableRecordingEndNotifications { get; set; } = true;
    
    public bool EnableComplianceAlerts { get; set; } = true;
    
    public bool EnableErrorNotifications { get; set; } = true;
    
    [StringLength(256)]
    public string? WebhookUrl { get; set; }
    
    [StringLength(256)]
    public string? EmailDistributionList { get; set; }
    
    [StringLength(256)]
    public string? SlackChannel { get; set; }
    
    [StringLength(256)]
    public string? TeamsChannel { get; set; }
    
    public List<string> NotificationRecipients { get; set; } = new();
    
    public Dictionary<ComplianceEventType, bool> EventNotificationSettings { get; set; } = new();
}

/// <summary>
/// Security configuration settings
/// </summary>
public class SecurityConfiguration
{
    public bool EnableEncryptionAtRest { get; set; } = true;
    
    public bool EnableEncryptionInTransit { get; set; } = true;
    
    public bool RequireMfaForAccess { get; set; } = true;
    
    public bool EnableAuditLogging { get; set; } = true;
    
    public bool EnableThreatDetection { get; set; } = true;
    
    [Range(1, 525600)] // 1 minute to 1 year
    public int SessionTimeoutMinutes { get; set; } = 480; // 8 hours
    
    [Range(1, 10)]
    public int MaxLoginAttempts { get; set; } = 3;
    
    [Range(1, 1440)] // 1 minute to 24 hours
    public int LockoutDurationMinutes { get; set; } = 30;
    
    public List<string> AllowedIpRanges { get; set; } = new();
    
    public List<string> BlockedIpRanges { get; set; } = new();
    
    [StringLength(256)]
    public string? KeyVaultUrl { get; set; }
    
    public Dictionary<string, object> SecurityPolicies { get; set; } = new();
}
