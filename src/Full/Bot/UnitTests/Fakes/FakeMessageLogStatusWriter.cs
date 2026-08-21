using Engine.Services;

namespace UnitTests.Fakes;

/// <summary>
/// In-memory <see cref="IMessageLogStatusWriter"/> recording every status update so
/// tests can assert on the recorded sequence without touching real storage.
/// </summary>
public class FakeMessageLogStatusWriter : IMessageLogStatusWriter
{
    public record StatusUpdate(string PartitionKey, string RowKey, string Status, string? LastError);
    public record CounterUpdate(string BatchId, int SentDelta, int FailedDelta);

    public List<StatusUpdate> Updates { get; } = new();
    public List<CounterUpdate> CounterUpdates { get; } = new();
    public List<(string Upn, string BatchId)> ClearedPending { get; } = new();
    public Exception? ThrowOnUpdate { get; set; }

    public Task UpdateMessageLogStatusAsync(string partitionKey, string rowKey, string status, string? lastError = null)
    {
        Updates.Add(new StatusUpdate(partitionKey, rowKey, status, lastError));

        if (ThrowOnUpdate != null)
        {
            throw ThrowOnUpdate;
        }

        return Task.CompletedTask;
    }

    public Task ClearPendingDeliveryAsync(string recipientUpn, string batchId)
    {
        ClearedPending.Add((recipientUpn, batchId));
        return Task.CompletedTask;
    }

    public Task IncrementBatchCountersAsync(string batchId, int sentDelta, int failedDelta)
    {
        CounterUpdates.Add(new CounterUpdate(batchId, sentDelta, failedDelta));
        return Task.CompletedTask;
    }
}
