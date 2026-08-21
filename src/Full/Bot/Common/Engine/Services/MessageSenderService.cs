using Engine.Notifications;
using Engine.Storage;
using Microsoft.Extensions.Logging;

namespace Engine.Services;

/// <summary>
/// How a send attempt should be treated by the queue consumer.
/// </summary>
public enum SendDisposition
{
    /// <summary>Delivered. Remove the queue message.</summary>
    Delivered,

    /// <summary>
    /// The bot app had to be installed first; the card will be delivered when the user next
    /// opens Teams, via the pending-delivery index. Remove the queue message.
    /// </summary>
    AwaitingInstall,

    /// <summary>
    /// Permanently undeliverable (user not found, not licensed). Remove the queue message and
    /// record the failure.
    /// </summary>
    PermanentFailure,

    /// <summary>
    /// Transient failure (throttling, timeout, transport error). The queue message must be
    /// left for redelivery - deleting it would silently drop the nudge.
    /// </summary>
    TransientFailure
}

/// <summary>
/// Whether the dispatcher may send for a given batch right now.
/// </summary>
public enum BatchGate
{
    /// <summary>Proceed with the send.</summary>
    Send,

    /// <summary>Batch is paused or not yet due; leave the message queued.</summary>
    Defer,

    /// <summary>Batch is cancelled or gone; discard the message.</summary>
    Drop
}

