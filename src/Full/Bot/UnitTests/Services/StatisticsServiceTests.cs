using Engine.Models;
using Engine.Services;
using Engine.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using UnitTests.Fakes;

namespace UnitTests.Services;

[TestClass]
public class StatisticsServiceTests
{
    private static MessageBatchTableEntity Batch(int total, int sent, int failed, string id = "b1")
        => new()
        {
            RowKey = id,
            BatchName = $"batch-{id}",
            TemplateId = "t1",
            SenderUpn = "admin@contoso.com",
            CreatedDate = DateTime.UtcNow,
            TotalCount = total,
            SentCount = sent,
            FailedCount = failed
        };

    [TestMethod]
    public async Task GetMessageStatusStats_AggregatesBatchCounters()
    {
        var batches = new FakeBatchStatsSource
        {
            Batches =
            {
                Batch(total: 4, sent: 2, failed: 1, id: "b1"),
                Batch(total: 2, sent: 0, failed: 0, id: "b2")
            }
        };
        var counter = new FakeTenantUserCounter();
        var interactions = new FakeBotInteractionSource();
        var service = new StatisticsService(batches, counter, interactions, NullLogger<StatisticsService>.Instance);

        var stats = await service.GetMessageStatusStats();

        Assert.AreEqual(2, stats.SentCount);
        Assert.AreEqual(1, stats.FailedCount);
        Assert.AreEqual(3, stats.PendingCount, "Anything neither sent nor failed is still in flight");
        Assert.AreEqual(6, stats.TotalCount);
        Assert.AreEqual(1, batches.CallCount);
        Assert.AreEqual(0, counter.CallCount, "Status stats must not touch the tenant user counter");
        Assert.AreEqual(0, interactions.CallCount, "Status stats must not touch the interaction source");
    }

    [TestMethod]
    public async Task GetMessageStatusStats_NeverReturnsNegativePending()
    {
        // Defensive: counters could over-report if a delivery were double-counted.
        var batches = new FakeBatchStatsSource { Batches = { Batch(total: 1, sent: 3, failed: 2) } };
        var service = new StatisticsService(
            batches, new FakeTenantUserCounter(), new FakeBotInteractionSource(), NullLogger<StatisticsService>.Instance);

        var stats = await service.GetMessageStatusStats();

        Assert.AreEqual(0, stats.PendingCount);
    }

    [TestMethod]
    public async Task GetUserCoverageStats_UsesReachedUserCountAndTenantSize()
    {
        var batches = new FakeBatchStatsSource();
        var counter = new FakeTenantUserCounter { Count = 10 };
        var interactions = new FakeBotInteractionSource { ReachedUserCountOverride = 2 };
        var service = new StatisticsService(batches, counter, interactions, NullLogger<StatisticsService>.Instance);

        var stats = await service.GetUserCoverageStats();

        Assert.AreEqual(2, stats.UsersMessaged);
        Assert.AreEqual(10, stats.TotalUsersInTenant);
        Assert.AreEqual(8, stats.UsersNotMessaged);
        Assert.AreEqual(20d, stats.CoveragePercentage);
        Assert.AreEqual(1, counter.CallCount);
        Assert.AreEqual(0, batches.CallCount, "Coverage must not scan delivery rows");
    }

    [TestMethod]
    public async Task GetUserCoverageStats_ZeroTenantUsers_DoesNotDivideByZero()
    {
        var service = new StatisticsService(
            new FakeBatchStatsSource(),
            new FakeTenantUserCounter { Count = 0 },
            new FakeBotInteractionSource { ReachedUserCountOverride = 0 },
            NullLogger<StatisticsService>.Instance);

        var stats = await service.GetUserCoverageStats();

        Assert.AreEqual(0d, stats.CoveragePercentage);
    }

    [TestMethod]
    public async Task GetMessageStatusStats_PropagatesSourceExceptions()
    {
        var service = new StatisticsService(
            new ThrowingBatchSource(), new FakeTenantUserCounter(), new FakeBotInteractionSource(),
            NullLogger<StatisticsService>.Instance);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => service.GetMessageStatusStats());
    }

    [TestMethod]
    public async Task GetBotInteractionStats_UsesInteractionSource()
    {
        var batches = new FakeBatchStatsSource();
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
        var service = new StatisticsService(batches, counter, interactions, NullLogger<StatisticsService>.Instance);

        var stats = await service.GetBotInteractionStats();

        Assert.AreEqual(3, stats.UsersWithConversation);
        Assert.AreEqual(2, stats.UsersInteracted);
        Assert.AreEqual(1, stats.UsersNotInteracted);
        Assert.IsTrue(stats.InteractionRatePercentage > 66.0 && stats.InteractionRatePercentage < 67.0);
        Assert.IsNotNull(stats.LastInteractionUtc);
        Assert.AreEqual(0, batches.CallCount, "Interaction stats must not touch batches");
        Assert.AreEqual(0, counter.CallCount, "Interaction stats must not touch the tenant user counter");
    }

    private sealed class ThrowingBatchSource : IBatchStatsSource
    {
        public Task<List<MessageBatchTableEntity>> GetAllBatches()
            => throw new InvalidOperationException("storage down");
    }
}
