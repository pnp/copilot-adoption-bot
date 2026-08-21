using Engine.Storage;

namespace UnitTests.Services;

/// <summary>
/// Pure unit tests for the delivery key scheme. These guard properties the storage layer
/// depends on: stability across processes, case-insensitive recipients, and a partition-key
/// range that covers every shard of a batch.
/// </summary>
[TestClass]
public class DeliveryKeyTests
{
    private const string BatchId = "5f2c1a90-1111-2222-3333-444455556666";

    [TestMethod]
    public void NormaliseUpn_LowercasesAndTrims()
    {
        Assert.AreEqual("alice@contoso.com", DeliveryKey.NormaliseUpn("  Alice@Contoso.COM  "));
    }

    [TestMethod]
    public void NormaliseUpn_NullOrWhitespace_Throws()
    {
        Assert.ThrowsException<ArgumentNullException>(() => DeliveryKey.NormaliseUpn(null!));
        Assert.ThrowsException<ArgumentException>(() => DeliveryKey.NormaliseUpn("   "));
    }

    [TestMethod]
    public void RowKey_IsCaseInsensitive()
    {
        // Table keys are case-sensitive but UPNs are not. Without normalisation
        // Alice@x.com and alice@x.com would be two separate deliveries to one person.
        Assert.AreEqual(DeliveryKey.RowKeyFor("alice@x.com"), DeliveryKey.RowKeyFor("ALICE@X.COM"));
    }

    [TestMethod]
    public void StableHash_IsDeterministic()
    {
        // string.GetHashCode() is randomised per process in .NET; if it were used here a
        // delivery row would move to a different shard after a restart and become unreachable.
        const string value = "alice@contoso.com";
        var expected = DeliveryKey.StableHash(value);

        for (var i = 0; i < 100; i++)
        {
            Assert.AreEqual(expected, DeliveryKey.StableHash(value));
        }
    }

    [TestMethod]
    public void StableHash_KnownValue_DoesNotDrift()
    {
        // Pinning the FNV-1a result: changing the hash would silently orphan every existing
        // delivery row, so this must be a deliberate, migration-aware change.
        Assert.AreEqual(0x811C9DC5u, DeliveryKey.StableHash(string.Empty));
    }

    [TestMethod]
    public void ShardFor_IsStableForSameUpnRegardlessOfCasing()
    {
        Assert.AreEqual(DeliveryKey.ShardFor("Bob@x.com"), DeliveryKey.ShardFor("bob@x.com"));
    }

    [TestMethod]
    public void ShardFor_StaysWithinShardCount()
    {
        for (var i = 0; i < 1000; i++)
        {
            var shard = DeliveryKey.ShardFor($"user{i}@contoso.com");
            Assert.IsTrue(shard >= 0 && shard < DeliveryKey.DefaultShardCount);
        }
    }

    [TestMethod]
    public void ShardFor_InvalidShardCount_Throws()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => DeliveryKey.ShardFor("a@x.com", 0));
    }

    [TestMethod]
    public void ShardFor_DistributesReasonablyEvenly()
    {
        // The whole point of sharding is to spread writes off a single hot partition, so a
        // badly skewed hash would defeat the change.
        var counts = new int[DeliveryKey.DefaultShardCount];
        const int users = 16000;

        for (var i = 0; i < users; i++)
        {
            counts[DeliveryKey.ShardFor($"user{i}@contoso.com")]++;
        }

        var expected = users / DeliveryKey.DefaultShardCount;
        foreach (var count in counts)
        {
            Assert.IsTrue(count > expected * 0.7 && count < expected * 1.3,
                $"Shard skew too high: expected ~{expected}, got {count}");
        }
    }

    [TestMethod]
    public void PartitionFor_HasBatchPrefixAndTwoDigitShard()
    {
        var pk = DeliveryKey.PartitionFor(BatchId, "alice@x.com");

        StringAssert.StartsWith(pk, $"{BatchId}~");
        Assert.AreEqual(BatchId.Length + 3, pk.Length, "Shard suffix should be two digits");
    }

    [TestMethod]
    public void PartitionRange_CoversEveryShardOfTheBatch()
    {
        var start = DeliveryKey.PartitionRangeStartInclusive(BatchId);
        var end = DeliveryKey.PartitionRangeEndExclusive(BatchId);

        for (var shard = 0; shard < 100; shard++)
        {
            var pk = $"{BatchId}~{shard:D2}";
            Assert.IsTrue(string.CompareOrdinal(pk, start) >= 0, $"{pk} should be >= range start");
            Assert.IsTrue(string.CompareOrdinal(pk, end) < 0, $"{pk} should be < range end");
        }
    }

    [TestMethod]
    public void PartitionRange_ExcludesOtherBatches()
    {
        var start = DeliveryKey.PartitionRangeStartInclusive(BatchId);
        var end = DeliveryKey.PartitionRangeEndExclusive(BatchId);

        var otherBatchPk = DeliveryKey.PartitionFor(Guid.NewGuid().ToString(), "alice@x.com");

        var withinRange = string.CompareOrdinal(otherBatchPk, start) >= 0
                          && string.CompareOrdinal(otherBatchPk, end) < 0;

        Assert.IsFalse(withinRange, "A different batch must not fall inside this batch's partition range");
    }

    [TestMethod]
    public void PendingRowKey_SortsNewestFirst()
    {
        var older = DeliveryKey.PendingRowKey(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), BatchId);
        var newer = DeliveryKey.PendingRowKey(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), BatchId);

        // Table Storage sorts row keys ascending, so the newest entry must sort first for
        // "read the first row of the partition" to return the newest pending delivery.
        Assert.IsTrue(string.CompareOrdinal(newer, older) < 0);
    }

    [TestMethod]
    public void PendingRowKey_IsFixedWidthSoOrderingIsLexicographic()
    {
        var a = DeliveryKey.PendingRowKey(DateTime.UtcNow, BatchId);
        var b = DeliveryKey.PendingRowKey(DateTime.UtcNow.AddYears(-5), BatchId);

        Assert.AreEqual(a.IndexOf('~'), b.IndexOf('~'),
            "Inverted ticks must be zero-padded to a fixed width or string ordering breaks");
    }
}
