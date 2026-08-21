using Azure.Data.Tables;
using Engine.Storage;

namespace UnitTests.Services;

[TestClass]
public class TableBatchTests
{
    private sealed class TestEntity : ITableEntity
    {
        public string PartitionKey { get; set; } = "p";
        public string RowKey { get; set; } = "";
        public DateTimeOffset? Timestamp { get; set; }
        public Azure.ETag ETag { get; set; }
    }

    private static IEnumerable<TableTransactionAction> Ops(int n) =>
        Enumerable.Range(0, n).Select(i =>
            new TableTransactionAction(
                TableTransactionActionType.Add,
                new TestEntity { RowKey = i.ToString() }));

    [TestMethod]
    public void Chunk_Null_Throws()
    {
        Assert.ThrowsException<ArgumentNullException>(() => TableBatch.Chunk(null!).ToList());
    }

    [TestMethod]
    public void Chunk_Empty_ReturnsNoBatches()
    {
        var batches = TableBatch.Chunk(Enumerable.Empty<TableTransactionAction>()).ToList();
        Assert.AreEqual(0, batches.Count);
    }

    [TestMethod]
    public void Chunk_FewerThanLimit_ReturnsSingleBatch()
    {
        var batches = TableBatch.Chunk(Ops(7)).ToList();
        Assert.AreEqual(1, batches.Count);
        Assert.AreEqual(7, batches[0].Count);
    }

    [TestMethod]
    public void Chunk_ExactlyLimit_ReturnsSingleBatchOf100()
    {
        var batches = TableBatch.Chunk(Ops(TableBatch.MaxOperationsPerBatch)).ToList();
        Assert.AreEqual(1, batches.Count);
        Assert.AreEqual(100, batches[0].Count);
    }

    [TestMethod]
    public void Chunk_OverLimit_SplitsAtBoundary()
    {
        var batches = TableBatch.Chunk(Ops(101)).ToList();
        Assert.AreEqual(2, batches.Count);
        Assert.AreEqual(100, batches[0].Count);
        Assert.AreEqual(1, batches[1].Count);
    }

    [TestMethod]
    public void Chunk_LargeBatch_SplitsCorrectly()
    {
        var batches = TableBatch.Chunk(Ops(250)).ToList();
        Assert.AreEqual(3, batches.Count);
        Assert.AreEqual(100, batches[0].Count);
        Assert.AreEqual(100, batches[1].Count);
        Assert.AreEqual(50, batches[2].Count);

        // All operations preserved in order
        var allRowKeys = batches.SelectMany(b => b)
            .Select(a => ((TestEntity)a.Entity).RowKey)
            .ToArray();
        CollectionAssert.AreEqual(
            Enumerable.Range(0, 250).Select(i => i.ToString()).ToArray(),
            allRowKeys);
    }

    private static IEnumerable<TableTransactionAction> OpsAcrossPartitions(int n, int partitions) =>
        Enumerable.Range(0, n).Select(i =>
            new TableTransactionAction(
                TableTransactionActionType.UpsertMerge,
                new TestEntity { PartitionKey = $"p{i % partitions}", RowKey = i.ToString() }));

    [TestMethod]
    public void Chunk_GroupsByPartitionKey()
    {
        // Azure Table transactions require every entity in a transaction to share a partition
        // key, so once deliveries are sharded the chunker must group rather than slice.
        var batches = TableBatch.Chunk(OpsAcrossPartitions(30, partitions: 3)).ToList();

        Assert.AreEqual(3, batches.Count);
        foreach (var batch in batches)
        {
            var distinctPartitions = batch.Select(a => a.Entity.PartitionKey).Distinct().Count();
            Assert.AreEqual(1, distinctPartitions, "Every transaction must target exactly one partition");
            Assert.AreEqual(10, batch.Count);
        }
    }

    [TestMethod]
    public void Chunk_GroupsByPartition_AndStillRespectsTheHundredOpLimit()
    {
        // 250 ops over 2 partitions => 125 each => 100 + 25 per partition.
        var batches = TableBatch.Chunk(OpsAcrossPartitions(250, partitions: 2)).ToList();

        Assert.AreEqual(4, batches.Count);
        Assert.IsTrue(batches.All(b => b.Count <= TableBatch.MaxOperationsPerBatch));
        Assert.IsTrue(batches.All(b => b.Select(a => a.Entity.PartitionKey).Distinct().Count() == 1));
        Assert.AreEqual(250, batches.Sum(b => b.Count), "No operation may be dropped");
    }

    [TestMethod]
    public void Chunk_NoOperationIsDuplicated()
    {
        // Guards the double-insert regression: every operation must appear exactly once.
        var batches = TableBatch.Chunk(OpsAcrossPartitions(137, partitions: 5)).ToList();

        var keys = batches.SelectMany(b => b)
            .Select(a => $"{a.Entity.PartitionKey}/{a.Entity.RowKey}")
            .ToList();

        Assert.AreEqual(137, keys.Count);
        Assert.AreEqual(137, keys.Distinct().Count());
    }
}
