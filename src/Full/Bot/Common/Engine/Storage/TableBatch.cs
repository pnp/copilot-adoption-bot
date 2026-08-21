using Azure;
using Azure.Data.Tables;

namespace Engine.Storage;

/// <summary>
/// Helpers for batching Azure Table operations into <c>SubmitTransactionAsync</c> calls.
/// Azure Table transactions are limited to 100 operations per batch and all entities in
/// a transaction must share the same partition key.
/// </summary>
public static class TableBatch
{
    /// <summary>
    /// Maximum number of operations per Azure Table transaction.
    /// </summary>
    public const int MaxOperationsPerBatch = 100;

    /// <summary>
    /// Default number of transactions issued concurrently. Chunks target different
    /// partitions, so there is no ordering constraint between them.
    /// </summary>
    public const int DefaultParallelism = 8;

    /// <summary>
    /// Splits a sequence of operations into transaction-sized chunks, grouped by partition
    /// key. Azure Table transactions require every entity in a transaction to share a
    /// partition key, so grouping is mandatory now that deliveries are sharded across
    /// partitions.
    /// Pure helper, no Azure dependency at call time - safe to unit test.
    /// </summary>
    public static IEnumerable<IReadOnlyList<TableTransactionAction>> Chunk(
        IEnumerable<TableTransactionAction> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);

        var byPartition = new Dictionary<string, List<TableTransactionAction>>(StringComparer.Ordinal);

        foreach (var op in operations)
        {
            var partitionKey = op.Entity.PartitionKey ?? string.Empty;

            if (!byPartition.TryGetValue(partitionKey, out var pending))
            {
                pending = new List<TableTransactionAction>(MaxOperationsPerBatch);
                byPartition[partitionKey] = pending;
            }

            pending.Add(op);

            if (pending.Count == MaxOperationsPerBatch)
            {
                yield return pending;
                byPartition[partitionKey] = new List<TableTransactionAction>(MaxOperationsPerBatch);
            }
        }

        foreach (var remaining in byPartition.Values)
        {
            if (remaining.Count > 0)
            {
                yield return remaining;
            }
        }
    }

    /// <summary>
    /// Executes the given operations in transactional batches, grouped by partition key and
    /// issued with bounded concurrency.
    ///
    /// <para>
    /// Each chunk is retried independently and, if it still fails, falls back to per-entity
    /// writes <em>for that chunk only</em>. This matters: the previous implementation caught a
    /// failure from any chunk and then re-issued <em>every</em> operation individually, which
    /// duplicated all rows already committed by earlier chunks.
    /// </para>
    ///
    /// <para>
    /// Callers should use idempotent operations (<see cref="TableTransactionActionType.UpsertMerge"/>
    /// or <see cref="TableTransactionActionType.UpsertReplace"/>) with a natural key, so
    /// re-applying an operation that already landed is a no-op.
    /// </para>
    /// </summary>
    /// <returns>The number of operations that could not be written.</returns>
    public static async Task<int> SubmitInBatchesAsync(
        TableClient tableClient,
        IEnumerable<TableTransactionAction> operations,
        int maxParallelism = DefaultParallelism,
        Action<TableTransactionAction, Exception>? onOperationFailed = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tableClient);
        if (maxParallelism < 1) maxParallelism = 1;

        var failures = 0;
        using var throttler = new SemaphoreSlim(maxParallelism);

        var tasks = Chunk(operations).Select(async chunk =>
        {
            await throttler.WaitAsync(cancellationToken);
            try
            {
                try
                {
                    await tableClient.SubmitTransactionAsync(chunk, cancellationToken);
                }
                catch (RequestFailedException)
                {
                    var chunkFailures = await SubmitIndividuallyAsync(
                        tableClient, chunk, onOperationFailed, cancellationToken);
                    Interlocked.Add(ref failures, chunkFailures);
                }
            }
            finally
            {
                throttler.Release();
            }
        });

        await Task.WhenAll(tasks);
        return failures;
    }

    private static async Task<int> SubmitIndividuallyAsync(
        TableClient tableClient,
        IReadOnlyList<TableTransactionAction> chunk,
        Action<TableTransactionAction, Exception>? onOperationFailed,
        CancellationToken cancellationToken)
    {
        var failures = 0;

        foreach (var op in chunk)
        {
            try
            {
                switch (op.ActionType)
                {
                    case TableTransactionActionType.Delete:
                        await tableClient.DeleteEntityAsync(
                            op.Entity.PartitionKey, op.Entity.RowKey, ETag.All, cancellationToken);
                        break;
                    case TableTransactionActionType.UpsertMerge:
                        await tableClient.UpsertEntityAsync(
                            op.Entity, TableUpdateMode.Merge, cancellationToken);
                        break;
                    default:
                        await tableClient.UpsertEntityAsync(
                            op.Entity, TableUpdateMode.Replace, cancellationToken);
                        break;
                }
            }
            catch (RequestFailedException ex) when (ex.Status == 404 && op.ActionType == TableTransactionActionType.Delete)
            {
                // Already gone - deleting a missing entity is success for our purposes.
            }
            catch (Exception ex)
            {
                failures++;
                onOperationFailed?.Invoke(op, ex);
            }
        }

        return failures;
    }
}
