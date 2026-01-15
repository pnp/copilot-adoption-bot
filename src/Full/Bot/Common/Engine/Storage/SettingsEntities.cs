using Azure;
using Azure.Data.Tables;

namespace Common.Engine.Storage;

/// <summary>
/// Table storage entity for application settings.
/// Stores configurable settings that can be modified at runtime.
/// </summary>
public class AppSettingsTableEntity : ITableEntity
{
    public static string PartitionKeyVal => "AppSettings";
    public static string SingletonRowKey => "Settings";
    
    public string PartitionKey { get => PartitionKeyVal; set { } }

    /// <summary>
    /// Using a singleton row key since there's only one settings record
    /// </summary>
    public string RowKey { get => SingletonRowKey; set { } }

    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    /// <summary>
    /// Custom system prompt for AI follow-up chat conversations.
    /// If null or empty, the default prompt will be used.
    /// </summary>
    public string? FollowUpChatSystemPrompt { get; set; }

    /// <summary>
    /// Date the settings were last modified
    /// </summary>
    public DateTime? LastModifiedDate { get; set; }

    /// <summary>
    /// UPN of the user who last modified the settings
    /// </summary>
    public string? LastModifiedByUpn { get; set; }
}
