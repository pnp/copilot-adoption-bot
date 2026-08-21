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
/// Service responsible for sending messages to recipients.
/// </summary>
public class MessageSenderService(
    IBotConvoResumeManager botConvoResumeManager,
    IMessageLogStatusWriter messageLogStatusWriter,
    ILogger<MessageSenderService> logger)
{
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
}
