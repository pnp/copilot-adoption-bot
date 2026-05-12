using Engine.Models;
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
        var interactions = new FakeBotInteractionSource();
        var service = new StatisticsService(reader, counter, interactions, NullLogger<StatisticsService>.Instance);

        var stats = await service.GetMessageStatusStats();

        Assert.AreEqual(2, stats.SentCount);
        Assert.AreEqual(1, stats.FailedCount);
        Assert.AreEqual(3, stats.PendingCount);
        Assert.AreEqual(6, stats.TotalCount);
        Assert.AreEqual(1, reader.CallCount);
        Assert.AreEqual(0, counter.CallCount, "Status stats must not touch the tenant user counter");
        Assert.AreEqual(0, interactions.CallCount, "Status stats must not touch the interaction source");
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
        var interactions = new FakeBotInteractionSource();
        var service = new StatisticsService(reader, counter, interactions, NullLogger<StatisticsService>.Instance);

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
        var interactions = new FakeBotInteractionSource();
        var service = new StatisticsService(reader, counter, interactions, NullLogger<StatisticsService>.Instance);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => service.GetMessageStatusStats());
    }

    [TestMethod]
    public async Task GetBotInteractionStats_UsesInteractionSource()
    {
        var reader = new FakeMessageLogReader();
        var counter = new FakeTenantUserCounter();
        var interactions = new FakeBotInteractionSource
        {
            Users =
            {
                new CachedUserAndConversationData { RowKey = "u1", ConversationId = "c1", LastInteractionUtc = DateTime.UtcNow.AddHours(-2) },
                new CachedUserAndConversationData { RowKey = "u2", ConversationId = "c2", LastInteractionUtc = null },
                new CachedUserAndConversationData { RowKey = "u3", ConversationId = "c3", LastInteractionUtc = DateTime.UtcNow.AddMinutes(-5) }
            }
        };
        var service = new StatisticsService(reader, counter, interactions, NullLogger<StatisticsService>.Instance);

        var stats = await service.GetBotInteractionStats();

        Assert.AreEqual(3, stats.UsersWithConversation);
        Assert.AreEqual(2, stats.UsersInteracted);
        Assert.AreEqual(1, stats.UsersNotInteracted);
        Assert.IsTrue(stats.InteractionRatePercentage > 66.0 && stats.InteractionRatePercentage < 67.0);
        Assert.IsNotNull(stats.LastInteractionUtc);
        Assert.AreEqual(1, interactions.CallCount);
        Assert.AreEqual(0, reader.CallCount, "Interaction stats must not touch message logs");
        Assert.AreEqual(0, counter.CallCount, "Interaction stats must not touch the tenant user counter");
    }

    private sealed class ThrowingReader : IMessageLogReader
    {
        public Task<List<MessageLogTableEntity>> GetAllMessageLogs()
            => throw new InvalidOperationException("storage down");
    }
}
