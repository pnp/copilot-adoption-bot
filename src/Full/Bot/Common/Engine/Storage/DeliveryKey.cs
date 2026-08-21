namespace Engine.Storage;

/// <summary>
/// Key scheme for delivery rows (<see cref="MessageLogTableEntity"/>).
///
/// <para>
/// <c>PartitionKey = "{batchId}~{shard}"</c>, <c>RowKey = normalised recipient UPN</c>.
/// </para>
///
/// <para>
/// This replaces the previous scheme of a single constant partition (<c>"MessageLogs"</c>)
/// with a random GUID row key, which had three problems at scale:
/// every row landed in one partition (capped at ~2,000 entities/s), queries by batch had
/// to scan the whole table because <c>MessageBatchId</c> was not a key, and a random row
/// key made writes non-idempotent so any retry duplicated rows.
/// </para>
///
/// <para>
/// Sharding spreads a single batch's writes over <see cref="DefaultShardCount"/> partitions.
/// All partitions for a batch share the <c>"{batchId}~"</c> prefix, so listing a batch is a
/// bounded range query that needs no knowledge of the shard count.
/// </para>
///
/// Pure helper with no Azure dependency so the scheme can be unit-tested directly.
/// </summary>
public static class DeliveryKey
{
    /// <summary>
    /// Number of partitions a batch's deliveries are spread across. 16 shards gives roughly
    /// 16 x 2,000 = 32,000 entities/s of headroom per batch, comfortably above the ~1.74
    /// rows/second average at the 150k-users-per-day target.
    /// </summary>
    public const int DefaultShardCount = 16;

    /// <summary>
    /// Separator between the batch id and the shard suffix. Chosen because it sorts above
    /// every digit, which makes <see cref="PartitionRangeEndExclusive"/> a safe upper bound.
    /// </summary>
    private const char Separator = '~';

    /// <summary>
    /// Normalise a UPN for use as a row key. Azure Table keys are case-sensitive while UPNs
    /// are treated case-insensitively everywhere else, so without this
    /// <c>User@contoso.com</c> and <c>user@contoso.com</c> would be two distinct deliveries.
    /// </summary>
    public static string NormaliseUpn(string upn)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(upn);
        return upn.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Deterministic FNV-1a hash. <see cref="string.GetHashCode()"/> must not be used here:
    /// .NET randomises it per process, so the same UPN would map to different shards after a
    /// restart and its delivery row would become unreachable.
    /// </summary>
    internal static uint StableHash(string value)
    {
        const uint offsetBasis = 2166136261;
        const uint prime = 16777619;

        var hash = offsetBasis;
        foreach (var c in value)
        {
            hash ^= c;
            hash *= prime;
        }
        return hash;
    }

    /// <summary>
    /// Shard index for a recipient within a batch.
    /// </summary>
    public static int ShardFor(string upn, int shardCount = DefaultShardCount)
    {
        if (shardCount < 1) throw new ArgumentOutOfRangeException(nameof(shardCount));
        return (int)(StableHash(NormaliseUpn(upn)) % (uint)shardCount);
    }

    /// <summary>
    /// Partition key for a single delivery.
    /// </summary>
    public static string PartitionFor(string batchId, string upn, int shardCount = DefaultShardCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(batchId);
        return $"{batchId}{Separator}{ShardFor(upn, shardCount):D2}";
    }

    /// <summary>
    /// Row key for a single delivery.
    /// </summary>
    public static string RowKeyFor(string upn) => NormaliseUpn(upn);

    /// <summary>
    /// Inclusive lower bound of the partition-key range covering every shard of a batch.
    /// </summary>
    public static string PartitionRangeStartInclusive(string batchId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(batchId);
        return $"{batchId}{Separator}";
    }

    /// <summary>
    /// Exclusive upper bound of the partition-key range covering every shard of a batch.
    /// <c>'~'</c> sorts above all digits, so <c>"{batchId}~~"</c> is greater than
    /// <c>"{batchId}~99"</c> for any shard count up to 100.
    /// </summary>
    public static string PartitionRangeEndExclusive(string batchId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(batchId);
        return $"{batchId}{Separator}{Separator}";
    }

    /// <summary>
    /// Row key for the per-user pending-delivery index, ordered newest-first.
    /// Table Storage sorts row keys ascending, so an inverted tick count puts the most
    /// recent pending delivery in the first row of the partition.
    /// </summary>
    public static string PendingRowKey(DateTime createdUtc, string batchId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(batchId);
        var inverted = DateTime.MaxValue.Ticks - createdUtc.Ticks;
        return $"{inverted:D19}{Separator}{batchId}";
    }
}
