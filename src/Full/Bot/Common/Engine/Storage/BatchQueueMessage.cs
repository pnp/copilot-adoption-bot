namespace Engine.Storage;

/// <summary>
/// Message queued for processing when a new batch is created.
///
/// <para>
/// Carries the full delivery identity (partition key + recipient) so the dispatcher can
/// address the exact delivery row with a point read. It must never fall back to searching
/// for "the newest pending delivery for this UPN" — doing so sends the wrong card when a
/// user has more than one pending delivery.
/// </para>
/// </summary>
public class BatchQueueMessage
{
    /// <summary>
    /// Batch ID that was created
    /// </summary>
    public string BatchId { get; set; } = null!;

    /// <summary>
    /// Partition key of the delivery row (<c>"{batchId}~{shard}"</c>). Resolved once at
    /// enqueue time so the dispatcher needs no shard-count lookup.
    /// </summary>
    public string DeliveryPartitionKey { get; set; } = null!;

    /// <summary>
    /// Row key of the delivery row: the normalised recipient UPN.
    /// </summary>
    public string DeliveryRowKey { get; set; } = null!;

    /// <summary>
    /// Recipient UPN in original casing.
    /// </summary>
    public string RecipientUpn { get; set; } = null!;

    /// <summary>
    /// Template ID for the message
    /// </summary>
    public string TemplateId { get; set; } = null!;
}
