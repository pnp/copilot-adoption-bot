using Azure.Data.Tables;

namespace Common.Engine.Storage;

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
    /// Splits a sequence of operations into chunks of <see cref="MaxOperationsPerBatch"/>.
    /// Pure helper, no Azure dependency at call time - safe to unit test.
    /// </summary>
    public static IEnumerable<IReadOnlyList<TableTransactionAction>> Chunk(
        IEnumerable<TableTransactionAction> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);

        var batch = new List<TableTransactionAction>(MaxOperationsPerBatch);
        foreach (var op in operations)
        {
            batch.Add(op);
            if (batch.Count == MaxOperationsPerBatch)
            {
                yield return batch;
                batch = new List<TableTransactionAction>(MaxOperationsPerBatch);
            }
        }

        if (batch.Count > 0)
        {
            yield return batch;
        }
    }

    /// <summary>
    /// Executes the given operations in batches against the table. All operations are
    /// assumed to share the same partition key (a precondition of Azure Table transactions).
    /// </summary>
    public static async Task SubmitInBatchesAsync(
        TableClient tableClient,
        IEnumerable<TableTransactionAction> operations,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tableClient);

        foreach (var batch in Chunk(operations))
        {
            await tableClient.SubmitTransactionAsync(batch, cancellationToken);
        }
    }
}
