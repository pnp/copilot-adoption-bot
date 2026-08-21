using Engine.Storage;
namespace Engine.Services;

/// <summary>
/// Narrow abstraction for updating the status of a message log entry.
/// Allows <see cref="MessageSenderService"/> to be unit-tested without
/// constructing a full <see cref="MessageTemplateService"/> or pulling
/// scoped services out of an <see cref="IServiceProvider"/>.
/// </summary>
public interface IMessageLogStatusWriter
{
    /// <summary>
    /// Update the status (and optional last-error message) for a delivery, addressed by its
    /// exact key. Implemented as a sparse merge, so no read-before-write is required.
    /// </summary>
    Task UpdateMessageLogStatusAsync(string partitionKey, string rowKey, string status, string? lastError = null);

    /// <summary>
    /// Remove the per-user pending-index entry for a delivery once it has been sent, so the
    /// user's "newest pending card" lookup doesn't resurface an already-delivered message.
    /// </summary>
    Task ClearPendingDeliveryAsync(string recipientUpn, string batchId);

    /// <summary>
    /// Apply a delta to a batch's running success/failure counters. Statistics are derived
    /// from these counters rather than by scanning delivery rows.
    /// </summary>
    Task IncrementBatchCountersAsync(string batchId, int sentDelta, int failedDelta);

    /// <summary>
    /// Read a batch's current lifecycle state, so the dispatcher can honour cancellation,
    /// pausing and scheduled start times before sending.
    /// </summary>
    Task<MessageBatchTableEntity?> GetBatchAsync(string batchId);
}
