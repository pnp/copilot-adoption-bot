using Azure;
using Azure.Data.Tables;

namespace Common.Engine.Storage;

/// <summary>
/// Table storage entity for cached user data from Microsoft Graph.
/// Supports delta query synchronization for efficient updates.
/// </summary>
public class UserCacheTableEntity : ITableEntity
{
    public static string PartitionKeyVal => "Users";

    public string PartitionKey { get => PartitionKeyVal; set { } }

    /// <summary>
    /// User Principal Name (used as RowKey for direct lookups)
    /// </summary>
    public string RowKey { get; set; } = null!;

    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    // Core user properties
    public string Id { get; set; } = null!;
    public string UserPrincipalName { get; set; } = null!;
    public string? DisplayName { get; set; }
    public string? GivenName { get; set; }
    public string? Surname { get; set; }
    public string? Mail { get; set; }

    // Extended properties for smart group matching
    public string? Department { get; set; }
    public string? JobTitle { get; set; }
    public string? OfficeLocation { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? State { get; set; }
    public string? CompanyName { get; set; }
    public string? EmployeeType { get; set; }
    public DateTime? EmployeeHireDate { get; set; }

    // Manager information
    public string? ManagerUpn { get; set; }
    public string? ManagerDisplayName { get; set; }

    // Caching metadata
    public DateTime LastSyncedDate { get; set; } = DateTime.UtcNow;
    public DateTime? LastCopilotStatsUpdate { get; set; }

    // Copilot usage stats (from beta API)
    public DateTime? CopilotLastActivityDate { get; set; }
    public DateTime? CopilotChatLastActivityDate { get; set; }
    public DateTime? TeamscopilotLastActivityDate { get; set; }
    public DateTime? WordCopilotLastActivityDate { get; set; }
    public DateTime? ExcelCopilotLastActivityDate { get; set; }
    public DateTime? PowerPointCopilotLastActivityDate { get; set; }
    public DateTime? OutlookCopilotLastActivityDate { get; set; }
    public DateTime? OneNoteCopilotLastActivityDate { get; set; }
    public DateTime? LoopCopilotLastActivityDate { get; set; }

    // License information
    public bool HasCopilotLicense { get; set; }

    /// <summary>
    /// Indicates if this record represents a deleted user (from delta query)
    /// </summary>
    public bool IsDeleted { get; set; }
}

/// <summary>
/// Table storage entity for tracking delta query synchronization state.
/// Stores the delta token/link for incremental user updates.
/// </summary>
public class UserSyncMetadataEntity : ITableEntity
{
    public static string PartitionKeyVal => "SyncMetadata";
    public static string SingletonRowKey => "UserDeltaSync";

    public string PartitionKey { get => PartitionKeyVal; set { } }
    public string RowKey { get => SingletonRowKey; set { } }

    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    /// <summary>
    /// The delta link or token from the last successful sync.
    /// Used to retrieve only changes since last sync.
    /// </summary>
    public string? DeltaLink { get; set; }

    /// <summary>
    /// Date/time of the last successful full sync
    /// </summary>
    public DateTime? LastFullSyncDate { get; set; }

    /// <summary>
    /// Date/time of the last successful delta sync
    /// </summary>
    public DateTime? LastDeltaSyncDate { get; set; }

    /// <summary>
    /// Number of users synchronized in the last sync operation
    /// </summary>
    public int LastSyncUserCount { get; set; }

    /// <summary>
    /// Date/time of the last successful Copilot stats update
    /// </summary>
    public DateTime? LastCopilotStatsUpdate { get; set; }

    /// <summary>
    /// Status of the last sync: "Success", "Failed", "InProgress"
    /// </summary>
    public string? LastSyncStatus { get; set; }

    /// <summary>
    /// Error message from the last sync if it failed
    /// </summary>
    public string? LastSyncError { get; set; }
}
