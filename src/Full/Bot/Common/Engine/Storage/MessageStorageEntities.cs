using Azure;
using Azure.Data.Tables;

namespace Common.Engine.Storage;

/// <summary>
/// Table storage entity for message template metadata with blob reference
/// </summary>
public class MessageTemplateTableEntity : ITableEntity
{
    public static string PartitionKeyVal => "MessageTemplates";
    
    public string PartitionKey { get => PartitionKeyVal; set { } }

    /// <summary>
    /// Template ID (GUID)
    /// </summary>
    public string RowKey { get; set; } = null!;

    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    /// <summary>
    /// Display name of the template
    /// </summary>
    public string TemplateName { get; set; } = null!;

    /// <summary>
    /// URL to the JSON blob in blob storage
    /// </summary>
    public string BlobUrl { get; set; } = null!;

    /// <summary>
    /// UPN of the user who created the template
    /// </summary>
    public string CreatedByUpn { get; set; } = null!;

    /// <summary>
    /// Date the template was created
    /// </summary>
    public DateTime CreatedDate { get; set; }
}

/// <summary>
/// Table storage entity for message batch (group of messages sent together)
/// </summary>
public class MessageBatchTableEntity : ITableEntity
{
    public static string PartitionKeyVal => "MessageBatches";
    
    public string PartitionKey { get => PartitionKeyVal; set { } }

    /// <summary>
    /// Batch ID (GUID)
    /// </summary>
    public string RowKey { get; set; } = null!;

    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    /// <summary>
    /// Display name of the batch
    /// </summary>
    public string BatchName { get; set; } = null!;

    /// <summary>
    /// Reference to the template ID
    /// </summary>
    public string TemplateId { get; set; } = null!;

    /// <summary>
    /// UPN of the user who sent the batch
    /// </summary>
    public string SenderUpn { get; set; } = null!;

    /// <summary>
    /// Date the batch was created
    /// </summary>
    public DateTime CreatedDate { get; set; }
}

/// <summary>
/// Table storage entity for message send logs
/// </summary>
public class MessageLogTableEntity : ITableEntity
{
    public static string PartitionKeyVal => "MessageLogs";
    
    public string PartitionKey { get => PartitionKeyVal; set { } }

    /// <summary>
    /// Log ID (GUID)
    /// </summary>
    public string RowKey { get; set; } = null!;

    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    /// <summary>
    /// Reference to the message batch ID
    /// </summary>
    public string MessageBatchId { get; set; } = null!;

    /// <summary>
    /// When the message was sent
    /// </summary>
    public DateTime SentDate { get; set; }

    /// <summary>
    /// UPN of the recipient (optional)
    /// </summary>
    public string? RecipientUpn { get; set; }

    /// <summary>
    /// Send status (e.g., "Sent", "Failed", "Pending")
    /// </summary>
    public string Status { get; set; } = null!;

    /// <summary>
    /// Last error message if the send failed
    /// </summary>
    public string? LastError { get; set; }
}
