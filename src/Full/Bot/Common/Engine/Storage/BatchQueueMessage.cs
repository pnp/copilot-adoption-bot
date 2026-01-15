namespace Common.Engine.Storage;

/// <summary>
/// Message queued for processing when a new batch is created
/// </summary>
public class BatchQueueMessage
{
    /// <summary>
    /// Batch ID that was created
    /// </summary>
    public string BatchId { get; set; } = null!;

    /// <summary>
    /// Message log ID to process
    /// </summary>
    public string MessageLogId { get; set; } = null!;

    /// <summary>
    /// Recipient UPN
    /// </summary>
    public string RecipientUpn { get; set; } = null!;

    /// <summary>
    /// Template ID for the message
    /// </summary>
    public string TemplateId { get; set; } = null!;
}
