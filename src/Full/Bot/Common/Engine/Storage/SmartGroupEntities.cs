using Azure;
using Azure.Data.Tables;

namespace Common.Engine.Storage;

/// <summary>
/// Table storage entity for smart groups (AI-driven dynamic user groups).
/// Smart groups use natural language descriptions to find matching users via AI Foundry.
/// </summary>
public class SmartGroupTableEntity : ITableEntity
{
    public static string PartitionKeyVal => "SmartGroups";
    
    public string PartitionKey { get => PartitionKeyVal; set { } }

    /// <summary>
    /// Smart Group ID (GUID)
    /// </summary>
    public string RowKey { get; set; } = null!;

    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    /// <summary>
    /// Display name of the smart group
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Natural language description of the target users.
    /// Example: "All employees in the Sales department based in the US"
    /// </summary>
    public string Description { get; set; } = null!;

    /// <summary>
    /// UPN of the user who created the smart group
    /// </summary>
    public string CreatedByUpn { get; set; } = null!;

    /// <summary>
    /// Date the smart group was created
    /// </summary>
    public DateTime CreatedDate { get; set; }

    /// <summary>
    /// Date the smart group was last resolved (members last computed)
    /// </summary>
    public DateTime? LastResolvedDate { get; set; }

    /// <summary>
    /// Number of members found during last resolution
    /// </summary>
    public int? LastResolvedMemberCount { get; set; }
}

/// <summary>
/// Table storage entity for caching resolved smart group members.
/// This is a transient cache that gets refreshed when the smart group is resolved.
/// </summary>
public class SmartGroupMemberCacheEntity : ITableEntity
{
    /// <summary>
    /// Partition key is the smart group ID for efficient querying
    /// </summary>
    public string PartitionKey { get; set; } = null!;

    /// <summary>
    /// Row key is the user UPN
    /// </summary>
    public string RowKey { get; set; } = null!;

    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    /// <summary>
    /// User's display name
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// User's department
    /// </summary>
    public string? Department { get; set; }

    /// <summary>
    /// User's job title
    /// </summary>
    public string? JobTitle { get; set; }

    /// <summary>
    /// Confidence score from AI (0.0 - 1.0)
    /// </summary>
    public double? ConfidenceScore { get; set; }

    /// <summary>
    /// When this member was added to the cache
    /// </summary>
    public DateTime CachedDate { get; set; }
}
