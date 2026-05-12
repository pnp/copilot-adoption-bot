using Engine.Services;
using Engine.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using UnitTests.Fakes;

namespace UnitTests.Services;

[TestClass]
public class StatisticsServiceTests
{
    private static MessageLogTableEntity Log(string status, string batchId = "b1")
        => new()
        {
            RowKey = Guid.NewGuid().ToString(),
            MessageBatchId = batchId,
            RecipientUpn = $"u{Guid.NewGuid():N}@contoso.com",
            Status = status,
            SentDate = DateTime.UtcNow
        };

    [TestMethod]
    public async Task GetMessageStatusStats_AggregatesByStatus()
    {
        var reader = new FakeMessageLogReader
        {
            Logs =
            {
                Log("Success"),
                Log("Success"),
                Log("Failed"),
                Log("Pending"),
                Log("Pending"),
                Log("Pending"),
            }
        };
        var counter = new FakeTenantUserCounter();
        var service = new StatisticsService(reader, counter, NullLogger<StatisticsService>.Instance);

        var stats = await service.GetMessageStatusStats();

        Assert.AreEqual(2, stats.SentCount);
        Assert.AreEqual(1, stats.FailedCount);
        Assert.AreEqual(3, stats.PendingCount);
        Assert.AreEqual(6, stats.TotalCount);
        Assert.AreEqual(1, reader.CallCount);
        Assert.AreEqual(0, counter.CallCount, "Status stats must not touch the tenant user counter");
    }

    [TestMethod]
    public async Task GetUserCoverageStats_UsesDistinctRecipientCountAndTenantSize()
    {
        var reader = new FakeMessageLogReader
        {
            Logs =
            {
                new MessageLogTableEntity { RowKey = "1", MessageBatchId = "b1", RecipientUpn = "a@contoso.com", Status = "Success" },
                new MessageLogTableEntity { RowKey = "2", MessageBatchId = "b1", RecipientUpn = "a@contoso.com", Status = "Failed" },
                new MessageLogTableEntity { RowKey = "3", MessageBatchId = "b1", RecipientUpn = "b@contoso.com", Status = "Success" }
            }
        };
        var counter = new FakeTenantUserCounter { Count = 10 };
        var service = new StatisticsService(reader, counter, NullLogger<StatisticsService>.Instance);

        var stats = await service.GetUserCoverageStats();

        Assert.AreEqual(2, stats.UsersMessaged, "Distinct recipients only");
        Assert.AreEqual(10, stats.TotalUsersInTenant);
        Assert.AreEqual(8, stats.UsersNotMessaged);
        Assert.AreEqual(1, counter.CallCount);
    }

    [TestMethod]
    public async Task GetMessageStatusStats_PropagatesReaderExceptions()
    {
        var reader = new ThrowingReader();
        var counter = new FakeTenantUserCounter();
        var service = new StatisticsService(reader, counter, NullLogger<StatisticsService>.Instance);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => service.GetMessageStatusStats());
    }

    private sealed class ThrowingReader : IMessageLogReader
    {
        public Task<List<MessageLogTableEntity>> GetAllMessageLogs()
            => throw new InvalidOperationException("storage down");
    }
}
