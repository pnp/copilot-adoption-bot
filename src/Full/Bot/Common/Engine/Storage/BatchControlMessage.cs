namespace Engine.Storage;

/// <summary>
/// Control message instructing the background worker to expand a batch's recipient sources
/// into delivery rows and enqueue them.
///
/// <para>
/// Only the recipient <em>sources</em> travel through the queue, never the expanded list: a
/// 150,000-UPN payload would exceed the 64 KB Azure Storage Queue message limit many times
/// over, and would have to be re-sent on every retry.
/// </para>
/// </summary>
public class BatchControlMessage
{
    /// <summary>Batch to expand.</summary>
    public string BatchId { get; set; } = null!;

    /// <summary>Explicitly listed recipients.</summary>
    public List<string> RecipientUpns { get; set; } = new();

    /// <summary>Smart groups to resolve into recipients.</summary>
    public List<string> SmartGroupIds { get; set; } = new();
}
