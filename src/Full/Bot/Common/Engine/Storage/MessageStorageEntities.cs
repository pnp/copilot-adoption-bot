using Azure;
using Azure.Data.Tables;

namespace Engine.Storage;

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

    /// <summary>
    /// Number of partitions this batch's deliveries were spread across. Persisted so the
    /// shard count can be raised later (see docs/SCALING.md) without making existing
    /// batches unreadable.
    /// </summary>
    public int ShardCount { get; set; } = DeliveryKey.DefaultShardCount;

    /// <summary>
    /// Total number of recipients in this batch.
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Running count of deliveries that succeeded. Maintained incrementally by the
    /// dispatcher so dashboard statistics never have to scan delivery rows.
    /// </summary>
    public int SentCount { get; set; }

    /// <summary>
    /// Running count of deliveries that failed permanently.
    /// </summary>
    public int FailedCount { get; set; }

    /// <summary>
    /// UTC timestamp of the last counter update, so operators can spot a stalled batch.
    /// </summary>
    public DateTime? LastProgressUtc { get; set; }

    /// <summary>
    /// Lifecycle state: Queued, Expanding, Running, Paused, Cancelled or Complete.
    /// The dispatcher checks this before each send, which is what makes an in-flight batch
    /// stoppable - previously 150,000 queued messages could not be recalled at all.
    /// </summary>
    public string Status { get; set; } = BatchStatus.Queued;

    /// <summary>
    /// Earliest UTC time this batch may start sending. Null means send immediately.
    /// </summary>
    public DateTime? ScheduledSendUtc { get; set; }

    /// <summary>
    /// How far recipient expansion has progressed, so an interrupted expansion resumes rather
    /// than restarting. Necessary because the worker is unloaded whenever it goes idle.
    /// </summary>
    public int ExpandedCount { get; set; }
}

/// <summary>
/// Batch lifecycle states.
/// </summary>
public static class BatchStatus
{
    /// <summary>Accepted, waiting for recipient expansion to begin.</summary>
    public const string Queued = "Queued";

    /// <summary>Recipients are being resolved and enqueued.</summary>
    public const string Expanding = "Expanding";

    /// <summary>Expansion complete; deliveries are being dispatched.</summary>
    public const string Running = "Running";

    /// <summary>Temporarily halted by an operator; queued messages are skipped.</summary>
    public const string Paused = "Paused";

    /// <summary>Stopped by an operator; remaining deliveries are dropped.</summary>
    public const string Cancelled = "Cancelled";

    /// <summary>All deliveries reached a terminal state.</summary>
    public const string Complete = "Complete";

    /// <summary>States in which the dispatcher must not send.</summary>
    public static bool IsStopped(string? status) =>
        string.Equals(status, Cancelled, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, Paused, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Table storage entity for message send logs (one row per recipient per batch).
///
/// <para>
/// Keys follow <see cref="DeliveryKey"/>: <c>PartitionKey = "{batchId}~{shard}"</c> and
/// <c>RowKey = normalised recipient UPN</c>. Using the recipient as the row key makes
/// writes idempotent — re-processing a recipient upserts the same row instead of
/// inserting a duplicate.
/// </para>
/// </summary>
public class MessageLogTableEntity : ITableEntity
{
    /// <summary>
    /// <c>"{batchId}~{shard}"</c>. Assign via <see cref="DeliveryKey.PartitionFor"/>.
    /// </summary>
    public string PartitionKey { get; set; } = null!;

    /// <summary>
    /// Normalised (lower-cased) recipient UPN. Assign via <see cref="DeliveryKey.RowKeyFor"/>.
    /// </summary>
    public string RowKey { get; set; } = null!;

    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    /// <summary>
    /// Reference to the message batch ID
    /// </summary>
    public string MessageBatchId { get; set; } = null!;

    /// <summary>
    /// When the message was queued
    /// </summary>
    public DateTime SentDate { get; set; }

    /// <summary>
    /// UPN of the recipient, in its original casing (the row key holds the normalised form).
    /// </summary>
    public string? RecipientUpn { get; set; }

    /// <summary>
    /// Send status (e.g., "Success", "Failed", "Pending")
    /// </summary>
    public string Status { get; set; } = null!;

    /// <summary>
    /// Last error message if the send failed
    /// </summary>
    public string? LastError { get; set; }
}

/// <summary>
/// Per-user index of deliveries that have not yet been sent.
///
/// <para>
/// Azure Table Storage has no secondary indexes, so "the newest pending delivery for this
/// user" — needed when a user opens Teams after the bot app was installed for them — is
/// served by this explicitly maintained index rather than by scanning every delivery row.
/// </para>
///
/// <para>
/// <c>PartitionKey</c> is the normalised UPN (a small per-user partition) and
/// <c>RowKey</c> is an inverted tick count so the newest entry sorts first; reading the
/// first row of the partition is therefore a bounded, cheap query.
/// </para>
/// </summary>
public class PendingDeliveryTableEntity : ITableEntity
{
    /// <summary>Normalised recipient UPN.</summary>
    public string PartitionKey { get; set; } = null!;

    /// <summary><c>"{invertedTicks}~{batchId}"</c>, newest first.</summary>
    public string RowKey { get; set; } = null!;

    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    /// <summary>Batch this pending delivery belongs to.</summary>
    public string BatchId { get; set; } = null!;

    /// <summary>Template to render for this delivery.</summary>
    public string TemplateId { get; set; } = null!;

    /// <summary>Recipient UPN in original casing.</summary>
    public string RecipientUpn { get; set; } = null!;

    /// <summary>When the delivery was queued.</summary>
    public DateTime CreatedUtc { get; set; }
}
