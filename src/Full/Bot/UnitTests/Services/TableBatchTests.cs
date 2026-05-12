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
}