/// <summary>
/// Service responsible for sending messages to recipients.
/// </summary>
public class MessageSenderService(
    IBotConvoResumeManager botConvoResumeManager,
    IMessageLogStatusWriter messageLogStatusWriter,
    ILogger<MessageSenderService> logger)
{
    /// <summary>
    /// How long a batch's lifecycle state is trusted before re-reading it. Short enough that a
    /// cancel takes effect within seconds, long enough that a 150,000-message drain doesn't read
    /// the batch row once per delivery.
    /// </summary>
    private static readonly TimeSpan GateCacheTtl = TimeSpan.FromSeconds(10);

    private static readonly BoundedCache<string, (BatchGate Gate, DateTime Expires)> _gateCache =
        new(1_000, StringComparer.Ordinal);

    /// <summary>
    /// Send the delivery identified by the queue message.
    ///
    /// <para>
    /// The queue message carries the exact delivery key, so the status write always targets
    /// the delivery that was actually attempted. The previous implementation looked up "the
    /// newest pending delivery for this UPN" while updating a different row, which meant a
    /// user with two pending deliveries got the wrong card and the wrong row was marked sent.
    /// </para>
    /// </summary>
    public async Task<MessageSendResult> SendMessageAsync(BatchQueueMessage queueMessage)
    {
        try
        {
            logger.LogDebug("Processing delivery for {RecipientUpn} in batch {BatchId}",
                queueMessage.RecipientUpn, queueMessage.BatchId);

            var resumeResult = await botConvoResumeManager.ResumeConversation(
                queueMessage.RecipientUpn, queueMessage.BatchId, queueMessage.TemplateId);

            switch (resumeResult.Status)
            {
                case ConversationResumeStatus.MessageSent:
                    await messageLogStatusWriter.UpdateMessageLogStatusAsync(
                        queueMessage.DeliveryPartitionKey, queueMessage.DeliveryRowKey, "Success");

                    // The card has been delivered, so it must not resurface as a pending card
                    // the next time this user opens Teams.
                    await messageLogStatusWriter.ClearPendingDeliveryAsync(
                        queueMessage.RecipientUpn, queueMessage.BatchId);

                    return Result(queueMessage, true, SendDisposition.Delivered);

                case ConversationResumeStatus.AppInstalledPending:
                    // Deliberately left as Pending with its index entry intact: the card is
                    // delivered when the user next opens Teams. Recorded as a distinct status
                    // so the dashboard doesn't report it as delivered.
                    await messageLogStatusWriter.UpdateMessageLogStatusAsync(
                        queueMessage.DeliveryPartitionKey, queueMessage.DeliveryRowKey, "AwaitingInstall");

                    return Result(queueMessage, true, SendDisposition.AwaitingInstall);

                case ConversationResumeStatus.TransientFailure:
                    logger.LogWarning("Transient failure sending to {RecipientUpn}: {Error}. Leaving queued for retry.",
                        queueMessage.RecipientUpn, resumeResult.Message);

                    return Result(queueMessage, false, SendDisposition.TransientFailure, resumeResult.Message);

                case ConversationResumeStatus.Failed:
                default:
                    logger.LogWarning("Permanent failure sending to {RecipientUpn}: {Error}",
                        queueMessage.RecipientUpn, resumeResult.Message);

                    await messageLogStatusWriter.UpdateMessageLogStatusAsync(
                        queueMessage.DeliveryPartitionKey, queueMessage.DeliveryRowKey, "Failed", resumeResult.Message);

                    return Result(queueMessage, false, SendDisposition.PermanentFailure, resumeResult.Message);
            }
        }
        catch (Exception ex)
        {
            // An unhandled exception is assumed transient: better to redeliver (the send is
            // idempotent on the delivery key) than to silently drop a user's nudge.
            logger.LogError(ex, "Unhandled error sending to {RecipientUpn}", queueMessage.RecipientUpn);
            return Result(queueMessage, false, SendDisposition.TransientFailure, ex.Message);
        }
    }

    private static MessageSendResult Result(
        BatchQueueMessage queueMessage, bool success, SendDisposition disposition, string? error = null) =>
        new()
        {
            Success = success,
            Disposition = disposition,
            RecipientUpn = queueMessage.RecipientUpn,
            BatchId = queueMessage.BatchId,
            ErrorMessage = error
        };

    /// <summary>
    /// Record a delivery that exhausted its retry budget, so a dropped nudge is always
    /// visible in the batch's failure count rather than disappearing silently.
    /// </summary>
    public async Task RecordExhaustedAsync(BatchQueueMessage queueMessage, string? error)
    {
        try
        {
            await messageLogStatusWriter.UpdateMessageLogStatusAsync(
                queueMessage.DeliveryPartitionKey, queueMessage.DeliveryRowKey, "Failed",
                error ?? "Exceeded maximum delivery attempts");

            await messageLogStatusWriter.IncrementBatchCountersAsync(queueMessage.BatchId, 0, 1);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to record exhausted delivery for {RecipientUpn}", queueMessage.RecipientUpn);
        }
    }

    /// <summary>
    /// Apply an aggregated counter delta for a batch. Called once per dequeue cycle rather
    /// than once per delivery, to keep contention on the single batch row negligible.
    /// </summary>
    public Task FlushBatchCountersAsync(string batchId, int sentDelta, int failedDelta) =>
        messageLogStatusWriter.IncrementBatchCountersAsync(batchId, sentDelta, failedDelta);

    /// <summary>
    /// Decide whether the dispatcher may send for this batch right now, honouring cancellation,
    /// pausing and scheduled start time. Cached briefly so a 150,000-message drain doesn't read
    /// the batch row once per delivery.
    /// </summary>
    public async Task<BatchGate> GetBatchGateAsync(string batchId)
    {
        if (_gateCache.TryGet(batchId, out var cached) && cached.Expires > DateTime.UtcNow)
        {
            return cached.Gate;
        }

        var gate = await ResolveBatchGateAsync(batchId);
        _gateCache.Set(batchId, (gate, DateTime.UtcNow.Add(GateCacheTtl)));
        return gate;
    }

    private async Task<BatchGate> ResolveBatchGateAsync(string batchId)
    {
        try
        {
            var batch = await messageLogStatusWriter.GetBatchAsync(batchId);

            // A missing batch means it was deleted; dropping is correct, and notably better
            // than the old behaviour where the queued messages still fired and each recipient
            // received an unsolicited "you have no pending messages" card.
            if (batch == null) return BatchGate.Drop;

            if (string.Equals(batch.Status, BatchStatus.Cancelled, StringComparison.OrdinalIgnoreCase))
                return BatchGate.Drop;

            if (string.Equals(batch.Status, BatchStatus.Paused, StringComparison.OrdinalIgnoreCase))
                return BatchGate.Defer;

            if (batch.ScheduledSendUtc.HasValue && batch.ScheduledSendUtc.Value > DateTime.UtcNow)
                return BatchGate.Defer;

            return BatchGate.Send;
        }
        catch (Exception ex)
        {
            // Never block delivery on a diagnostic read.
            logger.LogWarning(ex, "Could not read batch state for {BatchId}; proceeding with send", batchId);
            return BatchGate.Send;
        }
    }
}

/// <summary>
/// Result of a message send operation
/// </summary>
public class MessageSendResult
{
    public bool Success { get; set; }

    /// <summary>
    /// Determines whether the queue consumer removes the message or leaves it for redelivery.
    /// </summary>
    public SendDisposition Disposition { get; set; }

    public string RecipientUpn { get; set; } = null!;
    public string BatchId { get; set; } = null!;
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Server-supplied backoff from a throttling response, used to slow the shared send rate
    /// limiter rather than having each caller rediscover the limit independently.
    /// </summary>
    public TimeSpan? RetryAfter { get; set; }
}
